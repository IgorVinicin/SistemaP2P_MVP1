import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { AdminService } from '../../../../core/services/admin.service';
import { AdminWalletService } from '../../../../core/services/admin-wallet.service';
import { LoanService } from '../../../../core/services/loan.service';
import { PendingUserSummaryDto } from '../../../../core/models/user.models';
import { LoanRequest, LimitRequest } from '../../../../core/models/loan.models';
import { UserDetailDialogComponent } from '../../components/user-detail-dialog/user-detail-dialog.component';
import { AnalyzeLimitDialogComponent } from '../../components/analyze-limit-dialog/analyze-limit-dialog.component';

@Component({
  selector: 'app-admin-home',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatDialogModule,
    MatSnackBarModule
  ],
  templateUrl: './admin-home.component.html',
  styleUrls: ['./admin-home.component.css']
})
export class AdminHomeComponent implements OnInit {
  private adminService = inject(AdminService);
  private adminWalletService = inject(AdminWalletService);
  private loanService = inject(LoanService);
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public pendingTotal = 0;
  public pendingLoansTotal = 0;
  public usersTotal = 0;
  public walletsTotal = 0;

  // Tabela de Cadastros Pendentes
  public pendingUsers: PendingUserSummaryDto[] = [];
  public pendingPage = 1;
  public pendingPageSize = 5;
  public pendingSearch = '';
  public isLoadingPending = false;
  public displayedColumns: string[] = ['nome', 'email', 'role', 'scoreAtual', 'createdAt', 'acoes'];

  // Tabela de Empréstimos Pendentes
  public pendingLoans: LoanRequest[] = [];
  public loansPage = 1;
  public loansPageSize = 5;
  public loansSearch = '';
  public isLoadingLoans = false;
  public loanDisplayedColumns: string[] = ['tomador', 'cpf', 'valor', 'parcelas', 'risco', 'data', 'acoes'];

  // Tabela de Solicitações de Aumento de Limite
  public limitRequests: LimitRequest[] = [];
  public limitPage = 1;
  public limitPageSize = 5;
  public limitTotal = 0;
  public limitSearch = '';
  public isLoadingLimits = false;
  public limitDisplayedColumns: string[] = ['tomador', 'cpf', 'valorSolicitado', 'limiteAtual', 'acoes'];

  ngOnInit(): void {
    this.loadStats();
    this.loadPendingUsers();
    this.loadPendingLoans();
    this.loadLimitRequests();
  }

  public loadStats(): void {
    const emptyFilters = { nome: '', cpf: '', email: '', role: '', status: '', minScore: null, maxScore: null };
    this.adminService.getAllUsers(1, 1, '', emptyFilters, '').subscribe({
      next: (data) => this.usersTotal = data.totalCount,
      error: () => {}
    });

    this.adminWalletService.getWallets(1, 1, '').subscribe({
      next: (data) => this.walletsTotal = data.totalCount,
      error: () => {}
    });
  }

  public loadPendingUsers(): void {
    this.isLoadingPending = true;
    this.adminService.getPendingUsers(this.pendingPage, this.pendingPageSize, this.pendingSearch).subscribe({
      next: (data) => {
        this.pendingUsers = data.items;
        this.pendingTotal = data.totalCount;
        this.isLoadingPending = false;
      },
      error: () => {
        this.isLoadingPending = false;
        this.snackBar.open('Erro ao carregar solicitações pendentes de usuários.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadPendingLoans(): void {
    this.isLoadingLoans = true;
    // Passamos false no último parâmetro para trazer apenas empréstimos normais (sem aumento de limite)
    this.loanService.getPendingLoans(this.loansPage, this.loansPageSize, this.loansSearch, undefined, undefined, false).subscribe({
      next: (data) => {
        this.pendingLoans = data.items;
        this.pendingLoansTotal = data.totalCount;
        this.isLoadingLoans = false;
      },
      error: () => {
        this.isLoadingLoans = false;
        this.snackBar.open('Erro ao carregar solicitações pendentes de empréstimos.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadLimitRequests(): void {
    this.isLoadingLimits = true;
    this.loanService.getPendingLimitRequests(this.limitPage, this.limitPageSize, this.limitSearch).subscribe({
      next: (data) => {
        this.limitRequests = data.items;
        this.limitTotal = data.totalCount;
        this.isLoadingLimits = false;
      },
      error: () => {
        this.isLoadingLimits = false;
        this.snackBar.open('Erro ao carregar solicitações de aumento de limite.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public onPendingPageChange(event: PageEvent): void {
    this.pendingPage = event.pageIndex + 1;
    this.pendingPageSize = event.pageSize;
    this.loadPendingUsers();
  }

  public onLoansPageChange(event: PageEvent): void {
    this.loansPage = event.pageIndex + 1;
    this.loansPageSize = event.pageSize;
    this.loadPendingLoans();
  }

  public onLimitPageChange(event: PageEvent): void {
    this.limitPage = event.pageIndex + 1;
    this.limitPageSize = event.pageSize;
    this.loadLimitRequests();
  }

  public searchPending(): void {
    this.pendingPage = 1;
    this.loadPendingUsers();
  }

  public searchLoans(): void {
    this.loansPage = 1;
    this.loadPendingLoans();
  }

  public searchLimits(): void {
    this.limitPage = 1;
    this.loadLimitRequests();
  }

  public openAnalysis(userId: string): void {
    const dialogRef = this.dialog.open(UserDetailDialogComponent, {
      data: { userId },
      disableClose: true,
      width: '900px'
    });

    dialogRef.afterClosed().subscribe((changed) => {
      if (changed) {
        this.loadPendingUsers();
        this.loadStats();
      }
    });
  }

  public analyzeLoan(loanId: string): void {
    this.router.navigate(['/admin/loans'], { queryParams: { loanId } });
  }

  public openAnalyzeLimit(request: LimitRequest): void {
    const dialogRef = this.dialog.open(AnalyzeLimitDialogComponent, {
      data: { request },
      disableClose: true,
      width: '500px'
    });

    dialogRef.afterClosed().subscribe((changed) => {
      if (changed) {
        this.loadLimitRequests();
        this.loadStats();
      }
    });
  }
}