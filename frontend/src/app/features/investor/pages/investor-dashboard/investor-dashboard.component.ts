import { Component, OnInit, inject, TemplateRef, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { InvestorService } from '../../../../core/services/investor.service';
import { AuthService } from '../../../../core/services/auth.service';
import { InvestmentProductService } from '../../../../core/services/investment-product.service';
import { InvestorWalletDto } from '../../../../core/models/investor.models';
import { InvestmentProduct } from '../../../../core/models/investment-product.models';

@Component({
  selector: 'app-investor-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule,
    MatDialogModule,
    MatSelectModule,
    MatChipsModule
  ],
  templateUrl: './investor-dashboard.component.html',
  styleUrls: ['./investor-dashboard.component.css']
})
export class InvestorDashboardComponent implements OnInit {
  private investorService = inject(InvestorService);
  private investmentProductService = inject(InvestmentProductService);
  private snackBar = inject(MatSnackBar);
  public authService = inject(AuthService);
  private dialog = inject(MatDialog);

  public wallet: InvestorWalletDto | null = null;
  public products: InvestmentProduct[] = [];
  public rendimentoDiarioEstimado: number = 0;
  public totalRendimentosPagos: number = 0;

  public tipoChavePix: string = 'CPF';
  public chavePix: string = '';

  @ViewChild('saqueDialog') saqueDialogTpl!: TemplateRef<any>;
  @ViewChild('depositoDialog') depositoDialogTpl!: TemplateRef<any>;
  @ViewChild('productDetailDialog') productDetailDialogTpl!: TemplateRef<any>;
  @ViewChild('productsScrollContainer') productsScrollContainer?: ElementRef<HTMLDivElement>;
  public dialogRef?: MatDialogRef<any>;
  
  public isLoadingWallet = false;
  public isLoadingProducts = false;
  public isProcessing = false;

  public scrollProducts(direction: 'left' | 'right'): void {
    if (this.productsScrollContainer?.nativeElement) {
      const scrollAmount = direction === 'left' ? -260 : 260;
      this.productsScrollContainer.nativeElement.scrollBy({ left: scrollAmount, behavior: 'smooth' });
    }
  }

  public aporteValor: number | null = null;
  public selectedProduct: InvestmentProduct | null = null;
  public selectedDetailProduct: InvestmentProduct | null = null;
  public saqueValor: number | null = null;

  public get saldoTotal(): number {
    if (!this.wallet) return 0;
    return (this.wallet.saldoDisponivel ?? 0) + (this.wallet.saldoComprometido ?? 0) + (this.wallet.saldoInvestido ?? 0);
  }

  ngOnInit(): void {
    this.loadWallet();
    this.loadProducts();
  }

