describe('Fluxo de Investimento (Aporte/Alocação)', () => {
  beforeEach(() => {
    cy.visit('/login');
    cy.get('input[formControlName="email"]').type('investidor1@p2p.com');
    cy.get('input[formControlName="senha"]').type('Senha123!');
    cy.get('button[type="submit"]').click();

    cy.url().should('include', '/investor/dashboard');
  });

  it('Deve visualizar oportunidades e realizar um aporte simulado', () => {
    cy.contains('Oportunidades de Investimento');
    
    // Se a API retornar oportunidades, testamos o clique em "Investir"
    cy.get('body').then(($body) => {
      if ($body.text().includes('Risco:')) {
        cy.contains('button', 'Investir').first().click();
        
        // Verifica modal
        cy.get('mat-dialog-container').should('exist');
        cy.get('input[type="number"]').type('500'); // Valor do aporte
        cy.contains('button', 'Confirmar').click();
        
        cy.contains('sucesso', { matchCase: false }).should('exist');
      }
    });
  });
});
