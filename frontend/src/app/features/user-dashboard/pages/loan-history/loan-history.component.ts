import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { LoanService } from '../../../../core/services/loan.service';
import { LoanRequest } from '../../../../core/models/loan.models';

@Component({
  selector: 'app-loan-history',
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
  templateUrl: './loan-history.component.html',
  styleUrls: ['./loan-history.component.css']
})
export class LoanHistoryComponent implements OnInit {
  private api = inject(LoanService);
  private snackBar = inject(MatSnackBar);

  public loans: LoanRequest[] = [];
  public displayedColumns: string[] = ['valor', 'dataSolicitacao', 'parcelas', 'status', 'dataAprovacao', 'dataEncerramento'];
  public isLoading = false;

  ngOnInit(): void {
    this.loadHistory();
  }

  public loadHistory(): void {
    this.isLoading = true;
    this.api.getLoanHistory().subscribe({
      next: (data) => {
        this.loans = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar histórico de empréstimos.', 'Fechar', { duration: 4000 });
      }
    });
  }
}
