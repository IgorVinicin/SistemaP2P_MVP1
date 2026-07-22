import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface CashFlowSnapshot {
  id: string;
  snapshotDate: string;
  totalEntradas: number;
  totalSaidas: number;
  saldoOperacional: number;
  receitaPlataforma: number;
  totalDistribuidoInvestidores: number;
  totalCobertoProtectionFund: number;
  totalEmprestado: number;
  totalRecebidoParcelas: number;
  totalDepositosExternos: number;
  totalSaquesExternos: number;
  createdAt: string;
}

export interface CashFlowTransactionDto {
  id: string;
  transactionDate: string;
  tipoMovimentacao: number;
  valor: number;
  userName?: string;
  cpf?: string;
  observacao?: string;
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
export class AdminCashFlowService {
  private apiUrl = `${environment.apiUrl}/admin/cashflow`;

  constructor(private http: HttpClient) {}

  getSummary(period: string = 'hoje', startDate?: Date | null, endDate?: Date | null): Observable<CashFlowSnapshot> {
    let params = new HttpParams().set('period', period);
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());

    return this.http.get<CashFlowSnapshot>(`${this.apiUrl}/summary`, { params });
  }

  getSnapshots(page: number = 1, pageSize: number = 20): Observable<PagedResult<CashFlowSnapshot>> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString())
      .set('t', new Date().getTime().toString());

    return this.http.get<PagedResult<CashFlowSnapshot>>(`${this.apiUrl}/snapshots`, { params });
  }

  getTransactions(period: string = 'hoje', search?: string, type?: number, page: number = 1, pageSize: number = 20, startDate?: Date | null, endDate?: Date | null): Observable<PagedResult<CashFlowTransactionDto>> {
    let params = new HttpParams()
      .set('period', period)
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
      
    if (search) params = params.set('search', search);
    if (type !== undefined && type !== null) params = params.set('type', type.toString());
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());

    return this.http.get<PagedResult<CashFlowTransactionDto>>(`${this.apiUrl}/transactions`, { params });
  }

  exportTransactions(period: string = 'hoje', search?: string, type?: number, startDate?: Date | null, endDate?: Date | null): Observable<Blob> {
    let params = new HttpParams().set('period', period);
      
    if (search) params = params.set('search', search);
    if (type !== undefined && type !== null) params = params.set('type', type.toString());
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());

    return this.http.get(`${this.apiUrl}/transactions/export`, { params, responseType: 'blob' });
  }

  generateSnapshot(date?: string): Observable<any> {
    let params = new HttpParams();
    if (date) params = params.set('date', date);
    return this.http.post<any>(`${this.apiUrl}/snapshot`, {}, { params });
  }
}
