import { Component, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AdminDelinquencyService, DelinquentLoanSummary, ProtectionFundSummary, FinancialEventHistoryDto } from '../../../../core/services/admin-delinquency.service';

@Component({
  selector: 'app-admin-delinquency',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDialogModule,
    MatSnackBarModule
  ],
  templateUrl: './admin-delinquency.component.html',
  styleUrls: ['./admin-delinquency.component.css']
})
export class AdminDelinquencyComponent implements OnInit {
  private service = inject(AdminDelinquencyService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public delinquentLoans: DelinquentLoanSummary[] = [];
  public fundSummary: ProtectionFundSummary | null = null;
  public eventHistory: FinancialEventHistoryDto[] = [];

  // Filtros
  public filterStatus = '';
  public filterCliente = '';
  public filterPeriodoInicio = '';
  public filterPeriodoFim = '';

  // Colunas
  public loanColumns = [
    'cliente',
    'valorSolicitado',
    'valorEmAberto',
    'diasAtraso',
    'status',
    'acoes'
  ];

  public historyColumns = [
    'data',
    'usuarioResponsavel',
    'eventoExecutado',
    'valor',
    'observacao'
  ];

  public isLoadingLoans = false;
  public isLoadingFund = false;
  public isLoadingHistory = false;

  public activeLoanId = '';
  public activeLoanClient = '';
  public activeLoanBalance = 0;
  public dialogValue = 0;
  public dialogObservation = '';
  public dialogTitle = '';
  public dialogType: 'partial' = 'partial';

  // Encerramento de Contrato
  public terminateLoanId = '';
  public terminateLoanClient = '';
  public terminateLoanTotalDebt = 0;
  public terminateResolutionType: 'fundo' | 'perda' = 'fundo';
  public terminateObservation = '';

  // Fundo de Proteção Adjust
  public fundAdjustValor = 0;
  public fundAdjustTipo = 'ADICIONAR';
  public fundAdjustMotivo = '';

  @ViewChild('actionDialog') actionDialog!: TemplateRef<any>;
  @ViewChild('terminateDialog') terminateDialog!: TemplateRef<any>;
  @ViewChild('fundDialog') fundDialog!: TemplateRef<any>;
  private activeDialogRef?: MatDialogRef<any>;

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.loadLoans();
    this.loadFund();
    this.loadHistory();
  }

