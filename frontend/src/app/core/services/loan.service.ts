import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LoanRequest, LoanEligibility, AdminReproveLoan } from '../models/loan.models';
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
  public getPendingLoans(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/admin/loans/pending`);
  }

  public getActiveLoans(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/admin/loans/active`);
  }

  public getLoanDetails(id: string): Observable<LoanRequest> {
    return this.http.get<LoanRequest>(`${this.apiUrl}/admin/loans/${id}`);
  }

  public approveLoan(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/approve`, {});
  }

  public reproveLoan(id: string, dto: AdminReproveLoan): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/reprove`, dto);
  }

  public finishLoan(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/finish`, {});
  }
}
