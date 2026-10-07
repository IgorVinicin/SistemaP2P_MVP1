import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { of } from 'rxjs';
import { PixPaymentDialogComponent } from './pix-payment-dialog.component';

describe('PIX settlement display', () => {
  function create(status: string, paid: boolean) {
    TestBed.configureTestingModule({
      imports: [PixPaymentDialogComponent],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: { payment: { paymentId: 'charge', valor: 100, status, paid }, loadStatus: () => of({}) } },
        { provide: MatDialogRef, useValue: { close: () => {} } },
        { provide: MatSnackBar, useValue: { open: () => {} } }
      ]
    });
    return TestBed.createComponent(PixPaymentDialogComponent).componentInstance;
  }

  it('keeps an externally confirmed charge pending until the backend settles it', () => {
    const component = create('CONFIRMED', false);
    expect(component.confirmed).toBe(false);
    expect(component.statusLabel).toContain('Aguardando');
  });

  it('shows payment complete only after settlement is recorded', () => {
    const component = create('RECEIVED', true);
    expect(component.confirmed).toBe(true);
    expect(component.statusLabel).toBe('Pagamento confirmado.');
  });
});
