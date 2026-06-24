export interface LoanRequest {
  id?: string;
  userId?: string;
  valorSolicitado: number;
  quantidadeParcelas: number;
  status: string; // Pendente, Aprovado, Reprovado, EmCaptacao, Financiado, EmPagamento, Finalizado, Inadimplente
  taxaJuros: number;
  valorTotal?: number;
  risco: string;
  finalidade: string;
  observacoes: string;
  dataCriacao: string;
  dataSolicitacao?: string;
  dataAprovacao?: string;
  observacaoAdmin?: string;
  tipoEmprestimo?: string;
  periodicidade?: string;
  outrosEmprestimos?: {
    id: string;
    valorSolicitado: number;
    status: string;
    dataSolicitacao: string;
  }[];
  nome?: string;
  cpf?: string;
  score?: number;
  userProfile?: {
    nome: string;
    cpf: string;
    telefone: string;
    dataNascimento: string;
    score: number;
  };
}

export interface LoanEligibility {
  scoreAtual: number;
  risco: string;
  taxaJuros: number;
  limiteDisponivel: number;
  podeSolicitar: boolean;
  mensagemBlock?: string;
  parcelasPagas?: number;
  minimoExigido?: number;
  temEmprestimoAtivo?: boolean;
}

export interface AdminReproveLoan {
  observacao: string;
}
