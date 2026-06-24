import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { AdminReportService } from '../../../../core/services/admin-report.service';
import { AdminReportPdfService } from '../../../../core/services/admin-report-pdf.service';
import { AdminService } from '../../../../core/services/admin.service';
import { PendingUserSummaryDto } from '../../../../core/models/user.models';
import {
  BorrowerConsolidatedSummary,
  BorrowerConsolidatedItem,
  InvestorConsolidatedSummary,
  InvestorConsolidatedItem,
  LoanConsolidatedSummary,
  LoanConsolidatedItem,
  FinancialConsolidatedReport,
  AllocationAuditItem
} from '../../../../core/models/admin-report.models';

@Component({
  selector: 'app-admin-reports',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTabsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSnackBarModule,
    MatPaginatorModule
  ],
  templateUrl: './admin-reports.component.html',
  styleUrl: './admin-reports.component.css'
})
export class AdminReportsComponent implements OnInit {
  private reportService = inject(AdminReportService);
  private pdfService = inject(AdminReportPdfService);
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);

  // Aba Selecionada
  public selectedTabIndex = 0;

  // Loading States
  public isLoadingBorrowers = false;
  public isLoadingInvestors = false;
  public isLoadingLoans = false;
  public isLoadingFinancial = false;
  public isLoadingIndividual = false;

  // 1. Tomadores Consolidated State
  public borrowersSummary: BorrowerConsolidatedSummary | null = null;
  public borrowersList: BorrowerConsolidatedItem[] = [];
  public borrowersTotal = 0;
  public borrowersPage = 1;
  public borrowersPageSize = 10;
  public borrowersFilters = {
    nome: '',
    cpf: '',
    status: '',
    periodoInicio: '',
    periodoFim: ''
  };
  public borrowerColumns = ['nome', 'cpf', 'limiteDisponivel', 'quantidadeEmprestimos', 'valorTotalSolicitado', 'valorTotalAprovado', 'acoes'];

  // 2. Investidores Consolidated State
  public investorsSummary: InvestorConsolidatedSummary | null = null;
  public investorsList: InvestorConsolidatedItem[] = [];
  public investorsTotal = 0;
  public investorsPage = 1;
  public investorsPageSize = 10;
  public investorsFilters = {
    nome: '',
    cpf: '',
    periodoInicio: '',
    periodoFim: ''
  };
  public investorColumns = ['nome', 'cpf', 'valorInvestido', 'rentabilidade', 'saldoAtual', 'acoes'];

  // 3. Empréstimos Consolidated State
  public loansSummary: LoanConsolidatedSummary | null = null;
  public loansList: LoanConsolidatedItem[] = [];
  public loansTotal = 0;
  public loansPage = 1;
  public loansPageSize = 10;
  public loansFilters = {
    tipo: '',
    status: '',
    periodoInicio: '',
    periodoFim: ''
  };
  public loanColumns = ['tomador', 'tipoEmprestimo', 'valor', 'juros', 'parcelas', 'status', 'dataCriacao'];

  // 4. Financeiro Consolidated State
  public financialReport: FinancialConsolidatedReport | null = null;
  public financialFilters = {
    periodoInicio: '',
    periodoFim: ''
  };

  // 5. Relatórios Individuais State
  public searchUserQuery = '';
  public searchUserResults: PendingUserSummaryDto[] = [];
  public selectedUser: PendingUserSummaryDto | null = null;
  public searchColumns = ['nome', 'email', 'role', 'status', 'acoes'];

  // 6. Auditoria de Alocação State
  public isLoadingAllocations = false;
  public allocationsList: AllocationAuditItem[] = [];
  public allocationsTotal = 0;
  public allocationsPage = 1;
  public allocationsPageSize = 10;
  public allocationsFilters = {
    investorName: '',
    status: ''
  };
  public allocationColumns = ['investorName', 'loanId', 'valorInvestido', 'createdAt', 'status'];

  ngOnInit(): void {
    this.loadBorrowersReport();
  }

  // Mudança de Aba
  public onTabChange(index: number): void {
    this.selectedTabIndex = index;
    if (index === 0 && this.borrowersList.length === 0) {
      this.loadBorrowersReport();
    } else if (index === 1 && this.investorsList.length === 0) {
      this.loadInvestorsReport();
    } else if (index === 2 && this.loansList.length === 0) {
      this.loadLoansReport();
    } else if (index === 3 && !this.financialReport) {
      this.loadFinancialReport();
    } else if (index === 5 && this.allocationsList.length === 0) {
      this.loadAllocationsReport();
    }
  }

  // --- CARREGAMENTO DE DADOS ---

  public loadBorrowersReport(): void {
    this.isLoadingBorrowers = true;
    this.reportService.getBorrowersConsolidated(this.borrowersPage, this.borrowersPageSize, this.borrowersFilters).subscribe({
      next: (res) => {
        this.borrowersSummary = res.summary;
        this.borrowersList = res.details.items;
        this.borrowersTotal = res.details.totalCount;
        this.isLoadingBorrowers = false;
      },
      error: () => {
        this.isLoadingBorrowers = false;
        this.snackBar.open('Erro ao carregar relatório consolidado de tomadores.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadInvestorsReport(): void {
    this.isLoadingInvestors = true;
    this.reportService.getInvestorsConsolidated(this.investorsPage, this.investorsPageSize, this.investorsFilters).subscribe({
      next: (res) => {
        this.investorsSummary = res.summary;
        this.investorsList = res.details.items;
        this.investorsTotal = res.details.totalCount;
        this.isLoadingInvestors = false;
      },
      error: () => {
        this.isLoadingInvestors = false;
        this.snackBar.open('Erro ao carregar relatório consolidado de investidores.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadLoansReport(): void {
    this.isLoadingLoans = true;
    this.reportService.getLoansConsolidated(this.loansPage, this.loansPageSize, this.loansFilters).subscribe({
      next: (res) => {
        this.loansSummary = res.summary;
        this.loansList = res.details.items;
        this.loansTotal = res.details.totalCount;
        this.isLoadingLoans = false;
      },
      error: () => {
        this.isLoadingLoans = false;
        this.snackBar.open('Erro ao carregar relatório consolidado de empréstimos.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadFinancialReport(): void {
    this.isLoadingFinancial = true;
    const start = this.financialFilters.periodoInicio || undefined;
    const end = this.financialFilters.periodoFim || undefined;
    this.reportService.getFinancialConsolidated(start, end).subscribe({
      next: (res) => {
        this.financialReport = res;
        this.isLoadingFinancial = false;
      },
      error: () => {
        this.isLoadingFinancial = false;
        this.snackBar.open('Erro ao carregar relatório financeiro.', 'Fechar', { duration: 4000 });
      }
    });
  }

  // --- FILTROS E BUSCAS ---

  public applyBorrowersFilters(): void {
    this.borrowersPage = 1;
    this.loadBorrowersReport();
  }

  public clearBorrowersFilters(): void {
    this.borrowersFilters = { nome: '', cpf: '', status: '', periodoInicio: '', periodoFim: '' };
    this.borrowersPage = 1;
    this.loadBorrowersReport();
  }

  public applyInvestorsFilters(): void {
    this.investorsPage = 1;
    this.loadInvestorsReport();
  }

  public clearInvestorsFilters(): void {
    this.investorsFilters = { nome: '', cpf: '', periodoInicio: '', periodoFim: '' };
    this.investorsPage = 1;
    this.loadInvestorsReport();
  }

  public applyLoansFilters(): void {
    this.loansPage = 1;
    this.loadLoansReport();
  }

  public clearLoansFilters(): void {
    this.loansFilters = { tipo: '', status: '', periodoInicio: '', periodoFim: '' };
    this.loansPage = 1;
    this.loadLoansReport();
  }

  public applyFinancialFilters(): void {
    this.loadFinancialReport();
  }

  public clearFinancialFilters(): void {
    this.financialFilters = { periodoInicio: '', periodoFim: '' };
    this.loadFinancialReport();
  }

  public searchUsers(): void {
    if (!this.searchUserQuery || this.searchUserQuery.trim().length < 2) {
      this.searchUserResults = [];
      return;
    }

    this.isLoadingIndividual = true;
    this.adminService.getAllUsers(1, 20, this.searchUserQuery).subscribe({
      next: (res) => {
        // Filtrar apenas Tomadores (Cliente) e Investidores
        this.searchUserResults = res.items.filter(u => u.role === 'Cliente' || u.role === 'Investidor');
        this.isLoadingIndividual = false;
      },
      error: () => {
        this.isLoadingIndividual = false;
        this.snackBar.open('Erro ao pesquisar usuários.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public selectUserForReport(user: PendingUserSummaryDto): void {
    this.selectedUser = user;
    this.searchUserResults = [];
    this.searchUserQuery = '';
  }

  public clearSelectedUser(): void {
    this.selectedUser = null;
  }

  // --- PAGINAÇÃO EVENTOS ---

  public onBorrowersPageChange(event: PageEvent): void {
    this.borrowersPage = event.pageIndex + 1;
    this.borrowersPageSize = event.pageSize;
    this.loadBorrowersReport();
  }

  public onInvestorsPageChange(event: PageEvent): void {
    this.investorsPage = event.pageIndex + 1;
    this.investorsPageSize = event.pageSize;
    this.loadInvestorsReport();
  }

  public onLoansPageChange(event: PageEvent): void {
    this.loansPage = event.pageIndex + 1;
    this.loansPageSize = event.pageSize;
    this.loadLoansReport();
  }

  // --- EXPORTAÇÃO PDF ---

  public exportBorrowersPdf(): void {
    if (this.borrowersList.length === 0 || !this.borrowersSummary) return;
    this.pdfService.generateBorrowerConsolidated(this.borrowersList, this.borrowersSummary, this.borrowersFilters);
  }

  public exportInvestorsPdf(): void {
    if (this.investorsList.length === 0 || !this.investorsSummary) return;
    this.pdfService.generateInvestorConsolidated(this.investorsList, this.investorsSummary, this.investorsFilters);
  }

  public exportLoansPdf(): void {
    if (this.loansList.length === 0 || !this.loansSummary) return;
    this.pdfService.generateLoanConsolidated(this.loansList, this.loansSummary, this.loansFilters);
  }

  public exportFinancialPdf(): void {
    if (!this.financialReport) return;
    this.pdfService.generateFinancialConsolidated(this.financialReport, this.financialFilters);
  }

  public generateIndividualReport(userId: string, role: string): void {
    if (role === 'Cliente') {
      this.reportService.getBorrowerIndividual(userId).subscribe({
        next: (res) => this.pdfService.generateBorrowerIndividual(res),
        error: () => this.snackBar.open('Erro ao gerar relatório individual do tomador.', 'Fechar', { duration: 4000 })
      });
    } else if (role === 'Investidor') {
      this.reportService.getInvestorIndividual(userId).subscribe({
        next: (res) => this.pdfService.generateInvestorIndividual(res),
        error: () => this.snackBar.open('Erro ao gerar relatório individual do investidor.', 'Fechar', { duration: 4000 })
      });
    }
  }

  // --- EXPORTAÇÃO CSV ---

  public exportBorrowersCsv(): void {
    if (this.borrowersList.length === 0) return;
    const headers = ['Nome', 'CPF', 'Limite Disponivel', 'Quantidade Emprestimos', 'Valor Total Solicitado', 'Valor Total Aprovado'];
    const rows = this.borrowersList.map(i => [
      i.nome,
      i.cpf,
      i.limiteDisponivel.toFixed(2),
      i.quantidadeEmprestimos.toString(),
      i.valorTotalSolicitado.toFixed(2),
      i.valorTotalAprovado.toFixed(2)
    ]);
    this.exportToCsv('relatorio_tomadores_consolidado.csv', headers, rows);
  }

  public exportInvestorsCsv(): void {
    if (this.investorsList.length === 0) return;
    const headers = ['Nome', 'CPF', 'Valor Investido', 'Rentabilidade', 'Saldo Total'];
    const rows = this.investorsList.map(i => [
      i.nome,
      i.cpf,
      i.valorInvestido.toFixed(2),
      `${i.rentabilidade.toFixed(2)}%`,
      i.saldoAtual.toFixed(2)
    ]);
    this.exportToCsv('relatorio_investidores_consolidado.csv', headers, rows);
  }

  public exportLoansCsv(): void {
    if (this.loansList.length === 0) return;
    const headers = ['Tomador', 'Tipo', 'Valor', 'Juros', 'Parcelas', 'Status', 'Data Solic.'];
    const rows = this.loansList.map(i => [
      i.tomador,
      i.tipoEmprestimo,
      i.valor.toFixed(2),
      `${i.juros.toFixed(2)}%`,
      i.parcelas.toString(),
      i.status,
      new Date(i.dataCriacao).toLocaleDateString('pt-BR')
    ]);
    this.exportToCsv('relatorio_emprestimos_consolidado.csv', headers, rows);
  }

  public exportFinancialCsv(): void {
    if (!this.financialReport) return;
    const headers = ['Categoria', 'Descricao', 'Valor'];
    const rows = [
      ['ENTRADAS', 'Parcelas Recebidas (Amortizacao)', this.financialReport.entradas.parcelasRecebidas.toFixed(2)],
      ['ENTRADAS', 'Juros e Multas Recebidos', this.financialReport.entradas.jurosRecebidos.toFixed(2)],
      ['ENTRADAS', 'Taxas Administrativas', this.financialReport.entradas.taxasAdministrativas.toFixed(2)],
      ['ENTRADAS', 'TOTAL ENTRADAS', this.financialReport.entradas.totalEntradas.toFixed(2)],
      ['SAIDAS', 'Emprestimos Liberados', this.financialReport.saidas.emprestimosLiberados.toFixed(2)],
      ['SAIDAS', 'Rendimentos Pagos', this.financialReport.saidas.rendimentosPagos.toFixed(2)],
      ['SAIDAS', 'Coberturas Administrativas', this.financialReport.saidas.coberturasAdministrativas.toFixed(2)],
      ['SAIDAS', 'TOTAL SAIDAS', this.financialReport.saidas.totalSaidas.toFixed(2)],
      ['RESUMO', 'Total Recebido', this.financialReport.resumo.totalRecebido.toFixed(2)],
      ['RESUMO', 'Total Emprestado', this.financialReport.resumo.totalEmprestado.toFixed(2)],
      ['RESUMO', 'Total em Atraso', this.financialReport.resumo.totalEmAtraso.toFixed(2)],
      ['RESUMO', 'Total Inadimplente', this.financialReport.resumo.totalInadimplente.toFixed(2)],
      ['RESUMO', 'RESULTADO OPERACIONAL', this.financialReport.resumo.resultadoOperacional.toFixed(2)]
    ];
    this.exportToCsv('relatorio_financeiro_consolidado.csv', headers, rows);
  }

  // Helper nativo para exportação de CSV com suporte a UTF-8 e delimitador de ponto e vírgula
  private exportToCsv(filename: string, headers: string[], rows: any[][]): void {
    const csvStr = "\uFEFF" + headers.join(";") + "\n" + rows.map(e => e.map(val => {
      if (typeof val === 'string') {
        return `"${val.replace(/"/g, '""')}"`;
      }
      return val;
    }).join(";")).join("\n");
    
    const blob = new Blob([csvStr], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", filename);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  // --- MÉTODOS DE AUDITORIA DE ALOCAÇÃO ---

  public loadAllocationsReport(): void {
    this.isLoadingAllocations = true;
    this.reportService.getAllocationsAudit(this.allocationsPage, this.allocationsPageSize, this.allocationsFilters).subscribe({
      next: (res) => {
        this.allocationsList = res.items;
        this.allocationsTotal = res.totalCount;
        this.isLoadingAllocations = false;
      },
      error: () => {
        this.isLoadingAllocations = false;
        this.snackBar.open('Erro ao carregar auditoria de alocações.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public applyAllocationsFilters(): void {
    this.allocationsPage = 1;
    this.loadAllocationsReport();
  }

  public clearAllocationsFilters(): void {
    this.allocationsFilters = { investorName: '', status: '' };
    this.allocationsPage = 1;
    this.loadAllocationsReport();
  }

  public onAllocationsPageChange(event: PageEvent): void {
    this.allocationsPage = event.pageIndex + 1;
    this.allocationsPageSize = event.pageSize;
    this.loadAllocationsReport();
  }
}
