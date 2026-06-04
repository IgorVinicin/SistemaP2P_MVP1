export interface AnswerOptionDto {
  id?: string;
  questionId?: string;
  texto: string;
  pontuacao: number;
}

export interface QuestionDto {
  id?: string;
  titulo: string;
  ativa: boolean;
  options: AnswerOptionDto[];
}

export interface AnswerSubmissionDto {
  questionId: string;
  optionId: string;
}

export interface SubmitAnswersDto {
  answers: AnswerSubmissionDto[];
}

export interface SubmitAnswersResponse {
  userId?: string;
  score: number;
  faixaRisco?: string;
}

export interface QuestionsResponse {
  questions: QuestionDto[];
  questionnaireCompleted: boolean;
  userAnswers: AnswerSubmissionDto[];
}
