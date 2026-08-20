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

export interface CashFlowClosingItemDto {
  installmentId: string;
  loanRequestId: string;
  tomadorNome: string;
  tomadorCpf: string;
  tipoEmprestimo: string;
  valorParcela: number;
  multa: number;
  jurosMora: number;
  valorTotalDevido: number;
  valorPago: number;
  valorRestante: number;
  percentualPago: number;
  numeroParcela: number;
  quantidadeTotalParcelas: number;
  numeroParcelaFormatado: string;
  dataVencimento: string;
  dataPagamento?: string;
  statusOriginal: string;
  statusCalculado: string; // "Paga", "Parcialmente Paga", "Em Débito"
  diasAtraso: number;
}

export interface CashFlowClosingConferenceDto {
  totalParcelasPrevistas: number;
  totalParcelasPagas: number;
  totalParcelasParciais: number;
  totalParcelasEmDebito: number;
  valorTotalRecebido: number;
  valorAindaPendente: number;
  percentualAdimplencia: number;
  items: CashFlowClosingItemDto[];
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

  getClosingConference(
    period: string = 'hoje',
    startDate?: Date | null,
    endDate?: Date | null,
    status?: string,
    tipoEmprestimo?: string,
    tomador?: string
  ): Observable<CashFlowClosingConferenceDto> {
    let params = new HttpParams().set('period', period);
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());
    if (status) params = params.set('status', status);
    if (tipoEmprestimo) params = params.set('tipoEmprestimo', tipoEmprestimo);
    if (tomador) params = params.set('tomador', tomador);

    return this.http.get<CashFlowClosingConferenceDto>(`${this.apiUrl}/closing-conference`, { params });
  }

  getWithdrawals(status?: string): Observable<AdminWithdrawalRequestDto[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    return this.http.get<AdminWithdrawalRequestDto[]>(`${environment.apiUrl}/admin/treasury/withdrawals`, { params });
  }

  approveWithdrawal(id: string): Observable<any> {
    return this.http.post<any>(`${environment.apiUrl}/admin/treasury/withdrawals/${id}/approve`, {});
  }

  rejectWithdrawal(id: string, motivo: string): Observable<any> {
    return this.http.post<any>(`${environment.apiUrl}/admin/treasury/withdrawals/${id}/reject`, { motivo });
  }

  confirmWithdrawal(id: string, gatewayReference: string): Observable<any> {
    return this.http.post<any>(`${environment.apiUrl}/admin/treasury/withdrawals/${id}/confirm`, { gatewayReference });
  }
}

export interface AdminWithdrawalRequestDto {
  id: string;
  investorId: string;
  investor?: {
    nome?: string;
    cpf?: string;
    email?: string;
  };
  valor: number;
  status: string;
  chavePix?: string;
  tipoChavePix?: string;
  observacao?: string;
  createdAt: string;
  processedAt?: string;
}
