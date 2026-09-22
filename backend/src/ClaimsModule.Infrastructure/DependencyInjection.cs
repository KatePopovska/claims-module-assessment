using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Infrastructure.Services;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        var provider = configuration["Storage:Provider"] ?? "LocalFileSystem";
        if (provider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IStorageService, AzureBlobStorageService>();
        }
        else
        {
            services.AddScoped<IStorageService, LocalFileSystemStorageService>();
        }

        return services;
    }
}
