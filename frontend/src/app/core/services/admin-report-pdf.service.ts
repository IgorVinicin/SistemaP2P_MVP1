import { Injectable, inject } from '@angular/core';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';
import { AuthService } from './auth.service';
import {
  BorrowerConsolidatedItem,
  BorrowerConsolidatedSummary,
  InvestorConsolidatedItem,
  InvestorConsolidatedSummary,
  LoanConsolidatedItem,
  LoanConsolidatedSummary,
  FinancialConsolidatedReport,
  BorrowerIndividualReport,
  InvestorIndividualReport
} from '../models/admin-report.models';

@Injectable({
  providedIn: 'root'
})
export class AdminReportPdfService {
  private authService = inject(AuthService);

  // Formatação de valores
  private formatCurrency(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
  }

  private formatDate(dateStr: string | Date): string {
    if (!dateStr) return '-';
    const date = new Date(dateStr);
    return date.toLocaleDateString('pt-BR');
  }

  private getAdminEmail(): string {
    return this.authService.currentUser()?.email || 'Administrador';
  }

  // Adiciona cabeçalho e rodapé em todas as páginas do documento
  private applyHeaderFooter(doc: jsPDF, title: string) {
    const totalPages = doc.getNumberOfPages();
    const adminEmail = this.getAdminEmail();
    const emitDate = new Date().toLocaleString('pt-BR');

    for (let i = 1; i <= totalPages; i++) {
      doc.setPage(i);
      
      // Cabeçalho
      doc.setFontSize(8);
      doc.setTextColor(100, 100, 100);
      doc.setFont('helvetica', 'normal');
      doc.text('DINHEIRO A JATO - PLATAFORMA DE CRÉDITO', 14, 10);
      doc.text(`Emitido em: ${emitDate}`, 196, 10, { align: 'right' });
      
      // Linha separadora do cabeçalho
      doc.setDrawColor(220, 220, 220);
      doc.setLineWidth(0.5);
      doc.line(14, 12, 196, 12);

      // Rodapé
      doc.line(14, 280, 196, 280);
      doc.text(`Relatório: ${title}`, 14, 285);
      doc.text(`Gerado por: ${adminEmail}`, 105, 285, { align: 'center' });
      doc.text(`Página ${i} de ${totalPages}`, 196, 285, { align: 'right' });
    }
  }

