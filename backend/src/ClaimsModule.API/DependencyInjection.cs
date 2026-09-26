using System.Text.Json.Serialization;
using ClaimsModule.API.Auth;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;

namespace ClaimsModule.API;

public static class DependencyInjection
{
    public const string FrontendCorsPolicy = "Frontend";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(MockAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, MockAuthenticationHandler>(MockAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();

        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddRouting(options => options.LowercaseUrls = true);

        services.AddEndpointsApiExplorer();
        services.AddApiSwaggerGen();

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(FrontendCorsPolicy, policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });

        services.AddHangfireIfConfigured(configuration);

        return services;
    }

    private static IServiceCollection AddApiSwaggerGen(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Claims Module API", Version = "v1" });

            var bearerScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "mock",
                In = ParameterLocation.Header,
                Description = "Mock bearer token: base64-encoded JSON { \"userId\": \"...\", \"role\": \"handler|supervisor|manager\" }.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }                
            };
            options.AddSecurityDefinition("Bearer", bearerScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { { bearerScheme, [] } });
        });

        return services;
    }

    private static IServiceCollection AddHangfireIfConfigured(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.IsHangfireConfigured())
        {
            return services;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions { SqlClientFactory = SqlClientFactory.Instance }));
        services.AddHangfireServer();

        return services;
    }

    public static bool IsHangfireConfigured(this IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"));
}
