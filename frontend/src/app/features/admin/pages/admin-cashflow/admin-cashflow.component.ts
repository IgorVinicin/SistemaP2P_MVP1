import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminCashFlowService, CashFlowSnapshot } from '../../../../core/services/admin-cashflow.service';

@Component({
  selector: 'app-admin-cashflow',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule
  ],
  templateUrl: './admin-cashflow.component.html',
  styleUrls: ['./admin-cashflow.component.css']
})
export class AdminCashFlowComponent implements OnInit {
  summary: CashFlowSnapshot | null = null;
  snapshots: CashFlowSnapshot[] = [];
  
  periodoFiltro = 'hoje';
  
  displayedColumns: string[] = ['data', 'entradas', 'saidas', 'depositos', 'saques', 'saldo', 'receita', 'coberturas'];
  totalCount = 0;
  pageSize = 10;
  pageIndex = 0;
  loadingSummary = false;
  loadingSnapshots = false;

  constructor(
    private cashFlowService: AdminCashFlowService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadSummary();
    this.loadSnapshots();
  }

  loadSummary(): void {
    this.loadingSummary = true;
    this.cashFlowService.getSummary(this.periodoFiltro).subscribe({
      next: (res: CashFlowSnapshot) => {
        this.summary = res;
        this.loadingSummary = false;
      },
      error: (err: any) => {
        console.error('Erro ao carregar summary:', err);
        this.snackBar.open('Erro ao carregar o resumo de cashflow.', 'Fechar', { duration: 3000 });
        this.loadingSummary = false;
      }
    });
  }

  loadSnapshots(): void {
    this.loadingSnapshots = true;
    this.cashFlowService.getSnapshots(this.pageIndex + 1, this.pageSize).subscribe({
      next: (res: any) => {
        this.snapshots = res.items;
        this.totalCount = res.totalCount;
        this.loadingSnapshots = false;
      },
      error: (err: any) => {
        console.error('Erro ao carregar snapshots:', err);
        this.snackBar.open('Erro ao carregar os fechamentos diários.', 'Fechar', { duration: 3000 });
        this.loadingSnapshots = false;
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadSnapshots();
  }

  onPeriodChange(): void {
    this.loadSummary();
  }

  generateSnapshot(): void {
    if(confirm('Deseja realmente gerar/fechar o snapshot operacional do dia atual?')) {
      this.cashFlowService.generateSnapshot().subscribe({
        next: (res: any) => {
          this.snackBar.open(res.message || 'Fechamento gerado!', 'OK', { duration: 3000 });
          this.loadSnapshots();
        },
        error: (err: any) => {
          this.snackBar.open(err.error?.message || 'Erro ao gerar snapshot.', 'OK', { duration: 4000 });
        }
      });
    }
  }
}
