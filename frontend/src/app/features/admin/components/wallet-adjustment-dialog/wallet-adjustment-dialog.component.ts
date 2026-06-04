import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminWalletService } from '../../../../core/services/admin-wallet.service';

@Component({
  selector: 'app-wallet-adjustment-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSnackBarModule
  ],
  templateUrl: './wallet-adjustment-dialog.component.html'
})
export class WalletAdjustmentDialogComponent {
  private dialogRef = inject(MatDialogRef<WalletAdjustmentDialogComponent>);
  private adminWalletService = inject(AdminWalletService);
  private snackBar = inject(MatSnackBar);

  public valor: number | null = null;
  public descricao: string = '';
  public isProcessing = false;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { investorId: string; nome: string; email: string }) {}

  public confirmAdjustment(): void {
    if (this.valor === null || this.valor === 0) {
      this.snackBar.open('Por favor, informe um valor de ajuste diferente de zero.', 'Fechar', { duration: 4000 });
      return;
    }

    if (!this.descricao || this.descricao.trim().length < 5) {
      this.snackBar.open('Por favor, insira uma descrição justificando o ajuste (mínimo 5 caracteres).', 'Fechar', { duration: 4000 });
      return;
    }

    this.isProcessing = true;
    this.adminWalletService.adjustWallet(this.data.investorId, this.valor, this.descricao).subscribe({
      next: () => {
        this.isProcessing = false;
        this.snackBar.open('Ajuste de carteira realizado com sucesso!', 'Fechar', { duration: 3000 });
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.isProcessing = false;
        this.snackBar.open(err.error?.message || 'Erro ao realizar o ajuste da carteira.', 'Fechar', { duration: 4500 });
      }
    });
  }

  public cancel(): void {
    this.dialogRef.close(false);
  }
}
