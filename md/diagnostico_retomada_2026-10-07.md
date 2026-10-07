# Diagnóstico de retomada — Sistema P2P MVP1

Data: 07/10/2026. Referência: código local e HEAD `e6440a8`, de 06/09/2026.

Este diagnóstico registra o estado anterior às correções desta revisão. Para o código alterado e seus testes, consulte [validação atual](validacao_atual_2026-10-07.md).

## Onde o projeto parou

O MVP tem uma base funcional ampla. A integração Asaas já foi iniciada no último commit, mas não está conectada aos fluxos principais da interface nem completa para transferências. A próxima etapa é concluir a integração e corrigir a integridade financeira dos caminhos existentes antes de operar com dinheiro real.

O plano de 23/08 ainda diz “Pronto para Implementação”, embora já existam serviço Asaas, DTOs, geração de PIX e receptor de webhook. Os relatórios de agosto descrevem uma versão anterior e suas conclusões de segurança e conciliação não devem ser tomadas como homologação do código atual.

## Mapa do que existe

| Área | Implementação encontrada | Situação |
|---|---|---|
| Base técnica | ASP.NET Core/.NET 10, EF Core, PostgreSQL, Angular 21/Material | Implementada |
| Cadastro e acesso | Tomador/investidor, JWT, perfis, documentos, aprovação administrativa | Implementados; precisam de regressão completa |
| Crédito | Questionário, score, regras, limites e pedidos de aumento | Implementados |
| Empréstimos | Solicitação, aprovação, parcelas, juros e mora | Implementados; pagamentos ainda simulados |
| Investidores | Carteiras, grupos, produtos, taxas por tipo de saldo | Implementados |
| Alocação | Reservas e distribuição de capital nos empréstimos | Implementada |
| Rendimentos | Processamento mensal e serviço automático diário | Implementados; revisar consistência entre previsão e crédito |
| Tesouraria | Solicitação, aprovação e confirmação de depósitos/saques | Implementada com confirmação manual |
| Contabilidade | Ledger, receita, fundo de proteção, fechamento e relatórios | Implementados; integridade integral não demonstrada |
| Asaas | Clientes, cobranças PIX, QR Code, teste de conexão, webhook | Parcial |
| Transferências | Método de transferência PIX no serviço | Sem chamada no fluxo operacional encontrado |
| App, WhatsApp, sugestão automática de limite | Previstos no roadmap | Etapas futuras |

## Asaas: o que já foi feito e o que falta

Arquivos principais: `backend/Application/Services/Asaas/AsaasService.cs`, `backend/API/Controllers/AsaasGatewayController.cs`, `backend/Application/DTOs/AsaasDtos.cs`.

Existem as rotas:

- `GET /api/admin/gateway/test`: teste de conectividade autenticado como administrador.
- `POST /api/investor/treasury/deposit-pix`: solicita depósito e cria cobrança PIX quando configurado.
- `POST /api/loans/installments/{id}/pix`: gera cobrança PIX de parcela.
- `POST /api/webhooks/asaas`: trata recebimento/confirmação de cobrança e vencimento.

Falta concluir:

1. Conectar o depósito da interface à rota `deposit-pix`. Atualmente `InvestorDashboardComponent.makeDeposit()` chama `InvestorService.makeDeposit()`, que usa `/api/investor/deposit` e credita saldo por simulação.
2. Conectar o pagamento de parcelas ao PIX. O modal atual retorna valores/lotes para os métodos de pagamento simulado; não apresenta uma cobrança real vinculada à liquidação.
3. Exibir QR Code, copia e cola e situação da cobrança, com atualização após confirmação do backend.
4. Persistir vínculo e dados recuperáveis de cada cobrança. O depósito grava `GatewayReference`, mas a parcela não persiste o `paymentId`; chamadas repetidas criam cobranças novas.
5. Ligar aprovação de saque à transferência e acompanhar conclusão/falha/cancelamento por webhook. `CreatePixTransferAsync` existe, mas não é chamado pelos controllers/tesouraria encontrados.
6. Implementar desembolso real ao tomador, com vínculo da transferência ao empréstimo e ativação após confirmação.
7. Implementar autenticação obrigatória, processamento idempotente e atomicidade dos webhooks.
8. Tratar estornos, tarifas, valores bruto/líquido, cancelamentos, falhas e conciliação com a conta Asaas.
9. Homologar no sandbox e preparar a configuração de produção. A presença de `.env` não comprova uma conta configurada ou homologada; segredos não foram reproduzidos nesta análise.

