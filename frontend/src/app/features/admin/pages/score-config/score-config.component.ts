import { Component, OnInit, TemplateRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';

import { AdminQuestionsService } from '../../../../core/services/admin-questions.service';
import { QuestionDto, AnswerOptionDto } from '../../../../core/models/question.models';

@Component({
  selector: 'app-score-config',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatDialogModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule
  ],
  templateUrl: './score-config.component.html',
  styleUrls: ['./score-config.component.css']
})
export class ScoreConfigComponent implements OnInit {
  private api = inject(AdminQuestionsService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public questions: QuestionDto[] = [];
  public isLoading = false;

  // Dialog references
  @ViewChild('questionDialog') questionDialogTpl!: TemplateRef<any>;
  @ViewChild('optionDialog') optionDialogTpl!: TemplateRef<any>;

  private dialogRef?: MatDialogRef<any>;

  // Forms
  public editingQuestion: QuestionDto | null = null;
  public questionForm = {
    titulo: '',
    ativa: true
  };

  public editingOption: AnswerOptionDto | null = null;
  public currentQuestionIdForOption = '';
  public optionForm = {
    texto: '',
    pontuacao: 0
  };

  ngOnInit(): void {
    this.loadQuestions();
  }

  public loadQuestions(): void {
    this.isLoading = true;
    this.api.getAllQuestions().subscribe({
      next: (data) => {
        this.questions = data;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar perguntas.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public openQuestionDialog(question: QuestionDto | null = null): void {
    this.editingQuestion = question;
    if (question) {
      this.questionForm = {
        titulo: question.titulo,
        ativa: question.ativa
      };
    } else {
      this.questionForm = {
        titulo: '',
        ativa: true
      };
    }

    this.dialogRef = this.dialog.open(this.questionDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  public saveQuestion(): void {
    if (this.editingQuestion) {
      // Update
      const updated: QuestionDto = {
        ...this.editingQuestion,
        titulo: this.questionForm.titulo,
        ativa: this.questionForm.ativa
      };
      this.api.updateQuestion(this.editingQuestion.id!, updated).subscribe({
        next: () => {
          this.snackBar.open('Pergunta atualizada com sucesso!', 'Fechar', { duration: 3000 });
          this.dialogRef?.close();
          this.loadQuestions();
        },
        error: () => {
          this.snackBar.open('Erro ao atualizar pergunta.', 'Fechar', { duration: 4000 });
        }
      });
    } else {
      // Create
      const newQuestion: QuestionDto = {
        titulo: this.questionForm.titulo,
        ativa: this.questionForm.ativa,
        options: []
      };
      this.api.createQuestion(newQuestion).subscribe({
        next: () => {
          this.snackBar.open('Pergunta criada com sucesso!', 'Fechar', { duration: 3000 });
          this.dialogRef?.close();
          this.loadQuestions();
        },
        error: () => {
          this.snackBar.open('Erro ao criar pergunta.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  public toggleQuestionStatus(question: QuestionDto): void {
    const updated: QuestionDto = {
      ...question,
      ativa: !question.ativa
    };
    this.api.updateQuestion(question.id!, updated).subscribe({
      next: () => {
        this.snackBar.open(
          `Pergunta ${updated.ativa ? 'ativada' : 'inativada'} com sucesso!`,
          'Fechar',
          { duration: 3000 }
        );
        this.loadQuestions();
      },
      error: () => {
        this.snackBar.open('Erro ao alterar status da pergunta.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public deleteQuestion(id: string): void {
    if (confirm('Tem certeza que deseja excluir esta pergunta e todas as suas opções?')) {
      this.api.deleteQuestion(id).subscribe({
        next: () => {
          this.snackBar.open('Pergunta excluída com sucesso!', 'Fechar', { duration: 3000 });
          this.loadQuestions();
        },
        error: () => {
          this.snackBar.open('Erro ao excluir pergunta.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  public openOptionDialog(questionId: string, option: AnswerOptionDto | null = null): void {
    this.currentQuestionIdForOption = questionId;
    this.editingOption = option;

    if (option) {
      this.optionForm = {
        texto: option.texto,
        pontuacao: option.pontuacao
      };
    } else {
      this.optionForm = {
        texto: '',
        pontuacao: 0
      };
    }

    this.dialogRef = this.dialog.open(this.optionDialogTpl, {
      width: '500px',
      disableClose: true
    });
  }

  public saveOption(): void {
    if (this.editingOption) {
      // Update option
      const updated: AnswerOptionDto = {
        ...this.editingOption,
        texto: this.optionForm.texto,
        pontuacao: this.optionForm.pontuacao
      };
      this.api.updateOption(this.editingOption.id!, updated).subscribe({
        next: () => {
          this.snackBar.open('Opção atualizada com sucesso!', 'Fechar', { duration: 3000 });
          this.dialogRef?.close();
          this.loadQuestions();
        },
        error: () => {
          this.snackBar.open('Erro ao atualizar opção.', 'Fechar', { duration: 4000 });
        }
      });
    } else {
      // Add option
      const newOption: AnswerOptionDto = {
        texto: this.optionForm.texto,
        pontuacao: this.optionForm.pontuacao
      };
      this.api.addOption(this.currentQuestionIdForOption, newOption).subscribe({
        next: () => {
          this.snackBar.open('Opção adicionada com sucesso!', 'Fechar', { duration: 3000 });
          this.dialogRef?.close();
          this.loadQuestions();
        },
        error: () => {
          this.snackBar.open('Erro ao adicionar opção.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }

  public deleteOption(optionId: string): void {
    if (confirm('Deseja realmente excluir esta opção?')) {
      this.api.deleteOption(optionId).subscribe({
        next: () => {
          this.snackBar.open('Opção excluída com sucesso!', 'Fechar', { duration: 3000 });
          this.loadQuestions();
        },
        error: () => {
          this.snackBar.open('Erro ao excluir opção.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }
}
