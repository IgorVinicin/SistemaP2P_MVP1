# Validação local com PostgreSQL — 07/10/2026

Foi utilizada a conexão local configurada em `backend/.env`, sem expor credenciais. Servidor PostgreSQL 18.4.

O dump `sistemap2p_dump.sql` foi restaurado no banco isolado `p2p_migrationcheck_5015e50ef4f34039b99de1e84f0e99d5`, preservado para inspeção. O banco configurado da aplicação não recebeu migrações nem alterações de dados.

## Migrações na cópia

1. `20261007213016_GatewaySafetyAndFinancialConcurrency`.
2. `20261007220328_PreserveRequestedWithdrawalAmount`.

As duas foram aplicadas com sucesso. O histórico da cópia passou de 24 para 26 migrações. As contagens de usuários, carteiras, empréstimos, parcelas, investimentos, depósitos, saques e ledger coincidiram com o dump. Os totais de saldo disponível, bloqueado, comprometido, investido e rendimentos foram preservados.

## Dados históricos

A cópia possui 99 parcelas com valor pago e 67 investimentos com principal já amortizado. A migração adiciona campos de componentes pagos e distribuídos com zero; ela não comprova nem reconstrói os repasses antigos. Esses registros exigem conciliação histórica antes da operação do gateway sobre os contratos existentes.

Não foram inventados pagamentos, repasses ou componentes para corrigir o histórico. Pagamentos parciais antigos sem componentes e distribuição reconhecidos agora são bloqueados até conciliação, inclusive na emissão do PIX e no destino de rolagem. A inicialização automática por proporção foi removida; quatro casos de teste verificam ausência de alterações e de chamadas ao gateway.

## Testes de banco real

Os testes criam bancos temporários próprios e verificam rollback, depósitos simultâneos com reentregas e rejeição de uma versão antiga da carteira. A execução inicial encontrou o conflito PostgreSQL `40001` esperado dentro de um encapsulamento do driver que o teste não reconhecia. A classificação foi ajustada exclusivamente para esse código; outros erros continuam causando falha.

Resultado final: **130 testes aprovados, zero falhas e zero testes ignorados**, com os três testes de PostgreSQL habilitados e aprovados. O teste de concorrência também verifica duas reentregas com novos contextos: um único crédito, um movimento da carteira e dois lançamentos contábeis. O frontend permanece com 24 testes e build aprovados na execução anterior; não foi alterado nesta validação de banco.

Os bancos temporários criados pelos testes foram removidos pelo próprio ciclo dos testes; a cópia restaurada do dump foi mantida para inspeção. As validações locais não representam homologação da API Asaas e não certificam os saldos históricos.