## Problemas prioritários encontrados

### 1. Aporte e pagamento sem comprovação externa

`InvestorController.Deposit` soma o valor informado à carteira imediatamente. A própria interface usa esse caminho. `LoanInstallmentController` também permite baixa simulada de parcelas individuais e em lote. Esses caminhos precisam ser bloqueados no ambiente real e substituídos por liquidação validada.

### 2. Webhook pode operar sem autenticação

`HandleAsaasWebhook` só verifica o token quando ele está preenchido e não contém `seu_token`. Sem configuração válida, o endpoint anônimo continua processando eventos financeiros. O pagamento é identificado por `externalReference`, sem conferência suficiente de ID externo e valor esperado. Para depósitos, o crédito usa o valor local da solicitação, sem comparar com o valor recebido.

### 3. Distribuição de pagamento parcial incorreta

`WalletDistributionService` calcula o retorno como `installment.ValorPrincipal * fraction`, independentemente de quanto foi efetivamente recebido. Por exemplo: parcela de R$ 100, principal de R$ 80, pagamento de R$ 40 e financiamento integral por investidores pode devolver R$ 80 de principal. Isso contradiz a alegação de distribuição proporcional do relatório da Sprint 3.

Na última parcela parcialmente paga, o controller redefine `ValorPago` para zero antes de distribuir; o serviço retorna sem distribuir, pois exige `ValorPago > 0`. O histórico registra o recebimento, mas essa sequência compromete seu reflexo na carteira/ledger.

### 4. Concorrência não está garantida apenas pelos atributos

As entidades têm `[ConcurrencyCheck]` em `Version`, mas não foi encontrado incremento da versão, mapeamento para `xmin` ou geração automática desse valor no código. As migrações mostram coluna comum `bigint`. Assim, não há base para assumir que esses atributos, sozinhos, impedem crédito duplicado e gasto simultâneo. Validar e corrigir com PostgreSQL real.

`LoanInstallment` também não tem token de concorrência. O webhook verifica status, mas não reserva atomicamente o evento antes de executar seus efeitos.

### 5. Idempotência e transações incompletas

O middleware só funciona quando o cliente envia `X-Idempotency-Key`, procura uma chave global e grava a resposta depois de executar a operação. Duas chamadas simultâneas podem executar antes da gravação; a chave não é vinculada ao usuário e conteúdo da requisição e a consulta ocorre antes da autenticação. Ele não substitui deduplicação de eventos Asaas.

Na distribuição, o primeiro lançamento é salvo por `LedgerService.AddTransactionAsync` antes de iniciar a transação de repasse. Como o contexto é compartilhado, essa gravação também pode persistir a parcela como paga. Uma falha posterior pode deixar a baixa gravada e o repasse incompleto; a repetição do webhook pode ser ignorada pelo status. Centralizar uma única transação para baixa, histórico, ledger e carteiras.

### 6. Inicialização altera dados de demonstração em qualquer ambiente

`Program.cs` cria usuários de teste, redefine senhas e restaura o saldo disponível do investidor de demonstração para R$ 50.000 em reinicializações. O código não limita esses seeds a Development. Isso impede tratar a base como um ambiente financeiro de produção confiável. Há também chave JWT padrão e CORS aberto a qualquer origem com credenciais.

### 7. Simulação silenciosa e cobrança duplicada

A emissão de depósito captura erros do Asaas e responde com PIX simulado. Sem chave configurada, os endpoints também produzem códigos fictícios. No ambiente real, falhas devem aparecer como falhas e permitir recuperação da operação; o sistema deve reutilizar a cobrança existente quando aplicável.

### 8. Rendimentos precisam de definição única

