using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using backend.Infrastructure;

namespace temp_query
{
    class Program
    {
        static void Main(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=sistemap2p;Username=postgres;Password=postgres");
            using (var context = new AppDbContext(optionsBuilder.Options))
            {
                var toma20 = context.Users.FirstOrDefault(u => u.Email.Contains("toma20"));
                var inv60 = context.Users.FirstOrDefault(u => u.Email.Contains("inv60"));

                if (toma20 == null || inv60 == null)
                {
                    Console.WriteLine("Usuários não encontrados.");
                    return;
                }

                Console.WriteLine($"Toma20 Id: {toma20.Id}");
                Console.WriteLine($"Inv60 Id: {inv60.Id}");

                var lastLoan = context.LoanRequests
                    .Where(l => l.UserId == toma20.Id && l.Investments.Any(i => i.InvestorId == inv60.Id))
                    .OrderByDescending(l => l.DataSolicitacao)
                    .Include(l => l.Investments)
                    .FirstOrDefault();

                if (lastLoan == null)
                {
                    Console.WriteLine("Nenhum empréstimo encontrado entre eles.");
                    return;
                }

                Console.WriteLine($"Empréstimo ID: {lastLoan.Id}, Valor Solicitado: {lastLoan.ValorSolicitado}");
                var inv = lastLoan.Investments.First(i => i.InvestorId == inv60.Id);
                Console.WriteLine($"Investimento de Inv60: {inv.ValorInvestido}, Rentabilidade: {inv.Rentabilidade}, Juros: {lastLoan.TaxaJuros}");
                
                var installments = context.LoanInstallments.Where(i => i.LoanRequestId == lastLoan.Id && i.Status == "Paga").ToList();
                Console.WriteLine($"Parcelas pagas: {installments.Count}");
                foreach(var inst in installments) {
                    Console.WriteLine($"Parcela {inst.NumeroParcela}: Pago {inst.ValorPago}, Principal {inst.ValorPrincipal}, Juros {inst.ValorJuros}");
                }
                
                var investorWallet = context.InvestorWallets.FirstOrDefault(w => w.Id == inv60.Id);
                Console.WriteLine($"Rendimentos atuais do Inv60: {investorWallet?.Rendimentos}");
            }
        }
    }
}
