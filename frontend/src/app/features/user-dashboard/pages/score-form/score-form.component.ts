import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatRadioModule } from '@angular/material/radio';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { ScoreService } from '../../../../core/services/score.service';
import { QuestionDto } from '../../../../core/models/question.models';

@Component({
  selector: 'app-score-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatRadioModule,
    MatSnackBarModule
  ],
  templateUrl: './score-form.component.html',
  styleUrls: ['./score-form.component.css']
})
export class ScoreFormComponent implements OnInit {
  private api = inject(ScoreService);
  private snackBar = inject(MatSnackBar);

  public questions: QuestionDto[] = [];
  public isLoading = false;
  public isSubmitting = false;
  public isCompleted = false;
  
  // Mapeamento questionId -> optionId
  public answers: { [key: string]: string } = {};

  // Exibição de resultados
  public showResult = false;
  public scoreResult = 0;

  ngOnInit(): void {
    this.loadQuestions();
  }

  public loadQuestions(): void {
    this.isLoading = true;
    this.api.getActiveQuestions().subscribe({
      next: (data) => {
        this.questions = data.questions;
        this.isCompleted = data.questionnaireCompleted;
        this.isLoading = false;
        
        // Mapear respostas pré-existentes se preenchido
        if (this.isCompleted && data.userAnswers) {
          data.userAnswers.forEach(ans => {
            this.answers[ans.questionId] = ans.optionId;
          });
        } else {
          // Inicializar objeto de respostas vazias
          this.questions.forEach(q => {
            this.answers[q.id!] = '';
          });
        }
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar o questionário de score.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public isFormValid(): boolean {
    if (this.questions.length === 0) return false;
    // Todas as perguntas devem possuir uma opção selecionada
    return this.questions.every(q => !!this.answers[q.id!]);
  }

  public submitForm(): void {
    if (!this.isFormValid()) {
      this.snackBar.open('Por favor, responda todas as perguntas.', 'Fechar', { duration: 3000 });
      return;
    }

    this.isSubmitting = true;

    // Monta o DTO de submissão
    const submission = {
      answers: this.questions.map(q => ({
        questionId: q.id!,
        optionId: this.answers[q.id!]
      }))
    };

    this.api.submitAnswers(submission).subscribe({
      next: (res) => {
        this.isSubmitting = false;
        this.scoreResult = res.score;
        this.showResult = true;
        this.snackBar.open('Score calculado com sucesso!', 'Fechar', { duration: 3000 });
      },
      error: (err) => {
        this.isSubmitting = false;
        const msg = err.error?.message || 'Erro ao enviar respostas do score.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }
}
