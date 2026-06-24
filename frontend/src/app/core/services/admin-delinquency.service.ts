import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedResult } from '../models/user.models';
import { environment } from '../../../environments/environment';

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
  private apiUrl = `${environment.apiUrl}/admin/delinquency`;

  public getDelinquentLoans(
    page: number = 1,
    pageSize: number = 20,
    status?: string,
    cliente?: string,
    periodoInicio?: string,
    periodoFim?: string
  ): Observable<PagedResult<DelinquentLoanSummary>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (status) params = params.set('status', status);
    if (cliente) params = params.set('cliente', cliente);
    if (periodoInicio) params = params.set('periodoInicio', periodoInicio);
    if (periodoFim) params = params.set('periodoFim', periodoFim);

    return this.http.get<PagedResult<DelinquentLoanSummary>>(`${this.apiUrl}/loans`, {
      params
    });
  }

  public getProtectionFund(): Observable<ProtectionFundSummary> {
    return this.http.get<ProtectionFundSummary>(`${this.apiUrl}/protection-fund`);
  }

  public adjustProtectionFund(valor: number, tipo: string, motivo: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/protection-fund/adjust`, { valor, tipo, motivo });
  }

  public coverOverdue(loanId: string, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/cover-overdue`, { observacao });
  }

  public coverOverduePartial(loanId: string, valorCobertura: number, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/cover-overdue-partial`, { valorCobertura, observacao });
  }

  public terminateAndCover(loanId: string, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/terminate-and-cover`, { observacao });
  }

  public terminateAndLoss(loanId: string, observacao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/loans/${loanId}/terminate-and-loss`, { observacao });
  }

  public getHistory(page: number = 1, pageSize: number = 20, search?: string): Observable<PagedResult<FinancialEventHistoryDto>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<PagedResult<FinancialEventHistoryDto>>(`${this.apiUrl}/history`, { params });
  }
}
