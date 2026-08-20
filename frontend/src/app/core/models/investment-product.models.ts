export interface InvestmentProduct {
  id: string;
  nome: string;
  rentabilidadeMensal: number;
  valorMinimo: number;
  descricao: string;
  condicoes: string;
  risco: string;
  prazoMinimo: string;
  ativo: boolean;
  createdAt: string;
}

export interface CreateInvestmentProductDto {
  nome: string;
  rentabilidadeMensal: number;
  valorMinimo: number;
  descricao: string;
  condicoes: string;
  risco: string;
  prazoMinimo: string;
  ativo: boolean;
}

export interface UpdateInvestmentProductDto {
  nome: string;
  rentabilidadeMensal: number;
  valorMinimo: number;
  descricao: string;
  condicoes: string;
  risco: string;
  prazoMinimo: string;
  ativo: boolean;
}
