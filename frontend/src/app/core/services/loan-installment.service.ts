import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LoanInstallmentDto } from '../models/installment.models';

@Injectable({
  providedIn: 'root'
})
export class LoanInstallmentService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5141/api';

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  public getInstallments(loanRequestId: string): Observable<LoanInstallmentDto[]> {
    return this.http.get<LoanInstallmentDto[]>(`${this.apiUrl}/loans/${loanRequestId}/installments`, {
      headers: this.getHeaders()
    });
  }

  public payInstallment(installmentId: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/installments/${installmentId}/pay`, {}, {
      headers: this.getHeaders()
    });
  }

  public getAdminInstallments(filters: {
    cliente?: string;
    status?: string;
    periodoInicio?: string;
    periodoFim?: string;
    somenteAtrasadas?: boolean;
  }): Observable<any[]> {
    let params = new HttpParams();
    if (filters.cliente) {
      params = params.set('cliente', filters.cliente);
    }
    if (filters.status) {
      params = params.set('status', filters.status);
    }
    if (filters.periodoInicio) {
      params = params.set('periodoInicio', filters.periodoInicio);
    }
    if (filters.periodoFim) {
      params = params.set('periodoFim', filters.periodoFim);
    }
    if (filters.somenteAtrasadas !== undefined) {
      params = params.set('somenteAtrasadas', filters.somenteAtrasadas.toString());
    }

    return this.http.get<any[]>(`${this.apiUrl}/admin/loans/installments`, {
      headers: this.getHeaders(),
      params
    });
  }
}
