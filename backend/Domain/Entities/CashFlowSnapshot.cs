using System;

namespace backend.Domain.Entities
{
    public class CashFlowSnapshot
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// Data do fechamento/consolidação.
        /// </summary>
        public DateTime SnapshotDate { get; set; }
        
        /// <summary>
        /// Total de dinheiro que entrou no sistema no período (ex: pagamentos de parcelas).
        /// </summary>
        public decimal TotalEntradas { get; set; }
        
        /// <summary>
        /// Total de depósitos reais (dinheiro novo de fora da plataforma).
        /// </summary>
        public decimal TotalDepositosExternos { get; set; }
        
        /// <summary>
        /// Total de saques reais (dinheiro retirado para fora da plataforma).
        /// </summary>
        public decimal TotalSaquesExternos { get; set; }

        /// <summary>
        /// Total de dinheiro que saiu do sistema ou foi repassado no período (ex: repasses, multas pagas, ajustes).
        /// </summary>
        public decimal TotalSaidas { get; set; }
        
        /// <summary>
        /// Saldo operacional consolidado da plataforma.
        /// </summary>
        public decimal SaldoOperacional { get; set; }
        
        /// <summary>
        /// Lucro acumulado / Spread e multas da plataforma.
        /// </summary>
        public decimal ReceitaPlataforma { get; set; }
        
        /// <summary>
        /// Total distribuído para investidores em rendimentos e principal.
        /// </summary>
        public decimal TotalDistribuidoInvestidores { get; set; }
        
        /// <summary>
        /// Total utilizado pelo fundo de proteção para cobrir parcelas.
        /// </summary>
        public decimal TotalCobertoProtectionFund { get; set; }
        
        /// <summary>
        /// Total alocado e emprestado aos tomadores.
        /// </summary>
        public decimal TotalEmprestado { get; set; }
        
        /// <summary>
        /// Total absoluto recebido de pagamentos de tomadores.
        /// </summary>
        public decimal TotalRecebidoParcelas { get; set; }
        
        public DateTime CreatedAt { get; set; }
    }
}
