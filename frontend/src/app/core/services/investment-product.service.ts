import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  InvestmentProduct,
  CreateInvestmentProductDto,
  UpdateInvestmentProductDto
} from '../models/investment-product.models';

@Injectable({
  providedIn: 'root'
})
export class InvestmentProductService {
  private readonly baseUrl = `${environment.apiUrl}/investment-products`;
  private readonly adminUrl = `${environment.apiUrl}/admin/investment-products`;

  constructor(private http: HttpClient) {}

  // Investidor / Público
  getActiveProducts(): Observable<InvestmentProduct[]> {
    return this.http.get<InvestmentProduct[]>(this.baseUrl);
  }

  // Admin
  getAllProducts(): Observable<InvestmentProduct[]> {
    return this.http.get<InvestmentProduct[]>(this.adminUrl);
  }

  createProduct(dto: CreateInvestmentProductDto): Observable<InvestmentProduct> {
    return this.http.post<InvestmentProduct>(this.adminUrl, dto);
  }

  updateProduct(id: string, dto: UpdateInvestmentProductDto): Observable<InvestmentProduct> {
    return this.http.put<InvestmentProduct>(`${this.adminUrl}/${id}`, dto);
  }

  deleteProduct(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.adminUrl}/${id}`);
  }
}
