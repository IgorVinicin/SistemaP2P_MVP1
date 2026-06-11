import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ScoreCreditRule, CreditConfig } from '../models/credit-rule.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AdminCreditService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/admin/credit-rules`;

  public getRules(): Observable<ScoreCreditRule[]> {
    return this.http.get<ScoreCreditRule[]>(this.apiUrl);
  }

  public createRule(rule: ScoreCreditRule): Observable<ScoreCreditRule> {
    return this.http.post<ScoreCreditRule>(this.apiUrl, rule);
  }

  public updateRule(id: string, rule: ScoreCreditRule): Observable<ScoreCreditRule> {
    return this.http.put<ScoreCreditRule>(`${this.apiUrl}/${id}`, rule);
  }

  public deleteRule(id: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/${id}`);
  }

  public getConfig(): Observable<CreditConfig> {
    return this.http.get<CreditConfig>(`${this.apiUrl}/config`);
  }

  public updateConfig(config: CreditConfig): Observable<CreditConfig> {
    return this.http.put<CreditConfig>(`${this.apiUrl}/config`, config);
  }
}
