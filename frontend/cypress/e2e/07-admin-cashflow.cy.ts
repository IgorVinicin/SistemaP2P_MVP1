describe('Fluxo de Fechamento de Caixa', () => {
  beforeEach(() => {
    cy.visit('/login');
    cy.get('input[formControlName="email"]').type('admin@p2p.com');
    cy.get('input[formControlName="senha"]').type('Senha123!');
    cy.get('button[type="submit"]').click();

    cy.url().should('include', '/admin');
  });

  it('Deve navegar até CashFlow e gerar um snapshot', () => {
    cy.visit('/admin/cashflow');
    
    // Confirma que os cards financeiros existem
    cy.contains('Saldo Operacional (Caixa)');
    cy.contains('Aportes Reais (Depósitos)');
    
    // Dispara o Snapshot
    cy.contains('button', 'Gerar Fechamento de Hoje').click();
    
    // O sistema possui um `confirm()` nativo do navegador para o snapshot
    // O Cypress mocka confirms retornando true automaticamente, então o snapshot será gerado!
    
    cy.contains('sucesso', { matchCase: false }).should('exist');
  });
});
