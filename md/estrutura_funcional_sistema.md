# Estrutura e regras do Sistema P2P

Revisão de 07/10/2026, baseada no código, documentos Markdown, PDF de dez páginas e estrutura do dump SQL. A documentação original descreve uma versão com escolha manual de empréstimos pelo investidor; o código atual e as anotações posteriores adotam produtos e alocação administrativa em pool.

## Objetivo e responsabilidades

A plataforma intermedeia crédito entre tomadores e investidores, mantendo as decisões de cadastro, concessão de crédito, alocação e saques sob controle do administrador. Também admite empréstimos financiados pela própria administração quando não é escolhido um grupo de investidores. O Asaas executa cobranças e transferências; não substitui as regras internas de crédito e contabilidade.

O frontend existente define o escopo funcional desta revisão. Não foram mantidas novas telas de histórico financeiro, cancelamento ou desembolso. Rotas de diagnóstico, reset e baixa simulada sem correspondência operacional foram removidas.

## Organização do código

| Diretório | Responsabilidade |
|---|---|
| `frontend/src/app/core` | Modelos, clientes HTTP, sessão, interceptador JWT e guardas de rotas |
| `frontend/src/app/features/auth` | Cadastro de cliente/investidor e login |
| `frontend/src/app/features/user-dashboard` | Cadastro documental, formulário, perfil, solicitação, histórico e parcelas do tomador |
| `frontend/src/app/features/investor` | Produto, carteira, aporte e solicitação de saque |
| `frontend/src/app/features/admin` | Cadastros, grupos, regras, crédito, empréstimos, tesouraria, inadimplência e relatórios |
| `frontend/src/app/shared` | Layout e componentes reutilizados pelos fluxos existentes |
| `backend/API/Controllers` | Autorização, contratos HTTP e coordenação dos casos de uso |
| `backend/API/Middleware` | Tratamento de erros e repetição segura de requisições autenticadas |
| `backend/Application/Services` | Crédito, alocação, juros, pagamentos, rendimentos, tesouraria, ledger e fechamento |
| `backend/Application/Services/Asaas` | Cliente HTTP e operações persistentes do gateway |
| `backend/Domain/Entities` | Estado financeiro e cadastral |
| `backend/Infrastructure` | EF Core/PostgreSQL, migrações e administrador inicial |
| `backend.Tests` | Regressão de regras, gateway com respostas controladas e testes opcionais em PostgreSQL isolado |
| `frontend/cypress` | Cenários E2E históricos; exigem ambiente e dados de teste próprios |

Stack atual: .NET 10, EF Core 10, PostgreSQL, Angular 21, Material, TypeScript, xUnit, Vitest e Cypress. Referências antigas a .NET 8/Angular 19 não descrevem a versão atual.

## Cadastro e crédito

1. O usuário cadastra dados; o CPF é validado estruturalmente e a maioridade é obrigatória. Consulta cadastral externa é uma integração separada e sua ausência não comprova situação regular na Receita.
2. Documentos possuem tipo, arquivo e status próprios. O usuário acessa apenas os seus arquivos; o administrador revisa o cadastro.
3. O questionário dinâmico atribui pontos a respostas. Não aceita perguntas duplicadas, inativas ou formulário incompleto.
4. O administrador aprova ou reprova o usuário. Score e limite são parâmetros de avaliação, não confirmação de dinheiro concedido.
5. O tomador aprovado solicita empréstimo; limites consideram solicitações pendentes e principal ainda comprometido. Renovação considera os estados financeiros ativos e parcelas efetivamente quitadas.
6. Aumento de limite exige cadastro aprovado e empréstimo finalizado, conforme anotações do fluxo existente.

## Investimento e carteira

O investidor escolhe um produto cadastrado pelo administrador. Taxas e identificação do produto são refletidas na carteira; a alocação do capital é feita pela administração, podendo limitar participantes a um grupo.

| Campo | Significado |
|---|---|
| `SaldoDisponivel` | Valor utilizável para alocação; o saque também respeita a carência do depósito |
| `SaldoSacavel` | Parte do disponível liberada para saque; calculada pelo backend, sem somar capital investido, comprometido ou bloqueado |
| `SaldoEmCarencia` | Parte do disponível retida pela carência dos depósitos confirmados |
| `SaldoBloqueado` | Reserva de saque ainda não concluído |
| `SaldoComprometido` | Capital reservado para empréstimo autorizado |
| `SaldoInvestido` | Capital em empréstimo desembolsado, ainda não amortizado |
| `Rendimentos` | Acumulado histórico de rendimentos creditados; não deve ser somado novamente a um saldo que já contém esse crédito |
| `TotalAportado` / `TotalSacado` | Acumulados de entradas e saídas liquidadas |

Solicitar saque reserva saldo, sem contabilizar dinheiro transferido ao banco. A aprovação autoriza o envio; somente a confirmação da transferência conclui a saída. Falha devolve a reserva uma única vez.

