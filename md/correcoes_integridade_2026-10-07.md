# Correções de integridade financeira — 07/10/2026

O sistema combina carteira de investidores, alocação de capital em empréstimos e remuneração periódica do pool. O principal pago pelo tomador retorna proporcionalmente aos investidores; juros e encargos recebidos pertencem à plataforma. O rendimento do investidor é calculado separadamente, conforme produto/taxa, evitando duplicar juros do tomador.

## Alterações implementadas

- Pagamento confirmado usa `InstallmentPaymentService`: pagamento acumulado, histórico, distribuição, ledger e atualização do empréstimo participam da mesma transação. Webhook precisa adicionar sua própria identificação única para impedir que o mesmo recebimento externo seja aplicado novamente.
- Distribuição considera apenas o valor efetivamente recebido e ainda não contabilizado. Reexecutar distribuição com o mesmo acumulado não cria crédito adicional. Principal da plataforma permanece em caixa; apenas juros/encargos são receita.
- Rateio usa maior resto e desempate deterministico para conservar centavos; a liquidacao final devolve exatamente o capital pendente quando o recebimento cobre esse principal.
- Tokens existentes de concorrência passam a avançar a cada escrita. Parcela e investimento também possuem token, incluído na migração do gateway.
- Aprovação e alocação reservam capital, sem registrar dinheiro desembolsado ao tomador. O desembolso só deve entrar no ledger após confirmação da transferência pelo Asaas. Alocação prepara alterações; aprovação persiste atomicamente.
- Endpoints antigos de pagamento simulado e quitação integral sem confirmação foram removidos. Baixa administrativa individual exige observação contendo comprovante ou justificativa do recebimento confirmado.
- Parcelas calculadas mantêm `ValorParcela = ValorPrincipal + ValorJuros`, inclusive na última parcela, e conservam os totais em centavos.
- Parcela parcialmente paga segue no acompanhamento de atraso; empréstimos ainda sem desembolso não entram na rotina de inadimplência.
- Cobertura pelo fundo usa uma única entrada `ProtectionFund -> PlatformCash`; não registra um pagamento fictício adicional do tomador. A cobertura preserva pagamentos anteriores e deixa encargos descobertos na parcela. Quitação administrativa de todo empréstimo sem comprovantes foi bloqueada; cada recebimento deve ter confirmação individual.

## Rollover de pagamentos parciais

A rolagem preserva a regra confirmada pelo usuario: o saldo nao recebido passa para a proxima parcela pendente, separando principal, juros, multa e mora. A origem conserva o valor recebido, seus componentes efetivamente pagos e o historico; a entidade `DebtRollover` registra a origem, destino e cada componente transferido. Juros e multas nunca sao convertidos em principal. Na ultima parcela, o pagamento acumulado permanece e o saldo continua exigivel com status `Parcial`.

Uma cobranca PIX ativa da parcela de destino deve ser cancelada antes de qualquer mudanca de valor. O webhook cancela essa cobranca fora da transacao financeira e so aplica o recebimento/rolagem depois da confirmacao do cancelamento. Se o cancelamento falha ou concorre com um pagamento, o processamento precisa de nova tentativa ou conciliacao; nenhuma alteracao silenciosa do contrato deve ocorrer. A baixa manual rejeita rolagem enquanto existir cobranca ativa do destino.

Componentes pagos sao registrados separadamente (`PrincipalPago`, `JurosPagos`, `MultaPaga`, `JurosMoraPagos`), portanto novos encargos posteriores nao reinterpretam dinheiro ja distribuido. Multa e mora transferidas possuem campos separados e sao preservadas no recalculo de atraso.

## Validação e limites

### Aprovação administrativa de saque

O investidor solicita o saque e reserva o valor; o administrador pode aprovar integralmente ou escolher um valor positivo menor. `ValorSolicitado` mantém o pedido original e `Valor` passa a representar apenas o montante aprovado para a transferência. O corpo de aprovação aceita `valorAprovado`; omitir esse campo aprova todo o pedido. A aprovação só ocorre uma vez, no estado Pending.

Exemplo: pedido de R$ 1.000 com aprovação de R$ 400 libera R$ 600 imediatamente para o disponível e mantém R$ 400 reservados. O retorno parcial fica em `InvestorTransactions` e a decisão em `FinancialEventHistory`; não representa uma entrada externa de dinheiro. Se a transferência falhar, devolve somente os R$ 400 ainda reservados, uma única vez. O webhook de conclusão também contabiliza somente R$ 400.

A migração `20261007220328_PreserveRequestedWithdrawalAmount` acrescenta apenas `WithdrawalRequests.ValorSolicitado` nullable em numeric(18,2). Registros anteriores usam o valor existente como referência original até sua primeira aprovação. Foram acrescentados oito casos de teste para aprovação parcial, validação do valor, preservação do original, liberação e retorno de reservas e tentativa concorrente de aprovação.

### Carência de saque por depósito

Cada depósito confirmado libera para saque após 72 horas de `ProcessedAt`. A data de criação da solicitação não inicia a carência. O serviço compartilhado calcula `SaldoSacavel = max(0, SaldoDisponivel - soma dos depósitos Completed ainda em carência)`. Um novo aporte não bloqueia novamente o saldo antigo liberado. Essa fórmula é conservadora quando há investimentos e retornos simultâneos: apenas o saldo disponível participa do cálculo; saldos investido, comprometido e reservado nunca são acrescentados ao valor sacável.

Antes de existir ao menos um depósito confirmado que já completou 72 horas, o saldo sacável é zero, inclusive eventuais créditos diários. Isso também impede sacar saldos legados sem qualquer origem de depósito confirmado, que exigem conciliação. Depois que um depósito amadurece, rendimentos já creditados podem compor o disponível sacável, descontados os novos depósitos ainda em carência.

O saldo em carência não é movido para `SaldoBloqueado`, que continua representando reservas de pedidos de saque. A carteira expõe `SaldoSacavel`, `SaldoEmCarencia` e `ProximaLiberacaoSaque`. Depósitos antigos com status Completed sem confirmação registrada permanecem em carência até conciliação da data real, sem inventar data de liberação. A próxima liberação indica o primeiro depósito com data conhecida que amadurece, e não a liberação integral de todo saldo.

Foram acrescentados testes de recebimento parcial, liquidação em duas etapas, repetição da distribuição, rejeição de valor acima do saldo, conservação de componentes e conflito entre duas carteiras carregadas simultaneamente. A suíte passou com 53 testes neste ponto. O provedor desses testes é InMemory: eles validam regras e tokens, mas não comprovam rollback ou isolamento serializável no PostgreSQL. A homologação deve cobrir falha durante persistência, dois webhooks concorrentes, duas retiradas simultâneas e reconciliação de saldos reais.

Dados financeiros existentes não foram corrigidos automaticamente. Valores gerados por simulações, distribuições repetidas ou rolagens antigas precisam ser conciliados antes de produção.

Parcelas históricas com recebimento anterior e sem componentes pagos comprovados não permitem novos pagamentos nem emissão de PIX. Não há inferência proporcional automática: a soma dos componentes deve corresponder ao recebido, os valores devem estar dentro dos componentes devidos e o principal distribuído deve corresponder ao principal reconhecido. A mesma verificação protege a parcela de destino da rolagem. A mensagem de conciliação histórica indica uma pendência real dos dados, não uma autorização para preencher os novos campos por estimativa.
