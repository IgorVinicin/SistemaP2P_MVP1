describe('Fluxo do Tomador (Cliente)', () => {
  beforeEach(() => {
    cy.visit('/login');
    cy.get('input[formControlName="email"]').type('cliente1@p2p.com'); // Usuário padrão da seed
    cy.get('input[formControlName="senha"]').type('Senha123!');
    cy.get('button[type="submit"]').click();

    cy.url().should('include', '/dashboard');
  });

  it('Deve acessar o formulário de Solicitação de Empréstimo', () => {
    cy.visit('/loans/request');
    cy.contains('Nova Solicitação de Empréstimo');
  });

  it('Deve preencher e enviar uma solicitação de empréstimo', () => {
    cy.visit('/loans/request');
    
    // Preenche o formulário
    cy.get('input[formControlName="valorSolicitado"]').type('2500');
    cy.get('mat-select[formControlName="quantidadeParcelas"]').click();
    cy.get('mat-option').contains('12x').click();
    cy.get('textarea[formControlName="finalidade"]').type('Teste Automatizado Cypress - Capital de Giro');

    // Submete
    cy.get('button[type="submit"]').click();

    // Aguarda o snackbar ou redirecionamento
    cy.contains('sucesso', { matchCase: false }).should('exist');
  });

  it('Deve listar o empréstimo solicitado como Pendente', () => {
    cy.visit('/loans/my');
    cy.contains('Meus Empréstimos');
    
    // O último empréstimo solicitado deve aparecer na tabela com status Pendente
    cy.get('table').should('exist');
    cy.contains('Pendente');
    cy.contains('R$ 2.500,00');
  });
});
