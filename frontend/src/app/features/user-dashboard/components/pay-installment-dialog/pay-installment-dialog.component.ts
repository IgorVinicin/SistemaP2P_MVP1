import { Component, Inject, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDividerModule } from '@angular/material/divider';

import { LoanInstallmentDto } from '../../../../core/models/installment.models';

export interface PayInstallmentDialogData {
  installment: LoanInstallmentDto;
}

export interface PayInstallmentDialogResult {
  valorPago: number;
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
    MatDividerModule
  ],
  templateUrl: './pay-installment-dialog.component.html',
  styleUrls: ['./pay-installment-dialog.component.css']
})
export class PayInstallmentDialogComponent {
  public dialogRef = inject(MatDialogRef<PayInstallmentDialogComponent>);
  
  public valorDigitado: number = 0;
  
  constructor(
    @Inject(MAT_DIALOG_DATA) public data: PayInstallmentDialogData
  ) {
    // Pré-preenche com o total devido
    this.valorDigitado = this.totalDevido;
  }

  get totalDevido(): number {
    const inst = this.data.installment;
    const bruto = inst.valorParcela + (inst.multa ?? 0) + (inst.jurosMora ?? 0);
    const jaPago = inst.valorPago ?? 0;
    return Math.max(0, +(bruto - jaPago).toFixed(2));
  }

  public setValor(valor: number): void {
    this.valorDigitado = +valor.toFixed(2);
  }

  public isValid(): boolean {
    return Number.isFinite(this.valorDigitado) && this.valorDigitado > 0 && this.valorDigitado <= this.totalDevido;
  }

  public confirm(): void {
    if (!this.isValid()) return;

    this.dialogRef.close({ valorPago: this.valorDigitado });
  }

}
