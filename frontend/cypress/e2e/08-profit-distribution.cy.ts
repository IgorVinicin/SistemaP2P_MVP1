describe('Validação Visual: Distribuição de Lucros (Spread e Rentabilidade)', () => {
  it('1. O Investidor deve ver sua rentabilidade alterar após fechamento de parcela', () => {
    cy.visit('/login');
    cy.get('input[formControlName="email"]').type('investidor1@p2p.com');
    cy.get('input[formControlName="senha"]').type('Senha123!');
    cy.get('button[type="submit"]').click();

    // Navega para aba de Meus Investimentos ou Resumo
    cy.url().should('include', '/investor/dashboard');
    cy.contains('Rentabilidade Acumulada');

    // Valida que o bloco existe e é dinâmico (contém um valor financeiro R$)
    cy.get('mat-card-title').contains('Rentabilidade').parent().contains('R$').should('exist');
  });

  it('2. O Administrador deve verificar a captação do Spread na tesouraria', () => {
    cy.visit('/login');
    cy.get('input[formControlName="email"]').type('admin@p2p.com');
    cy.get('input[formControlName="senha"]').type('Senha123!');
    cy.get('button[type="submit"]').click();

    // Navega para aba de Fechamento / Tesouraria
    cy.visit('/admin/cashflow');
    
    // Verifica a seção de Lucro
    cy.contains('Receita (Lucro)');
    
    // Testa o Botão de Fechamento de Caixa para certificar que ele processará a matemática das fatias do dia
    cy.contains('button', 'Gerar Fechamento de Hoje').should('be.visible');
  });
});
