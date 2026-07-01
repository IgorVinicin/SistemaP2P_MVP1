using backend.Application.Services;
using backend.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/revenue")]
    [Authorize(Roles = "Administrador")]
    public class AdminRevenueController : ControllerBase
    {
        private readonly PlatformRevenueService _revenueService;

        public AdminRevenueController(PlatformRevenueService revenueService)
        {
            _revenueService = revenueService;
        }

        // GET /api/admin/revenue/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var metrics = await _revenueService.GetDashboardMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/revenue
        [HttpGet]
        public async Task<IActionResult> GetRevenues(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? period = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var result = await _revenueService.GetRevenuesAsync(page, pageSize, period, startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