  public loadWallet(): void {
    this.isLoadingWallet = true;
    this.investorService.getWallet().subscribe({
      next: (data) => {
        this.wallet = data;
        this.isLoadingWallet = false;
        
        // Carrega estimativa diária de rendimentos
        this.investorService.getYieldPreview().subscribe({
          next: (previewData) => {
            this.rendimentoDiarioEstimado = previewData.rendimentoDiarioEstimado;
            this.totalRendimentosPagos = previewData.totalRendimentosPagos ?? 0;
          }
        });
      },
      error: () => {
        this.isLoadingWallet = false;
        this.snackBar.open('Erro ao carregar dados da carteira do investidor.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadProducts(): void {
    this.isLoadingProducts = true;
    this.investmentProductService.getActiveProducts().subscribe({
      next: (data) => {
        this.products = data || [];
        this.isLoadingProducts = false;
      },
      error: () => {
        this.isLoadingProducts = false;
        this.snackBar.open('Erro ao carregar opções de investimento.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public openProductDetail(product: InvestmentProduct): void {
    this.selectedDetailProduct = product;
    this.dialogRef = this.dialog.open(this.productDetailDialogTpl, {
      width: '520px',
      disableClose: false
    });
  }

  public openCurrentProductDetail(): void {
    if (!this.wallet) return;
    let prod = this.products.find(p => p.id === this.wallet?.produtoInvestimentoId || p.nome === this.wallet?.produtoInvestimentoNome);
    if (!prod) {
      prod = {
        id: this.wallet.produtoInvestimentoId || 'ativo',
        nome: this.wallet.produtoInvestimentoNome || 'Modalidade Contratada',
        descricao: 'Modalidade de investimento ativa e vinculada à sua carteira, definindo sua taxa de rentabilidade mensal sobre o saldo comprometido.',
        rentabilidadeMensal: this.wallet.rentabilidadeSaldoComprometido || this.wallet.rentabilidade || 0,
        valorMinimo: 0,
        condicoes: 'Rendimentos creditados conforme operações ativas.',
        risco: 'Médio',
        prazoMinimo: '30 dias',
        ativo: true,
        createdAt: new Date().toISOString()
      } as InvestmentProduct;
    }
    this.openProductDetail(prod);
  }

  public investFromDetail(product: InvestmentProduct): void {
    this.dialogRef?.close();
    this.confirmProductChoice(product);
  }

  public confirmProductChoice(product: InvestmentProduct): void {
    if (!product) return;
    this.isProcessing = true;
    this.investorService.selectProduct(product.id).subscribe({
      next: (res) => {
        this.isProcessing = false;
        this.dialogRef?.close();
        this.snackBar.open(res.message || 'Opção de investimento confirmada e rentabilidade definida!', 'OK', { duration: 5000 });
        this.loadWallet();
      },
      error: (err) => {
        this.isProcessing = false;
        const msg = err.error?.message || 'Erro ao definir opção de investimento.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }

  public openDepositDialog(product?: InvestmentProduct): void {
    if (product) {
      this.selectedProduct = product;
      this.aporteValor = product.valorMinimo;
    } else {
      this.selectedProduct = null;
      this.aporteValor = null;
    }

    this.dialogRef = this.dialog.open(this.depositoDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  public setAportePreset(val: number): void {
    this.aporteValor = (this.aporteValor ?? 0) + val;
  }

  public selectProductForInvestment(product: InvestmentProduct): void {
    this.openDepositDialog(product);
  }

  public makeDeposit(): void {
    if (!this.aporteValor || this.aporteValor <= 0) {
      this.snackBar.open('Por favor, informe um valor de aporte maior que zero.', 'Fechar', { duration: 4000 });
      return;
    }

    if (this.selectedProduct && this.aporteValor < this.selectedProduct.valorMinimo) {
      this.snackBar.open(`O valor mínimo para o produto selecionado é de R$ ${this.selectedProduct.valorMinimo.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}.`, 'Fechar', { duration: 4500 });
      return;
    }

    this.isProcessing = true;
    this.investorService.makeDeposit(this.aporteValor).subscribe({
      next: () => {
        this.isProcessing = false;
        this.snackBar.open('Depósito confirmado com sucesso! Saldo adicionado à sua carteira.', 'Fechar', { duration: 3500 });
        this.aporteValor = null;
        this.selectedProduct = null;
        this.dialogRef?.close();
        this.loadWallet();
      },
      error: (err) => {
        this.isProcessing = false;
        this.snackBar.open(err.error?.message || 'Erro ao realizar depósito.', 'Fechar', { duration: 4500 });
      }
    });
  }

  public openWithdrawDialog(): void {
    this.saqueValor = null;
    this.chavePix = '';
    this.dialogRef = this.dialog.open(this.saqueDialogTpl, {
      width: '450px',
      disableClose: true
    });
  }

  public makeWithdraw(): void {
    if (!this.saqueValor || this.saqueValor <= 0) {
      this.snackBar.open('Por favor, informe um valor de saque maior que zero.', 'Fechar', { duration: 4000 });
      return;
    }

    if (this.wallet && this.saqueValor > this.wallet.saldoDisponivel) {
      this.snackBar.open('Saldo disponível insuficiente para o valor solicitado.', 'Fechar', { duration: 4000 });
      return;
    }

    if (!this.chavePix || this.chavePix.trim() === '') {
      this.snackBar.open('Por favor, informe a Chave PIX.', 'Fechar', { duration: 4000 });
      return;
    }

    this.isProcessing = true;
    this.investorService.makeWithdraw(this.saqueValor, this.chavePix, this.tipoChavePix).subscribe({
      next: (res: any) => {
        this.isProcessing = false;
        this.snackBar.open(res.message || 'Solicitação de saque enviada com sucesso e aguardando aprovação!', 'Fechar', { duration: 3500 });
        this.saqueValor = null;
        this.dialogRef?.close();
        this.loadWallet();
      },
      error: (err) => {
        this.isProcessing = false;
        this.snackBar.open(err.error?.message || 'Erro ao solicitar saque.', 'Fechar', { duration: 4500 });
      }
    });
  }
}
