using System;

namespace backend.Application.Services
{
    public interface ILoginAttemptService
    {
        bool IsLockedOut(string identifier, string? ipAddress, out TimeSpan remainingLockout);
        void RegisterFailedAttempt(string identifier, string? ipAddress);
        void ResetAttempts(string identifier, string? ipAddress);
        int GetFailedAttempts(string identifier);
    }
}
