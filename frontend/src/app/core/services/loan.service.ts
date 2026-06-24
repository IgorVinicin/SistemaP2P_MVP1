import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LoanRequest, LoanEligibility, AdminReproveLoan } from '../models/loan.models';
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
    tipoEmprestimo?: string
  ): Observable<PagedResult<LoanRequest>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) params = params.set('search', search);
    if (nivelRisco) params = params.set('nivelRisco', nivelRisco);
    if (tipoEmprestimo) params = params.set('tipoEmprestimo', tipoEmprestimo);
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

  public getLoanDetails(id: string): Observable<LoanRequest> {
    return this.http.get<LoanRequest>(`${this.apiUrl}/admin/loans/${id}`);
  }

  public approveLoan(id: string, payload: any): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/approve`, payload);
  }

  public reproveLoan(id: string, dto: AdminReproveLoan): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/reprove`, dto);
  }

  public finishLoan(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/finish`, {});
  }
}
