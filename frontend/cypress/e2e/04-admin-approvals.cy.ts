describe('Fluxo de Aprovação do Admin', () => {
  beforeEach(() => {
    cy.visit('/login');
    cy.get('input[formControlName="email"]').type('admin@p2p.com');
    cy.get('input[formControlName="senha"]').type('Senha123!');
    cy.get('button[type="submit"]').click();

    cy.url().should('include', '/admin');
  });

  it('Deve carregar a lista de empréstimos e aprovar um pendente', () => {
    cy.visit('/admin/loans');
    cy.contains('Gestão de Empréstimos');
    cy.get('table').should('exist');

    // Abre o detalhe do primeiro empréstimo pendente
    cy.contains('td', 'Pendente')
      .parent('tr')
      .find('button')
      .contains('Detalhes')
      .click();

    // Dentro do modal de detalhes, clica em Aprovar
    cy.get('mat-dialog-container').should('exist');
    cy.contains('Aprovar Empréstimo').click();

    // Valida snackbar de sucesso
    cy.contains('sucesso', { matchCase: false }).should('exist');
  });
});
