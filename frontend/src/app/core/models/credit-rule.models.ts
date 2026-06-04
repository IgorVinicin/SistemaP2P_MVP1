export interface ScoreCreditRule {
  id?: string;
  scoreMin: number;
  scoreMax: number;
  limiteEmprestimo: number;
  taxaJuros: number;
  nivelRisco: string;
  ativo: boolean;
}

export interface CreditConfig {
  id?: string;
  scoreMinimoParaSolicitacao: number;
}
