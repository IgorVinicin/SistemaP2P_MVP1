export interface InvestorWalletDto {
  saldoDisponivel: number;
  saldoInvestido: number;
  rendimentos: number;
  totalAportado: number;
  totalSacado: number;
  perfilInvestimento: string;
}

export interface InvestorTransactionDto {
  id: string;
  tipo: string;
  valor: number;
  saldoAnterior: number;
  saldoPosterior: number;
  dataMovimentacao: string;
  descricao: string;
  usuarioResponsavel: string;
}
