import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ScoreCreditRule, CreditConfig } from '../models/credit-rule.models';

@Injectable({
  providedIn: 'root'
})
export class AdminCreditService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5141/api/admin/credit-rules';

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  public getRules(): Observable<ScoreCreditRule[]> {
    return this.http.get<ScoreCreditRule[]>(this.apiUrl, { headers: this.getHeaders() });
  }

  public createRule(rule: ScoreCreditRule): Observable<ScoreCreditRule> {
    return this.http.post<ScoreCreditRule>(this.apiUrl, rule, { headers: this.getHeaders() });
  }

  public updateRule(id: string, rule: ScoreCreditRule): Observable<ScoreCreditRule> {
    return this.http.put<ScoreCreditRule>(`${this.apiUrl}/${id}`, rule, { headers: this.getHeaders() });
  }

  public deleteRule(id: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/${id}`, { headers: this.getHeaders() });
  }

  public getConfig(): Observable<CreditConfig> {
    return this.http.get<CreditConfig>(`${this.apiUrl}/config`, { headers: this.getHeaders() });
  }

  public updateConfig(config: CreditConfig): Observable<CreditConfig> {
    return this.http.put<CreditConfig>(`${this.apiUrl}/config`, config, { headers: this.getHeaders() });
  }
}
