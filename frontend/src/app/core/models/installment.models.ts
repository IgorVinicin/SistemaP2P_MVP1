export interface LoanInstallmentDto {
  id: string;
  loanRequestId: string;
  numeroParcela: number;
  valorParcela: number;
  valorPrincipal: number;
  valorJuros: number;
  dataVencimento: string;
  dataPagamento?: string;
  status: string; // 'Pendente' | 'Paga' | 'Atrasada' | 'Parcial' | 'Cancelada'
  valorPago: number;
  diasAtraso: number;
  multa: number;
  jurosMora: number;
  createdAt: string;
  updatedAt: string;
}
