import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface UserProfileDto {
  id: string;
  email: string;
  role: string;
  status: string;
  createdAt: string;
  profile?: {
    nome: string;
    cpf: string;
    dataNascimento?: string;
    telefone?: string;
    possuiEmpresa?: boolean;
    scoreAtual?: number;
    limitePersonalizado?: number;
    questionnaireCompleted?: boolean;
  };
  company?: {
    razaoSocial: string;
    nomeFantasia?: string;
    cnpj: string;
    tempoEmpresa?: number;
    faturamentoMensal?: number;
  };
}

export interface UpdateProfileRequest {
  telefone?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/profile`;

  public getMyProfile(): Observable<UserProfileDto> {
    return this.http.get<UserProfileDto>(`${this.apiUrl}/me`);
  }

  public updateMyProfile(dto: UpdateProfileRequest): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.apiUrl}/me`, dto);
  }
}
