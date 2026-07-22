import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { AdminRevenueService, RevenueDashboardMetricsDto, PlatformRevenueDto } from '../../../../core/services/admin-revenue.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-admin-revenue',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule
  ],
  templateUrl: './admin-revenue.component.html',
  styleUrls: ['./admin-revenue.component.css']
})
export class AdminRevenueComponent implements OnInit {
  private revenueService = inject(AdminRevenueService);
  private snackBar = inject(MatSnackBar);

  public metrics: RevenueDashboardMetricsDto | null = null;
  public revenues: PlatformRevenueDto[] = [];
  
  public page = 1;
  public pageSize = 20;
  public total = 0;
  
  public period: string = '';
  public startDate: Date | null = null;
  public endDate: Date | null = null;
  public isCustomPeriod = false;

  public isLoadingMetrics = false;
  public isLoadingRevenues = false;
  public isProcessingYield = false;

  public displayedColumns: string[] = ['dataCriacao', 'tipoReceita', 'valor', 'observacao', 'createdBy'];

  ngOnInit(): void {
    this.loadMetrics();
    this.loadRevenues();
  }

  loadMetrics(): void {
    this.isLoadingMetrics = true;
    this.revenueService.getDashboardMetrics().subscribe({
      next: (data) => {
        this.metrics = data;
        this.isLoadingMetrics = false;
      },
      error: () => {
        this.isLoadingMetrics = false;
        this.snackBar.open('Erro ao carregar métricas de receita.', 'Fechar', { duration: 4000 });
      }
    });
  }

  loadRevenues(): void {
    this.isLoadingRevenues = true;
    const startStr = this.startDate ? this.startDate.toISOString() : undefined;
    const endStr = this.endDate ? this.endDate.toISOString() : undefined;
    const currentPeriod = this.isCustomPeriod ? undefined : this.period;

    this.revenueService.getRevenues(this.page, this.pageSize, currentPeriod, startStr, endStr).subscribe({
      next: (data) => {
        this.revenues = data.items;
        this.total = data.totalCount;
        this.isLoadingRevenues = false;
      },
      error: () => {
        this.isLoadingRevenues = false;
        this.snackBar.open('Erro ao carregar lista de receitas.', 'Fechar', { duration: 4000 });
      }
    });
  }

  onPageChange(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadRevenues();
  }

  processYield(): void {
    if (confirm('Deseja realmente processar os rendimentos do pool de liquidez? Esta ação calculará os rendimentos de todos os investidores ativos e distribuirá o saldo.')) {
      this.isProcessingYield = true;
      this.revenueService.processYield().subscribe({
        next: (response) => {
          this.isProcessingYield = false;
          this.snackBar.open(`Sucesso: ${response.investidoresProcessados} investidores processados. Total distribuído: R$ ${response.totalRendimentoDistribuido}`, 'Fechar', { duration: 6000 });
          this.loadMetrics();
          this.loadRevenues();
        },
        error: (err) => {
          this.isProcessingYield = false;
          this.snackBar.open(`Erro: ${err.error?.message || 'Falha ao processar rendimentos.'}`, 'Fechar', { duration: 6000 });
        }
      });
    }
  }

  onPeriodChange(): void {
    this.isCustomPeriod = this.period === 'personalizado';
    if (!this.isCustomPeriod) {
      this.startDate = null;
      this.endDate = null;
      this.page = 1;
      this.loadRevenues();
    }
  }

  applyCustomFilter(): void {
    if (this.startDate && this.endDate) {
      this.page = 1;
      this.loadRevenues();
    } else {
      this.snackBar.open('Selecione a data de início e fim.', 'Fechar', { duration: 3000 });
    }
  }

  getRevenueTypeName(type: number): string {
    const types = [
      'Juros Empréstimo',
      'Multa e Atraso',
      'Tarifa Operacional (Spread)',
      'Receita Administrativa',
      'Receita de Saque',
      'Receita Manual',
      'Receita Diversa'
    ];
    return types[type] || 'Desconhecido';
  }
}
