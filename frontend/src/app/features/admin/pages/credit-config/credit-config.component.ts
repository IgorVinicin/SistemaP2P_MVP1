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

import { AdminCreditService } from '../../../../core/services/admin-credit.service';
import { ScoreCreditRule, CreditConfig } from '../../../../core/models/credit-rule.models';

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
    MatSelectModule
  ],
  templateUrl: './credit-config.component.html',
  styleUrls: ['./credit-config.component.css']
})
export class CreditConfigComponent implements OnInit {
  private api = inject(AdminCreditService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public rules: ScoreCreditRule[] = [];
  public displayedColumns: string[] = ['faixaScore', 'limite', 'juros', 'risco', 'status', 'acoes'];
  public isLoading = false;

  public globalConfig: CreditConfig = {
    scoreMinimoParaSolicitacao: 150
  };

  // Dialog and Form elements
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

  ngOnInit(): void {
    this.loadRules();
    this.loadGlobalConfig();
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
    if (this.globalConfig.scoreMinimoParaSolicitacao < 0 || this.globalConfig.scoreMinimoParaSolicitacao > 1000) {
      this.snackBar.open('O score mínimo deve estar entre 0 e 1000.', 'Fechar', { duration: 4000 });
      return;
    }

    this.api.updateConfig(this.globalConfig).subscribe({
      next: () => {
        this.snackBar.open('Configuração de score mínimo salva com sucesso!', 'Fechar', { duration: 3000 });
      },
      error: (err) => {
        const msg = err.error?.message || 'Erro ao salvar configuração de score mínimo.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }

  public openRuleDialog(rule: ScoreCreditRule | null = null): void {
    this.editingRule = rule;

    if (rule) {
      this.ruleForm = {
        scoreMin: rule.scoreMin,
        scoreMax: rule.scoreMax,
        limiteEmprestimo: rule.limiteEmprestimo,
        taxaJuros: rule.taxaJuros,
        nivelRisco: rule.nivelRisco,
        ativo: rule.ativo
      };
    } else {
      this.ruleForm = {
        scoreMin: 0,
        scoreMax: 100,
        limiteEmprestimo: 500,
        taxaJuros: 8.0,
        nivelRisco: 'Alto',
        ativo: true
      };
    }

    this.dialogRef = this.dialog.open(this.ruleDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  // Validação de sobreposição local
  private hasOverlap(min: number, max: number, excludeId?: string): boolean {
    return this.rules.some(r => {
      if (!r.ativo || r.id === excludeId) return false;
      return min <= r.scoreMax && max >= r.scoreMin;
    });
  }

  public saveRule(): void {
    const { scoreMin, scoreMax, limiteEmprestimo, taxaJuros, nivelRisco, ativo } = this.ruleForm;

    if (scoreMin < 0 || scoreMax > 1000 || scoreMin > scoreMax) {
      this.snackBar.open('Faixa de score inválida.', 'Fechar', { duration: 4000 });
      return;
    }

    // Verificar sobreposição localmente apenas para dar feedback imediato
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
