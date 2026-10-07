# Planejamento Técnico — Integração com Gateway de Pagamento Asaas

**Status atualizado em 07/10/2026:** camada de integração implementada; configuração, decisões finais e homologação pendentes. Consulte [validação atual](validacao_atual_2026-10-07.md) e [fluxos implementados](asaas_fluxos_e_homologacao.md). O restante deste documento registra o planejamento original e não descreve integralmente o código atual.  
**Ambiente Inicial:** Asaas Sandbox (`https://sandbox.asaas.com/api/v3`)  
**Data do Planejamento:** 23 de Agosto de 2026  

---

## 1. Visão Geral da Integração

O ecossistema do Sistema P2P passará a realizar cobranças dinâmicas PIX, liquidações automáticas via Webhook e transferências PIX diretamente através da API do Asaas.

### Fluxos Principais

1. **Aporte / Depósito de Investidor:**
   - Investidor solicita depósito via painel.
   - Backend cria/obtém cliente no Asaas (`/v3/customers`) e gera cobrança PIX (`/v3/payments`).
   - Frontend exibe imagem do QR Code + Código Copia-e-Cola com botão de cópia de 1 clique.
   - Investidor paga no banco -> Asaas dispara webhook `PAYMENT_RECEIVED` -> Backend dá baixa instantânea na carteira (`InvestorWallet`) com lançamento no `LedgerTransaction` (duplo rastro contábil).

2. **Pagamento de Parcela de Empréstimo (Tomador):**
   - Tomador acessa modal de pagamento de parcela.
   - Backend gera cobrança PIX com multa/juros de mora contratuais já embutidos.
   - Tomador paga o PIX -> Asaas dispara webhook `PAYMENT_RECEIVED` -> Backend atualiza status da parcela para "Paga", atualiza status do empréstimo e aciona o `WalletDistributionService` para amortização e repasse proporcional aos investidores do grupo.

3. **Saque de Investidor / Desembolso de Empréstimo:**
   - Ao aprovar o saque ou desembolso de empréstimo no Admin, o sistema pode acionar a transferência PIX automática via Asaas (`/v3/transfers`).

---

## 2. Configurações e Variáveis de Ambiente Necessárias

No arquivo `backend/.env` ou `backend/appsettings.json`:
```env
ASAAS_API_KEY="$seu_token_api_asaas_sandbox"
ASAAS_BASE_URL="https://sandbox.asaas.com/api/v3"
ASAAS_WEBHOOK_ACCESS_TOKEN="token_secreto_definido_para_o_webhook"
```

---

## 3. Estrutura a Ser Criada no Código

### 3.1 Backend (.NET 8)
- **Entidades:**
  - `User`: adicionar `AsaasCustomerId` (string).
  - `DepositRequest`: adicionar `AsaasPaymentId`, `PixQrCodeBase64`, `PixCopiaECola`, `AsaasInvoiceUrl`.
  - `LoanInstallment`: adicionar `AsaasPaymentId`, `PixQrCodeBase64`, `PixCopiaECola`, `AsaasInvoiceUrl`.
  - `WithdrawalRequest` / `LoanRequest`: adicionar `AsaasTransferId`, `ChavePix`, `TipoChavePix`.
- **Serviços:**
  - `backend/Application/Services/Asaas/IAsaasService.cs`
  - `backend/Application/Services/Asaas/AsaasService.cs` (com chamadas a `/v3/customers`, `/v3/payments`, `/v3/transfers`).
- **Webhooks & Controllers:**
  - `backend/API/Controllers/AsaasWebhookController.cs` (`POST /api/webhooks/asaas`).
  - Atualização do `InvestorTreasuryController.cs` e `LoanInstallmentController.cs` para emitir PIX dinâmico.

### 3.2 Frontend (Angular)
- `frontend/src/app/core/services/asaas.service.ts`
- Atualização do modal de depósito em `investor-dashboard.component` para exibir QR Code PIX e Copia-e-Cola.
- Atualização do `pay-installment-dialog.component` para exibir QR Code PIX dinâmico para pagamento da parcela.

---

## 4. Como Retomar Este Trabalho no Futuro
Quando for iniciar a implementação, basta informar:
> *"Quero iniciar a implementação da integração com o Asaas conforme o plano em plano_integracao_asaas.md"*
