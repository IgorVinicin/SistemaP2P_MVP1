describe('Dashboard do Investidor', () => {
  beforeEach(() => {
    // Fazer login como Investidor antes de cada teste desta suíte
    cy.visit('/login');
    cy.get('input[formControlName="email"]').type('investidor1@p2p.com');
    cy.get('input[formControlName="senha"]').type('Senha123!');
    cy.get('button[type="submit"]').click();

    // Garantir que chegou no painel
    cy.url().should('include', '/investor/dashboard');
  });

  it('Deve carregar o resumo da carteira', () => {
    // Validando a renderização dos cards (baseado na estrutura do componente que conhecemos)
    cy.contains('Saldo Disponível');
    cy.contains('Em Risco (Inadimplência)');
    cy.contains('Rentabilidade Acumulada');
  });

  it('Deve carregar a tabela de Oportunidades de Investimento', () => {
    // Verificar se o título ou a tabela renderizaram
    cy.contains('Oportunidades de Investimento');
    cy.get('table').should('exist');
  });
});
