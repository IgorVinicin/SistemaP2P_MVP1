import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SubmitAnswersDto, SubmitAnswersResponse, QuestionsResponse } from '../models/question.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ScoreService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/score`;

  public getActiveQuestions(): Observable<QuestionsResponse> {
    return this.http.get<QuestionsResponse>(`${this.apiUrl}/questions`);
  }

  public submitAnswers(dto: SubmitAnswersDto): Observable<SubmitAnswersResponse> {
    return this.http.post<SubmitAnswersResponse>(`${this.apiUrl}/submit`, dto);
  }
}
