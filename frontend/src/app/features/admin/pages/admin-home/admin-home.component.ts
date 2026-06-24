import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { PendingUserSummaryDto } from '../../../../core/models/user.models';
import { UserDetailDialogComponent } from '../../components/user-detail-dialog/user-detail-dialog.component';
import { UserEditDialogComponent } from '../../components/user-edit-dialog/user-edit-dialog.component';
import { AdminService } from '../../../../core/services/admin.service';
import { AdminWalletService, AdminInvestorWalletSummaryDto } from '../../../../core/services/admin-wallet.service';
import { WalletAdjustmentDialogComponent } from '../../components/wallet-adjustment-dialog/wallet-adjustment-dialog.component';
import { WalletHistoryDialogComponent } from '../../components/wallet-history-dialog/wallet-history-dialog.component';
import { AdminReportService } from '../../../../core/services/admin-report.service';
import { AdminReportPdfService } from '../../../../core/services/admin-report-pdf.service';

@Component({
  selector: 'app-admin-home',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatDialogModule,
    MatSnackBarModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './admin-home.component.html',
  styleUrls: ['./admin-home.component.css']
})
export class AdminHomeComponent implements OnInit {
  private adminService = inject(AdminService);
  private adminWalletService = inject(AdminWalletService);
  private reportService = inject(AdminReportService);
  private reportPdfService = inject(AdminReportPdfService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public pendingUsers: PendingUserSummaryDto[] = [];
  public allUsers: PendingUserSummaryDto[] = [];
  public wallets: AdminInvestorWalletSummaryDto[] = [];

  // Paging and search states
  public pendingPage = 1;
  public pendingPageSize = 5;
  public pendingTotal = 0;
  public pendingSearch = '';

  public allPage = 1;
  public allPageSize = 10;
  public allTotal = 0;
  public allSearch = '';
  public allFilters = {
    nome: '',
    cpf: '',
    email: '',
    role: '',
    status: '',
    minScore: null as number | null,
    maxScore: null as number | null
  };

  public walletsPage = 1;
  public walletsPageSize = 5;
  public walletsTotal = 0;
  public walletsSearch = '';

  public displayedColumns: string[] = [
    'nome',
    'email',
    'role',
    'scoreAtual',
    'createdAt',
    'acoes'
  ];

  public gestaoColumns: string[] = [
    'nome',
    'email',
    'role',
    'scoreAtual',
    'status',
    'acoes'
  ];

  public walletColumns: string[] = [
    'investor',
    'email',
    'saldoDisponivel',
    'saldoInvestido',
    'rendimentos',
    'totalAportado',
    'totalSacado',
    'acoes'
  ];

  public isLoadingPending = false;
  public isLoadingAll = false;
  public isLoadingWallets = false;

  ngOnInit(): void {
    this.loadPendingUsers();
    this.loadAllUsers();
    this.loadInvestorWallets();
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
        this.snackBar.open('Erro ao carregar solicitações de cadastro pendentes.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadAllUsers(): void {
    this.isLoadingAll = true;
    this.adminService.getAllUsers(this.allPage, this.allPageSize, this.allSearch, this.allFilters).subscribe({
      next: (data) => {
        this.allUsers = data.items;
        this.allTotal = data.totalCount;
        this.isLoadingAll = false;
      },
      error: () => {
        this.isLoadingAll = false;
        this.snackBar.open('Erro ao carregar usuários cadastrados.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadInvestorWallets(): void {
    this.isLoadingWallets = true;
    this.adminWalletService.getWallets(this.walletsPage, this.walletsPageSize, this.walletsSearch).subscribe({
      next: (data) => {
        this.wallets = data.items;
        this.walletsTotal = data.totalCount;
        this.isLoadingWallets = false;
      },
      error: () => {
        this.isLoadingWallets = false;
        this.snackBar.open('Erro ao carregar carteiras dos investidores.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public onPendingPageChange(event: PageEvent): void {
    this.pendingPage = event.pageIndex + 1;
    this.pendingPageSize = event.pageSize;
    this.loadPendingUsers();
  }

  public onAllPageChange(event: PageEvent): void {
    this.allPage = event.pageIndex + 1;
    this.allPageSize = event.pageSize;
    this.loadAllUsers();
  }

  public onWalletsPageChange(event: PageEvent): void {
    this.walletsPage = event.pageIndex + 1;
    this.walletsPageSize = event.pageSize;
    this.loadInvestorWallets();
  }

  public searchPending(): void {
    this.pendingPage = 1;
    this.loadPendingUsers();
  }

  public searchAll(): void {
    this.allPage = 1;
    this.loadAllUsers();
  }

  public clearAllFilters(): void {
    this.allSearch = '';
    this.allFilters = {
      nome: '',
      cpf: '',
      email: '',
      role: '',
      status: '',
      minScore: null,
      maxScore: null
    };
    this.allPage = 1;
    this.loadAllUsers();
  }

  public searchWallets(): void {
    this.walletsPage = 1;
    this.loadInvestorWallets();
  }

  openAnalysis(userId: string): void {
    const dialogRef = this.dialog.open(UserDetailDialogComponent, {
      data: { userId },
      disableClose: true,
      width: '900px'
    });

    dialogRef.afterClosed().subscribe((changed) => {
      if (changed) {
        this.loadPendingUsers();
        this.loadAllUsers();
        this.loadInvestorWallets();
      }
    });
  }

  openEdit(userId: string): void {
    const dialogRef = this.dialog.open(UserEditDialogComponent, {
      data: { userId },
      disableClose: true,
      width: '900px'
    });

    dialogRef.afterClosed().subscribe(() => {
      this.loadPendingUsers();
      this.loadAllUsers();
      this.loadInvestorWallets();
    });
  }

  openWalletAdjustment(wallet: AdminInvestorWalletSummaryDto): void {
    const dialogRef = this.dialog.open(WalletAdjustmentDialogComponent, {
      data: {
        investorId: wallet.investorId,
        nome: wallet.nome,
        email: wallet.email
      },
      disableClose: true,
      width: '500px'
    });

    dialogRef.afterClosed().subscribe((changed) => {
      if (changed) {
        this.loadInvestorWallets();
      }
    });
  }

  openWalletHistory(wallet: AdminInvestorWalletSummaryDto): void {
    this.dialog.open(WalletHistoryDialogComponent, {
      data: {
        investorId: wallet.investorId,
        nome: wallet.nome,
        email: wallet.email
      },
      width: '900px'
    });
  }

  public generateUserReport(userId: string, role: string): void {
    if (role === 'Cliente') {
      this.reportService.getBorrowerIndividual(userId).subscribe({
        next: (res) => this.reportPdfService.generateBorrowerIndividual(res),
        error: () => this.snackBar.open('Erro ao gerar relatório individual do tomador.', 'Fechar', { duration: 4000 })
      });
    } else if (role === 'Investidor') {
      this.reportService.getInvestorIndividual(userId).subscribe({
        next: (res) => this.reportPdfService.generateInvestorIndividual(res),
        error: () => this.snackBar.open('Erro ao gerar relatório individual do investidor.', 'Fechar', { duration: 4000 })
      });
    }
  }
}