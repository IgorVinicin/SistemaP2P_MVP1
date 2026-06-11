import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { QuestionDto, AnswerOptionDto } from '../models/question.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AdminQuestionsService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/admin`;

  public getAllQuestions(): Observable<QuestionDto[]> {
    return this.http.get<QuestionDto[]>(`${this.apiUrl}/questions`);
  }

  public createQuestion(dto: QuestionDto): Observable<QuestionDto> {
    return this.http.post<QuestionDto>(`${this.apiUrl}/questions`, dto);
  }

  public updateQuestion(id: string, dto: QuestionDto): Observable<QuestionDto> {
    return this.http.put<QuestionDto>(`${this.apiUrl}/questions/${id}`, dto);
  }

  public deleteQuestion(id: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/questions/${id}`);
  }

  public addOption(questionId: string, option: AnswerOptionDto): Observable<AnswerOptionDto> {
    return this.http.post<AnswerOptionDto>(`${this.apiUrl}/questions/${questionId}/options`, option);
  }

  public updateOption(optionId: string, option: AnswerOptionDto): Observable<AnswerOptionDto> {
    return this.http.put<AnswerOptionDto>(`${this.apiUrl}/options/${optionId}`, option);
  }

  public deleteOption(optionId: string): Observable<any> {
    return this.http.delete<any>(`${this.apiUrl}/options/${optionId}`);
  }
}
