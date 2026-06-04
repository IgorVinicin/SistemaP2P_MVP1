import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LoanRequest, LoanEligibility, AdminReproveLoan } from '../models/loan.models';

@Injectable({
  providedIn: 'root'
})
export class LoanService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5141/api';

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  // Métodos do Cliente
  public getEligibility(): Observable<LoanEligibility> {
    return this.http.get<LoanEligibility>(`${this.apiUrl}/loans/eligibility`, { headers: this.getHeaders() });
  }

  public requestLoan(dto: Partial<LoanRequest>): Observable<LoanRequest> {
    return this.http.post<LoanRequest>(`${this.apiUrl}/loans/request`, dto, { headers: this.getHeaders() });
  }

  public getMyLoans(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/loans/my`, { headers: this.getHeaders() });
  }

  public getLoanHistory(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/loans/history`, { headers: this.getHeaders() });
  }

  // Métodos do Administrador
  public getPendingLoans(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/admin/loans/pending`, { headers: this.getHeaders() });
  }

  public getActiveLoans(): Observable<LoanRequest[]> {
    return this.http.get<LoanRequest[]>(`${this.apiUrl}/admin/loans/active`, { headers: this.getHeaders() });
  }

  public getLoanDetails(id: string): Observable<LoanRequest> {
    return this.http.get<LoanRequest>(`${this.apiUrl}/admin/loans/${id}`, { headers: this.getHeaders() });
  }

  public approveLoan(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/approve`, {}, { headers: this.getHeaders() });
  }

  public reproveLoan(id: string, dto: AdminReproveLoan): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/reprove`, dto, { headers: this.getHeaders() });
  }

  public finishLoan(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/${id}/finish`, {}, { headers: this.getHeaders() });
  }
}
