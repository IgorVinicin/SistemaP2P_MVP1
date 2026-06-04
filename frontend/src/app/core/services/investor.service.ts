import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { InvestorWalletDto, InvestorTransactionDto } from '../models/investor.models';

@Injectable({
  providedIn: 'root'
})
export class InvestorService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5141/api/investor';

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  public getWallet(): Observable<InvestorWalletDto> {
    return this.http.get<InvestorWalletDto>(`${this.apiUrl}/wallet`, { headers: this.getHeaders() });
  }

  public makeDeposit(valor: number): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/deposit`, { valor }, { headers: this.getHeaders() });
  }

  public makeWithdraw(valor: number): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/withdraw`, { valor }, { headers: this.getHeaders() });
  }

  public getTransactions(): Observable<InvestorTransactionDto[]> {
    return this.http.get<InvestorTransactionDto[]>(`${this.apiUrl}/transactions`, { headers: this.getHeaders() });
  }

  public updateProfile(perfil: string): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/profile`, { perfil }, { headers: this.getHeaders() });
  }
}
