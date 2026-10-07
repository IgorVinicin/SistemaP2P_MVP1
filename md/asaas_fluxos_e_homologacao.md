# Fluxos Asaas e homologação

Este arquivo descreve os contratos implementados nesta retomada. Não comprova homologação na conta Asaas nem auditoria dos saldos existentes.

## Cobranças PIX

- O depósito e o pagamento de parcela emitem PIX somente após a ação explícita do usuário.
- A operação local registra o valor autorizado e uma referência `CHARGE_<id>` antes de enviar a cobrança ao gateway.
- O QR Code e a referência de pagamento ficam persistidos para recuperar a mesma cobrança. Falhas não produzem PIX fictício nem crédito simulado.
- Existe no máximo uma cobrança ativa por parcela. A cobrança pode ter um valor parcial, respeitando o saldo devedor.
- A carteira e a parcela só recebem baixa em `PAYMENT_RECEIVED`, com token de webhook, ID do evento, ID da cobrança, referência, forma PIX e valor correspondentes à operação local.
- `PAYMENT_CONFIRMED` não libera saldo: a documentação atual admite bloqueio cautelar para PIX nesse estado. [Criar cobrança](https://docs.asaas.com/reference/criar-nova-cobranca).
- Pagamento parcial com próxima parcela transfere principal, juros, multa e mora separadamente. Se a próxima parcela já possuir PIX, o gateway precisa confirmar seu cancelamento antes da transferência de dívida. A última parcela mantém o saldo parcial em aberto.
- Tarifas informadas pelo gateway são registradas separadamente no ledger. A carteira recebe o aporte bruto; a plataforma arca com a diferença entre bruto e líquido.

## Saques e transferências

- Solicitar saque reserva somente o saldo sacável calculado pelo backend e exige chave PIX e seu tipo. Cada depósito tem carência de 72 horas desde sua confirmação. Antes do primeiro depósito maduro, nenhum saldo é sacável, inclusive rendimento. Novos depósitos não reiniciam o prazo dos valores antigos. Capital investido, comprometido ou reservado não entra no saldo sacável. Aprovar autoriza o envio; enviar não equivale a liquidar.
- Uma tentativa durável `TRANSFER_<id>` é criada antes do envio externo.
- A análise administrativa pode aprovar um valor menor que o solicitado. O pedido preserva seu valor original e libera a diferença da reserva para o disponível; o PIX usa somente o valor aprovado. A aprovação não envia dinheiro automaticamente.
- `TRANSFER_DONE` baixa a reserva e registra o saque. Falha ou cancelamento confirmado libera a reserva uma única vez.
- O gateway prevê conclusão, falha e cancelamento por webhook; a criação da transferência não prova conclusão. [Referência oficial de transferências](https://docs.asaas.com/reference/transferir-para-conta-de-outra-instituicao-ou-chave-pix).
- O administrador pode enviar o empréstimo aprovado pelo painel, funcionalidade autorizada pelo usuário. A confirmação apresenta nome, CPF e valor; usa exclusivamente a chave CPF cadastrada do tomador, que precisa estar registrada no banco destinatário. Não há consulta de titularidade via DICT. O backend confere aprovação do tomador, valor revisado, reservas de investimento, parcelas e ausência de desembolso anterior. Somente a conclusão ativa os investimentos e registra a saída.
- Para uma tentativa pendente, a ação de atualizar envio recupera a transferência existente; não cria outra. O empréstimo concluído não permite reenvio. Uma recusa definitiva permite nova tentativa; uma resposta incerta exige recuperar e conciliar a anterior.

## Falhas, concorrência e conciliação

- Eventos possuem identificação única e são deduplicados. As transações financeiras usam isolamento serializável no PostgreSQL e versões incrementadas para detectar gravações concorrentes.
- Uma falha incerta de rede não dispara automaticamente outro `POST`. A referência local é consultada no gateway. Transferências são procuradas nas páginas do histórico, porque a documentação de listagem não declara filtro por `externalReference`. [Listar transferências](https://docs.asaas.com/reference/listar-transferencias).
- Recusas HTTP inequívocas antes da criação, como 400/401/403/422, encerram a tentativa; saques recusados devolvem sua reserva. Timeout ou falha de servidor preservam o estado para conciliação.
- Estornos e eventos contraditórios ficam em `NeedsReview` e bloqueiam novas saídas financeiras. Não há reversão automática de dinheiro que possa ter sido aplicado, distribuído ou sacado. Essas ocorrências precisam de conciliação administrativa e lançamento corretivo fundamentado.
- Os endpoints de consulta e recuperação apoiam os fluxos existentes; não representam novas funcionalidades de interface.

## Banco de dados

A migração `GatewaySafetyAndFinancialConcurrency` é incremental: adiciona campos de reconhecimento/rolagem às parcelas, versões às parcelas e aos investimentos, hash às chaves de idempotência e as tabelas `DebtRollovers`, `GatewayCharges`, `GatewayTransfers`, `GatewayWebhookEvents`. Não recria tabelas antigas.

O script gerado está em [gateway_safety_migration.sql](gateway_safety_migration.sql). Aplicar a migração exige avaliar os dados legados e preservar um backup. A geração do script não aplicou alterações ao banco.

A migração posterior `PreserveRequestedWithdrawalAmount` adiciona apenas o campo nullable `ValorSolicitado` aos pedidos de saque, conservando o valor original quando a aprovação administrativa é menor. Seu script incremental está em [partial_withdrawal_migration.sql](partial_withdrawal_migration.sql).

## O que depende da conexão real

1. Configurar `ASAAS_API_KEY`, `ASAAS_BASE_URL` e `ASAAS_WEBHOOK_ACCESS_TOKEN`, inicialmente em sandbox.
2. Cadastrar webhook HTTPS para `/api/webhooks/asaas`, com o mesmo token enviado no cabeçalho `asaas-access-token`, e os eventos relevantes de cobranças e transferências. [Webhooks oficiais](https://docs.asaas.com/docs/sobre-os-webhooks).
3. Aplicar a migração em uma cópia segura do banco e conferir cobranças/saldos/parcelas legados antes da produção.
4. Homologar cobrança, QR Code, recebimento, tarifa, pagamento parcial com rolagem, repetição de eventos, indisponibilidade, saque concluído e saque recusado.
5. Validar a política de conciliação de estornos e eventos contraditórios com o operador financeiro antes de usar dinheiro real.

Os testes de API Asaas usam transportes falsos. A suíte também foi executada com PostgreSQL local: rollback, confirmações concorrentes/reentregas e versões antigas de carteira passaram. As duas migrações foram aplicadas na cópia isolada do dump com registros e totais preservados; detalhes em [validação PostgreSQL](validacao_postgresql_2026-10-07.md). A entrega real de webhooks e os fluxos externos permanecem sujeitos à homologação em sandbox; nenhum débito, cobrança ou transferência bancária real foi enviado nesta revisão.
