using ClaimsModule.API.Middleware;
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
        }

        app.MapControllers();

        return app;
    }
}
