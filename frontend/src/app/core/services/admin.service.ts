import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PendingUserSummaryDto, UserDetailDto, PagedResult } from '../models/user.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = `${environment.apiUrl}/admin/users`;

  constructor(private http: HttpClient) {}

  public getPendingUsers(page: number = 1, pageSize: number = 20, search?: string): Observable<PagedResult<PendingUserSummaryDto>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<PagedResult<PendingUserSummaryDto>>(`${this.apiUrl}/pending`, { params });
  }

  public getUserById(id: string): Observable<UserDetailDto> {
    return this.http.get<UserDetailDto>(`${this.apiUrl}/${id}`);
  }

  public approveUser(id: string, rentabilidade?: number, observacaoAdministrativa?: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/${id}/approve`, { rentabilidade, observacaoAdministrativa });
  }

  public reproveUser(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/${id}/reprove`, {});
  }

  public getDocumentFile(userId: string, documentId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${userId}/documents/${documentId}`, {
      responseType: 'blob'
    });
  }

  public getAllUsers(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    filters?: {
      nome?: string;
      cpf?: string;
      email?: string;
      role?: string;
      status?: string;
      minScore?: number | null;
      maxScore?: number | null;
    }
  ): Observable<PagedResult<PendingUserSummaryDto>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (search) {
      params = params.set('search', search);
    }
    if (filters) {
      if (filters.nome) params = params.set('nome', filters.nome);
      if (filters.cpf) params = params.set('cpf', filters.cpf);
      if (filters.email) params = params.set('email', filters.email);
      if (filters.role) params = params.set('role', filters.role);
      if (filters.status) params = params.set('status', filters.status);
      if (filters.minScore !== undefined && filters.minScore !== null) params = params.set('minScore', filters.minScore.toString());
      if (filters.maxScore !== undefined && filters.maxScore !== null) params = params.set('maxScore', filters.maxScore.toString());
    }
    return this.http.get<PagedResult<PendingUserSummaryDto>>(this.apiUrl, { params });
  }

  public updateUserProfile(id: string, profile: any): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/${id}/profile`, profile);
  }

  public updateUserStatus(id: string, status: string): Observable<any> {
    const headers = { 'Content-Type': 'application/json' };
    return this.http.put<any>(`${this.apiUrl}/${id}/status`, JSON.stringify(status), { headers });
  }

  public updateUserScore(id: string, novoScore: number, motivo: string): Observable<any> {
    return this.http.put<any>(`${this.apiUrl}/${id}/score`, { novoScore, motivo });
  }
}