O crédito diário considera saldo disponível, enquanto a previsão diária inclui também saldo investido/comprometido. Existe ainda processamento mensal sobre disponível e comprometido/investido. Definir a regra e evitar sobreposição dos créditos; validar origem e disponibilidade do caixa que financia esses rendimentos. O ledger registra movimentações, mas não verifica por si só disponibilidade de dinheiro na conta externa.

## Verificação realizada

- `dotnet test backend.Tests/backend.Tests.csproj --no-restore --verbosity minimal`: **47 aprovados, zero falhas**. O backend compilou.
- Surgiram avisos de conflito entre versões do EF Core Relational 10.0.4/10.0.8 e NU1903 sobre `Microsoft.OpenApi` 2.0.0. Reconciliar dependências restauradas e revisar a vulnerabilidade antes da entrega.
- O teste de distribuição encontrado cobre recebimento integral. Os testes usam EF InMemory; não comprovam locks, transações e concorrência no PostgreSQL.
- `npm.cmd run build`: **aprovado** após execução autorizada fora do sandbox, que inicialmente bloqueou a leitura dos arquivos. Avisos: bundle inicial de 2,31 MB acima do orçamento de 1,50 MB, cinco estilos acima de 4 kB e dependências CommonJS na geração de PDFs.
- Existem oito fluxos Cypress e screenshots de falhas antigas; isso não prova falha atual nem aprovação. A suíte E2E não foi executada nesta análise.
- Não executei a API contra o banco existente, seeds, migrações, cobranças ou transferências reais. Não foi realizada auditoria de saldos do banco nem benchmark de carga.
- A análise foi de estrutura, documentação, rotas e caminhos críticos do código. O PDF e o dump SQL não foram validados integralmente; não se trata de certificação exaustiva de cada arquivo ou cenário.

## Sequência recomendada para retomar

1. **Sanear a base:** restringir seed, corrigir pagamentos parciais, concorrência, idempotência, atomicidade e autenticação de webhook; desabilitar simulações em produção.
2. **Completar entrada de dinheiro:** persistência de cobranças, depósito e parcela PIX conectados ao frontend, tarifas e confirmação somente pelo fluxo validado.
3. **Completar saída de dinheiro:** saque e desembolso com transferência, confirmação/falha/cancelamento, sem duplicação em retries.
4. **Homologar:** PostgreSQL de teste e Asaas sandbox, eventos repetidos/simultâneos, valores divergentes, pagamentos parciais, falhas intermediárias, estornos e conciliação.
5. **Preparar operação:** build, E2E, configuração segura, backups com restauração testada, logs e monitoramento, implantação e regressão dos ajustes anotados em `sprint.md`.
6. **Depois:** aplicativo, WhatsApp e sugestão automática de crédito, se continuarem no escopo.

## Critérios mínimos de conclusão

- Nenhum aporte ou pagamento é reconhecido por simples clique do usuário.
- Cada cobrança/transferência tem identificação externa persistida e pode ser recuperada.
- Repetição e simultaneidade de eventos produzem uma única liquidação.
- Pagamento parcial só distribui valores efetivamente recebidos conforme a regra definida.
- Falha durante liquidação não deixa carteira, parcela e ledger em estados divergentes.
- Saque só é finalizado após conclusão validada da transferência.
- Conciliação demonstra diferenças explicadas entre banco, ledger, carteiras e Asaas, incluindo tarifas.
- Reinício da API não altera saldos ou credenciais operacionais.

## Referências Asaas consultadas

- [Eventos para cobranças](https://docs.asaas.com/docs/webhook-para-cobrancas): `PAYMENT_CONFIRMED` ainda não significa saldo disponível; `PAYMENT_RECEIVED` significa valor disponível. O código atual trata ambos como liquidação equivalente, regra que precisa ser revista.
- [Transferências](https://docs.asaas.com/docs/transferencias): acompanhar eventos de conclusão, falha e cancelamento; aprovação local não equivale à efetivação da transferência.

## Estado do repositório

Antes desta análise, o Git já mostrava documentos removidos da raiz e a pasta `md/` não rastreada, compatível com reorganização da documentação ainda não registrada. Preservei essas alterações. Este diagnóstico é um novo arquivo; não alterei regras de negócio.
