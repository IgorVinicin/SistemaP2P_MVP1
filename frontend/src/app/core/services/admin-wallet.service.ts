import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { InvestorTransactionDto } from '../models/investor.models';

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
  private apiUrl = 'http://localhost:5141/api/admin';

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  public getWallets(): Observable<AdminInvestorWalletSummaryDto[]> {
    return this.http.get<AdminInvestorWalletSummaryDto[]>(`${this.apiUrl}/wallets`, { headers: this.getHeaders() });
  }

  public getTransactions(investorId: string): Observable<InvestorTransactionDto[]> {
    return this.http.get<InvestorTransactionDto[]>(`${this.apiUrl}/wallets/${investorId}/transactions`, { headers: this.getHeaders() });
  }

  public adjustWallet(investorId: string, valor: number, descricao: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/wallet-adjustment`, { investorId, valor, descricao }, { headers: this.getHeaders() });
  }
}
