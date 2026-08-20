using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Application.Services
{
    public class LoginAttemptService : ILoginAttemptService
    {
        private class AttemptRecord
        {
            public int FailedCount { get; set; }
            public DateTime? LockoutEnd { get; set; }
            public DateTime LastAttempt { get; set; }
        }

        private readonly ConcurrentDictionary<string, AttemptRecord> _attempts = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _maxFailedAttempts;
        private readonly TimeSpan _lockoutDuration;
        private readonly ILogger<LoginAttemptService> _logger;

        public LoginAttemptService(IConfiguration configuration, ILogger<LoginAttemptService> logger)
        {
            _logger = logger;
            _maxFailedAttempts = configuration.GetValue<int>("Security:MaxLoginAttempts", 5);
            int lockoutMinutes = configuration.GetValue<int>("Security:LockoutMinutes", 15);
            _lockoutDuration = TimeSpan.FromMinutes(lockoutMinutes);
        }

        public bool IsLockedOut(string identifier, string? ipAddress, out TimeSpan remainingLockout)
        {
            remainingLockout = TimeSpan.Zero;
            var now = DateTime.UtcNow;

            // 1. Checa por identificador (E-mail ou CPF)
            if (!string.IsNullOrWhiteSpace(identifier) && _attempts.TryGetValue(NormalizeKey(identifier), out var userRecord))
            {
                if (userRecord.LockoutEnd.HasValue && userRecord.LockoutEnd.Value > now)
                {
                    remainingLockout = userRecord.LockoutEnd.Value - now;
                    return true;
                }
            }

            // 2. Checa por IP (se configurado)
            if (!string.IsNullOrWhiteSpace(ipAddress) && _attempts.TryGetValue($"ip_{ipAddress}", out var ipRecord))
            {
                if (ipRecord.LockoutEnd.HasValue && ipRecord.LockoutEnd.Value > now)
                {
                    remainingLockout = ipRecord.LockoutEnd.Value - now;
                    return true;
                }
            }

            return false;
        }

        public void RegisterFailedAttempt(string identifier, string? ipAddress)
        {
            var now = DateTime.UtcNow;

            // Registra por identificador
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                var key = NormalizeKey(identifier);
                _attempts.AddOrUpdate(key,
                    _ => new AttemptRecord
                    {
                        FailedCount = 1,
                        LastAttempt = now
                    },
                    (_, record) =>
                    {
                        // Se já passou do tempo de bloqueio anterior, reseta o contador
                        if (record.LockoutEnd.HasValue && record.LockoutEnd.Value <= now)
                        {
                            record.FailedCount = 0;
                            record.LockoutEnd = null;
                        }

                        record.FailedCount++;
                        record.LastAttempt = now;

                        if (record.FailedCount >= _maxFailedAttempts)
                        {
                            record.LockoutEnd = now.Add(_lockoutDuration);
                            _logger.LogWarning("Conta/Identificador {Key} temporariamente bloqueado por {Minutes} minutos após {Count} tentativas incorretas.", key, _lockoutDuration.TotalMinutes, record.FailedCount);
                        }

                        return record;
                    });
            }

            // Registra por IP
            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                var ipKey = $"ip_{ipAddress}";
                _attempts.AddOrUpdate(ipKey,
                    _ => new AttemptRecord
                    {
                        FailedCount = 1,
                        LastAttempt = now
                    },
                    (_, record) =>
                    {
                        if (record.LockoutEnd.HasValue && record.LockoutEnd.Value <= now)
                        {
                            record.FailedCount = 0;
                            record.LockoutEnd = null;
                        }

                        record.FailedCount++;
                        record.LastAttempt = now;

                        // Limite de IP é 2x o limite de conta para evitar denial-of-service em redes compartilhadas (NAT/Proxy)
                        if (record.FailedCount >= _maxFailedAttempts * 2)
                        {
                            record.LockoutEnd = now.Add(_lockoutDuration);
                            _logger.LogWarning("IP {Ip} temporariamente bloqueado após {Count} tentativas incorretas consecutivas.", ipAddress, record.FailedCount);
                        }

                        return record;
                    });
            }
        }

        public void ResetAttempts(string identifier, string? ipAddress)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                _attempts.TryRemove(NormalizeKey(identifier), out _);
            }

            if (!string.IsNullOrWhiteSpace(ipAddress))
            {
                _attempts.TryRemove($"ip_{ipAddress}", out _);
            }
        }

        public int GetFailedAttempts(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return 0;
            if (_attempts.TryGetValue(NormalizeKey(identifier), out var record))
            {
                return record.FailedCount;
            }
            return 0;
        }

        private static string NormalizeKey(string key)
        {
            return key.Trim().ToLower().Replace(".", "").Replace("-", "").Replace("/", "");
        }
    }
}
