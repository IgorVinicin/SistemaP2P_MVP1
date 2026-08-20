import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { InvestorWalletDto, InvestorTransactionDto } from '../models/investor.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class InvestorService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/investor`;

  public getWallet(): Observable<InvestorWalletDto> {
    return this.http.get<InvestorWalletDto>(`${this.apiUrl}/wallet`);
  }

  public getYieldPreview(): Observable<{ rendimentoDiarioEstimado: number, totalRendimentosPagos?: number }> {
    return this.http.get<{ rendimentoDiarioEstimado: number, totalRendimentosPagos?: number }>(`${this.apiUrl}/yield/preview`);
  }

  public makeDeposit(valor: number): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/deposit`, { valor });
  }

  public makeWithdraw(valor: number, chavePix?: string, tipoChavePix?: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/withdraw`, { valor, chavePix, tipoChavePix });
  }

  public getTransactions(): Observable<InvestorTransactionDto[]> {
    return this.http.get<InvestorTransactionDto[]>(`${this.apiUrl}/transactions`);
  }

  public selectProduct(productId: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/select-product`, { productId });
  }

  public updateProfile(perfil: string): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/profile`, { perfil });
  }
}
