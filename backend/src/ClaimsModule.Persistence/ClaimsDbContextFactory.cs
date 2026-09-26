using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClaimsModule.Persistence;

public class ClaimsDbContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    private const string LocalConnectionString = "Server=(localdb)\\mssqllocaldb;Database=ClaimsModule;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public ClaimsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? LocalConnectionString;

        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ClaimsDbContext).Assembly.FullName))
            .Options;

        return new ClaimsDbContext(options, new DesignTimeCurrentUserService(), TimeProvider.System);
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Role => null;
        public Guid OrganisationId => Guid.Empty;
    }
}
