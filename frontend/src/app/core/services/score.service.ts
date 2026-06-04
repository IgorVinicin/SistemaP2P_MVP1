import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { QuestionDto, SubmitAnswersDto, SubmitAnswersResponse, QuestionsResponse } from '../models/question.models';

@Injectable({
  providedIn: 'root'
})
export class ScoreService {
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5141/api/score';

  public getActiveQuestions(): Observable<QuestionsResponse> {
    const token = localStorage.getItem('token');
    const headers = new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
    return this.http.get<QuestionsResponse>(`${this.apiUrl}/questions`, { headers });
  }

  public submitAnswers(dto: SubmitAnswersDto): Observable<SubmitAnswersResponse> {
    const token = localStorage.getItem('token');
    const headers = new HttpHeaders({
      Authorization: `Bearer ${token}`
    });
    return this.http.post<SubmitAnswersResponse>(`${this.apiUrl}/submit`, dto, { headers });
  }
}