O rendimento diário exibido é uma estimativa informativa dos saldos remunerados e suas taxas, proporcional a 30 dias. Não cria saldo sacável, transação financeira ou lançamento contábil. O crédito efetivo acontece no fechamento mensal. Créditos reais gerados por versões antigas precisam de conciliação; não são confundidos com a estimativa atual.

O administrador analisa o pedido de saque e pode aprovar o valor solicitado ou um valor menor. A diferença volta ao disponível; o pedido preserva o valor original. Aprovação não efetua transferência. O PIX envia somente o valor aprovado, e sua confirmação liquida somente a reserva correspondente.

Cada depósito precisa completar 72 horas desde a confirmação (`ProcessedAt`). A data de criação do pedido de PIX não conta como depósito. Novo aporte não reinicia o prazo do dinheiro antigo. Depósitos sem data de confirmação exigem conciliação; a API não presume que já foram liberados. A carência não altera o campo `SaldoBloqueado`, reservado às solicitações de saque.

Antes de existir um primeiro depósito confirmado com 72 horas completas, o saldo sacável é zero, inclusive para créditos de rendimento. Depois disso, o cálculo retém do disponível os depósitos novos ainda em carência. Por exemplo, R$ 500 antigos disponíveis mais R$ 300 de aporte novo permitem sacar até R$ 500. Saldos antigos sem histórico de confirmação exigem conciliação para liberação.

## Empréstimos e pagamentos

O usuário autorizou o envio do empréstimo pelo Asaas no painel administrativo. A aprovação reserva o financiamento; o envio exige confirmação do valor e do CPF cadastrado do tomador como chave PIX. A chave CPF precisa existir na instituição de destino. O aplicativo não consulta nem comprova titularidade via DICT. A conclusão confirmada pelo gateway ativa o capital investido e registra a saída; um envio pendente não autoriza outra transferência.

Juros simples são a regra dos contratos. Principal e juros são calculados e arredondados de forma que cada parcela conserve seus componentes e o total do contrato.

Pagamentos são recebimentos confirmados. O clique em pagar/gerar PIX não quita a parcela. O principal efetivamente recebido retorna proporcionalmente aos investidores; juros e encargos recebidos são receita da plataforma. O principal financiado pela administração permanece em seu caixa.

A regra confirmada em 07/10/2026 permite pagamento parcial: o saldo passa à próxima parcela pendente, com principal, juros, multa e mora separados. A origem preserva os valores recebidos e o histórico. Na última parcela, o saldo continua exigível e o acumulado pago não é zerado. `DebtRollover` registra a origem e o destino de cada componente.

O PIX só é emitido após ação explícita de geração, usando o valor devido atualizado. Caso já exista cobrança ativa da parcela que receberá o saldo, é necessário cancelar essa cobrança antes de alterar seus valores. Falha ou concorrência externa exige nova tentativa/conciliação, sem gerar dinheiro ou transferir uma dívida silenciosamente.

## Contabilidade e proteção

`InvestorTransaction` explica alterações da carteira. `FinancialEventHistory` registra os acontecimentos operacionais. `LedgerTransaction` registra origem, destino, valor e referência. `PlatformRevenue` identifica receitas reconhecidas. `CashFlowSnapshot` guarda o fechamento; não corrige automaticamente a base.

Movimentos internos entre caixa, carteira, receita e fundo não são uma segunda entrada de dinheiro externo. Resumos de caixa separam aportes, recebimentos, saques, desembolsos e tarifas. Datas de fechamento seguem o dia de Brasília, com limites UTC próprios para os recebimentos.

A cobertura por `ProtectionFund` usa o fundo como origem do pagamento; não registra simultaneamente um pagamento inexistente do tomador. Os pagamentos, repasses, históricos e estados relacionados são persistidos na mesma transação. As versões de carteira, operação, investimento e parcela avançam para rejeitar escritas antigas.

## Gateway

As operações persistidas `GatewayCharge`, `GatewayTransfer` e `GatewayWebhookEvent` vinculam a intenção local ao identificador externo. Um timeout após um envio não autoriza repetir a transferência: a referência persistida deve ser recuperada e conciliada primeiro.

O webhook exige token, identificação do evento, correspondência de pagamento/transferência, referência e valor. O evento `PAYMENT_CONFIRMED` não libera saldo; a liquidação ocorre em `PAYMENT_RECEIVED`. Saída exige `TRANSFER_DONE`. Eventos duplicados não repetem a contabilização. Estornos ou estados contraditórios são registrados para revisão, bloqueando novas saídas até a conciliação.

## Limites desta revisão

Nenhum banco operacional foi migrado, apagado ou conciliado automaticamente. Nenhuma cobrança ou transferência real foi enviada. Testes com respostas controladas não substituem homologação de credenciais, webhooks, cancelamentos, tarifas e saldos reais no Asaas. Dados antigos produzidos pelas simulações precisam de conferência própria.

O PDF, as anotações e relatórios antigos contêm regras de versões diferentes. Benchmarks e alegações anteriores de “impossibilidade de divergência” não foram reutilizados como prova. A validação atual será registrada no relatório de entrega desta revisão.
