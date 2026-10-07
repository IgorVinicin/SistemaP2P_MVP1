import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { LoanInstallmentDto } from '../models/installment.models';
import { PagedResult } from '../models/user.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class LoanInstallmentService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  public getInstallments(loanRequestId: string): Observable<LoanInstallmentDto[]> {
    return this.http.get<LoanInstallmentDto[]>(`${this.apiUrl}/loans/${loanRequestId}/installments?t=${new Date().getTime()}`);
  }

  public getAdminInstallments(
    page: number = 1,
    pageSize: number = 20,
    filters: {
      cliente?: string;
      status?: string;
      periodoInicio?: string;
      periodoFim?: string;
      somenteAtrasadas?: boolean;
    }
  ): Observable<PagedResult<any>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
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

    return this.http.get<PagedResult<any>>(`${this.apiUrl}/admin/loans/installments`, {
      params
    });
  }

  public settleInstallment(
    installmentId: string, 
    valorPago?: number, 
    formaPagamento?: string, 
    observacao?: string
  ): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/admin/loans/installments/${installmentId}/settle`, {
      valorPago,
      formaPagamento,
      observacao
    });
  }
}
