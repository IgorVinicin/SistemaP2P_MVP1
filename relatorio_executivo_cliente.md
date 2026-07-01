# 📊 Dossiê Técnico e Funcional: Plataforma P2P (MVP V1)

**Data:** 30 de Junho de 2026  
**Foco do Documento:** Regras de Negócio, Motores Financeiros e Arquitetura do Sistema  

---

## 1. Visão Arquitetural do Negócio
O sistema opera como um ecossistema multilateral em "Partidas Dobradas". Ou seja, todo o dinheiro que transita entre um tomador de crédito e um investidor não é apenas uma "soma em uma tabela", mas sim uma movimentação contábil espelhada em um *Livro Razão (Ledger)*. Isso garante que a Plataforma P2P funcione sob rígidos padrões de *Compliance*, impossibilitando a criação ou o sumiço de capital no vácuo.

---

## 2. Regras de Negócio e Funções Detalhadas

### 2.1. Motor de Risco e Análise de Crédito (Score)
O coração da concessão de limites de crédito. O sistema possui um motor autônomo que analisa o Tomador antes de permitir qualquer solicitação.
* **Simulação de Score (Mocada):** Ao invés de uma dependência rígida de birôs (Serasa/SPC) na fase de MVP, o sistema possui um algoritmo interno que gera um Score baseado em critérios simulados (Renda, Histórico, CPF). 
* **Classificação de Risco (Rating):** Os tomadores caem em quatro faixas:
  * **Score A (Acima de 800):** Baixo risco. Libera o limite máximo (ex: R$ 50.000). Acesso às menores taxas de juros do sistema (ex: 1.5% a.m.).
  * **Score B (600 a 799):** Risco moderado. Limite intermediário. Taxas padrão.
  * **Score C (400 a 599):** Risco alto. Limite restrito. Alta taxa de juros (prêmio de risco para atrair o investidor).
  * **Score D (Abaixo de 400):** Risco crítico. Bloqueio sumário da concessão de crédito. A solicitação é morta na origem, poupando a tesouraria.
* O Administrador tem uma tela exclusiva no Backoffice chamada **Configurações de Crédito**, onde ele altera em tempo real quais são os limites e juros de cada faixa de Score, sem precisar de um programador.

### 2.2. A Esteira do Tomador de Crédito (Borrower Flow)
A jornada do usuário que precisa de dinheiro segue uma esteira imutável:
1. **Solicitação:** Ele informa o valor desejado, o número de parcelas e o motivo do crédito. O sistema imediatamente trava a requisição contra o seu limite máximo aprovado pelo Score.
2. **Status "Pendente":** A solicitação entra no limbo. O dinheiro não é liberado e o sistema notifica o *Backoffice* (Administradores).
3. **Assinatura e Garantia:** Uma vez aprovada pelos Administradores, a solicitação cai no mercado para captação. O tomador visualiza um "Contrato Simulado" no seu painel e os boletos/pix (parcelas matemáticas) são gerados no banco de dados.

### 2.3. O Motor de Captação e Investimentos (Funding Engine)
É aqui que a Plataforma P2P ganha vida e conecta as pontas.
* **A Vitrine (Marketplace):** Investidores fazem login e enxergam uma lista de oportunidades. Eles veem o risco (Score do Tomador), a finalidade do empréstimo e a taxa prometida. Identidades sensíveis (CPF completo) não são vazadas.
* **Investimento Fracionado:** O sistema não obriga um investidor a bancar os R$ 10.000 de um pedido. Um investidor pode aportar R$ 500, outro R$ 1.500 e outro R$ 8.000. 
* **Regra de "Saldo Comprometido":** Quando o investidor clica em investir, o sistema *não* desconta o dinheiro imediatamente. Ele move o valor do `Saldo Disponível` para o `Saldo Comprometido`. Se o empréstimo for cancelado antes da captação total, o dinheiro volta limpo para o investidor. Se a captação tiver sucesso (Funding de 100%), o dinheiro se transforma em `Saldo Ativo` e a operação inicia.

