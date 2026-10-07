# Estado da revisão em 07/10/2026

Este documento atualiza os diagnósticos históricos. A revisão inclui a carência de saque e o desembolso pelo Asaas autorizados pelo usuário.

## Estrutura e alterações

O mapa do sistema está em [estrutura_funcional_sistema.md](estrutura_funcional_sistema.md). As correções financeiras estão em [correcoes_integridade_2026-10-07.md](correcoes_integridade_2026-10-07.md), e os fluxos implementados do gateway em [asaas_fluxos_e_homologacao.md](asaas_fluxos_e_homologacao.md).

Foram corrigidos pagamentos simulados, distribuição duplicada, perda de centavos, componentes de pagamentos parciais, reserva e confirmação de saques, concorrência financeira, autenticação de webhooks, repetição de requisições, contabilização de caixa e proteção de documentos. Rotinas de demonstração deixaram de redefinir contas e saldos automaticamente. O frontend preserva os fluxos existentes; a nova ação de desembolso foi expressamente autorizada pelo usuário.

PIX só é criado após a ação explícita de gerar. Saldo parcial passa à próxima parcela com principal, juros e encargos separados; a última mantém o saldo pendente. Uma cobrança já emitida precisa ser cancelada antes da alteração do valor.

## Validação executada

- Backend: 130 testes aprovados, nenhum com falha ou ignorado. Os três testes de PostgreSQL passaram com servidor local e bancos temporários isolados.
- Frontend: 24 testes aprovados; compilação de produção aprovada, com avisos de tamanho e CommonJS.
- Duas migrações incrementais aplicadas na cópia isolada do dump, com registros e totais de carteiras preservados. Modelo conferido sem alterações pendentes. O banco operacional não foi alterado. Veja [validação PostgreSQL](validacao_postgresql_2026-10-07.md).
- As chamadas do Asaas foram validadas com respostas controladas em testes. Não houve transação bancária real nem homologação com credenciais.

Há avisos de restauração NuGet por indisponibilidade da fonte e resolução de dependência pelo cache. Não foi possível concluir auditoria remota de dependências nesta execução.

## Regras confirmadas e rendimento

1. Rendimento diário: o usuário esclareceu que é apenas estimativa. A rotina de crédito automático e o endpoint administrativo diário foram removidos. O painel calcula a projeção com os saldos remunerados e taxas correspondentes, sem movimentar a carteira. O crédito ocorre no fechamento mensal; a estimativa não é descontada. Apenas créditos diários efetivamente lançados por versões antigas continuam sendo considerados no fechamento para evitar pagamento duplicado, até a conciliação dos dados legados.
2. Saques: apenas saldo disponível liberado, com 72 horas desde a confirmação de cada depósito. Antes do primeiro depósito maduro, nenhum valor pode ser sacado; aportes posteriores não reiniciam o prazo do saldo antigo.
3. Desembolso: o usuário autorizou o envio pelo painel administrativo após validar os fluxos. A aprovação reserva capital; a confirmação de envio apresenta tomador, CPF e valor aprovado. A conclusão no Asaas efetiva o desembolso. Atualizar uma tentativa incerta recupera a operação anterior.

O fechamento mensal tem registro durável mesmo quando o total adicional devido é zero, impedindo repetição. A estimativa diária continua disponível após o fechamento, sem criar créditos.

O saque solicitado passa pela análise do administrador, que pode aprovar integralmente ou um valor menor. O valor original permanece no pedido, a diferença volta ao disponível e somente o montante aprovado pode seguir para o PIX. Aprovação e envio continuam sendo ações separadas.

A migração `PreserveRequestedWithdrawalAmount` acrescenta somente `WithdrawalRequests.ValorSolicitado`, nullable e com precisão de centavos. Pedidos legados usam seu valor existente como original; novos pedidos preservam o solicitado antes da aprovação parcial. O script incremental está em [partial_withdrawal_migration.sql](partial_withdrawal_migration.sql), posterior à migração do gateway. Nenhuma migração foi aplicada ao banco operacional.

## Conexão e homologação

Preencher as configurações pelos exemplos do backend, configurar o webhook e homologar aportes, parcelas parciais, saques, desembolsos, falhas e eventos repetidos em sandbox. Conferir backup e dados antigos antes da migração. Saldos históricos não foram reescritos automaticamente.

O antigo plano de integração é histórico: a camada Asaas já foi implementada. Configuração e homologação permanecem necessárias; os testes locais não comprovam liquidação bancária real.
