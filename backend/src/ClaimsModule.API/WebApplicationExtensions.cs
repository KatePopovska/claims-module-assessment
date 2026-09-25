using ClaimsModule.API.Middleware;
using ClaimsModule.Infrastructure.Jobs;
using Hangfire;

namespace ClaimsModule.API;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(DependencyInjection.AngularDevCorsPolicy);

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Configuration.IsHangfireConfigured())
        {
            app.UseHangfireDashboard();
            RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());
        }

        app.MapControllers();

        return app;
    }
}
