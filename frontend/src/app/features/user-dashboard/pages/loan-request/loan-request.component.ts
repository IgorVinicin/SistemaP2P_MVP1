import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';

import { LoanService } from '../../../../core/services/loan.service';
import { LoanEligibility } from '../../../../core/models/loan.models';

@Component({
  selector: 'app-loan-request',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSnackBarModule,
    MatDividerModule
  ],
  templateUrl: './loan-request.component.html',
  styleUrls: ['./loan-request.component.css']
})
export class LoanRequestComponent implements OnInit {
  private api = inject(LoanService);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  public eligibility: LoanEligibility | null = null;
  public isLoading = false;
  public isSubmitting = false;

  public loanForm = {
    valorSolicitado: 0,
    quantidadeParcelas: 12,
    finalidade: '',
    observacoes: ''
  };

  ngOnInit(): void {
    this.checkEligibility();
  }

  public checkEligibility(): void {
    this.isLoading = true;
    this.api.getEligibility().subscribe({
      next: (data) => {
        this.eligibility = data;
        this.isLoading = false;
        if (data.podeSolicitar) {
          // Inicializa valorSolicitado padrão como metade do limite
          this.loanForm.valorSolicitado = Math.min(5000, Math.floor(data.limiteDisponivel / 2));
        }
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao validar perfil de elegibilidade para empréstimo.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public submitRequest(): void {
    if (!this.eligibility || !this.eligibility.podeSolicitar) return;

    const { valorSolicitado, quantidadeParcelas, finalidade, observacoes } = this.loanForm;

    if (valorSolicitado <= 0) {
      this.snackBar.open('O valor solicitado deve ser maior que zero.', 'Fechar', { duration: 3000 });
      return;
    }

    if (valorSolicitado > this.eligibility.limiteDisponivel) {
      this.snackBar.open('Valor solicitado excede o limite disponível para seu perfil.', 'Fechar', { duration: 4000 });
      return;
    }

    if (quantidadeParcelas <= 0) {
      this.snackBar.open('A quantidade de parcelas deve ser maior que zero.', 'Fechar', { duration: 3000 });
      return;
    }

    this.isSubmitting = true;

    const payload = {
      valorSolicitado,
      quantidadeParcelas,
      finalidade,
      observacoes
    };

    this.api.requestLoan(payload).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.snackBar.open('Solicitação de empréstimo criada com sucesso!', 'Fechar', { duration: 3500 });
        this.router.navigate(['/loans/my']);
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err.error?.message || 'Erro ao submeter solicitação de empréstimo.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }
}
