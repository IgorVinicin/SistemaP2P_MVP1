import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PixPayment } from '../models/pix-payment.models';
import { LoanRequest, LoanEligibility, AdminReproveLoan, LimitRequest, LoanTransferStatus, LoanDisbursementRequest } from '../models/loan.models';
import { PagedResult } from '../models/user.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class LoanService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  // Métodos do Cliente
  public getEligibility(): Observable<LoanEligibility> {
    return this.http.get<LoanEligibility>(`${this.apiUrl}/loans/eligibility`);
  }

  public requestLoan(dto: Partial<LoanRequest>): Observable<LoanRequest> {
    return this.http.post<LoanRequest>(`${this.apiUrl}/loans/request`, dto);
  }

  public getMyLoans(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/loans/my`);
  }

  public getLoanHistory(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/loans/history`);
  }

  // Métodos do Administrador
  public getPendingLoans(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    nivelRisco?: string,
    tipoEmprestimo?: string,
    somenteAumentoLimite?: boolean
  ): Observable<PagedResult<LoanRequest>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) params = params.set('search', search);
    if (nivelRisco) params = params.set('nivelRisco', nivelRisco);
    if (tipoEmprestimo) params = params.set('tipoEmprestimo', tipoEmprestimo);
    if (somenteAumentoLimite !== undefined) params = params.set('somenteAumentoLimite', somenteAumentoLimite.toString());
    return this.http.get<PagedResult<LoanRequest>>(`${this.apiUrl}/admin/loans/pending`, { params });
  }

  public getActiveLoans(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    status?: string,
    tipoEmprestimo?: string
  ): Observable<PagedResult<LoanRequest>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) params = params.set('search', search);
    if (status) params = params.set('status', status);
    if (tipoEmprestimo) params = params.set('tipoEmprestimo', tipoEmprestimo);
    return this.http.get<PagedResult<LoanRequest>>(`${this.apiUrl}/admin/loans/active`, { params });
  }

  public getActiveLoansGrouped(
    page: number = 1,
    pageSize: number = 10,
    search?: string,
    status?: string,
    tipoEmprestimo?: string
  ): Observable<PagedResult<any>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) params = params.set('search', search);
    if (status) params = params.set('status', status);
    if (tipoEmprestimo) params = params.set('tipoEmprestimo', tipoEmprestimo);
    return this.http.get<PagedResult<any>>(`${this.apiUrl}/admin/loans/active/grouped`, { params });
  }

  public getLoanDetails(id: string): Observable<LoanRequest> {
    return this.http.get<LoanRequest>(`${this.apiUrl}/admin/loans/${id}`);
  }

  public approveLoan(id: string, payload: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/approve`, payload);
  }

  public reproveLoan(id: string, dto: AdminReproveLoan): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/reprove`, dto);
  }

  public disburseLoan(id: string, request: LoanDisbursementRequest): Observable<LoanTransferStatus> {
    return this.http.post<LoanTransferStatus>(`${this.apiUrl}/admin/gateway/loans/${id}/disburse`, request);
  }

  public getLoanTransferStatus(id: string): Observable<LoanTransferStatus> {
    return this.http.get<LoanTransferStatus>(`${this.apiUrl}/admin/gateway/loans/${id}/transfer/status`);
  }

  // Métodos de Solicitação de Aumento de Limite
  public requestLimit(limiteSolicitado: number): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/limits/request`, { limiteSolicitado });
  }

  public getMyLimitRequests(): Observable<LimitRequest[]> {
    return this.http.get<LimitRequest[]>(`${this.apiUrl}/limits/my-requests`);
  }

  public getPendingLimitRequests(
    page: number = 1,
    pageSize: number = 10,
    search?: string
  ): Observable<PagedResult<LimitRequest>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) params = params.set('search', search);
    return this.http.get<PagedResult<LimitRequest>>(`${this.apiUrl}/limits/admin/pending`, { params });
  }

  public approveLimitRequest(id: string, valorAprovado?: number, observacaoAdmin?: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/limits/admin/${id}/approve`, { valorAprovado, observacaoAdmin });
  }

  public rejectLimitRequest(id: string, observacaoAdmin?: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/limits/admin/${id}/reject`, { observacaoAdmin });
  }

  // Cobrança PIX de parcela
  public createInstallmentPix(id: string, valor: number): Observable<PixPayment> {
    return this.http.post<PixPayment>(`${this.apiUrl}/loans/installments/${id}/pix`, { valor });
  }

  public getInstallmentPixStatus(id: string): Observable<PixPayment> {
    return this.http.get<PixPayment>(`${this.apiUrl}/loans/installments/${id}/pix/status`);
  }

}
