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
import { MatChipsModule } from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminLedgerService, LedgerTransaction } from '../../../../core/services/admin-ledger.service';

@Component({
  selector: 'app-admin-ledger',
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
    MatChipsModule,
    MatSnackBarModule
  ],
  templateUrl: './admin-ledger.component.html',
  styleUrls: ['./admin-ledger.component.css']
})
export class AdminLedgerComponent implements OnInit {
  displayedColumns: string[] = ['data', 'tipo', 'origem', 'destino', 'valor', 'referencia', 'observacao'];
  transactions: LedgerTransaction[] = [];
  
  totalCount = 0;
  pageSize = 20;
  pageIndex = 0;
  
  periodoFiltro = '30dias';
  tipoMovimentacaoFiltro: number | null = null;
  contaFiltro: number | null = null;
  loading = false;

  ledgerTransactionTypes: { value: number; label: string }[] = [
    { value: 0, label: 'Desembolso Empréstimo' },
    { value: 1, label: 'Pagamento Parcela' },
    { value: 2, label: 'Depósito Investidor' },
    { value: 3, label: 'Saque Investidor' },
    { value: 4, label: 'Distribuição Receita' },
    { value: 5, label: 'Crédito Fundo Proteção' },
    { value: 6, label: 'Débito Fundo Proteção' },
    { value: 7, label: 'Ajuste Administrativo' },
    { value: 8, label: 'Transferência Interna' },
    { value: 9, label: 'Entrada Manual' }
  ];

  ledgerAccounts: { value: number; label: string }[] = [
    { value: 0, label: 'Caixa Plataforma' },
    { value: 1, label: 'Carteira Investidor' },
    { value: 2, label: 'Fundo Proteção' },
    { value: 3, label: 'Receita Plataforma' },
    { value: 4, label: 'Conta Tomador' },
    { value: 5, label: 'Gateway Externo' },
    { value: 6, label: 'Ajuste Administrativo' }
  ];

  constructor(
    private ledgerService: AdminLedgerService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading = true;
    this.ledgerService.getHistory(
      this.pageIndex + 1,
      this.pageSize,
      this.periodoFiltro,
      undefined,
      undefined,
      this.tipoMovimentacaoFiltro !== null ? this.tipoMovimentacaoFiltro : undefined,
      this.contaFiltro !== null ? this.contaFiltro : undefined
    ).subscribe({
      next: (res: any) => {
        this.transactions = res.items;
        this.totalCount = res.totalCount;
        this.loading = false;
      },
      error: (err: any) => {
        console.error('Erro ao carregar ledger:', err);
        this.snackBar.open('Erro ao carregar o histórico financeiro.', 'Fechar', { duration: 3000 });
        this.loading = false;
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadHistory();
  }

  applyFilters(): void {
    this.pageIndex = 0;
    this.loadHistory();
  }

  getAccountLabel(value: number): string {
    const acc = this.ledgerAccounts.find(a => a.value === value);
    return acc ? acc.label : 'Desconhecido';
  }

  getTypeLabel(value: number): string {
    const type = this.ledgerTransactionTypes.find(t => t.value === value);
    return type ? type.label : 'Desconhecido';
  }
}
