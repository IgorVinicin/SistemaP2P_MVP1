import { ChangeDetectorRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MAT_DIALOG_DATA, MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, firstValueFrom, Observable, of } from 'rxjs';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot } from '@angular/router';
import { InvestorDashboardComponent } from './features/investor/pages/investor-dashboard/investor-dashboard.component';
import { PayInstallmentDialogComponent } from './features/user-dashboard/components/pay-installment-dialog/pay-installment-dialog.component';
import { MyLoansComponent } from './features/user-dashboard/pages/my-loans/my-loans.component';
import { AuthService } from './core/services/auth.service';
import { borrowerGuard } from './core/guards/borrower.guard';
import { environment } from '../environments/environment';

describe('Explicit PIX workflow', () => {
  const dialogRef = { close: () => {}, afterClosed: () => EMPTY };
  beforeEach(() => TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ChangeDetectorRef, useValue: { detectChanges: () => {} } },
      { provide: MatDialog, useValue: { open: () => dialogRef } },
      { provide: MatSnackBar, useValue: { open: () => {} } },
      { provide: AuthService, useValue: { currentUser: () => null, userStatus: () => 'Aprovado' } }
    ]
  }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('opens the deposit form without issuing a charge, and generates only on explicit confirmation', () => {
    const component = TestBed.runInInjectionContext(() => new InvestorDashboardComponent());
    const http = TestBed.inject(HttpTestingController);
    component.openDepositDialog();
    http.expectNone(() => true);
    component.aporteValor = 123.45;
    component.makeDeposit();
    const request = http.expectOne(`${environment.apiUrl}/investor/treasury/deposit-pix`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body.valor).toBe(123.45);
    component.makeDeposit();
    http.expectNone(() => true); // double click while the first request is pending
    request.flush({ depositId: 'deposit', paymentId: 'pix', valor: 123.45, status: 'PENDING', paid: false });
    expect(component.wallet).toBeNull(); // issuing PIX never credits the displayed balance
  });

  it('counts credited yields once while preserving money reserved for withdrawals in total assets', () => {
    const component = TestBed.runInInjectionContext(() => new InvestorDashboardComponent());
    component.wallet = { saldoDisponivel: 300, saldoComprometido: 200, saldoInvestido: 400, saldoBloqueado: 100, rendimentos: 50 } as any;
    expect(component.saldoTotal).toBe(1000);
  });

  it('opens a borrower installment dialog without requesting PIX or settling a payment', () => {
    const component = TestBed.runInInjectionContext(() => new MyLoansComponent());
    component.installments = [{ id: 'installment', valorParcela: 250, valorPago: 50, multa: 10, jurosMora: 5 }] as any;
    component.payInstallment('installment');
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });

  it('keeps pending borrowers read-only even when they have an unpaid installment', () => {
    TestBed.overrideProvider(AuthService, { useValue: { userStatus: () => 'Pendente' } });
    let opened = false;
    TestBed.overrideProvider(MatDialog, { useValue: { open: () => { opened = true; return dialogRef; } } });
    const component = TestBed.runInInjectionContext(() => new MyLoansComponent());
    component.installments = [{ id: 'installment', valorParcela: 100, valorPago: 0 }] as any;
    component.payInstallment('installment');
    expect(opened).toBe(false);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });

  it('sends the selected partial amount after the generate action and never calls the simulated pay route', () => {
    let opened = 0;
    TestBed.overrideProvider(MatDialog, { useValue: { open: () => ++opened === 1
      ? { afterClosed: () => of({ valorPago: 80 }) } : dialogRef } });
    const component = TestBed.runInInjectionContext(() => new MyLoansComponent());
    component.installments = [{ id: 'installment', valorParcela: 250, valorPago: 50, multa: 10, jurosMora: 5 }] as any;
    component.payInstallment('installment');
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(`${environment.apiUrl}/loans/installments/installment/pix`);
    expect(request.request.body).toEqual({ valor: 80 });
    http.expectNone(req => req.url.endsWith('/pay'));
    request.flush({ installmentId: 'installment', valor: 80, paymentId: 'pix', status: 'PENDING', paid: false });
    expect(component.installments[0].valorPago).toBe(50);
  });

  it('preloads an updated installment balance, including rolled-over debt, and subtracts prior payments', () => {
    TestBed.overrideProvider(MAT_DIALOG_DATA, { useValue: { installment: { valorParcela: 250, multa: 10, jurosMora: 5, valorPago: 50 } } });
    TestBed.overrideProvider(MatDialogRef, { useValue: dialogRef });
    const component = TestBed.createComponent(PayInstallmentDialogComponent).componentInstance;
    expect(component.totalDevido).toBe(215);
    expect(component.valorDigitado).toBe(215);
    component.valorDigitado = 216;
    expect(component.isValid()).toBe(false);
    component.valorDigitado = 80;
    expect(component.isValid()).toBe(true);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });
});

describe('Pending borrower access', () => {
  async function access(role: string, status: string, requiresApproval: boolean) {
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: AuthService, useValue: { getMyStatus: () => of({ role, status }) } }
    ] });
    const result = TestBed.runInInjectionContext(() => borrowerGuard({ data: { requiresApproval } } as unknown as ActivatedRouteSnapshot, {} as RouterStateSnapshot));
    return firstValueFrom(result as Observable<any>);
  }
  it('allows a pending borrower to inspect existing loans', async () => {
    expect(await access('Cliente', 'Pendente', false)).toBe(true);
  });
  it('blocks a pending borrower from new financial requests', async () => {
    const result = await access('Cliente', 'Pendente', true);
    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/dashboard');
  });
  it('allows an approved borrower to request credit', async () => {
    expect(await access('Cliente', 'Aprovado', true)).toBe(true);
  });
});
