using System;
using System.Threading.Tasks;
using backend.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/yield")]
    [Authorize(Roles = "Administrador")]
    public class AdminYieldController : ControllerBase
    {
        private readonly InvestorYieldService _yieldService;

        public AdminYieldController(InvestorYieldService yieldService)
        {
            _yieldService = yieldService;
        }

        // GET /api/admin/yield/preview
        [HttpGet("preview")]
        public async Task<IActionResult> GetMonthlyYieldPreview([FromQuery] DateTime? targetDate = null)
        {
            try
            {
                var date = targetDate ?? DateTime.UtcNow;
                var preview = await _yieldService.GetMonthlyYieldPreviewAsync(date);
                return Ok(preview);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/yield/process
        [HttpPost("process")]
        public async Task<IActionResult> ProcessMonthlyYield()
        {
            try
            {
                var usuario = User.Identity?.Name ?? "Admin";
                var (processed, totalYield) = await _yieldService.ProcessMonthlyYieldAsync(usuario);

                return Ok(new 
                { 
                    message = "Fechamento de rendimentos processado com sucesso.", 
                    investidoresProcessados = processed, 
                    totalRendimentoDistribuido = totalYield 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/yield/process-daily
        [HttpPost("process-daily")]
        public async Task<IActionResult> ProcessDailyYield([FromQuery] DateTime? targetDate = null)
        {
            try
            {
                var usuario = User.Identity?.Name ?? "Admin";
                var (processed, totalYield) = await _yieldService.ProcessDailyYieldAsync(usuario, targetDate);

                return Ok(new 
                { 
                    message = "Rendimento diário processado com sucesso.", 
                    investidoresProcessados = processed, 
                    totalRendimentoDistribuido = totalYield 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