  public loadLoans(): void {
    this.isLoadingLoans = true;
    this.service.getDelinquentLoans(
      this.filterStatus || undefined,
      this.filterCliente || undefined,
      this.filterPeriodoInicio || undefined,
      this.filterPeriodoFim || undefined
    ).subscribe({
      next: (data) => {
        this.delinquentLoans = data;
        this.isLoadingLoans = false;
      },
      error: (err) => {
        this.isLoadingLoans = false;
        this.snackBar.open(err.error?.message || 'Erro ao carregar inadimplentes.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadFund(): void {
    this.isLoadingFund = true;
    this.service.getProtectionFund().subscribe({
      next: (data) => {
        this.fundSummary = data;
        this.isLoadingFund = false;
      },
      error: (err) => {
        this.isLoadingFund = false;
        this.snackBar.open(err.error?.message || 'Erro ao carregar fundo de proteção.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadHistory(): void {
    this.isLoadingHistory = true;
    this.service.getHistory().subscribe({
      next: (data) => {
        this.eventHistory = data;
        this.isLoadingHistory = false;
      },
      error: (err) => {
        this.isLoadingHistory = false;
        this.snackBar.open(err.error?.message || 'Erro ao carregar histórico.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public handleCoverOverdue(loan: DelinquentLoanSummary): void {
    if (confirm(`Deseja realmente aplicar COBERTURA DE PARCELAS VENCIDAS para o cliente ${loan.clienteNome} no valor de ${loan.valorEmAberto.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}?`)) {
      const obs = prompt('Observação da Cobertura de Parcelas Vencidas:', 'Cobertura de parcelas vencidas efetuada pelo Administrador.');
      if (obs === null) return; // Cancelado

      this.service.coverOverdue(loan.loanId, obs).subscribe({
        next: () => {
          this.snackBar.open('Cobertura de parcelas vencidas realizada com sucesso!', 'Fechar', { duration: 3000 });
          this.loadData();
        },
        error: (err) => {
          this.snackBar.open(err.error?.message || 'Erro ao realizar cobertura.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  public openPartialCoverDialog(loan: DelinquentLoanSummary): void {
    this.activeLoanId = loan.loanId;
    this.activeLoanClient = loan.clienteNome;
    this.activeLoanBalance = loan.valorEmAberto;
    this.dialogValue = loan.valorEmAberto;
    this.dialogObservation = 'Cobertura parcial efetuada pelo Administrador.';
    this.dialogTitle = 'Acionar Cobertura Parcial';
    this.dialogType = 'partial';

    this.activeDialogRef = this.dialog.open(this.actionDialog, {
      width: '450px',
      disableClose: true
    });
  }

  public openTerminateContractDialog(loan: DelinquentLoanSummary): void {
    this.terminateLoanId = loan.loanId;
    this.terminateLoanClient = loan.clienteNome;
    this.terminateLoanTotalDebt = loan.saldoDevedorTotal || loan.valorEmAberto;
    this.terminateResolutionType = 'fundo';
    this.terminateObservation = 'Encerramento contratual definitivo devido a inadimplência.';

    this.activeDialogRef = this.dialog.open(this.terminateDialog, {
      width: '450px',
      disableClose: true
    });
  }

  public submitAction(): void {
    if (this.dialogValue <= 0) {
      this.snackBar.open('O valor deve ser maior que zero.', 'Fechar', { duration: 3000 });
      return;
    }

    if (this.dialogType === 'partial') {
      if (this.dialogValue > this.activeLoanBalance) {
        this.snackBar.open('O valor não pode exceder o saldo devedor.', 'Fechar', { duration: 3000 });
        return;
      }

      this.service.coverOverduePartial(this.activeLoanId, this.dialogValue, this.dialogObservation).subscribe({
        next: () => {
          this.snackBar.open('Cobertura parcial efetuada com sucesso!', 'Fechar', { duration: 3000 });
          this.activeDialogRef?.close();
          this.loadData();
        },
        error: (err) => {
          this.snackBar.open(err.error?.message || 'Erro ao realizar cobertura parcial.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  public submitTerminateContract(): void {
    if (this.terminateResolutionType === 'fundo') {
      this.service.terminateAndCover(this.terminateLoanId, this.terminateObservation).subscribe({
        next: () => {
          this.snackBar.open('Contrato encerrado e saldo devedor coberto com sucesso pelo Fundo!', 'Fechar', { duration: 3000 });
          this.activeDialogRef?.close();
          this.loadData();
        },
        error: (err) => {
          this.snackBar.open(err.error?.message || 'Erro ao encerrar contrato com cobertura do Fundo.', 'Fechar', { duration: 4000 });
        }
      });
    } else {
      this.service.terminateAndLoss(this.terminateLoanId, this.terminateObservation).subscribe({
        next: () => {
          this.snackBar.open('Contrato encerrado e prejuízo distribuído como perda definitiva aos investidores!', 'Fechar', { duration: 3000 });
          this.activeDialogRef?.close();
          this.loadData();
        },
        error: (err) => {
          this.snackBar.open(err.error?.message || 'Erro ao encerrar contrato com distribuição de perda.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  public cancelDialog(): void {
    this.activeDialogRef?.close();
  }

  public getEventLabel(event: string): string {
    const map: { [key: string]: string } = {
      MUDANCA_STATUS: 'Mudança de Status',
      ENTRADA_COBRANCA: 'Entrada em Cobrança',
      ENTRADA_INADIMPLENCIA: 'Entrada em Inadimplência',
      UTILIZACAO_FUNDO: 'Utilização do Fundo',
      PERDA_INVESTIDOR: 'Prejuízo de Investidor',
      INTERVENCAO_ADMIN: 'Intervenção Admin',
      PAGAMENTO_PARCELA: 'Pagamento de Parcela'
    };
    return map[event] || event;
  }

  public openFundAdjustmentDialog(): void {
    this.fundAdjustValor = 0;
    this.fundAdjustTipo = 'ADICIONAR';
    this.fundAdjustMotivo = 'Ajuste manual efetuado pelo Administrador.';
    
    this.activeDialogRef = this.dialog.open(this.fundDialog, {
      width: '450px',
      disableClose: true
    });
  }

  public submitFundAdjustment(): void {
    if (this.fundAdjustValor <= 0) {
      this.snackBar.open('O valor deve ser maior que zero.', 'Fechar', { duration: 3000 });
      return;
    }

    this.service.adjustProtectionFund(this.fundAdjustValor, this.fundAdjustTipo, this.fundAdjustMotivo).subscribe({
      next: () => {
        this.snackBar.open('Fundo de Proteção ajustado com sucesso!', 'Fechar', { duration: 3000 });
        this.activeDialogRef?.close();
        this.loadData();
      },
      error: (err) => {
        this.snackBar.open(err.error?.message || 'Erro ao ajustar fundo de proteção.', 'Fechar', { duration: 4000 });
      }
    });
  }
}
