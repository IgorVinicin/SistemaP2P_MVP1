import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/user.models';
import {
  BorrowerConsolidatedReport,
  InvestorConsolidatedReport,
  LoanConsolidatedReport,
  FinancialConsolidatedReport,
  BorrowerIndividualReport,
  InvestorIndividualReport,
  AllocationAuditItem
} from '../models/admin-report.models';

@Injectable({
  providedIn: 'root'
})
export class AdminReportService {
  private apiUrl = `${environment.apiUrl}/admin/reports`;

  constructor(private http: HttpClient) {}

  public getBorrowersConsolidated(
    page: number = 1,
    pageSize: number = 20,
    filters?: {
      nome?: string;
      cpf?: string;
      status?: string;
      periodoInicio?: string;
      periodoFim?: string;
    }
  ): Observable<BorrowerConsolidatedReport> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (filters) {
      if (filters.nome) params = params.set('nome', filters.nome);
      if (filters.cpf) params = params.set('cpf', filters.cpf);
      if (filters.status) params = params.set('status', filters.status);
      if (filters.periodoInicio) params = params.set('periodoInicio', filters.periodoInicio);
      if (filters.periodoFim) params = params.set('periodoFim', filters.periodoFim);
    }

    return this.http.get<BorrowerConsolidatedReport>(`${this.apiUrl}/consolidated/borrowers`, { params });
  }

  public getInvestorsConsolidated(
    page: number = 1,
    pageSize: number = 20,
    filters?: {
      nome?: string;
      cpf?: string;
      periodoInicio?: string;
      periodoFim?: string;
    }
  ): Observable<InvestorConsolidatedReport> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (filters) {
      if (filters.nome) params = params.set('nome', filters.nome);
      if (filters.cpf) params = params.set('cpf', filters.cpf);
      if (filters.periodoInicio) params = params.set('periodoInicio', filters.periodoInicio);
      if (filters.periodoFim) params = params.set('periodoFim', filters.periodoFim);
    }

    return this.http.get<InvestorConsolidatedReport>(`${this.apiUrl}/consolidated/investors`, { params });
  }

  public getLoansConsolidated(
    page: number = 1,
    pageSize: number = 20,
    filters?: {
      tipo?: string;
      status?: string;
      periodoInicio?: string;
      periodoFim?: string;
    }
  ): Observable<LoanConsolidatedReport> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (filters) {
      if (filters.tipo) params = params.set('tipo', filters.tipo);
      if (filters.status) params = params.set('status', filters.status);
      if (filters.periodoInicio) params = params.set('periodoInicio', filters.periodoInicio);
      if (filters.periodoFim) params = params.set('periodoFim', filters.periodoFim);
    }

    return this.http.get<LoanConsolidatedReport>(`${this.apiUrl}/consolidated/loans`, { params });
  }

  public getFinancialConsolidated(
    periodoInicio?: string,
    periodoFim?: string
  ): Observable<FinancialConsolidatedReport> {
    let params = new HttpParams();

    if (periodoInicio) params = params.set('periodoInicio', periodoInicio);
    if (periodoFim) params = params.set('periodoFim', periodoFim);

    return this.http.get<FinancialConsolidatedReport>(`${this.apiUrl}/consolidated/financial`, { params });
  }

  public getBorrowerIndividual(id: string): Observable<BorrowerIndividualReport> {
    return this.http.get<BorrowerIndividualReport>(`${this.apiUrl}/individual/borrower/${id}`);
  }

  public getInvestorIndividual(id: string): Observable<InvestorIndividualReport> {
    return this.http.get<InvestorIndividualReport>(`${this.apiUrl}/individual/investor/${id}`);
  }

  public getAllocationsAudit(
    page: number = 1,
    pageSize: number = 20,
    filters?: {
      investorName?: string;
      status?: string;
    }
  ): Observable<PagedResult<AllocationAuditItem>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (filters) {
      if (filters.investorName) params = params.set('investorName', filters.investorName);
      if (filters.status) params = params.set('status', filters.status);
    }

    return this.http.get<PagedResult<AllocationAuditItem>>(`${this.apiUrl}/allocations`, { params });
  }
}
