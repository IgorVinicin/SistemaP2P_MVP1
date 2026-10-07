import { Observable } from 'rxjs';

export interface PixPayment {
  chargeId?: string;
  depositId?: string;
  installmentId?: string;
  paymentId: string;
  valor: number;
  qrCodeBase64?: string;
  copiaECola?: string;
  expirationDate?: string;
  status: string;
  paid?: boolean;
}

export interface PixPaymentDialogData {
  payment: PixPayment;
  loadStatus: () => Observable<PixPayment>;
}
