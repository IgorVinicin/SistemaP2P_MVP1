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
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminCashFlowService, CashFlowSnapshot, CashFlowTransactionDto } from '../../../../core/services/admin-cashflow.service';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

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
    MatSnackBarModule,
    MatDatepickerModule,
    MatNativeDateModule
  ],
  templateUrl: './admin-cashflow.component.html',
  styleUrls: ['./admin-cashflow.component.css']
})
export class AdminCashFlowComponent implements OnInit {
  summary: CashFlowSnapshot | null = null;
  periodoFiltro = 'hoje';
  tipoFiltro: number | null = null;
  searchQuery: string = '';
  searchSubject = new Subject<string>();

  startDate: Date | null = null;
  endDate: Date | null = null;

  // Ledger History (Today)
  transactions: CashFlowTransactionDto[] = [];
  displayedColumns: string[] = ['data', 'tipo', 'valor', 'usuario', 'cpf', 'observacao'];
  totalCount = 0;
  pageSize = 15;
  pageIndex = 0;
  
  loadingSummary = false;
  loadingTransactions = false;

  constructor(
    private cashFlowService: AdminCashFlowService,
    private snackBar: MatSnackBar
  ) {
    this.searchSubject.pipe(
      debounceTime(500),
      distinctUntilChanged()
    ).subscribe(() => {
      this.pageIndex = 0;
      this.loadTransactions();
    });
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loadSummary();
    this.loadTransactions();
  }

  get totalMovimentacao(): number {
    if (!this.summary) return 0;
    return this.summary.totalRecebidoParcelas + this.summary.totalEmprestado;
  }

  loadSummary(): void {
    this.loadingSummary = true;
    this.cashFlowService.getSummary(this.periodoFiltro, this.startDate, this.endDate).subscribe({
      next: (res: CashFlowSnapshot) => {
        this.summary = res;
        this.loadingSummary = false;
      },
      error: (err: any) => {
        console.error('Erro ao carregar summary:', err);
        this.snackBar.open('Erro ao carregar os indicadores do período.', 'Fechar', { duration: 3000 });
        this.loadingSummary = false;
      }
    });
  }

  loadTransactions(): void {
    this.loadingTransactions = true;
    this.cashFlowService.getTransactions(
        this.periodoFiltro, 
        this.searchQuery, 
        this.tipoFiltro ?? undefined, 
        this.pageIndex + 1, 
        this.pageSize,
        this.startDate ?? undefined,
        this.endDate ?? undefined
    ).subscribe({
      next: (res: any) => {
        this.transactions = res.items;
        this.totalCount = res.totalCount;
        this.loadingTransactions = false;
      },
      error: (err: any) => {
        console.error('Erro ao carregar histórico:', err);
        this.snackBar.open('Erro ao carregar o histórico de transações.', 'Fechar', { duration: 3000 });
        this.loadingTransactions = false;
      }
    });
  }

  onPeriodChange(): void {
    if (this.periodoFiltro !== 'custom') {
      this.startDate = null;
      this.endDate = null;
      this.pageIndex = 0;
      this.loadData();
    }
  }

  onDateRangeChange(): void {
    if (this.startDate && this.endDate) {
      this.pageIndex = 0;
      this.loadData();
    }
  }

  onFilterChange(): void {
    this.pageIndex = 0;
    this.loadTransactions();
  }

  onSearchChange(): void {
    this.searchSubject.next(this.searchQuery);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadTransactions();
  }

  downloadReport(): void {
    this.cashFlowService.exportTransactions(
      this.periodoFiltro, 
      this.searchQuery, 
      this.tipoFiltro ?? undefined,
      this.startDate ?? undefined,
      this.endDate ?? undefined
    ).subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `relatorio-fluxo-caixa-${this.periodoFiltro}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err: any) => {
        console.error('Erro ao baixar relatório:', err);
        this.snackBar.open('Erro ao exportar o relatório.', 'Fechar', { duration: 3000 });
      }
    });
  }

  getTipoDescricao(tipo: number): string {
    switch (tipo) {
      case 0: return 'Empréstimo (Desembolso)';
      case 1: return 'Pagamento de Parcela';
      default: return 'Desconhecido';
    }
  }

  getTipoIcon(tipo: number): string {
    switch (tipo) {
      case 0: return 'send';
      case 1: return 'request_quote';
      default: return 'help_outline';
    }
  }
}
