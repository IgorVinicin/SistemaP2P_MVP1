import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

import { LoanService } from '../../../../core/services/loan.service';
import { LimitRequest } from '../../../../core/models/loan.models';

@Component({
  selector: 'app-analyze-limit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule,
    MatDialogModule
  ],
  templateUrl: './analyze-limit-dialog.component.html'
})
export class AnalyzeLimitDialogComponent {
  private api = inject(LoanService);
  private snackBar = inject(MatSnackBar);
  private dialogRef = inject(MatDialogRef<AnalyzeLimitDialogComponent>);

  public request: LimitRequest;
  public valorAprovado: number;
  public observacaoAdmin: string = '';
  public isSubmitting = false;

  constructor(@Inject(MAT_DIALOG_DATA) public data: { request: LimitRequest }) {
    this.request = data.request;
    this.valorAprovado = data.request.limiteSolicitado;
  }

  public approve(): void {
    if (this.valorAprovado <= 0) {
      this.snackBar.open('O valor aprovado deve ser maior que zero.', 'Fechar', { duration: 3000 });
      return;
    }

    this.isSubmitting = true;
    this.api.approveLimitRequest(this.request.id!, this.valorAprovado, this.observacaoAdmin).subscribe({
      next: (res) => {
        this.isSubmitting = false;
        this.snackBar.open(res.message || 'Solicitação de aumento de limite aprovada com sucesso!', 'Fechar', { duration: 3500 });
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err.error?.message || 'Erro ao aprovar solicitação de aumento de limite.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }

  public reject(): void {
    this.isSubmitting = true;
    this.api.rejectLimitRequest(this.request.id!, this.observacaoAdmin).subscribe({
      next: (res) => {
        this.isSubmitting = false;
        this.snackBar.open(res.message || 'Solicitação de aumento de limite rejeitada com sucesso.', 'Fechar', { duration: 3500 });
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err.error?.message || 'Erro ao rejeitar solicitação de aumento de limite.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }

  public cancel(): void {
    this.dialogRef.close(false);
  }
}
