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

import { LoanService } from '../../../../core/services/loan.service';
import { LoanRequest } from '../../../../core/models/loan.models';

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
    MatInputModule
  ],
  templateUrl: './admin-loans.component.html',
  styleUrls: ['./admin-loans.component.css']
})
export class AdminLoansComponent implements OnInit {
  private api = inject(LoanService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public loans: LoanRequest[] = [];
  public activeLoans: LoanRequest[] = [];
  public displayedColumns: string[] = ['nome', 'cpf', 'score', 'valor', 'parcelas', 'risco', 'data', 'acoes'];
  public activeDisplayedColumns: string[] = ['nome', 'cpf', 'valor', 'parcelas', 'status', 'data', 'acoes'];
  public isLoading = false;

  // Dialog and details state
  @ViewChild('detailDialog') detailDialogTpl!: TemplateRef<any>;
  @ViewChild('reproveDialog') reproveDialogTpl!: TemplateRef<any>;
  
  private mainDialogRef?: MatDialogRef<any>;
  private subDialogRef?: MatDialogRef<any>;

  public selectedLoan: LoanRequest | null = null;
  public isActionProcessing = false;

  public reproveForm = {
    observacao: ''
  };

  ngOnInit(): void {
    this.loadPendingLoans();
    this.loadActiveLoans();
  }

  public loadPendingLoans(): void {
    this.isLoading = true;
    this.api.getPendingLoans().subscribe({
      next: (data) => {
        this.loans = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar solicitações de empréstimo pendentes.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadActiveLoans(): void {
    this.api.getActiveLoans().subscribe({
      next: (data) => {
        this.activeLoans = data;
      },
      error: () => {
        this.snackBar.open('Erro ao carregar empréstimos ativos.', 'Fechar', { duration: 4000 });
      }
    });
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

  public approveLoan(): void {
    if (!this.selectedLoan) return;

    if (confirm('Deseja realmente APROVAR esta solicitação de empréstimo?')) {
      this.isActionProcessing = true;
      this.api.approveLoan(this.selectedLoan.id!).subscribe({
        next: () => {
          this.isActionProcessing = false;
          this.snackBar.open('Solicitação de empréstimo aprovada com sucesso!', 'Fechar', { duration: 3000 });
          this.mainDialogRef?.close();
          this.loadPendingLoans();
          this.loadActiveLoans();
        },
        error: (err) => {
          this.isActionProcessing = false;
          const msg = err.error?.message || 'Erro ao aprovar solicitação.';
          this.snackBar.open(msg, 'Fechar', { duration: 4500 });
        }
      });
    }
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
        },
        error: (err) => {
          this.isActionProcessing = false;
          const msg = err.error?.message || 'Erro ao finalizar empréstimo.';
          this.snackBar.open(msg, 'Fechar', { duration: 4500 });
        }
      });
    }
  }
}
