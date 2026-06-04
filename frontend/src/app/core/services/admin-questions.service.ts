import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { QuestionDto, AnswerOptionDto } from '../models/question.models';

@Injectable({
  providedIn: 'root'
})
export class AdminQuestionsService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5141/api/admin';

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
  }

  public getAllQuestions(): Observable<QuestionDto[]> {
    return this.http.get<QuestionDto[]>(`${this.apiUrl}/questions`, { headers: this.getHeaders() });
  }

  public createQuestion(dto: QuestionDto): Observable<QuestionDto> {
    return this.http.post<QuestionDto>(`${this.apiUrl}/questions`, dto, { headers: this.getHeaders() });
  }

  public updateQuestion(id: string, dto: QuestionDto): Observable<QuestionDto> {
    return this.http.put<QuestionDto>(`${this.apiUrl}/questions/${id}`, dto, { headers: this.getHeaders() });
  }

  public deleteQuestion(id: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/questions/${id}`, { headers: this.getHeaders() });
  }

  public addOption(questionId: string, option: AnswerOptionDto): Observable<AnswerOptionDto> {
    return this.http.post<AnswerOptionDto>(`${this.apiUrl}/questions/${questionId}/options`, option, { headers: this.getHeaders() });
  }

  public updateOption(optionId: string, option: AnswerOptionDto): Observable<AnswerOptionDto> {
    return this.http.put<AnswerOptionDto>(`${this.apiUrl}/options/${optionId}`, option, { headers: this.getHeaders() });
  }

  public deleteOption(optionId: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/options/${optionId}`, { headers: this.getHeaders() });
  }
}
