import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDividerModule } from '@angular/material/divider';
import { MatCheckboxModule } from '@angular/material/checkbox';

import { LoanInstallmentDto } from '../../../../core/models/installment.models';
import { LoanService } from '../../../../core/services/loan.service';

export interface PayInstallmentDialogData {
  installment: LoanInstallmentDto;
}

export interface PayInstallmentDialogResult {
  valorPago: number;
  isLote: boolean;
  loteItems?: { installmentId: string, valorPago: number }[];
}

@Component({
  selector: 'app-pay-installment-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatDividerModule,
    MatCheckboxModule
  ],
  templateUrl: './pay-installment-dialog.component.html',
  styleUrls: ['./pay-installment-dialog.component.css']
})
export class PayInstallmentDialogComponent implements OnInit {
  private api = inject(LoanService);
  public dialogRef = inject(MatDialogRef<PayInstallmentDialogComponent>);
  
  public valorDigitado: number = 0;
  
  // Variáveis para pagamento em lote
  public otherPending: any[] = [];
  public isLoadingPending = false;
  public selectedAdditional: { [key: string]: boolean } = {};

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: PayInstallmentDialogData
  ) {
    // Pré-preenche com o total devido
    this.valorDigitado = this.totalDevido;
  }

  ngOnInit(): void {
    this.loadOtherPending();
  }

  public loadOtherPending(): void {
    this.isLoadingPending = true;
    this.api.getPendingUserInstallments().subscribe({
      next: (data) => {
        const mainDate = new Date(this.data.installment.dataVencimento);
        const mainYear = mainDate.getFullYear();
        const mainMonth = mainDate.getMonth();
        const mainDay = mainDate.getDate();

        // Filtrar para trazer apenas parcelas de mesma data que não sejam a principal atual
        this.otherPending = data.filter(i => {
          const itemDate = new Date(i.dataVencimento);
          return i.id !== this.data.installment.id &&
                 itemDate.getFullYear() === mainYear &&
                 itemDate.getMonth() === mainMonth &&
                 itemDate.getDate() === mainDay;
        });
        
        // Auto-selecionar as parcelas do mesmo dia por padrão para facilitar o PIX unificado
        this.otherPending.forEach(i => {
          this.selectedAdditional[i.id] = true;
        });

        this.isLoadingPending = false;
      },
      error: () => {
        this.isLoadingPending = false;
      }
    });
  }

  /** Total que o tomador ainda deve nesta parcela (parcela + multa + juros mora - já pago) */
  get totalDevido(): number {
    const inst = this.data.installment;
    const bruto = inst.valorParcela + (inst.multa ?? 0) + (inst.jurosMora ?? 0);
    const jaPago = inst.valorPago ?? 0;
    return Math.max(0, +(bruto - jaPago).toFixed(2));
  }

  public setValor(valor: number): void {
    this.valorDigitado = +valor.toFixed(2);
  }

  public toggleAllAdditional(select: boolean): void {
    this.otherPending.forEach(i => {
      this.selectedAdditional[i.id] = select;
    });
  }

  // Retorna a soma das parcelas adicionais selecionadas
  get totalAdicionais(): number {
    return this.otherPending
      .filter(i => this.selectedAdditional[i.id])
      .reduce((sum, i) => sum + i.totalDevido, 0);
  }

  // Retorna o valor unificado a pagar (principal + adicionais)
  get totalGeral(): number {
    return +(this.valorDigitado + this.totalAdicionais).toFixed(2);
  }

  public isValid(): boolean {
    return this.valorDigitado > 0 && this.valorDigitado <= this.totalDevido;
  }

  public confirm(): void {
    if (!this.isValid()) return;

    // Verificar se há outras parcelas selecionadas
    const loteItems = [];
    
    // Adicionar a parcela principal
    loteItems.push({
      installmentId: this.data.installment.id!,
      valorPago: this.valorDigitado
    });

    // Adicionar as parcelas adicionais marcadas
    this.otherPending.forEach(i => {
      if (this.selectedAdditional[i.id]) {
        loteItems.push({
          installmentId: i.id,
          valorPago: i.totalDevido
        });
      }
    });

    const result: PayInstallmentDialogResult = {
      valorPago: this.valorGeralParaExibicao(),
      isLote: loteItems.length > 1,
      loteItems: loteItems
    };

    this.dialogRef.close(result);
  }

  private valorGeralParaExibicao(): number {
    return this.totalGeral;
  }
}
