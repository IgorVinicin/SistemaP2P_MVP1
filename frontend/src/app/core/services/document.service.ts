import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DocumentStatusDto, UploadResponseDto } from '../models/document.models';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private apiUrl = 'http://localhost:5141/api/documents';

  constructor(private http: HttpClient) {}

  public uploadDocument(file: File, tipoDocumento: string): Observable<UploadResponseDto> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('tipoDocumento', tipoDocumento);

    return this.http.post<UploadResponseDto>(`${this.apiUrl}/upload`, formData);
  }

  public getDocumentsStatus(): Observable<DocumentStatusDto[]> {
    return this.http.get<DocumentStatusDto[]>(`${this.apiUrl}/status`);
  }
}
