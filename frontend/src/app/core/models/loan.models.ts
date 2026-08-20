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
  solicitaAumentoLimite?: boolean;
  limiteNoMomentoSolicitacao?: number;
  temParcelaAtrasada?: boolean;
  diasAtrasoMax?: number;
  parcelaAtrasadaFormatada?: string;
  historicoPagamentos?: {
    loanRequestId: string;
    numeroParcela: number;
    valorParcela: number;
    valorPago?: number;
    dataVencimento: string;
    dataPagamento?: string;
    diasAtraso: number;
    status: string;
    multa: number;
    jurosMora: number;
  }[];
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

export interface LimitRequest {
  id?: string;
  userId?: string;
  nome?: string;
  cpf?: string;
  score?: number;
  limiteSolicitado: number;
  limiteAtualNoMomento: number;
  status: string; // Pendente, Aprovado, Rejeitado
  dataCriacao: string;
  dataAnalise?: string;
  observacaoAdmin?: string;
}
