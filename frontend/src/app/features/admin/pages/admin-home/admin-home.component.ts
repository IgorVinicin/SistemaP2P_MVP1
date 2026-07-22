import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { AdminService } from '../../../../core/services/admin.service';
import { AdminWalletService } from '../../../../core/services/admin-wallet.service';

@Component({
  selector: 'app-admin-home',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    RouterLink
  ],
  templateUrl: './admin-home.component.html',
  styleUrls: ['./admin-home.component.css']
})
export class AdminHomeComponent implements OnInit {
  private adminService = inject(AdminService);
  private adminWalletService = inject(AdminWalletService);

  public pendingTotal = 0;
  public usersTotal = 0;
  public walletsTotal = 0;

  ngOnInit(): void {
    this.loadStats();
  }

  private loadStats(): void {
    this.adminService.getPendingUsers(1, 1, '').subscribe({
      next: (data) => this.pendingTotal = data.totalCount,
      error: () => {}
    });

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
}