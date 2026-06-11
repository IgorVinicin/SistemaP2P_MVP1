import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface DelinquentLoanSummary {
  loanId: string;
  clienteNome: string;
  clienteEmail: string;
  valorSolicitado: number;
  valorEmAberto: number;
  diasAtraso: number;
  status: string;
  parcelasAtrasadasCount: number;
  saldoDevedorTotal?: number;
}

export interface ProtectionFundSummary {
  id: string;
  saldoAtual: number;
  totalRecebido: number;
  totalUtilizado: number;
  ultimaAtualizacao: string;
  coberturasRealizadasCount: number;
}

export interface FinancialEventHistoryDto {
  id: string;
  data: string;
  usuarioResponsavel: string;
  observacao: string;
  eventoExecutado: string;
  loanRequestId?: string;
  valor?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AdminDelinquencyService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5141/api/admin/delinquency';

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  public getDelinquentLoans(
    status?: string,
    cliente?: string,
    periodoInicio?: string,
    periodoFim?: string
  ): Observable<DelinquentLoanSummary[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (cliente) params = params.set('cliente', cliente);
    if (periodoInicio) params = params.set('periodoInicio', periodoInicio);
    if (periodoFim) params = params.set('periodoFim', periodoFim);

    return this.http.get<DelinquentLoanSummary[]>(`${this.apiUrl}/loans`, {
      headers: this.getHeaders(),
      params
    });
  }

  public getProtectionFund(): Observable<ProtectionFundSummary> {
    return this.http.get<ProtectionFundSummary>(`${this.apiUrl}/protection-fund`, {
      headers: this.getHeaders()
    });
  }

  public adjustProtectionFund(valor: number, tipo: string, motivo: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/protection-fund/adjust`, { valor, tipo, motivo }, {
      headers: this.getHeaders()
    });
  }

  public coverOverdue(loanId: string, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/cover-overdue`, { observacao }, {
      headers: this.getHeaders()
    });
  }

  public coverOverduePartial(loanId: string, valorCobertura: number, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/cover-overdue-partial`, { valorCobertura, observacao }, {
      headers: this.getHeaders()
    });
  }

  public terminateAndCover(loanId: string, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/terminate-and-cover`, { observacao }, {
      headers: this.getHeaders()
    });
  }

  public terminateAndLoss(loanId: string, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/terminate-and-loss`, { observacao }, {
      headers: this.getHeaders()
    });
  }

  public getHistory(): Observable<FinancialEventHistoryDto[]> {
    return this.http.get<FinancialEventHistoryDto[]>(`${this.apiUrl}/history`, {
      headers: this.getHeaders()
    });
  }
}
