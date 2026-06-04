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
  private snackBar = inject(MatSnackBar);

  public loans: LoanRequest[] = [];
  public displayedColumns: string[] = ['valor', 'parcelas', 'taxa', 'status', 'data'];
  public isLoading = false;

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
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar lista de empréstimos.', 'Fechar', { duration: 4000 });
      }
    });
  }
}
