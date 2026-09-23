using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClaimsModule.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<ClaimsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ClaimsDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ClaimsDbContext>());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClaimNumberGenerator, ClaimNumberGenerator>();
        services.AddScoped<IClaimRepository, ClaimRepository>();

        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
