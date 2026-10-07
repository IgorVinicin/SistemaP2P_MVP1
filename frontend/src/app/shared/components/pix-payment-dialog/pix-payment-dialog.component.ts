import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Clipboard, ClipboardModule } from '@angular/cdk/clipboard';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, exhaustMap, catchError, timer } from 'rxjs';
import { PixPaymentDialogData } from '../../../core/models/pix-payment.models';

@Component({
  selector: 'app-pix-payment-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, ClipboardModule],
  template: `
    <h2 mat-dialog-title>Pagamento PIX</h2>
    <mat-dialog-content>
      <p><strong>{{ payment.valor | currency:'BRL' }}</strong></p>
      <p role="status" aria-live="polite">{{ statusLabel }}</p>
      <ng-container *ngIf="!confirmed && !closed">
        <img *ngIf="payment.qrCodeBase64" [src]="qrImage" alt="QR Code para pagar via PIX" width="240" height="240" style="display:block;margin:0 auto;max-width:100%;object-fit:contain">
        <label *ngIf="payment.copiaECola" for="pix-code">PIX copia e cola</label>
        <textarea *ngIf="payment.copiaECola" id="pix-code" readonly [value]="payment.copiaECola" rows="3" style="width:100%;box-sizing:border-box;overflow-wrap:anywhere"></textarea>
        <button mat-stroked-button *ngIf="payment.copiaECola" (click)="copy()">Copiar código PIX</button>
        <p *ngIf="payment.expirationDate">Validade: {{ payment.expirationDate | date:'dd/MM/yyyy HH:mm' }}</p>
        <p>O saldo ou a parcela será atualizado após a confirmação do pagamento. Fechar esta janela não cancela a cobrança.</p>
        <p *ngIf="statusError" role="alert">{{ statusError }}</p>
      </ng-container>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close(confirmed)">Fechar</button>
      <button mat-flat-button *ngIf="!confirmed" (click)="refresh()" [disabled]="checking">Consultar pagamento</button>
    </mat-dialog-actions>
  `
})
export class PixPaymentDialogComponent implements OnInit {
  readonly data = inject<PixPaymentDialogData>(MAT_DIALOG_DATA);
  readonly dialogRef = inject(MatDialogRef<PixPaymentDialogComponent>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly clipboard = inject(Clipboard);
  private readonly snackBar = inject(MatSnackBar);
  payment = this.data.payment;
  checking = false;
  statusError = '';

  get confirmed(): boolean { return this.payment.paid === true; }
  get closed(): boolean { return ['REFUNDED', 'CANCELLED', 'CANCELED', 'EXPIRED', 'DELETED'].includes(this.payment.status?.toUpperCase()); }
  get qrImage(): string { return this.payment.qrCodeBase64?.startsWith('data:') ? this.payment.qrCodeBase64 : `data:image/png;base64,${this.payment.qrCodeBase64}`; }
  get statusLabel(): string {
    if (this.confirmed) return 'Pagamento confirmado.';
    if (this.closed) return `Cobrança encerrada (${this.payment.status}).`;
    return 'Aguardando confirmação do pagamento.';
  }
  ngOnInit(): void {
    timer(5000, 10000).pipe(
      exhaustMap(() => this.confirmed || this.checking ? EMPTY : this.data.loadStatus().pipe(catchError(() => EMPTY))),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(payment => { this.payment = { ...this.payment, ...payment }; this.statusError = ''; });
  }
  refresh(): void {
    if (this.checking) return;
    this.checking = true;
    this.data.loadStatus().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: payment => { this.payment = { ...this.payment, ...payment }; this.checking = false; this.statusError = ''; },
      error: () => { this.checking = false; this.statusError = 'Não foi possível consultar agora. Tente novamente.'; }
    });
  }
  copy(): void {
    const copied = this.clipboard.copy(this.payment.copiaECola ?? '');
    this.snackBar.open(copied ? 'Código PIX copiado.' : 'Selecione e copie o código PIX.', 'Fechar', { duration: 3000 });
  }
}
