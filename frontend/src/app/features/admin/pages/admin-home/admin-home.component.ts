import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { PendingUserSummaryDto } from '../../../../core/models/user.models';
import { UserDetailDialogComponent } from '../../components/user-detail-dialog/user-detail-dialog.component';
import { UserEditDialogComponent } from '../../components/user-edit-dialog/user-edit-dialog.component';
import { AdminService } from '../../../../core/services/admin.service';
import { AdminWalletService, AdminInvestorWalletSummaryDto } from '../../../../core/services/admin-wallet.service';
import { WalletAdjustmentDialogComponent } from '../../components/wallet-adjustment-dialog/wallet-adjustment-dialog.component';
import { WalletHistoryDialogComponent } from '../../components/wallet-history-dialog/wallet-history-dialog.component';

@Component({
  selector: 'app-admin-home',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatDialogModule,
    MatSnackBarModule,
    UserDetailDialogComponent,
    UserEditDialogComponent,
    WalletAdjustmentDialogComponent,
    WalletHistoryDialogComponent
  ],
  templateUrl: './admin-home.component.html',
  styleUrls: ['./admin-home.component.css']
})
export class AdminHomeComponent implements OnInit {
  private adminService = inject(AdminService);
  private adminWalletService = inject(AdminWalletService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public pendingUsers: PendingUserSummaryDto[] = [];
  public allUsers: PendingUserSummaryDto[] = [];
  public wallets: AdminInvestorWalletSummaryDto[] = [];

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
    this.adminService.getPendingUsers().subscribe({
      next: (data) => {
        this.pendingUsers = data;
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
    this.adminService.getAllUsers().subscribe({
      next: (data) => {
        this.allUsers = data;
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
    this.adminWalletService.getWallets().subscribe({
      next: (data) => {
        this.wallets = data;
        this.isLoadingWallets = false;
      },
      error: () => {
        this.isLoadingWallets = false;
        this.snackBar.open('Erro ao carregar carteiras dos investidores.', 'Fechar', { duration: 4000 });
      }
    });
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
}