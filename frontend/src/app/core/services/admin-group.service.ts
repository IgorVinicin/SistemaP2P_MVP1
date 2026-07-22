import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface InvestorGroup {
  id: string;
  nome: string;
  descricao?: string;
  createdAt: string;
  investorsCount: number;
  investors: Array<{ investorId: string; name: string }>;
}

export interface CreateUpdateGroupDto {
  nome: string;
  descricao?: string;
  investorIds: string[];
}

@Injectable({
  providedIn: 'root'
})
export class AdminGroupService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/admin/groups`;

  getGroups(): Observable<InvestorGroup[]> {
    return this.http.get<InvestorGroup[]>(this.apiUrl);
  }

  getGroup(id: string): Observable<InvestorGroup> {
    return this.http.get<InvestorGroup>(`${this.apiUrl}/${id}`);
  }

  createGroup(data: CreateUpdateGroupDto): Observable<any> {
    return this.http.post(this.apiUrl, data);
  }

  updateGroup(id: string, data: CreateUpdateGroupDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}`, data);
  }

  deleteGroup(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`);
  }
}
