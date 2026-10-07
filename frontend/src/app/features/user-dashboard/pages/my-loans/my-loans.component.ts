import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';

import { PixPaymentDialogComponent } from '../../../../shared/components/pix-payment-dialog/pix-payment-dialog.component';
import { AuthService } from '../../../../core/services/auth.service';
import { LoanService } from '../../../../core/services/loan.service';
import { LoanRequest } from '../../../../core/models/loan.models';
import { LoanInstallmentService } from '../../../../core/services/loan-installment.service';
import { LoanInstallmentDto } from '../../../../core/models/installment.models';
import { PayInstallmentDialogComponent, PayInstallmentDialogResult } from '../../components/pay-installment-dialog/pay-installment-dialog.component';

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
    MatSnackBarModule,
    MatDialogModule
  ],
  templateUrl: './my-loans.component.html',
  styleUrls: ['./my-loans.component.css']
})
export class MyLoansComponent implements OnInit {
  public authService = inject(AuthService);
  private api = inject(LoanService);
  private installmentService = inject(LoanInstallmentService);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(MatDialog);

  public loans: LoanRequest[] = [];
  public displayedColumns: string[] = ['valor', 'status', 'data', 'acoes'];
  public isLoading = false;
  public isCreatingPix = false;

  public selectedLoan: LoanRequest | null = null;
  public installments: LoanInstallmentDto[] = [];
  public isLoadingInstallments = false;
  public displayedInstallmentColumns: string[] = ['numero', 'vencimento', 'valor', 'multa', 'jurosMora', 'valorPago', 'status', 'acoes'];

  public get hasActiveLoan(): boolean {
    const statusAtivos = ['pendente', 'emanalise', 'aprovado', 'ativo', 'empagamento', 'inadimplente', 'emcaptacao', 'financiado', 'desembolsopendente'];
    return this.loans.some(l => l.status && statusAtivos.includes(l.status.toLowerCase()));
  }

  public get hasPaidLoan(): boolean {
    const statusPagos = ['quitado', 'finalizado', 'liquidado', 'pago'];
    return this.loans.some(l => l.status && statusPagos.includes(l.status.toLowerCase()));
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
        if (this.selectedLoan) {
          const updated = this.loans.find(l => l.id === this.selectedLoan?.id);
          if (updated) {
            this.selectedLoan = updated;
          }
        }
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
    if (this.isCreatingPix || this.authService.userStatus() !== 'Aprovado') return;
    const installment = this.installments.find(i => i.id === installmentId);
    if (!installment) return;

    const dialogRef = this.dialog.open(PayInstallmentDialogComponent, {
      data: { installment },
      width: '480px',
      maxWidth: '95vw',
      disableClose: false
    });

    dialogRef.afterClosed().subscribe((result: PayInstallmentDialogResult | undefined) => {
      if (!result) return; // usuário cancelou

      this.isCreatingPix = true;
      this.api.createInstallmentPix(installmentId, result.valorPago).subscribe({
        next: payment => {
          this.isCreatingPix = false;
          const pix = this.dialog.open(PixPaymentDialogComponent, {
            width: '480px', maxWidth: '95vw',
            data: { payment, loadStatus: () => this.api.getInstallmentPixStatus(installmentId) }
          });
          pix.afterClosed().subscribe(() => {
            this.loadMyLoans();
            if (this.selectedLoan?.id) this.loadInstallments(this.selectedLoan.id);
          });
        },
        error: err => {
          this.isCreatingPix = false;
          this.snackBar.open(err.error?.message || 'Erro ao gerar PIX da parcela.', 'Fechar', { duration: 4500 });
          this.cdr.detectChanges();
        }
      });
    });
  }
}
