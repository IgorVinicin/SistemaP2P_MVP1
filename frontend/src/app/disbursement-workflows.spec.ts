import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute } from '@angular/router';
import { EMPTY } from 'rxjs';
import { AdminLoansComponent } from './features/admin/pages/admin-loans/admin-loans.component';
import { InvestorDashboardComponent } from './features/investor/pages/investor-dashboard/investor-dashboard.component';
import { AuthService } from './core/services/auth.service';
import { environment } from '../environments/environment';

describe('Authorized loan disbursement', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
    { provide: MatDialog, useValue: { open: () => ({ close: () => {}, afterClosed: () => EMPTY }) } },
    { provide: MatSnackBar, useValue: { open: () => {} } },
    { provide: ActivatedRoute, useValue: { queryParams: EMPTY } },
    { provide: AuthService, useValue: {} }
  ] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  function loan() {
    const component = TestBed.runInInjectionContext(() => new AdminLoansComponent());
    component.selectedLoan = { id: 'loan', status: 'Aprovado', valorSolicitado: 500, userProfile: { nome: 'Tomador', cpf: '529.982.247-25' } } as any;
    component.transferChecked = true;
    return component;
  }

  it('opens the confirmation dialog without making a transfer', () => {
    const component = loan();
    component.openDisburseDialog();
    component.submitDisbursement();
    TestBed.inject(HttpTestingController).expectNone(() => true);
    expect(component.destinationConfirmed).toBe(false);
  });

  it('sends the approved value and fixed borrower CPF only after confirmation, blocking double click', () => {
    const component = loan();
    component.destinationConfirmed = true;
    component.submitDisbursement();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(`${environment.apiUrl}/admin/gateway/loans/loan/disburse`);
    expect(request.request.body).toEqual({ chavePix: '52998224725', tipoChavePix: 'CPF', confirmacaoDestino: true, valorConfirmado: 500 });
    component.submitDisbursement();
    http.expectNone(() => true);
    request.flush({ status: 'PENDING', transferId: 'transfer', valor: 500 });
    expect(component.selectedLoan?.status).toBe('DesembolsoPendente');
    expect(component.canSendLoan).toBe(false);
    http.expectOne(req => req.url.endsWith('/admin/loans/active/grouped')).flush({ items: [], totalCount: 0 });
  });

  it('never sends another transfer for a completed disbursement or an active pending attempt', () => {
    const component = loan();
    component.transfer = { status: 'PENDING', transferId: 'transfer', valor: 500 };
    component.destinationConfirmed = true;
    component.submitDisbursement();
    expect(component.canSendLoan).toBe(false);
    component.transfer = { status: 'DONE', settledAt: '2026-10-07T12:00:00Z', valor: 500 };
    component.selectedLoan!.status = 'EmPagamento';
    component.submitDisbursement();
    expect(component.canReconcileLoan).toBe(false);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });

  it('reconciles status after uncertain failure instead of permitting a blind retry', () => {
    const component = loan();
    component.destinationConfirmed = true;
    component.submitDisbursement();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(`${environment.apiUrl}/admin/gateway/loans/loan/disburse`).flush({ message: 'Resultado incerto' }, { status: 503, statusText: 'Unavailable' });
    expect(component.canSendLoan).toBe(false);
    expect(component.destinationConfirmed).toBe(false);
    http.expectOne(`${environment.apiUrl}/admin/gateway/loans/loan/transfer/status`).flush({ status: 'Creating', valor: 500 });
    expect(component.selectedLoan?.status).toBe('DesembolsoPendente');
    expect(component.canSendLoan).toBe(false);
  });

  it('keeps a finalized loan finalized while checking its historical transfer', () => {
    const component = loan();
    component.selectedLoan!.status = 'Finalizado';
    component.refreshTransferStatus();
    TestBed.inject(HttpTestingController).expectOne(`${environment.apiUrl}/admin/gateway/loans/loan/transfer/status`).flush({ status: 'DONE', valor: 500 });
    expect(component.selectedLoan?.status).toBe('Finalizado');
    expect(component.canSendLoan).toBe(false);
  });

  it('limits withdrawal to released deposits, with missing availability failing closed', () => {
    const component = TestBed.runInInjectionContext(() => new InvestorDashboardComponent());
    component.wallet = { saldoDisponivel: 1000, saldoSacavel: 200, saldoEmCarencia: 800 } as any;
    component.saqueValor = 201;
    component.chavePix = '52998224725';
    component.makeWithdraw();
    expect(component.saldoSacavel).toBe(200);
    TestBed.inject(HttpTestingController).expectNone(() => true);
    component.wallet = { saldoDisponivel: 1000 } as any;
    expect(component.saldoSacavel).toBe(0);
  });
});
