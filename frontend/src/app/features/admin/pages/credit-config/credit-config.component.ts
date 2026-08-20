import { Component, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';

import { AdminCreditService } from '../../../../core/services/admin-credit.service';
import { InvestmentProductService } from '../../../../core/services/investment-product.service';
import { ScoreCreditRule, CreditConfig } from '../../../../core/models/credit-rule.models';
import { InvestmentProduct, CreateInvestmentProductDto } from '../../../../core/models/investment-product.models';

@Component({
  selector: 'app-credit-config',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTableModule,
    MatDialogModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatSelectModule,
    MatTabsModule
  ],
  templateUrl: './credit-config.component.html',
  styleUrls: ['./credit-config.component.css']
})
export class CreditConfigComponent implements OnInit {
  private api = inject(AdminCreditService);
  private investmentProductService = inject(InvestmentProductService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  // Regras de Score
  public rules: ScoreCreditRule[] = [];
  public displayedColumns: string[] = ['faixaScore', 'limite', 'risco', 'status', 'acoes'];
  public isLoading = false;

  // Produtos de Investimento
  public products: InvestmentProduct[] = [];
  public productColumns: string[] = ['nome', 'rentabilidade', 'valorMinimo', 'status', 'acoes'];
  public isLoadingProducts = false;

  public globalConfig: CreditConfig = {
    scoreMinimoParaSolicitacao: 150,
    percentualMultaAtraso: 2.0,
    percentualJurosDiario: 0.033,
    diasParaCobranca: 16,
    diasParaCobrancaIntensiva: 31,
    diasParaInadimplencia: 61,
    valorMinimoInvestimento: 5000.00,
    minimoParcelasPagasNovaSolicitacao: 4,
    maxInvestidoresPorEmprestimo: 10,
    spreadMinimoPlataforma: 1.0,
    taxaRendimentoPool: 2.0
  };

  // Dialog and Form elements for Score Rules
  @ViewChild('ruleDialog') ruleDialogTpl!: TemplateRef<any>;
  private dialogRef?: MatDialogRef<any>;
  
  public editingRule: ScoreCreditRule | null = null;
  public ruleForm = {
    scoreMin: 0,
    scoreMax: 100,
    limiteEmprestimo: 0,
    taxaJuros: 0,
    nivelRisco: 'Alto',
    ativo: true
  };

  // Dialog and Form elements for Investment Products
  @ViewChild('productDialog') productDialogTpl!: TemplateRef<any>;
  private productDialogRef?: MatDialogRef<any>;

  public editingProduct: InvestmentProduct | null = null;
  public productForm: CreateInvestmentProductDto = {
    nome: '',
    rentabilidadeMensal: 2.0,
    valorMinimo: 5000.00,
    descricao: '',
    condicoes: '',
    risco: 'Baixo',
    prazoMinimo: 'Livre',
    ativo: true
  };

  ngOnInit(): void {
    this.loadRules();
    this.loadGlobalConfig();
    this.loadProducts();
  }

  public loadRules(): void {
    this.isLoading = true;
    this.api.getRules().subscribe({
      next: (data) => {
        this.rules = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar regras de crédito.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadProducts(): void {
    this.isLoadingProducts = true;
    this.investmentProductService.getAllProducts().subscribe({
      next: (data) => {
        this.products = data || [];
        this.isLoadingProducts = false;
      },
      error: () => {
        this.isLoadingProducts = false;
        this.snackBar.open('Erro ao carregar produtos de investimento.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public loadGlobalConfig(): void {
    this.api.getConfig().subscribe({
      next: (config) => {
        this.globalConfig = config;
      },
      error: () => {
        this.snackBar.open('Erro ao obter configuração geral de crédito.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public saveGlobalConfig(): void {
    this.api.updateConfig(this.globalConfig).subscribe({
      next: () => {
        this.snackBar.open('Configuração geral atualizada com sucesso!', 'Fechar', { duration: 3000 });
      },
      error: (err) => {
        this.snackBar.open(err.error?.message || 'Erro ao salvar configuração.', 'Fechar', { duration: 4000 });
      }
    });
  }

  // --- MÉTODOS PARA PRODUTOS DE INVESTIMENTO ---

  public openProductDialog(product?: InvestmentProduct): void {
    if (product) {
      this.editingProduct = product;
      this.productForm = {
        nome: product.nome,
        rentabilidadeMensal: product.rentabilidadeMensal,
        valorMinimo: product.valorMinimo,
        descricao: product.descricao,
        condicoes: product.condicoes,
        risco: product.risco,
        prazoMinimo: product.prazoMinimo,
        ativo: product.ativo
      };
    } else {
      this.editingProduct = null;
      this.productForm = {
        nome: '',
        rentabilidadeMensal: 2.0,
        valorMinimo: 5000.00,
        descricao: '',
        condicoes: '',
        risco: 'Baixo',
        prazoMinimo: 'Livre',
        ativo: true
      };
    }

    this.productDialogRef = this.dialog.open(this.productDialogTpl, {
      width: '560px',
      disableClose: true
    });
  }

  public saveProduct(): void {
    if (!this.productForm.nome || this.productForm.nome.trim() === '') {
      this.snackBar.open('O nome do produto é obrigatório.', 'Fechar', { duration: 4000 });
      return;
    }
    if (this.productForm.rentabilidadeMensal < 0) {
      this.snackBar.open('A rentabilidade mensal não pode ser negativa.', 'Fechar', { duration: 4000 });
      return;
    }
    if (this.productForm.valorMinimo <= 0) {
      this.snackBar.open('O valor mínimo deve ser positivo.', 'Fechar', { duration: 4000 });
      return;
    }

    if (this.editingProduct) {
      this.investmentProductService.updateProduct(this.editingProduct.id, this.productForm).subscribe({
        next: () => {
          this.snackBar.open('Produto de investimento atualizado com sucesso!', 'Fechar', { duration: 3000 });
          this.productDialogRef?.close();
          this.loadProducts();
        },
        error: (err) => {
          this.snackBar.open(err.error?.message || 'Erro ao atualizar produto.', 'Fechar', { duration: 4000 });
        }
      });
    } else {
      this.investmentProductService.createProduct(this.productForm).subscribe({
        next: () => {
          this.snackBar.open('Produto de investimento criado com sucesso!', 'Fechar', { duration: 3000 });
          this.productDialogRef?.close();
          this.loadProducts();
        },
        error: (err) => {
          this.snackBar.open(err.error?.message || 'Erro ao criar produto.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  public deleteProduct(id: string): void {
    if (confirm('Tem certeza que deseja excluir este produto de investimento?')) {
      this.investmentProductService.deleteProduct(id).subscribe({
        next: () => {
          this.snackBar.open('Produto excluído com sucesso!', 'Fechar', { duration: 3000 });
          this.loadProducts();
        },
        error: () => {
          this.snackBar.open('Erro ao excluir produto de investimento.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  // --- MÉTODOS PARA REGRAS DE SCORE ---

  public openRuleDialog(rule?: ScoreCreditRule): void {
    if (rule) {
      this.editingRule = rule;
      this.ruleForm = {
        scoreMin: rule.scoreMin,
        scoreMax: rule.scoreMax,
        limiteEmprestimo: rule.limiteEmprestimo,
        taxaJuros: rule.taxaJuros,
        nivelRisco: rule.nivelRisco,
        ativo: rule.ativo
      };
    } else {
      this.editingRule = null;
      this.ruleForm = {
        scoreMin: 0,
        scoreMax: 100,
        limiteEmprestimo: 1000,
        taxaJuros: 5.0,
        nivelRisco: 'Alto',
        ativo: true
      };
    }

    this.dialogRef = this.dialog.open(this.ruleDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  public hasOverlap(min: number, max: number, ignoreId?: string): boolean {
    return this.rules.some(r => {
      if (!r.ativo) return false;
      if (ignoreId && r.id === ignoreId) return false;
      return (min <= r.scoreMax && max >= r.scoreMin);
    });
  }

  public saveRule(): void {
    const { scoreMin, scoreMax, limiteEmprestimo, taxaJuros, nivelRisco, ativo } = this.ruleForm;

    if (scoreMin >= scoreMax) {
      this.snackBar.open('Erro: O Score Mínimo deve ser menor que o Score Máximo.', 'Fechar', { duration: 4000 });
      return;
    }

    if (ativo && this.hasOverlap(scoreMin, scoreMax, this.editingRule?.id)) {
      this.snackBar.open('Erro: A faixa de score informada se sobrepõe com uma regra já existente ativa.', 'Fechar', { duration: 5000 });
      return;
    }

    const payload: ScoreCreditRule = {
      scoreMin,
      scoreMax,
      limiteEmprestimo,
      taxaJuros,
      nivelRisco,
      ativo
    };

    if (this.editingRule) {
      payload.id = this.editingRule.id;
      this.api.updateRule(this.editingRule.id!, payload).subscribe({
        next: () => {
          this.snackBar.open('Regra de crédito atualizada com sucesso!', 'Fechar', { duration: 3000 });
          this.dialogRef?.close();
          this.loadRules();
        },
        error: (err) => {
          const msg = err.error?.message || 'Erro ao atualizar regra de crédito.';
          this.snackBar.open(msg, 'Fechar', { duration: 5000 });
        }
      });
    } else {
      this.api.createRule(payload).subscribe({
        next: () => {
          this.snackBar.open('Regra de crédito criada com sucesso!', 'Fechar', { duration: 3000 });
          this.dialogRef?.close();
          this.loadRules();
        },
        error: (err) => {
          const msg = err.error?.message || 'Erro ao criar regra de crédito.';
          this.snackBar.open(msg, 'Fechar', { duration: 5000 });
        }
      });
    }
  }

  public deleteRule(id: string): void {
    if (confirm('Tem certeza que deseja excluir esta regra de crédito?')) {
      this.api.deleteRule(id).subscribe({
        next: () => {
          this.snackBar.open('Regra de crédito excluída com sucesso!', 'Fechar', { duration: 3000 });
          this.loadRules();
        },
        error: () => {
          this.snackBar.open('Erro ao excluir regra de crédito.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }
}
