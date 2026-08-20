using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace backend.Application.Services
{
    public static class ValidationHelper
    {
        public static bool IsValidCpf(string? cpf)
        {
            if (string.IsNullOrWhiteSpace(cpf)) return false;

            var clean = new string(cpf.Where(char.IsDigit).ToArray());
            if (clean.Length != 11) return false;

            // Bloquear dígitos todos iguais (ex: 111.111.111-11)
            if (clean.Distinct().Count() == 1) return false;

            // 1º dígito verificador
            int[] mult1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum1 = 0;
            for (int i = 0; i < 9; i++)
            {
                sum1 += (clean[i] - '0') * mult1[i];
            }
            int rem1 = sum1 % 11;
            int digit1 = rem1 < 2 ? 0 : 11 - rem1;
            if ((clean[9] - '0') != digit1) return false;

            // 2º dígito verificador
            int[] mult2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum2 = 0;
            for (int i = 0; i < 10; i++)
            {
                sum2 += (clean[i] - '0') * mult2[i];
            }
            int rem2 = sum2 % 11;
            int digit2 = rem2 < 2 ? 0 : 11 - rem2;
            return (clean[10] - '0') == digit2;
        }

        public static bool IsValidCnpj(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj)) return false;

            var clean = new string(cnpj.Where(char.IsDigit).ToArray());
            if (clean.Length != 14) return false;

            // Bloquear dígitos todos iguais
            if (clean.Distinct().Count() == 1) return false;

            int[] mult1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum1 = 0;
            for (int i = 0; i < 12; i++)
            {
                sum1 += (clean[i] - '0') * mult1[i];
            }
            int rem1 = sum1 % 11;
            int digit1 = rem1 < 2 ? 0 : 11 - rem1;
            if ((clean[12] - '0') != digit1) return false;

            int[] mult2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int sum2 = 0;
            for (int i = 0; i < 13; i++)
            {
                sum2 += (clean[i] - '0') * mult2[i];
            }
            int rem2 = sum2 % 11;
            int digit2 = rem2 < 2 ? 0 : 11 - rem2;
            return (clean[13] - '0') == digit2;
        }

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(email.Trim());
        }

        public static bool IsAdult(DateTime birthDate)
        {
            var today = DateTime.UtcNow.Date;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            return age >= 18;
        }
    }
}
