using backend.Application.Services;
using backend.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/cashflow")]
    [Authorize(Roles = "Administrador")]
    public class AdminCashFlowController : ControllerBase
    {
        private readonly CashFlowService _cashFlowService;

        public AdminCashFlowController(CashFlowService cashFlowService)
        {
            _cashFlowService = cashFlowService;
        }

        // GET /api/admin/cashflow/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] string period = "hoje", 
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                DateTime? start = startDate;
                DateTime? end = endDate;
                var brasiliaNow = CashFlowService.ToBrasiliaDate(DateTime.UtcNow);

                if (string.IsNullOrEmpty(startDate?.ToString()) && string.IsNullOrEmpty(endDate?.ToString()))
                {
                    DateTime rangeStart = brasiliaNow;
                    DateTime rangeEnd = brasiliaNow;

                    switch (period.ToLower())
                    {
                        case "hoje":
                            rangeStart = brasiliaNow;
                            rangeEnd = brasiliaNow;
                            break;
                        case "ontem":
                            rangeStart = brasiliaNow.AddDays(-1);
                            rangeEnd = brasiliaNow.AddDays(-1);
                            break;
                        case "7dias":
                            rangeStart = brasiliaNow.AddDays(-7);
                            rangeEnd = brasiliaNow;
                            break;
                        case "30dias":
                            rangeStart = brasiliaNow.AddDays(-30);
                            rangeEnd = brasiliaNow;
                            break;
                        case "mesatual":
                            rangeStart = new DateTime(brasiliaNow.Year, brasiliaNow.Month, 1);
                            rangeEnd = brasiliaNow;
                            break;
                    }

                    var (startUtc, _) = CashFlowService.GetBrasiliaDayRangeUtc(rangeStart);
                    var (_, endUtc) = CashFlowService.GetBrasiliaDayRangeUtc(rangeEnd);
                    start = startUtc;
                    end = endUtc;
                }
                else
                {
                    if (startDate.HasValue) start = CashFlowService.GetBrasiliaDayRangeUtc(startDate.Value).StartUtc;
                    if (endDate.HasValue) end = CashFlowService.GetBrasiliaDayRangeUtc(endDate.Value).EndUtc;
                }

                var summary = await _cashFlowService.GetSummaryAsync(start, end);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/cashflow/snapshot
        [HttpPost("snapshot")]
        public async Task<IActionResult> GenerateSnapshot([FromQuery] DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? DateTime.UtcNow;
                var snapshot = await _cashFlowService.GenerateSnapshotAsync(targetDate);
                return Ok(new { message = "Fechamento de caixa diário (Snapshot) gerado com sucesso.", data = snapshot });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/cashflow/snapshots
        [HttpGet("snapshots")]
        public async Task<IActionResult> GetSnapshots([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _cashFlowService.GetSnapshotsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/cashflow/transactions
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] string period = "hoje", 
            [FromQuery] string? search = null, 
            [FromQuery] int? type = null, 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var result = await _cashFlowService.GetTransactionsAsync(period, search, type, page, pageSize, startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/cashflow/transactions/export
        [HttpGet("transactions/export")]
        public async Task<IActionResult> ExportTransactions(
            [FromQuery] string period = "hoje", 
            [FromQuery] string? search = null, 
            [FromQuery] int? type = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var csvBytes = await _cashFlowService.GetTransactionsCsvAsync(period, search, type, startDate, endDate);
                return File(csvBytes, "text/csv", "relatorio-fluxo-caixa.csv");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/cashflow/closing-conference
        [HttpGet("closing-conference")]
        public async Task<IActionResult> GetClosingConference(
            [FromQuery] string period = "hoje",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? status = null,
            [FromQuery] string? tipoEmprestimo = null,
            [FromQuery] string? tomador = null)
        {
            try
            {
                var conference = await _cashFlowService.GetClosingConferenceAsync(period, startDate, endDate, status, tipoEmprestimo, tomador);
                return Ok(conference);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
