using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Infrastructure.Jobs;
using ClaimsModule.Infrastructure.Services;
using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClaimsModule.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddScoped<IGlPostingScheduler, HangfireGlPostingScheduler>();

        if (configuration.UsesAzureBlobStorage())
        {
            services.AddSingleton<IStorageService, AzureBlobStorageService>();
        }
        else if (environment.IsDevelopment())
        {
            services.AddScoped<IStorageService, LocalFileSystemStorageService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Document storage must be Azure Blob Storage outside Development (environment: {environment.EnvironmentName}). " +
                "Set Storage:Provider=AzureBlob and Storage:AzureBlob:ServiceUri (managed identity) or Storage:AzureBlob:ConnectionString.");
        }

        return services;
    }

    public static bool UsesAzureBlobStorage(this IConfiguration configuration) =>
        string.Equals(configuration["Storage:Provider"], "AzureBlob", StringComparison.OrdinalIgnoreCase)
        && (!string.IsNullOrWhiteSpace(configuration["Storage:AzureBlob:ServiceUri"])
            || !string.IsNullOrWhiteSpace(configuration["Storage:AzureBlob:ConnectionString"]));
}
