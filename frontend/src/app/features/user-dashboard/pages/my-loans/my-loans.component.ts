import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { LoanService } from '../../../../core/services/loan.service';
import { LoanRequest } from '../../../../core/models/loan.models';
import { LoanInstallmentService } from '../../../../core/services/loan-installment.service';
import { LoanInstallmentDto } from '../../../../core/models/installment.models';

@Component({
  selector: 'app-my-loans',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTableModule,
    MatSnackBarModule
  ],
  templateUrl: './my-loans.component.html',
  styleUrls: ['./my-loans.component.css']
})
export class MyLoansComponent implements OnInit {
  private api = inject(LoanService);
  private installmentService = inject(LoanInstallmentService);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);

  public loans: LoanRequest[] = [];
  public displayedColumns: string[] = ['valor', 'parcelas', 'status', 'data', 'acoes'];
  public isLoading = false;

  public selectedLoan: LoanRequest | null = null;
  public installments: LoanInstallmentDto[] = [];
  public isLoadingInstallments = false;
  public displayedInstallmentColumns: string[] = ['numero', 'vencimento', 'valor', 'multa', 'jurosMora', 'valorPago', 'status', 'acoes'];

  public get hasActiveLoan(): boolean {
    const statusAtivos = ['pendente', 'emanalise', 'aprovado', 'ativo'];
    return this.loans.some(l => l.status && statusAtivos.includes(l.status.toLowerCase()));
  }

  ngOnInit(): void {
    this.loadMyLoans();
  }

  public loadMyLoans(): void {
    this.isLoading = true;
    this.api.getMyLoans().subscribe({
      next: (data) => {
        this.loans = data;
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar lista de empréstimos.', 'Fechar', { duration: 4000 });
        this.cdr.detectChanges();
      }
    });
  }

  public selectLoan(loan: LoanRequest): void {
    if (loan.id) {
      this.selectedLoan = loan;
      this.loadInstallments(loan.id);
    }
  }

  public loadInstallments(loanRequestId: string): void {
    this.isLoadingInstallments = true;
    this.installmentService.getInstallments(loanRequestId).subscribe({
      next: (data) => {
        this.installments = data;
        this.isLoadingInstallments = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.isLoadingInstallments = false;
        this.snackBar.open('Erro ao carregar parcelas do empréstimo.', 'Fechar', { duration: 4000 });
        this.cdr.detectChanges();
      }
    });
  }

  public payInstallment(installmentId: string): void {
    if (!confirm('Deseja realmente simular o pagamento desta parcela?')) {
      return;
    }
    this.installmentService.payInstallment(installmentId).subscribe({
      next: (res) => {
        this.snackBar.open(res.message || 'Parcela paga com sucesso!', 'Fechar', { duration: 3000 });
        this.loadMyLoans();
        if (this.selectedLoan && this.selectedLoan.id) {
          this.loadInstallments(this.selectedLoan.id);
        }
      },
      error: (err) => {
        const msg = err.error?.message || 'Erro ao realizar pagamento da parcela.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
        this.cdr.detectChanges();
      }
    });
  }
}
