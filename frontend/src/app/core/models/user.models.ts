export interface ProfileDetailDto {
  nome: string;
  cpf: string;
  telefone: string;
  dataNascimento: string;
  scoreAtual: number;
  possuiEmpresa: boolean;
}

export interface CompanyDetailDto {
  razaoSocial: string;
  nomeFantasia: string;
  cnpj: string;
  tempoEmpresa: number;
  faturamentoMensal: number;
}

export interface DocumentDetailDto {
  id: string;
  tipoDocumento: 'Frente' | 'Verso' | 'Selfie' | 'FachadaEmpresa';
  status: 'Pendente' | 'Aprovado' | 'Reprovado';
  createdAt: string;
}

export interface UserAnswerDetailDto {
  pergunta: string;
  respostaSelecionada: string;
  pontuacao: number;
}

export interface UserLoanDetailDto {
  id: string;
  valorSolicitado: number;
  valorTotal: number;
  quantidadeParcelas: number;
  status: string;
  dataSolicitacao: string;
  dataAprovacao?: string;
  dataEncerramento?: string;
}

export interface UserScoreHistoryDto {
  dataAlteracao: string;
  scoreAnterior: number;
  scoreNovo: number;
  motivo: string;
  adminEmail: string;
}

export interface UserDetailDto {
  id: string;
  email: string;
  role: string;
  status: 'Pendente' | 'EmAnalise' | 'Aprovado' | 'Reprovado';
  createdAt: string;
  profile: ProfileDetailDto | null;
  company: CompanyDetailDto | null;
  documents: DocumentDetailDto[];
  answers?: UserAnswerDetailDto[];
  loans?: UserLoanDetailDto[];
  scoreHistory?: UserScoreHistoryDto[];
  rentabilidade?: number;
  observacaoAdministrativa?: string;
}

export interface PendingUserSummaryDto {
  id: string;
  email: string;
  role: string;
  status: string;
  createdAt: string;
  nome: string;
  cpf: string;
  scoreAtual: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