  // 1. Relatório Consolidado de Tomadores (PDF)
  public generateBorrowerConsolidated(
    data: BorrowerConsolidatedItem[],
    summary: BorrowerConsolidatedSummary,
    filtersApplied: any
  ): void {
    const doc = new jsPDF('p', 'mm', 'a4');
    
    // Título Principal
    doc.setFontSize(18);
    doc.setTextColor(33, 37, 41);
    doc.setFont('helvetica', 'bold');
    doc.text('Relatório Consolidado de Tomadores', 14, 22);

    // Filtros Aplicados
    doc.setFontSize(9);
    doc.setFont('helvetica', 'italic');
    doc.setTextColor(120, 120, 120);
    const filterText = this.buildFilterText(filtersApplied);
    doc.text(`Filtros: ${filterText}`, 14, 28);

    // Painel de Resumo (KPI Cards simulated as table)
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.setTextColor(33, 37, 41);
    doc.text('Resumo Consolidado', 14, 38);

    autoTable(doc, {
      startY: 40,
      head: [['Total Tomadores', 'Ativos', 'Inativos', 'Em Análise']],
      body: [[
        summary.totalTomadores.toString(),
        summary.ativos.toString(),
        summary.inativos.toString(),
        summary.emAnalise.toString()
      ]],
      theme: 'grid',
      headStyles: { fillColor: [40, 116, 240], halign: 'center' },
      bodyStyles: { halign: 'center', fontSize: 10, fontStyle: 'bold' }
    });

    // Tabela de Detalhes
    doc.text('Detalhamento de Tomadores', 14, (doc as any).lastAutoTable.finalY + 10);

    const tableBody = data.map(item => [
      item.nome,
      item.cpf,
      this.formatCurrency(item.limiteDisponivel),
      item.quantidadeEmprestimos.toString(),
      this.formatCurrency(item.valorTotalSolicitado),
      this.formatCurrency(item.valorTotalAprovado)
    ]);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Nome', 'CPF', 'Lim. Disponível', 'Qtd. Empr.', 'Total Solicitado', 'Total Aprovado']],
      body: tableBody,
      theme: 'striped',
      headStyles: { fillColor: [33, 37, 41] },
      columnStyles: {
        2: { halign: 'right' },
        3: { halign: 'center' },
        4: { halign: 'right' },
        5: { halign: 'right' }
      }
    });

    // Aplicar Cabeçalho e Rodapé com numeração
    this.applyHeaderFooter(doc, 'Consolidado de Tomadores');
    
    // Salvar PDF
    doc.save(`relatorio_tomadores_consolidado_${Date.now()}.pdf`);
  }

  // 2. Relatório Consolidado de Investidores (PDF)
  public generateInvestorConsolidated(
    data: InvestorConsolidatedItem[],
    summary: InvestorConsolidatedSummary,
    filtersApplied: any
  ): void {
    const doc = new jsPDF('p', 'mm', 'a4');
    
    doc.setFontSize(18);
    doc.setTextColor(33, 37, 41);
    doc.setFont('helvetica', 'bold');
    doc.text('Relatório Consolidado de Investidores', 14, 22);

    doc.setFontSize(9);
    doc.setFont('helvetica', 'italic');
    doc.setTextColor(120, 120, 120);
    const filterText = this.buildFilterText(filtersApplied);
    doc.text(`Filtros: ${filterText}`, 14, 28);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.setTextColor(33, 37, 41);
    doc.text('Resumo Consolidado', 14, 38);

    autoTable(doc, {
      startY: 40,
      head: [['Total Investidores', 'Ativos', 'Em Análise']],
      body: [[
        summary.totalInvestidores.toString(),
        summary.ativos.toString(),
        summary.emAnalise.toString()
      ]],
      theme: 'grid',
      headStyles: { fillColor: [40, 116, 240], halign: 'center' },
      bodyStyles: { halign: 'center', fontSize: 10, fontStyle: 'bold' }
    });

    doc.text('Detalhamento de Investidores', 14, (doc as any).lastAutoTable.finalY + 10);

    const tableBody = data.map(item => [
      item.nome,
      item.cpf,
      this.formatCurrency(item.valorInvestido),
      `${item.rentabilidade.toFixed(2)}%`,
      this.formatCurrency(item.saldoAtual)
    ]);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Nome', 'CPF', 'Valor Investido', 'Rentabilidade', 'Saldo Total']],
      body: tableBody,
      theme: 'striped',
      headStyles: { fillColor: [33, 37, 41] },
      columnStyles: {
        2: { halign: 'right' },
        3: { halign: 'center' },
        4: { halign: 'right' }
      }
    });

    this.applyHeaderFooter(doc, 'Consolidado de Investidores');
    doc.save(`relatorio_investidores_consolidado_${Date.now()}.pdf`);
  }

  // 3. Relatório Consolidado de Empréstimos (PDF)
  public generateLoanConsolidated(
    data: LoanConsolidatedItem[],
    summary: LoanConsolidatedSummary,
    filtersApplied: any
  ): void {
    const doc = new jsPDF('p', 'mm', 'a4');
    
    doc.setFontSize(18);
    doc.setTextColor(33, 37, 41);
    doc.setFont('helvetica', 'bold');
    doc.text('Relatório Consolidado de Empréstimos', 14, 22);

    doc.setFontSize(9);
    doc.setFont('helvetica', 'italic');
    doc.setTextColor(120, 120, 120);
    const filterText = this.buildFilterText(filtersApplied);
    doc.text(`Filtros: ${filterText}`, 14, 28);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.setTextColor(33, 37, 41);
    doc.text('Resumo Consolidado', 14, 38);

    autoTable(doc, {
      startY: 40,
      head: [['Solicitados', 'Aprovados', 'Reprovados', 'Em Análise']],
      body: [[
        summary.solicitados.toString(),
        summary.aprovados.toString(),
        summary.reprovados.toString(),
        summary.emAnalise.toString()
      ]],
      theme: 'grid',
      headStyles: { fillColor: [40, 116, 240], halign: 'center' },
      bodyStyles: { halign: 'center', fontSize: 10, fontStyle: 'bold' }
    });

    doc.text('Detalhamento de Contratos', 14, (doc as any).lastAutoTable.finalY + 10);

    const tableBody = data.map(item => [
      item.tomador,
      item.tipoEmprestimo,
      this.formatCurrency(item.valor),
      `${item.juros.toFixed(2)}%`,
      item.parcelas.toString(),
      item.status,
      this.formatDate(item.dataCriacao)
    ]);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Tomador', 'Tipo', 'Valor', 'Juros p/ Parc', 'Parcelas', 'Status', 'Data Solic.']],
      body: tableBody,
      theme: 'striped',
      headStyles: { fillColor: [33, 37, 41] },
      columnStyles: {
        2: { halign: 'right' },
        3: { halign: 'center' },
        4: { halign: 'center' },
        5: { halign: 'center' }
      }
    });

    this.applyHeaderFooter(doc, 'Consolidado de Empréstimos');
    doc.save(`relatorio_emprestimos_consolidado_${Date.now()}.pdf`);
  }

  // 4. Relatório Financeiro Consolidado (PDF)
  public generateFinancialConsolidated(
    report: FinancialConsolidatedReport,
    filtersApplied: any
  ): void {
    const doc = new jsPDF('p', 'mm', 'a4');
    
    doc.setFontSize(18);
    doc.setTextColor(33, 37, 41);
    doc.setFont('helvetica', 'bold');
    doc.text('Relatório Financeiro Consolidado', 14, 22);

    doc.setFontSize(9);
    doc.setFont('helvetica', 'italic');
    doc.setTextColor(120, 120, 120);
    const filterText = this.buildFilterText(filtersApplied);
    doc.text(`Filtros: ${filterText}`, 14, 28);

    // Seção ENTRADAS
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12);
    doc.setTextColor(33, 37, 41);
    doc.text('ENTRADAS (Recebimentos)', 14, 38);

    autoTable(doc, {
      startY: 40,
      head: [['Origem da Entrada', 'Valor Recebido']],
      body: [
        ['Parcelas Recebidas (Amortizações)', this.formatCurrency(report.entradas.parcelasRecebidas)],
        ['Juros e Multas Recebidos', this.formatCurrency(report.entradas.jurosRecebidos)],
        ['Taxas Administrativas', this.formatCurrency(report.entradas.taxasAdministrativas)],
        ['TOTAL ENTRADAS', this.formatCurrency(report.entradas.totalEntradas)]
      ],
      theme: 'striped',
      headStyles: { fillColor: [40, 167, 69] }, // Verde para Entradas
      columnStyles: { 1: { halign: 'right', fontStyle: 'bold' } },
      didParseCell: (data) => {
        if (data.row.index === 3) {
          data.cell.styles.fontStyle = 'bold';
          data.cell.styles.fillColor = [220, 245, 225];
        }
      }
    });

    // Seção SAÍDAS
    doc.text('SAÍDAS (Desembolsos e Custos)', 14, (doc as any).lastAutoTable.finalY + 10);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Destino da Saída', 'Valor Pago']],
      body: [
        ['Empréstimos Liberados (Capital)', this.formatCurrency(report.saidas.emprestimosLiberados)],
        ['Rendimentos Pagos aos Investidores', this.formatCurrency(report.saidas.rendimentosPagos)],
        ['Coberturas Administrativas (Aportes Fundo)', this.formatCurrency(report.saidas.coberturasAdministrativas)],
        ['TOTAL SAÍDAS', this.formatCurrency(report.saidas.totalSaidas)]
      ],
      theme: 'striped',
      headStyles: { fillColor: [220, 53, 69] }, // Vermelho para Saídas
      columnStyles: { 1: { halign: 'right', fontStyle: 'bold' } },
      didParseCell: (data) => {
        if (data.row.index === 3) {
          data.cell.styles.fontStyle = 'bold';
          data.cell.styles.fillColor = [253, 235, 236];
        }
      }
    });

    // Seção RESUMO E RESULTADO OPERACIONAL
    doc.text('RESUMO E RESULTADO OPERACIONAL', 14, (doc as any).lastAutoTable.finalY + 10);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Indicador Operacional', 'Valor Acumulado']],
      body: [
        ['Total Recebido (A)', this.formatCurrency(report.resumo.totalRecebido)],
        ['Total Emprestado (B)', this.formatCurrency(report.resumo.totalEmprestado)],
        ['Total em Atraso (Pendência Recente)', this.formatCurrency(report.resumo.totalEmAtraso)],
        ['Total Inadimplente (Pendência > 60 dias)', this.formatCurrency(report.resumo.totalInadimplente)],
        ['RESULTADO OPERACIONAL (Entradas - Saídas)', this.formatCurrency(report.resumo.resultadoOperacional)]
      ],
      theme: 'grid',
      headStyles: { fillColor: [108, 117, 125] },
      columnStyles: { 1: { halign: 'right', fontStyle: 'bold' } },
      didParseCell: (data) => {
        if (data.row.index === 4) {
          data.cell.styles.fontStyle = 'bold';
          const isPositive = report.resumo.resultadoOperacional >= 0;
          data.cell.styles.fillColor = isPositive ? [220, 245, 225] : [253, 235, 236];
          data.cell.styles.textColor = isPositive ? [40, 167, 69] : [220, 53, 69];
        }
      }
    });

    this.applyHeaderFooter(doc, 'Financeiro Consolidado');
    doc.save(`relatorio_financeiro_consolidado_${Date.now()}.pdf`);
  }

  // 5. Relatório Individual de Tomador (PDF)
  public generateBorrowerIndividual(report: BorrowerIndividualReport): void {
    const doc = new jsPDF('p', 'mm', 'a4');
    
    // Título Principal
    doc.setFontSize(18);
    doc.setTextColor(33, 37, 41);
    doc.setFont('helvetica', 'bold');
    doc.text('Relatório Individual de Tomador', 14, 22);

    // Seção 1: Dados Cadastrais
    doc.setFontSize(11);
    doc.text('DADOS CADASTRAIS', 14, 32);
    
    autoTable(doc, {
      startY: 34,
      head: [['Campo', 'Informação cadastral']],
      body: [
        ['Nome Completo', report.nome],
        ['CPF', report.cpf],
        ['E-mail', report.email],
        ['Telefone de Contato', report.telefone],
        ['Empresa Vinculada', report.empresa || 'Não informado / Sem empresa'],
        ['CNPJ', report.cnpj || '-']
      ],
      theme: 'striped',
      headStyles: { fillColor: [40, 116, 240] },
      columnStyles: { 0: { fontStyle: 'bold', cellWidth: 50 } }
    });

    // Seção 2: Crédito
    doc.text('CRÉDITO E LIMITES', 14, (doc as any).lastAutoTable.finalY + 10);
    
    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Limite Disponível', 'Limite Utilizado']],
      body: [[
        this.formatCurrency(report.limiteDisponivel),
        this.formatCurrency(report.limiteUtilizado)
      ]],
      theme: 'grid',
      headStyles: { fillColor: [108, 117, 125], halign: 'center' },
      bodyStyles: { halign: 'center', fontSize: 10, fontStyle: 'bold' }
    });

    // Seção 3: Empréstimos
    doc.text('HISTÓRICO DE EMPRÉSTIMOS', 14, (doc as any).lastAutoTable.finalY + 10);
    
    const loansBody = report.emprestimos.map(l => [
      l.valorSolicitado ? this.formatCurrency(l.valorSolicitado) : '-',
      l.valorAprovado ? this.formatCurrency(l.valorAprovado) : '-',
      l.tipoEmprestimo,
      `${l.juros.toFixed(2)}%`,
      l.parcelas.toString(),
      l.status
    ]);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Valor Solicitado', 'Valor Aprovado', 'Modalidade', 'Taxa Juros', 'Parcelas', 'Situação']],
      body: loansBody.length > 0 ? loansBody : [['Nenhum empréstimo solicitado.', '', '', '', '', '']],
      theme: 'striped',
      headStyles: { fillColor: [33, 37, 41] },
      columnStyles: {
        0: { halign: 'right' },
        1: { halign: 'right' },
        3: { halign: 'center' },
        4: { halign: 'center' },
        5: { halign: 'center' }
      }
    });

    // Seção 4: Parcelas do Último Contrato
    if (report.parcelas && report.parcelas.length > 0) {
      doc.text('DETALHAMENTO DE PARCELAS (CONTRATO RECENTE)', 14, (doc as any).lastAutoTable.finalY + 10);
      
      const instBody = report.parcelas.map(p => [
        p.numero.toString(),
        this.formatCurrency(p.valor),
        this.formatDate(p.vencimento),
        p.situacao,
        p.dataPagamento ? this.formatDate(p.dataPagamento) : '-'
      ]);

      autoTable(doc, {
        startY: (doc as any).lastAutoTable.finalY + 12,
        head: [['Nº Parcela', 'Valor da Parcela', 'Data Vencimento', 'Status Parcela', 'Data Pagamento']],
        body: instBody,
        theme: 'striped',
        headStyles: { fillColor: [33, 37, 41] },
        columnStyles: {
          0: { halign: 'center' },
          1: { halign: 'right' },
          2: { halign: 'center' },
          3: { halign: 'center' },
          4: { halign: 'center' }
        }
      });
    }

    // Seção 5: Resumo Financeiro do Usuário
    doc.text('RESUMO DO TOMADOR', 14, (doc as any).lastAutoTable.finalY + 10);
    
    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Total Solicitado', 'Total Aprovado', 'Total Pago', 'Parcelas em Aberto', 'Parcelas Atrasadas']],
      body: [[
        this.formatCurrency(report.totalSolicitado),
        this.formatCurrency(report.totalAprovado),
        this.formatCurrency(report.totalPago),
        report.parcelasEmAberto.toString(),
        report.parcelasAtrasadas.toString()
      ]],
      theme: 'grid',
      headStyles: { fillColor: [33, 37, 41], halign: 'center' },
      bodyStyles: { halign: 'center', fontSize: 10, fontStyle: 'bold' }
    });

    this.applyHeaderFooter(doc, `Individual - Tomador: ${report.nome}`);
    doc.save(`relatorio_tomador_${report.nome.replace(/\s+/g, '_')}_${Date.now()}.pdf`);
  }

  // 6. Relatório Individual de Investidor (PDF)
  public generateInvestorIndividual(report: InvestorIndividualReport): void {
    const doc = new jsPDF('p', 'mm', 'a4');
    
    doc.setFontSize(18);
    doc.setTextColor(33, 37, 41);
    doc.setFont('helvetica', 'bold');
    doc.text('Relatório Individual de Investidor', 14, 22);

    // Seção 1: Dados Cadastrais
    doc.setFontSize(11);
    doc.text('DADOS CADASTRAIS', 14, 32);
    
    autoTable(doc, {
      startY: 34,
      head: [['Campo', 'Informação cadastral']],
      body: [
        ['Nome Completo', report.nome],
        ['CPF', report.cpf],
        ['E-mail', report.email]
      ],
      theme: 'striped',
      headStyles: { fillColor: [40, 116, 240] },
      columnStyles: { 0: { fontStyle: 'bold', cellWidth: 50 } }
    });

    // Seção 2: Investimentos
    doc.text('HISTÓRICO DE ALOCAÇÕES', 14, (doc as any).lastAutoTable.finalY + 10);
    
    const invBody = report.investimentos.map(i => [
      this.formatCurrency(i.valorInvestido),
      `${i.rentabilidade.toFixed(2)}%`,
      this.formatDate(i.dataAprovacao)
    ]);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Valor Investido', 'Rentabilidade Acordada', 'Data Aprovação']],
      body: invBody.length > 0 ? invBody : [['Sem investimentos ativos.', '', '']],
      theme: 'striped',
      headStyles: { fillColor: [33, 37, 41] },
      columnStyles: {
        0: { halign: 'right' },
        1: { halign: 'center' },
        2: { halign: 'center' }
      }
    });

    // Seção 3: Movimentações
    doc.text('HISTÓRICO DE TRANSAÇÕES FINANCEIRAS', 14, (doc as any).lastAutoTable.finalY + 10);
    
    const movBody = report.movimentacoes.map(m => [
      m.tipo,
      this.formatCurrency(m.valor),
      this.formatDate(m.data),
      m.descricao
    ]);

    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Tipo', 'Valor', 'Data da Transação', 'Descrição']],
      body: movBody.length > 0 ? movBody : [['Nenhuma movimentação encontrada.', '', '', '']],
      theme: 'striped',
      headStyles: { fillColor: [33, 37, 41] },
      columnStyles: {
        0: { halign: 'center' },
        1: { halign: 'right' },
        2: { halign: 'center' }
      }
    });

    // Seção 4: Resumo Financeiro do Investidor
    doc.text('RESUMO DA CARTEIRA', 14, (doc as any).lastAutoTable.finalY + 10);
    
    autoTable(doc, {
      startY: (doc as any).lastAutoTable.finalY + 12,
      head: [['Total Investido', 'Total Recebido (Rendimentos)', 'Saldo Atual da Conta']],
      body: [[
        this.formatCurrency(report.totalInvestido),
        this.formatCurrency(report.totalRecebido),
        this.formatCurrency(report.saldoAtual)
      ]],
      theme: 'grid',
      headStyles: { fillColor: [33, 37, 41], halign: 'center' },
      bodyStyles: { halign: 'center', fontSize: 10, fontStyle: 'bold' }
    });

    this.applyHeaderFooter(doc, `Individual - Investidor: ${report.nome}`);
    doc.save(`relatorio_investidor_${report.nome.replace(/\s+/g, '_')}_${Date.now()}.pdf`);
  }

  // Helper para construir string de filtros
  private buildFilterText(filters: any): string {
    if (!filters) return 'Nenhum';
    const textList: string[] = [];

    if (filters.nome) textList.push(`Nome: "${filters.nome}"`);
    if (filters.cpf) textList.push(`CPF: "${filters.cpf}"`);
    if (filters.status) textList.push(`Status: "${filters.status}"`);
    if (filters.tipo) textList.push(`Tipo: "${filters.tipo}"`);
    if (filters.periodoInicio) textList.push(`Período Início: ${this.formatDate(filters.periodoInicio)}`);
    if (filters.periodoFim) textList.push(`Período Fim: ${this.formatDate(filters.periodoFim)}`);

    return textList.length > 0 ? textList.join(' | ') : 'Nenhum';
  }
}
