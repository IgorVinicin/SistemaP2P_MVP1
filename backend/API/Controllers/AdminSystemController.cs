using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/system")]
    [Authorize(Roles = "Administrador")]
    public class AdminSystemController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminSystemController(AppDbContext context)
        {
            _context = context;
        }

        // POST /api/admin/system/reset-database
        [HttpPost("reset-database")]
        public async Task<IActionResult> ResetDatabase()
        {
            var executionResults = new Dictionary<string, int>();

            try
            {
                // Iniciar transação para garantir atomicidade
                using var transaction = await _context.Database.BeginTransactionAsync();

                // 1. Históricos e Transações sem dependentes
                executionResults.Add("LedgerTransactions", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"LedgerTransactions\";"));
                executionResults.Add("PlatformRevenues", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"PlatformRevenues\";"));
                executionResults.Add("CashFlowSnapshots", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"CashFlowSnapshots\";"));
                executionResults.Add("FinancialEventHistories", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"FinancialEventHistories\";"));
                executionResults.Add("InvestorTransactions", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"InvestorTransactions\";"));
                executionResults.Add("ScoreHistories", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"ScoreHistories\";"));

                // 2. Operações e Crédito
                executionResults.Add("DepositRequests", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"DepositRequests\";"));
                executionResults.Add("WithdrawalRequests", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"WithdrawalRequests\";"));
                executionResults.Add("LoanInstallments", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"LoanInstallments\";"));
                executionResults.Add("Investments", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Investments\";"));
                executionResults.Add("LoanRequests", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"LoanRequests\";"));

                // 3. Grupos e Carteiras
                executionResults.Add("InvestorGroupMembers", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"InvestorGroupMembers\";"));
                executionResults.Add("InvestorGroups", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"InvestorGroups\";"));
                executionResults.Add("InvestorWallets", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"InvestorWallets\";"));

                // 4. Perfil e Documentos
                executionResults.Add("UserAnswers", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"UserAnswers\";"));
                executionResults.Add("Documents", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Documents\";"));
                executionResults.Add("CompanyProfiles", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"CompanyProfiles\";"));
                executionResults.Add("UserProfiles", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"UserProfiles\";"));

                // 5. Usuários (Apenas não administradores)
                executionResults.Add("Users", await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Role\" != 'Administrador';"));

                // 6. Reset do Fundo de Proteção (Configurações Mantidas, Saldos Zerados)
                executionResults.Add("ProtectionFunds", await _context.Database.ExecuteSqlRawAsync("UPDATE \"ProtectionFunds\" SET \"SaldoAtual\" = 0, \"TotalRecebido\" = 0, \"TotalUtilizado\" = 0, \"UltimaAtualizacao\" = CURRENT_TIMESTAMP;"));

                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Banco de dados limpo com sucesso! Pronto para nova massa de testes.",
                    status = "success",
                    registrosRemovidos = executionResults,
                    tabelasPreservadas = new[] 
                    { 
                        "ScoreCreditRules", 
                        "CreditConfigs", 
                        "Questions", 
                        "AnswerOptions", 
                        "ProtectionFunds (Saldos zerados)", 
                        "Users (Apenas Administradores)" 
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Erro ao limpar banco de dados", erro = ex.Message, inner = ex.InnerException?.Message });
            }
        }
    }
}
