import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PlatformRevenueDto {
  id: string;
  dataCriacao: string;
  tipoReceita: number; // Enum: JurosEmprestimo=0, MultaAtraso=1, etc.
  valor: number;
  loanId?: string;
  installmentId?: string;
  userId?: string;
  observacao?: string;
}

export interface RevenueDashboardMetricsDto {
  receitaHoje: number;
  receitaSemana: number;
  receitaMes: number;
  receitaTotal: number;
  quantidadeEventos: number;
  ultimaReceita: PlatformRevenueDto;
}

export interface PagedRevenueResultDto {
  items: PlatformRevenueDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

@Injectable({
  providedIn: 'root'
})
export class AdminRevenueService {
  private apiUrl = `${environment.apiUrl}/admin/revenue`;

  constructor(private http: HttpClient) {}

  getDashboardMetrics(): Observable<RevenueDashboardMetricsDto> {
    return this.http.get<RevenueDashboardMetricsDto>(`${this.apiUrl}/dashboard`);
  }

  getRevenues(
    page: number = 1,
    pageSize: number = 20,
    period?: string,
    startDate?: string,
    endDate?: string
  ): Observable<PagedRevenueResultDto> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (period) {
      params = params.set('period', period);
    }
    if (startDate) {
      params = params.set('startDate', startDate);
    }
    if (endDate) {
      params = params.set('endDate', endDate);
    }

    return this.http.get<PagedRevenueResultDto>(this.apiUrl, { params });
  }
}
