import { PagedResult } from './user.models';

export interface BorrowerConsolidatedSummary {
  totalTomadores: number;
  ativos: number;
  inativos: number;
  emAnalise: number;
}

export interface BorrowerConsolidatedItem {
  id: string;
  nome: string;
  cpf: string;
  limiteDisponivel: number;
  quantidadeEmprestimos: number;
  valorTotalSolicitado: number;
  valorTotalAprovado: number;
}

export interface BorrowerConsolidatedReport {
  summary: BorrowerConsolidatedSummary;
  details: PagedResult<BorrowerConsolidatedItem>;
}

export interface InvestorConsolidatedSummary {
  totalInvestidores: number;
  ativos: number;
  emAnalise: number;
}

export interface InvestorConsolidatedItem {
  id: string;
  nome: string;
  cpf: string;
  valorInvestido: number;
  rentabilidade: number;
  saldoAtual: number;
}

export interface InvestorConsolidatedReport {
  summary: InvestorConsolidatedSummary;
  details: PagedResult<InvestorConsolidatedItem>;
}

export interface LoanConsolidatedSummary {
  solicitados: number;
  aprovados: number;
  reprovados: number;
  emAnalise: number;
}

export interface LoanConsolidatedItem {
  id: string;
  tomador: string;
  tipoEmprestimo: string;
  valor: number;
  juros: number;
  parcelas: number;
  status: string;
  dataCriacao: string;
}

export interface LoanConsolidatedReport {
  summary: LoanConsolidatedSummary;
  details: PagedResult<LoanConsolidatedItem>;
}

export interface FinancialEntradas {
  parcelasRecebidas: number;
  jurosRecebidos: number;
  taxasAdministrativas: number;
  totalEntradas: number;
}

export interface FinancialSaidas {
  emprestimosLiberados: number;
  rendimentosPagos: number;
  coberturasAdministrativas: number;
  totalSaidas: number;
}

export interface FinancialResumo {
  totalRecebido: number;
  totalEmprestado: number;
  totalEmAtraso: number;
  totalInadimplente: number;
  resultadoOperacional: number;
}

export interface FinancialConsolidatedReport {
  entradas: FinancialEntradas;
  saidas: FinancialSaidas;
  resumo: FinancialResumo;
}

export interface BorrowerIndividualLoan {
  id: string;
  valorSolicitado: number;
  valorAprovado: number;
  tipoEmprestimo: string;
  juros: number;
  parcelas: number;
  status: string;
}

export interface BorrowerIndividualInstallment {
  numero: number;
  valor: number;
  vencimento: string;
  situacao: string;
  dataPagamento?: string;
}

export interface BorrowerIndividualReport {
  nome: string;
  cpf: string;
  email: string;
  telefone: string;
  empresa: string;
  cnpj: string;
  limiteDisponivel: number;
  limiteUtilizado: number;
  emprestimos: BorrowerIndividualLoan[];
  parcelas: BorrowerIndividualInstallment[];
  totalSolicitado: number;
  totalAprovado: number;
  totalPago: number;
  parcelasEmAberto: number;
  parcelasAtrasadas: number;
}

export interface InvestorIndividualInvestment {
  valorInvestido: number;
  rentabilidade: number;
  dataAprovacao: string;
}

export interface InvestorIndividualTransaction {
  tipo: string;
  valor: number;
  data: string;
  descricao: string;
}

export interface InvestorIndividualReport {
  nome: string;
  cpf: string;
  email: string;
  investimentos: InvestorIndividualInvestment[];
  movimentacoes: InvestorIndividualTransaction[];
  totalInvestido: number;
  totalRecebido: number;
  saldoAtual: number;
}

export interface AllocationAuditItem {
  id: string;
  investorName: string;
  loanId: string;
  valorInvestido: number;
  status: string;
  createdAt: string;
}
