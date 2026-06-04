export interface LoginDto {
  email: string;
  senha: string;
}

export interface RegisterDto {
  nome: string;
  cpf: string;
  telefone: string;
  dataNascimento: string; // ISO format
  email: string;
  senha: string;
  role: string; // 'Cliente' | 'Investidor'
  possuiEmpresa: boolean;
  razaoSocial?: string;
  nomeFantasia?: string;
  cnpj?: string;
  tempoEmpresa?: number;
  faturamentoMensal?: number;
}

export interface AuthResponseDto {
  userId: string;
  email: string;
  role: string;
  status: string; // 'Pendente' | 'EmAnalise' | 'Aprovado' | 'Reprovado'
  token: string;
}