### 2.4. A Regra de Ouro: Distribuição de Lucros e o Spread da Plataforma
A funcionalidade mais complexa da engenharia P2P é quando o tomador **paga a parcela**. O sistema precisa fatiar um único PIX do tomador para 5, 10 ou 50 investidores simultaneamente, e ainda tirar o lucro do dono da plataforma.
* **A Fração de Aporte:** O sistema calcula a % de posse de cada investidor. (Se ele investiu 1.000 em um empréstimo de 10.000, ele tem direito a 10% do principal pago na parcela).
* **O Spread (Lucro da Plataforma):** O Tomador paga uma taxa de, por exemplo, 3% ao mês (baseado no risco dele). Mas a plataforma *promete* ao Investidor um retorno seguro de apenas 1.5% ao mês.
* **O Repasse Cíclico:** O sistema roda um loop em altíssima velocidade no servidor no momento do pagamento. Ele deposita 10% do principal pro investidor 1 + o juros dele (limitado a 1.5%). Ao fim do cálculo de todos os investidores, os 1.5% restantes (a gordura do empréstimo) são enviados automaticamente para a conta contábil `PlatformRevenue` (Receita da Plataforma).
* **Testes Científicos:** Uma bateria inteira de robôs no backend roda 24 horas por dia simulando pagamentos com dízimas periódicas. Se a conta fechar sobrando ou faltando *1 centavo*, o sistema aborta a transação e protege o caixa.

### 2.5. A Tesouraria e o Livro-Razão (Ledger)
A área mais restrita do Administrador e a fundação técnica do sistema.
* **Caixa Operacional:** O administrador enxerga todas as moedas rodando na plataforma. Quanto de dinheiro vivo os investidores têm na mão, quanto está girando nos empréstimos e o lucro líquido acumulado retido (*Spread* e multas por atraso).
* **Snapshots Diários:** A função de "Fechamento de Caixa". Ao clicar no botão, o sistema tira uma fotografia perene do balanço financeiro, permitindo que a diretoria acompanhe em gráficos a saúde do fundo no longo prazo, como num sistema bancário real.
* **Bloqueio em Saques (Saldo Bloqueado):** Para impedir fraudes onde o usuário solicita saque de R$ 5.000 duas vezes ao mesmo tempo (Race Conditions), o sistema possui travas *ACID* no banco de dados. Assim que o pedido de saque é feito para a conta física real, o saldo na tela é "congelado". O investidor não pode mais alocar esse dinheiro na Vitrine enquanto o saque está pendente na Tesouraria.

---

## 3. Arquitetura de Software Atual (Tecnologia Empregada)
* **Frontend:** *Angular 19* com arquitetura modular (Standalone Components). O design obedece as diretrizes rígidas do Google Material UI. Painéis assíncronos e dinâmicos para suportar milhares de acessos.
* **Backend:** *.NET C#* com Entity Framework Core. Motor fortemente tipado. Possui proteção em memória e transações rígidas anti-falha.
* **Garantia de Qualidade:** O projeto detém suíte de auditoria Cypress instalada, que simula bots testando formulários na interface em frações de segundo, para provar que uma atualização visual não quebrou um motor financeiro.

---

## 4. Próxima Fronteira (Foco do Alinhamento)
O MVP encontra-se "Maturado" nas suas lógicas matemáticas e regras de banco. O passo primordial após esta revisão é o **Plano de Integração (Gateway de Pagamentos)**.
* Transformar o botão *Simular Depósito* num **Gerador de PIX Copy-Paste Dinâmico**.
* Substituir as "Aprovações de Saque Manuais" na Tesouraria por disparos de TED/PIX via API (Efí/Stripe/MercadoPago).
* Subir a aplicação para Produção (Go-Live na AWS, Azure ou Host similar).

---
*Relatório confidencial gerado pelo Módulo de Engenharia e Arquitetura do Sistema P2P.*
