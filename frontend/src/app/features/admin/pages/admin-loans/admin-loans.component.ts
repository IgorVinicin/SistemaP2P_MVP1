import { Component, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSelectModule } from '@angular/material/select';
import { MatExpansionModule } from '@angular/material/expansion';

import { LoanService } from '../../../../core/services/loan.service';
import { LoanRequest } from '../../../../core/models/loan.models';
import { LoanInstallmentService } from '../../../../core/services/loan-installment.service';
import { AdminGroupService, InvestorGroup } from '../../../../core/services/admin-group.service';

import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';

@Component({
  selector: 'app-admin-loans',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTableModule,
    MatDialogModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatSelectModule,
    MatExpansionModule
  ],
  templateUrl: './admin-loans.component.html',
  styleUrls: ['./admin-loans.component.css']
})
export class AdminLoansComponent implements OnInit {
  private api = inject(LoanService);
  private installmentService = inject(LoanInstallmentService);
  private groupService = inject(AdminGroupService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public availableGroups: InvestorGroup[] = [];

  public loans: LoanRequest[] = [];
  public activeLoans: any[] = [];
  public displayedColumns: string[] = ['nome', 'cpf', 'score', 'valor', 'parcelas', 'risco', 'data', 'acoes'];
  public activeDisplayedColumns: string[] = ['nome', 'cpf', 'valor', 'parcelas', 'status', 'data', 'acoes'];
  public isLoading = false;

  // Pagination states
  public pendingPage = 1;
  public pendingPageSize = 5;
  public pendingTotal = 0;
  public pendingSearch = '';
  public pendingRisco = '';
  public pendingTipo = '';

  public activePage = 1;
  public activePageSize = 10;
  public activeTotal = 0;
  public activeSearch = '';
  public activeStatus = '';
  public activeTipo = '';

  // Global Installments Section
  public globalInstallments: any[] = [];
  public isLoadingInstallments = false;
  public installmentDisplayedColumns: string[] = ['cliente', 'numero', 'vencimento', 'valor', 'multa', 'jurosMora', 'valorPago', 'pagamento', 'status', 'acoes'];
  
  public installmentsPage = 1;
  public installmentsPageSize = 10;
  public installmentsTotal = 0;
  
  public filters = {
    cliente: '',
    status: '',
    periodoInicio: '',
    periodoFim: '',
    somenteAtrasadas: false
  };

  // Dialog and details state
  @ViewChild('detailDialog') detailDialogTpl!: TemplateRef<any>;
  @ViewChild('reproveDialog') reproveDialogTpl!: TemplateRef<any>;
  @ViewChild('approveDialog') approveDialogTpl!: TemplateRef<any>;
  
  private mainDialogRef?: MatDialogRef<any>;
  private subDialogRef?: MatDialogRef<any>;

  public selectedLoan: LoanRequest | null = null;
  public isActionProcessing = false;

  public reproveForm = {
    observacao: ''
  };

  public approveForm = {
    tipoEmprestimo: 'Master',
    quantidadeParcelas: 12,
    valorPagoPorDia: 0,
    taxaJuros: 0,
    periodicidadeGold: 'Semanal',
    valorAprovado: 0,
    investorGroupId: null as string | null
  };

  ngOnInit(): void {
    this.loadPendingLoans();
    this.loadActiveLoans();
    this.loadAdminInstallments();
    this.loadGroups();
  }

  public loadGroups(): void {
    this.groupService.getGroups().subscribe({
      next: (data) => this.availableGroups = data,
      error: () => console.error('Erro ao carregar grupos de investidores')
    });
  }

  public loadAdminInstallments(): void {
    this.isLoadingInstallments = true;
    this.installmentService.getAdminInstallments(this.installmentsPage, this.installmentsPageSize, this.filters).subscribe({
      next: (data) => {
        this.globalInstallments = data.items;
        this.installmentsTotal = data.totalCount;
        this.isLoadingInstallments = false;
      },
      error: () => {
        this.isLoadingInstallments = false;
        this.snackBar.open('Erro ao carregar lista geral de parcelas.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public onFilterChange(): void {
    this.installmentsPage = 1;
    this.loadAdminInstallments();
  }

  public clearFilters(): void {
    this.filters = {
      cliente: '',
      status: '',
      periodoInicio: '',
      periodoFim: '',
      somenteAtrasadas: false
    };
    this.installmentsPage = 1;
    this.loadAdminInstallments();
  }

  public loadPendingLoans(): void {
    this.isLoading = true;
    this.api.getPendingLoans(this.pendingPage, this.pendingPageSize, this.pendingSearch, this.pendingRisco, this.pendingTipo).subscribe({
      next: (data) => {
        this.loans = data.items;
        this.pendingTotal = data.totalCount;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar solicitações de empréstimo pendentes.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadActiveLoans(): void {
    this.api.getActiveLoansGrouped(this.activePage, this.activePageSize, this.activeSearch, this.activeStatus, this.activeTipo).subscribe({
      next: (data) => {
        this.activeLoans = data.items;
        this.activeTotal = data.totalCount;
      },
      error: () => {
        this.snackBar.open('Erro ao carregar empréstimos ativos.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadInstallmentsForLoan(loan: any): void {
    if (loan.installments && loan.installments.length > 0) return;
    loan.isLoadingInstallments = true;
    this.installmentService.getInstallments(loan.id).subscribe({
      next: (data) => {
        loan.installments = data;
        loan.isLoadingInstallments = false;
      },
      error: () => {
        loan.isLoadingInstallments = false;
        this.snackBar.open('Erro ao carregar parcelas do empréstimo.', 'Fechar', { duration: 3000 });
      }
    });
  }

  public onPendingPageChange(event: PageEvent): void {
    this.pendingPage = event.pageIndex + 1;
    this.pendingPageSize = event.pageSize;
    this.loadPendingLoans();
  }

  public onActivePageChange(event: PageEvent): void {
    this.activePage = event.pageIndex + 1;
    this.activePageSize = event.pageSize;
    this.loadActiveLoans();
  }

  public onInstallmentsPageChange(event: PageEvent): void {
    this.installmentsPage = event.pageIndex + 1;
    this.installmentsPageSize = event.pageSize;
    this.loadAdminInstallments();
  }

  public searchPending(): void {
    this.pendingPage = 1;
    this.loadPendingLoans();
  }

  public searchActive(): void {
    this.activePage = 1;
    this.loadActiveLoans();
  }

  public openDetailDialog(loanId: string): void {
    this.selectedLoan = null;
    this.isActionProcessing = false;

    // Busca detalhes completos antes de abrir
    this.api.getLoanDetails(loanId).subscribe({
      next: (data) => {
        this.selectedLoan = data;
        this.mainDialogRef = this.dialog.open(this.detailDialogTpl, {
          width: '750px',
          disableClose: true
        });
      },
      error: () => {
        this.snackBar.open('Erro ao carregar detalhes da proposta.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public openApproveDialog(): void {
    if (!this.selectedLoan) return;
    this.approveForm.tipoEmprestimo = 'Master';
    this.approveForm.quantidadeParcelas = this.selectedLoan.quantidadeParcelas || 12;
    this.approveForm.taxaJuros = this.selectedLoan.taxaJuros || 0;
    this.approveForm.periodicidadeGold = 'Semanal';
    this.approveForm.valorAprovado = this.selectedLoan.valorSolicitado;
    this.approveForm.investorGroupId = null;

    this.subDialogRef = this.dialog.open(this.approveDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  public closeApproveDialog(): void {
    this.subDialogRef?.close();
  }

  get jurosTotal(): number {
    if (!this.selectedLoan) return 0;
    const baseValor = this.approveForm.valorAprovado > 0 ? this.approveForm.valorAprovado : this.selectedLoan.valorSolicitado;
    return (baseValor * this.approveForm.taxaJuros) / 100;
  }

  get valorParcelaEstimada(): number {
    if (!this.selectedLoan) return 0;
    const qtd = this.approveForm.tipoEmprestimo === 'Gold' ? 1 : this.approveForm.quantidadeParcelas;
    if (qtd <= 0) return 0;
    const baseValor = this.approveForm.valorAprovado > 0 ? this.approveForm.valorAprovado : this.selectedLoan.valorSolicitado;
    const totalAPagar = baseValor + this.jurosTotal;
    return totalAPagar / qtd;
  }

  get isMasterValorValido(): boolean {
    if (this.approveForm.tipoEmprestimo !== 'Master') return true;
    if (!this.selectedLoan) return false;
    // Como o juros é simples e calculado sobre o total, qualquer pagamento > 0 eventualmente quita o empréstimo.
    // Garantimos apenas que o administrador não digite 0 ou negativo.
    return this.approveForm.valorPagoPorDia > 0;
  }

  public onMasterCalculationChange(): void {
    if (this.approveForm.tipoEmprestimo === 'Master' && this.selectedLoan && this.approveForm.valorPagoPorDia > 0) {
      const baseValor = this.approveForm.valorAprovado > 0 ? this.approveForm.valorAprovado : this.selectedLoan.valorSolicitado;
      const totalAPagar = baseValor + this.jurosTotal;
      // Quantidade de parcelas = Total a Pagar / Valor Pago por Dia (arredondado para cima para garantir quitação)
      this.approveForm.quantidadeParcelas = Math.ceil(totalAPagar / this.approveForm.valorPagoPorDia);
    }
  }

  public confirmApproveLoan(): void {
    if (!this.selectedLoan) return;

    this.isActionProcessing = true;
    const payload = {
      tipoEmprestimo: this.approveForm.tipoEmprestimo,
      quantidadeParcelas: this.approveForm.tipoEmprestimo === 'Gold' ? 1 : this.approveForm.quantidadeParcelas,
      taxaJuros: this.approveForm.taxaJuros,
      periodicidadeGold: this.approveForm.tipoEmprestimo === 'Gold' ? this.approveForm.periodicidadeGold : null,
      valorAprovado: this.approveForm.valorAprovado,
      investorGroupId: this.approveForm.investorGroupId
    };

    this.api.approveLoan(this.selectedLoan.id!, payload).subscribe({
      next: () => {
        this.snackBar.open('Solicitação de empréstimo aprovada com sucesso!', 'Fechar', { duration: 3000 });
        this.subDialogRef?.close();
        this.mainDialogRef?.close();
        this.loadPendingLoans();
        this.loadActiveLoans();
        this.loadAdminInstallments();

        // Gerar PDF da Fichinha
        const loanForPdf = { ...this.selectedLoan, ...payload };
        this.installmentService.getInstallments(this.selectedLoan!.id!).subscribe({
          next: (installments) => {
            this.generateFichinhaPdf(loanForPdf, installments);
            this.isActionProcessing = false;
          },
          error: () => {
            this.isActionProcessing = false;
            this.snackBar.open('Aprovado, mas ocorreu um erro ao gerar o PDF da Fichinha.', 'Fechar', { duration: 4000 });
          }
        });
      },
      error: (err) => {
        this.isActionProcessing = false;
        const msg = err.error?.message || 'Erro ao aprovar solicitação.';
        this.snackBar.open(msg, 'Fechar', { duration: 4500 });
      }
    });
  }

  private generateFichinhaPdf(loan: any, installments: any[]): void {
    const doc = new jsPDF();
    const nomeCliente = loan.usuarioNome || loan.user?.profile?.nome || 'Cliente Desconhecido';
    const cpfCliente = loan.usuarioCpf || loan.user?.profile?.cpf || 'CPF Indisponível';
    const valorStr = (loan.valorAprovado || loan.valorSolicitado).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
    const dataAprovacao = new Date().toLocaleDateString('pt-BR');
    
    // Configurar layout da "Fichinha"
    // Cabeçalho - Usando autoTable para criar os quadrados superiores
    autoTable(doc, {
      theme: 'plain',
      styles: { lineWidth: 0.1, lineColor: [0, 0, 0], fontSize: 11, cellPadding: 2 },
      body: [
        [
          { content: `\n${loan.tipoEmprestimo?.toUpperCase() || 'EMPRÉSTIMO'}`, styles: { fontStyle: 'bold', minCellHeight: 15 } },
          { content: `VALOR:\n${valorStr}`, styles: { fontStyle: 'bold', minCellHeight: 15 } }
        ],
        [
          { content: `DATA:\n${dataAprovacao}`, styles: { fontStyle: 'bold', minCellHeight: 15 } },
          { content: `CLIENTE:\n${nomeCliente}\n${cpfCliente}`, styles: { fontStyle: 'bold', minCellHeight: 20 } }
        ]
      ],
      margin: { top: 15 }
    });

    const finalY = (doc as any).lastAutoTable.finalY + 5;

    // Tabela de Parcelas
    const tableData = installments.map(inst => [
      new Date(inst.dataVencimento).toLocaleDateString('pt-BR'),
      inst.valorParcela.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }),
      '', // Dt. Receb. (vazio para preenchimento)
      ''  // Observação (vazio para preenchimento)
    ]);

    autoTable(doc, {
      startY: finalY,
      theme: 'grid',
      head: [['Data Parcela', 'Saldo Devedor', 'Dt. Receb.', 'Observação']],
      body: tableData,
      headStyles: { fillColor: [240, 240, 240], textColor: [0, 0, 0], fontStyle: 'bold', lineWidth: 0.1, lineColor: [0, 0, 0] },
      styles: { lineWidth: 0.1, lineColor: [0, 0, 0], textColor: [0, 0, 0] },
      columnStyles: {
        0: { cellWidth: 35 },
        1: { cellWidth: 35 },
        2: { cellWidth: 40 },
        3: { cellWidth: 70 }
      }
    });

    // Forçar o download do arquivo
    const fileName = `fichinha_${nomeCliente.replace(/[^a-z0-9]/gi, '_').toLowerCase()}.pdf`;
    doc.save(fileName);
  }

  public openReproveDialog(): void {
    this.reproveForm.observacao = '';
    this.subDialogRef = this.dialog.open(this.reproveDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  public closeReproveDialog(): void {
    this.subDialogRef?.close();
  }

  public confirmReproveLoan(): void {
    if (!this.selectedLoan) return;

    this.isActionProcessing = true;
    const payload = {
      observacao: this.reproveForm.observacao
    };

    this.api.reproveLoan(this.selectedLoan.id!, payload).subscribe({
      next: () => {
        this.isActionProcessing = false;
        this.snackBar.open('Solicitação de empréstimo reprovada com sucesso.', 'Fechar', { duration: 3000 });
        this.subDialogRef?.close();
        this.mainDialogRef?.close();
        this.loadPendingLoans();
        this.loadActiveLoans();
        this.loadAdminInstallments();
      },
      error: (err) => {
        this.isActionProcessing = false;
        const msg = err.error?.message || 'Erro ao reprovar solicitação.';
        this.snackBar.open(msg, 'Fechar', { duration: 4500 });
      }
    });
  }

  public finishLoan(loanId: string): void {
    if (confirm('Deseja realmente finalizar/quitar este empréstimo?')) {
      this.isActionProcessing = true;
      this.api.finishLoan(loanId).subscribe({
        next: () => {
          this.isActionProcessing = false;
          this.snackBar.open('Empréstimo finalizado/quitado com sucesso!', 'Fechar', { duration: 3000 });
          this.mainDialogRef?.close();
          this.loadPendingLoans();
          this.loadActiveLoans();
          this.loadAdminInstallments();
        },
        error: (err) => {
          this.isActionProcessing = false;
          const msg = err.error?.message || 'Erro ao finalizar empréstimo.';
          this.snackBar.open(msg, 'Fechar', { duration: 4500 });
        }
      });
    }
  }

  public payInstallment(installmentId: string): void {
    if (confirm('Deseja realmente quitar esta parcela administrativamente?')) {
      this.isActionProcessing = true;
      this.installmentService.payInstallment(installmentId).subscribe({
        next: (res) => {
          this.isActionProcessing = false;
          this.snackBar.open(res.message || 'Parcela quitada com sucesso!', 'Fechar', { duration: 3000 });
          this.loadPendingLoans();
          this.loadActiveLoans();
          this.loadAdminInstallments();
        },
        error: (err) => {
          this.isActionProcessing = false;
          const msg = err.error?.message || 'Erro ao quitar parcela.';
          this.snackBar.open(msg, 'Fechar', { duration: 4500 });
        }
      });
    }
  }
}
