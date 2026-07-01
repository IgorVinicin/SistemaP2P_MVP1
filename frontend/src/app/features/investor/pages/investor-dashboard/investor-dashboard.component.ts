import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { InvestorService } from '../../../../core/services/investor.service';
import { AuthService } from '../../../../core/services/auth.service';
import { InvestorWalletDto, InvestorTransactionDto } from '../../../../core/models/investor.models';

@Component({
  selector: 'app-investor-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule
  ],
  templateUrl: './investor-dashboard.component.html',
  styleUrls: ['./investor-dashboard.component.css']
})
export class InvestorDashboardComponent implements OnInit {
  private investorService = inject(InvestorService);
  private snackBar = inject(MatSnackBar);
  public authService = inject(AuthService);  public wallet: InvestorWalletDto | null = null;
  public transactions: InvestorTransactionDto[] = [];
  public displayedColumns: string[] = ['data', 'tipo', 'descricao', 'valor', 'saldoAnterior', 'saldoPosterior', 'responsavel'];
  
  public isLoadingWallet = false;
  public isLoadingHistory = false;
  public isProcessing = false;

  public aporteValor: number | null = null;
  public saqueValor: number | null = null;

  public get saldoTotal(): number {
    if (!this.wallet) return 0;
    return (this.wallet.saldoDisponivel ?? 0) + (this.wallet.saldoComprometido ?? 0) + (this.wallet.saldoInvestido ?? 0);
  }

  ngOnInit(): void {
    this.loadWallet();
    this.loadHistory();
  }

  public loadWallet(): void {
    this.isLoadingWallet = true;
    this.investorService.getWallet().subscribe({
      next: (data) => {
        this.wallet = data;
        this.isLoadingWallet = false;
      },
      error: () => {
        this.isLoadingWallet = false;
        this.snackBar.open('Erro ao carregar saldo da carteira.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadHistory(): void {
    this.isLoadingHistory = true;
    this.investorService.getTransactions().subscribe({
      next: (data) => {
        this.transactions = data;
        this.isLoadingHistory = false;
      },
      error: () => {
        this.isLoadingHistory = false;
        this.snackBar.open('Erro ao carregar histórico de transações.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public makeDeposit(): void {
    if (!this.aporteValor || this.aporteValor <= 0) {
      this.snackBar.open('Por favor, informe um valor de aporte maior que zero.', 'Fechar', { duration: 4000 });
      return;
    }

    this.isProcessing = true;
    this.investorService.makeDeposit(this.aporteValor).subscribe({
      next: () => {
        this.isProcessing = false;
        this.snackBar.open('Aporte simulado realizado com sucesso!', 'Fechar', { duration: 3000 });
        this.aporteValor = null;
        this.loadWallet();
        this.loadHistory();
      },
      error: (err) => {
        this.isProcessing = false;
        this.snackBar.open(err.error?.message || 'Erro ao realizar aporte.', 'Fechar', { duration: 4500 });
      }
    });
  }

  public makeWithdraw(): void {
    if (!this.saqueValor || this.saqueValor <= 0) {
      this.snackBar.open('Por favor, informe um valor de saque maior que zero.', 'Fechar', { duration: 4000 });
      return;
    }

    this.isProcessing = true;
    this.investorService.makeWithdraw(this.saqueValor).subscribe({
      next: () => {
        this.isProcessing = false;
        this.snackBar.open('Saque simulado realizado com sucesso!', 'Fechar', { duration: 3000 });
        this.saqueValor = null;
        this.loadWallet();
        this.loadHistory();
      },
      error: (err) => {
        this.isProcessing = false;
        this.snackBar.open(err.error?.message || 'Erro ao realizar saque.', 'Fechar', { duration: 4500 });
      }
    });
  }
}
