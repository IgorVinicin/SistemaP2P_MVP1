using System;

namespace backend.Application.DTOs
{
    public class AuthResponseDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
