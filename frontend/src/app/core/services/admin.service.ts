import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PendingUserSummaryDto, UserDetailDto } from '../models/user.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = `${environment.apiUrl}/admin/users`;

  constructor(private http: HttpClient) {}

  public getPendingUsers(): Observable<PendingUserSummaryDto[]> {
    return this.http.get<PendingUserSummaryDto[]>(`${this.apiUrl}/pending`);
  }

  public getUserById(id: string): Observable<UserDetailDto> {
    return this.http.get<UserDetailDto>(`${this.apiUrl}/${id}`);
  }

  public approveUser(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/${id}/approve`, {});
  }

  public reproveUser(id: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/${id}/reprove`, {});
  }

  public getDocumentFile(userId: string, documentId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${userId}/documents/${documentId}`, {
      responseType: 'blob'
    });
  }

  public getAllUsers(): Observable<PendingUserSummaryDto[]> {
    return this.http.get<PendingUserSummaryDto[]>(this.apiUrl);
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
