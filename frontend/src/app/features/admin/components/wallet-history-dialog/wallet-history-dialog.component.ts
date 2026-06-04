import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminWalletService } from '../../../../core/services/admin-wallet.service';
import { InvestorTransactionDto } from '../../../../core/models/investor.models';

@Component({
  selector: 'app-wallet-history-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule
  ],
  templateUrl: './wallet-history-dialog.component.html',
  styles: [`
    .history-table {
      width: 100%;
      margin-top: 15px;
    }
    .tx-badge {
      display: inline-block;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
    }
    .tx-badge.aporte { background-color: #d1fae5; color: #065f46; }
    .tx-badge.saque { background-color: #fee2e2; color: #991b1b; }
    .tx-badge.rendimento { background-color: #dbeafe; color: #1e40af; }
    .tx-badge.investimento { background-color: #fef3c7; color: #92400e; }
    .tx-badge.ajuste_admin { background-color: #f3e8ff; color: #6b21a8; }
    .tx-badge.estorno { background-color: #f1f5f9; color: #334155; }
  `]
})
export class WalletHistoryDialogComponent implements OnInit {
  private dialogRef = inject(MatDialogRef<WalletHistoryDialogComponent>);
  private adminWalletService = inject(AdminWalletService);
  private snackBar = inject(MatSnackBar);

  public transactions: InvestorTransactionDto[] = [];
  public displayedColumns: string[] = ['data', 'tipo', 'descricao', 'valor', 'saldoAnterior', 'saldoPosterior', 'responsavel'];
  public isLoading = false;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { investorId: string; nome: string; email: string }) {}

  ngOnInit(): void {
    this.loadHistory();
  }

  public loadHistory(): void {
    this.isLoading = true;
    this.adminWalletService.getTransactions(this.data.investorId).subscribe({
      next: (res) => {
        this.transactions = res;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar histórico de transações.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public close(): void {
    this.dialogRef.close();
  }
}
