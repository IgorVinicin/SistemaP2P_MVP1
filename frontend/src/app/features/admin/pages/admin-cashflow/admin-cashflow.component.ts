import { Component, OnInit, ViewChild, TemplateRef } from '@angular/core';
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
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatTabsModule } from '@angular/material/tabs';
import { 
  AdminCashFlowService, 
  CashFlowSnapshot, 
  CashFlowTransactionDto,
  CashFlowClosingConferenceDto
} from '../../../../core/services/admin-cashflow.service';
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
    MatNativeDateModule,
    MatDialogModule,
    MatTabsModule
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

  // Snapshots History state
  snapshots: CashFlowSnapshot[] = [];
  displayedSnapshotColumns: string[] = ['data', 'entradas', 'saidas', 'saldo', 'receita', 'coberto', 'acoes'];
  totalSnapshots = 0;
  snapshotPageSize = 10;
  snapshotPageIndex = 0;
  loadingSnapshots = false;

  @ViewChild('snapshotDetailDialog') snapshotDetailDialogTpl!: TemplateRef<any>;
  public snapshotDetailDialogRef?: MatDialogRef<any>;
  public selectedSnapshot: CashFlowSnapshot | null = null;

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

  // State for Fechamento de Caixa Conference
  @ViewChild('conferenceDialog') conferenceDialogTpl!: TemplateRef<any>;
  public conferenceDialogRef?: MatDialogRef<any>;

  public conferenceData: CashFlowClosingConferenceDto | null = null;
  public loadingConference = false;
  public isSubmittingSnapshot = false;

  public confFilters = {
    tomador: '',
    status: '',
    tipoEmprestimo: ''
  };

  // Withdrawals (Resgates) state
  withdrawals: any[] = [];
  loadingWithdrawals = false;
  displayedWithdrawalColumns: string[] = ['data', 'investidor', 'valor', 'pix', 'status', 'acoes'];
  withdrawalStatusFilter = '';

  public confSortColumn = 'vencimento';
  public confSortDirection: 'asc' | 'desc' = 'asc';

  public conferenceDisplayedColumns: string[] = [
    'tomador',
    'tipo',
    'parcela',
    'vencimento',
    'valor',
    'valorPago',
    'valorRestante',
    'pagamento',
    'status'
  ];

  constructor(
    private cashFlowService: AdminCashFlowService,
    private dialog: MatDialog,
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
    this.loadSnapshots();
    this.loadWithdrawals();
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

  // Métodos do Painel de Conferência do Fechamento de Caixa
  public openClosingConferenceDialog(): void {
    this.confFilters = { tomador: '', status: '', tipoEmprestimo: '' };
    this.loadClosingConference();
    this.conferenceDialogRef = this.dialog.open(this.conferenceDialogTpl, {
      width: '1150px',
      maxWidth: '95vw',
      disableClose: true
    });
  }

  public loadClosingConference(): void {
    this.loadingConference = true;
    this.cashFlowService.getClosingConference(
      this.periodoFiltro,
      this.startDate,
      this.endDate,
      this.confFilters.status,
      this.confFilters.tipoEmprestimo,
      this.confFilters.tomador
    ).subscribe({
      next: (res) => {
        this.conferenceData = res;
        this.applyConferenceSorting();
        this.loadingConference = false;
      },
      error: (err) => {
        console.error('Erro ao carregar conferência de fechamento:', err);
        this.snackBar.open('Erro ao carregar dados da conferência.', 'Fechar', { duration: 3000 });
        this.loadingConference = false;
      }
    });
  }

  public setConfSort(column: string): void {
    if (this.confSortColumn === column) {
      this.confSortDirection = this.confSortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.confSortColumn = column;
      this.confSortDirection = 'asc';
    }
    this.applyConferenceSorting();
  }

  public applyConferenceSorting(): void {
    if (!this.conferenceData || !this.conferenceData.items) return;

    const mult = this.confSortDirection === 'asc' ? 1 : -1;
    this.conferenceData.items.sort((a, b) => {
      switch (this.confSortColumn) {
        case 'tomador':
          return a.tomadorNome.localeCompare(b.tomadorNome) * mult;
        case 'valor':
          return (a.valorTotalDevido - b.valorTotalDevido) * mult;
        case 'status':
          return a.statusCalculado.localeCompare(b.statusCalculado) * mult;
        case 'parcela':
          return (a.numeroParcela - b.numeroParcela) * mult;
        case 'vencimento':
        default:
          return (new Date(a.dataVencimento).getTime() - new Date(b.dataVencimento).getTime()) * mult;
      }
    });
  }

  public confirmCloseCashFlow(): void {
    this.isSubmittingSnapshot = true;
    const targetDateStr = this.endDate ? this.endDate.toISOString().split('T')[0] : new Date().toISOString().split('T')[0];
    this.cashFlowService.generateSnapshot(targetDateStr).subscribe({
      next: () => {
        this.isSubmittingSnapshot = false;
        this.snackBar.open('Fechamento de caixa concluído e snapshot gerado com sucesso!', 'Fechar', { duration: 4000 });
        this.conferenceDialogRef?.close();
        this.loadData();
      },
      error: (err) => {
        this.isSubmittingSnapshot = false;
        const msg = err.error?.message || 'Erro ao fechar caixa.';
        this.snackBar.open(msg, 'Fechar', { duration: 5000 });
      }
    });
  }

  loadSnapshots(): void {
    this.loadingSnapshots = true;
    this.cashFlowService.getSnapshots(this.snapshotPageIndex + 1, this.snapshotPageSize).subscribe({
      next: (res: any) => {
        this.snapshots = res?.items || res?.Items || [];
        this.totalSnapshots = res?.totalCount || res?.TotalCount || 0;
        this.loadingSnapshots = false;
      },
      error: (err: any) => {
        console.error('Erro ao carregar histórico de fechamentos:', err);
        this.snackBar.open('Erro ao carregar o histórico de fechamentos.', 'Fechar', { duration: 3000 });
        this.loadingSnapshots = false;
      }
    });
  }

  onSnapshotPageChange(event: PageEvent): void {
    this.snapshotPageIndex = event.pageIndex;
    this.snapshotPageSize = event.pageSize;
    this.loadSnapshots();
  }

  openSnapshotDetailDialog(snapshot: CashFlowSnapshot): void {
    this.selectedSnapshot = snapshot;
    this.snapshotDetailDialogRef = this.dialog.open(this.snapshotDetailDialogTpl, {
      width: '600px',
      maxWidth: '90vw'
    });
  }

  loadWithdrawals(): void {
    this.loadingWithdrawals = true;
    this.cashFlowService.getWithdrawals(this.withdrawalStatusFilter || undefined).subscribe({
      next: (res) => {
        this.withdrawals = res;
        this.loadingWithdrawals = false;
      },
      error: (err) => {
        console.error('Erro ao carregar saques:', err);
        this.snackBar.open('Erro ao carregar solicitações de saque.', 'Fechar', { duration: 3000 });
        this.loadingWithdrawals = false;
      }
    });
  }

  approveWithdraw(id: string): void {
    if (confirm('Deseja realmente aprovar esta solicitação de saque? O valor correspondente continuará bloqueado até a baixa manual ser confirmada.')) {
      this.cashFlowService.approveWithdrawal(id).subscribe({
        next: (res) => {
          this.snackBar.open(res.message || 'Saque aprovado com sucesso!', 'Fechar', { duration: 3000 });
          this.loadWithdrawals();
          this.loadData();
        },
        error: (err) => {
          this.snackBar.open(err.error?.message || 'Erro ao aprovar saque.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  rejectWithdraw(id: string): void {
    const motivo = prompt('Por favor, informe o motivo da rejeição do saque:');
    if (motivo === null) return; // Cancelled
    if (motivo.trim() === '') {
      this.snackBar.open('Motivo é obrigatório para rejeição.', 'Fechar', { duration: 3500 });
      return;
    }

    this.cashFlowService.rejectWithdrawal(id, motivo).subscribe({
      next: (res) => {
        this.snackBar.open(res.message || 'Saque rejeitado com sucesso. O saldo foi liberado ao investidor.', 'Fechar', { duration: 3000 });
        this.loadWithdrawals();
        this.loadData();
      },
      error: (err) => {
        this.snackBar.open(err.error?.message || 'Erro ao rejeitar saque.', 'Fechar', { duration: 4000 });
      }
    });
  }

  confirmWithdrawManual(id: string): void {
    const ref = prompt('Informe a referência de transação bancária (ou Pix EndToEnd ID) para a baixa manual:');
    if (ref === null) return; // Cancelled
    const finalRef = ref.trim() === '' ? 'MANUAL-PIX' : ref.trim();

    this.cashFlowService.confirmWithdrawal(id, finalRef).subscribe({
      next: (res) => {
        this.snackBar.open(res.message || 'Saque liquidado com sucesso!', 'Fechar', { duration: 3000 });
        this.loadWithdrawals();
        this.loadData();
      },
      error: (err) => {
        this.snackBar.open(err.error?.message || 'Erro ao confirmar saque.', 'Fechar', { duration: 4000 });
      }
    });
  }
}
