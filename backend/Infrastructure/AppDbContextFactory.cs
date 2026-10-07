using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace backend.Infrastructure;

// Schema tooling never starts hosted jobs, seeds users, or connects to a live gateway.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=p2p_schema_design;Username=postgres;Password=design-only")
            .Options;
        return new AppDbContext(options);
    }
}
