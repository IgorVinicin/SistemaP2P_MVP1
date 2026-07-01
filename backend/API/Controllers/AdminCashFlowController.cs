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
                var now = DateTime.UtcNow;

                if (string.IsNullOrEmpty(startDate?.ToString()) && string.IsNullOrEmpty(endDate?.ToString()))
                {
                    switch (period.ToLower())
                    {
                        case "hoje":
                            start = now.Date;
                            end = now.Date;
                            break;
                        case "ontem":
                            start = now.Date.AddDays(-1);
                            end = now.Date.AddDays(-1);
                            break;
                        case "7dias":
                            start = now.Date.AddDays(-7);
                            end = now.Date;
                            break;
                        case "30dias":
                            start = now.Date.AddDays(-30);
                            end = now.Date;
                            break;
                        case "mesatual":
                            start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                            end = now.Date;
                            break;
                    }
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
                var targetDate = date ?? DateTime.UtcNow.Date;
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
    }
}
