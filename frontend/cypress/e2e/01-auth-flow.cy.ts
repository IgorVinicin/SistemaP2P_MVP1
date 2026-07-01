describe('Fluxo de Autenticação', () => {
  it('Deve carregar a tela de login', () => {
    cy.visit('/login');
    cy.get('h2').contains('Entrar no P2P Lending');
  });

  it('Deve tentar logar sem preencher campos e ver mensagens de erro', () => {
    cy.visit('/login');
    cy.get('button[type="submit"]').click();
    
    // Supondo que você tenha validações de required no form
    cy.get('mat-error').should('exist');
  });

  it('Deve fazer login como Administrador', () => {
    cy.visit('/login');
    
    // Inserir dados do Admin padrão do DB
    cy.get('input[formControlName="email"]').type('admin@p2p.com');
    cy.get('input[formControlName="senha"]').type('Senha123!');
    
    cy.get('button[type="submit"]').click();

    // Aguardar redirecionamento para o dashboard do admin
    cy.url().should('include', '/admin');
    
    // Verificar se algum componente da home do admin renderizou (ajuste o seletor conforme seu HTML)
    cy.contains('Métricas de Operações'); 
  });
});
