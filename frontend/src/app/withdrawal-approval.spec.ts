import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AdminCashFlowService } from './core/services/admin-cashflow.service';
import { AdminCashFlowComponent } from './features/admin/pages/admin-cashflow/admin-cashflow.component';
import { environment } from '../environments/environment';

describe('Withdrawal amount approval', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  function create() {
    const component = new AdminCashFlowComponent(TestBed.inject(AdminCashFlowService),
      { open: () => ({ close: () => {} }) } as unknown as MatDialog,
      { open: () => {} } as unknown as MatSnackBar);
    component.withdrawals = [{ id: 'withdrawal', investorId: 'investor', valor: 500, valorSolicitado: 500, status: 'Pending', createdAt: '2026-10-07T12:00:00Z' }];
    component.loadWithdrawals = () => {};
    component.loadData = () => {};
    return component;
  }

  it('opens analysis with the full requested amount without approving automatically', () => {
    const component = create();
    component.approveWithdraw('withdrawal');
    expect(component.approvedWithdrawalAmount).toBe(500);
    expect(component.requestedWithdrawalAmount).toBe(500);
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });

  it('approves a smaller amount once and preserves the original request for display', () => {
    const component = create();
    component.approveWithdraw('withdrawal');
    component.approvedWithdrawalAmount = 300;
    component.confirmWithdrawalApproval();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(`${environment.apiUrl}/admin/treasury/withdrawals/withdrawal/approve`);
    expect(request.request.body).toEqual({ valorAprovado: 300 });
    component.confirmWithdrawalApproval();
    http.expectNone(() => true);
    request.flush({ message: 'Aprovado' });
    expect(component.selectedWithdrawal?.valorSolicitado).toBe(500);
    expect(component.selectedWithdrawal?.valorAprovado).toBe(300);
    expect(component.selectedWithdrawal?.status).toBe('Approved');
    component.confirmWithdrawalApproval();
    http.expectNone(() => true);
  });

  it('rejects zero, excessive, non-finite and fractional-cent approval values', () => {
    const component = create();
    component.approveWithdraw('withdrawal');
    for (const amount of [0, -1, 501, NaN, Infinity, 100.001]) {
      component.approvedWithdrawalAmount = amount;
      expect(component.validWithdrawalApproval).toBe(false);
      component.confirmWithdrawalApproval();
    }
    TestBed.inject(HttpTestingController).expectNone(() => true);
  });

  it('preserves the pending request and displays the backend reason when approval fails', () => {
    const component = create();
    component.approveWithdraw('withdrawal');
    component.confirmWithdrawalApproval();
    TestBed.inject(HttpTestingController).expectOne(`${environment.apiUrl}/admin/treasury/withdrawals/withdrawal/approve`)
      .flush({ message: 'Solicitação já processada' }, { status: 409, statusText: 'Conflict' });
    expect(component.isApprovingWithdrawal).toBe(false);
    expect(component.selectedWithdrawal?.status).toBe('Pending');
    expect(component.withdrawalApprovalError).toBe('Solicitação já processada');
  });
});
