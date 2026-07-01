import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LedgerTransaction {
  id: string;
  createdAt: string;
  transactionDate: string;
  tipoMovimentacao: number;
  contaOrigem: number;
  contaDestino: number;
  valor: number;
  referenceType?: string;
  referenceId?: string;
  loanId?: string;
  installmentId?: string;
  userId?: string;
  observacao?: string;
  createdBy: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

@Injectable({
  providedIn: 'root'
})
export class AdminLedgerService {
  private apiUrl = `${environment.apiUrl}/admin/ledger`;

  constructor(private http: HttpClient) {}

  getHistory(
    page: number = 1,
    pageSize: number = 20,
    period?: string,
    startDate?: string,
    endDate?: string,
    type?: number,
    account?: number,
    loanId?: string,
    userId?: string
  ): Observable<PagedResult<LedgerTransaction>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (period) params = params.set('period', period);
    if (startDate) params = params.set('startDate', startDate);
    if (endDate) params = params.set('endDate', endDate);
    if (type !== undefined && type !== null) params = params.set('type', type.toString());
    if (account !== undefined && account !== null) params = params.set('account', account.toString());
    if (loanId) params = params.set('loanId', loanId);
    if (userId) params = params.set('userId', userId);

    return this.http.get<PagedResult<LedgerTransaction>>(this.apiUrl, { params });
  }

  coverInstallment(installmentId: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/cover-installment/${installmentId}`, {});
  }
}
