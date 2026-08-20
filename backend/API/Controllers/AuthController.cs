using backend.Application.DTOs;
using backend.Application.Services;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [EnableRateLimiting("AuthRateLimit")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly AppDbContext _context;

        public AuthController(AuthService authService, AppDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                var response = await _authService.RegisterAsync(dto);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var response = await _authService.LoginAsync(dto, ip);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retorna o status atual do usuário autenticado diretamente do banco de dados.
        /// Usado pelo frontend para detectar aprovações sem precisar fazer login novamente.
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            return Ok(new
            {
                userId = user.Id,
                email = user.Email,
                nome = user.Profile?.Nome ?? string.Empty,
                role = user.Role,
                status = user.Status
            });
        }
    }
}
