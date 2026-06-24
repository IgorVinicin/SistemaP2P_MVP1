export interface InvestorWalletDto {
  saldoDisponivel: number;
  saldoComprometido: number;
  saldoInvestido: number;
  rendimentos: number;
  totalAportado: number;
  totalSacado: number;
  perfilInvestimento: string;
  valorProtegido: number;
  valorEmRisco: number;
  valorPerdido: number;
  coberturasRecebidas: number;
  perdasDefinitivas: number;
  coberturasFundo: number;
  rentabilidade: number;
  observacaoAdministrativa: string;
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
