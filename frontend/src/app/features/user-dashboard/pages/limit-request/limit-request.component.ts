import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatTableModule } from '@angular/material/table';

import { LoanService } from '../../../../core/services/loan.service';
import { LoanEligibility, LimitRequest } from '../../../../core/models/loan.models';

@Component({
  selector: 'app-limit-request',
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
    MatSnackBarModule,
    MatDividerModule,
    MatTableModule
  ],
  templateUrl: './limit-request.component.html',
  styleUrls: ['./limit-request.component.css']
})
export class LimitRequestComponent implements OnInit {
  private api = inject(LoanService);
  private snackBar = inject(MatSnackBar);

  public eligibility: LoanEligibility | null = null;
  public myRequests: LimitRequest[] = [];
  public isLoading = false;
  public isSubmitting = false;

  public requestedValue = 0;
  public displayedColumns: string[] = ['limiteSolicitado', 'limiteAtual', 'status', 'dataCriacao', 'observacoes'];

  ngOnInit(): void {
    this.checkEligibility();
    this.loadMyRequests();
  }

  public checkEligibility(): void {
    this.isLoading = true;
    this.api.getEligibility().subscribe({
      next: (data) => {
        this.eligibility = data;
        this.isLoading = false;
        // Inicializa valor sugerido como limite atual + 500
        this.requestedValue = (data.limiteDisponivel || 0) + 500;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar dados de limite.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadMyRequests(): void {
    this.api.getMyLimitRequests().subscribe({
      next: (data) => {
        this.myRequests = data;
      },
      error: () => {
        this.snackBar.open('Erro ao carregar histórico de solicitações de limite.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public submitRequest(): void {
    if (this.requestedValue <= 0) {
      this.snackBar.open('O limite solicitado deve ser maior que zero.', 'Fechar', { duration: 3000 });
      return;
    }

    if (this.eligibility && this.requestedValue <= this.eligibility.limiteDisponivel) {
      this.snackBar.open('O novo limite solicitado deve ser maior que o seu limite atual.', 'Fechar', { duration: 4000 });
      return;
    }

    this.isSubmitting = true;
    this.api.requestLimit(this.requestedValue).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.snackBar.open('Solicitação de aumento de limite enviada com sucesso!', 'Fechar', { duration: 4000 });
        this.checkEligibility();
        this.loadMyRequests();
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err.error?.message || 'Erro ao solicitar aumento de limite.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }
}
