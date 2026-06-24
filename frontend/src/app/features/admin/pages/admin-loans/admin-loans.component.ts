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

import { LoanService } from '../../../../core/services/loan.service';
import { LoanRequest } from '../../../../core/models/loan.models';
import { LoanInstallmentService } from '../../../../core/services/loan-installment.service';

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
    MatSelectModule
  ],
  templateUrl: './admin-loans.component.html',
  styleUrls: ['./admin-loans.component.css']
})
export class AdminLoansComponent implements OnInit {
  private api = inject(LoanService);
  private installmentService = inject(LoanInstallmentService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public loans: LoanRequest[] = [];
  public activeLoans: LoanRequest[] = [];
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
    taxaJuros: 0,
    periodicidadeGold: 'Semanal'
  };

  ngOnInit(): void {
    this.loadPendingLoans();
    this.loadActiveLoans();
    this.loadAdminInstallments();
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
    this.api.getActiveLoans(this.activePage, this.activePageSize, this.activeSearch, this.activeStatus, this.activeTipo).subscribe({
      next: (data) => {
        this.activeLoans = data.items;
        this.activeTotal = data.totalCount;
      },
      error: () => {
        this.snackBar.open('Erro ao carregar empréstimos ativos.', 'Fechar', { duration: 4000 });
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

    this.subDialogRef = this.dialog.open(this.approveDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  public closeApproveDialog(): void {
    this.subDialogRef?.close();
  }

  public confirmApproveLoan(): void {
    if (!this.selectedLoan) return;

    this.isActionProcessing = true;
    const payload = {
      tipoEmprestimo: this.approveForm.tipoEmprestimo,
      quantidadeParcelas: this.approveForm.quantidadeParcelas,
      taxaJuros: this.approveForm.taxaJuros,
      periodicidadeGold: this.approveForm.tipoEmprestimo === 'Gold' ? this.approveForm.periodicidadeGold : null
    };

    this.api.approveLoan(this.selectedLoan.id!, payload).subscribe({
      next: () => {
        this.isActionProcessing = false;
        this.snackBar.open('Solicitação de empréstimo aprovada com sucesso!', 'Fechar', { duration: 3000 });
        this.subDialogRef?.close();
        this.mainDialogRef?.close();
        this.loadPendingLoans();
        this.loadActiveLoans();
        this.loadAdminInstallments();
      },
      error: (err) => {
        this.isActionProcessing = false;
        const msg = err.error?.message || 'Erro ao aprovar solicitação.';
        this.snackBar.open(msg, 'Fechar', { duration: 4500 });
      }
    });
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
