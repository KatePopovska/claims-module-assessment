using System.Reflection;
using ClaimsModule.Application.Common.Behaviours;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddAutoMapper(cfg => { }, assembly);

        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
