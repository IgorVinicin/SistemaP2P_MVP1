import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AsaasTestResult {
  success: boolean;
  message: string;
  environment: string;
  baseUrl: string;
  balance?: number;
  accountName?: string;
  cpfCnpj?: string;
  checkedAt: string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminGatewayService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/admin/gateway`;

  public testConnection(): Observable<AsaasTestResult> {
    return this.http.get<AsaasTestResult>(`${this.apiUrl}/test`);
  }
}
