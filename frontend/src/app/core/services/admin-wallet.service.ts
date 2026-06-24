import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { InvestorTransactionDto } from '../models/investor.models';
import { PagedResult } from '../models/user.models';
import { environment } from '../../../environments/environment';

export interface AdminInvestorWalletSummaryDto {
  investorId: string;
  nome: string;
  email: string;
  saldoDisponivel: number;
  saldoInvestido: number;
  rendimentos: number;
  totalAportado: number;
  totalSacado: number;
  perfilInvestimento: string;
  dataCriacao: string;
  dataAtualizacao: string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminWalletService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/admin`;

  public getWallets(page: number = 1, pageSize: number = 20, search?: string): Observable<PagedResult<AdminInvestorWalletSummaryDto>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<PagedResult<AdminInvestorWalletSummaryDto>>(`${this.apiUrl}/wallets`, { params });
  }

  public getTransactions(investorId: string): Observable<InvestorTransactionDto[]> {
    return this.http.get<InvestorTransactionDto[]>(`${this.apiUrl}/wallets/${investorId}/transactions`);
  }

  public adjustWallet(investorId: string, valor: number, descricao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/wallet-adjustment`, { investorId, valor, descricao });
  }
}
