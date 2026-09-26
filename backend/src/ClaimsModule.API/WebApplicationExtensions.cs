using ClaimsModule.API.Middleware;
using ClaimsModule.Infrastructure;
using ClaimsModule.Infrastructure.Jobs;
using ClaimsModule.Infrastructure.Storage;
using Hangfire;
using Microsoft.Extensions.FileProviders;

namespace ClaimsModule.API;

public static class WebApplicationExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseCors(DependencyInjection.FrontendCorsPolicy);

        app.UseHttpsRedirection();

        if (app.Environment.IsDevelopment() && !app.Configuration.UsesAzureBlobStorage())
        {
            app.UseLocalDocumentFiles();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseMiddleware<IdempotencyMiddleware>();

        if (app.Configuration.IsHangfireConfigured())
        {
            app.UseHangfireDashboard();
            RecurringJobs.Register(app.Services.GetRequiredService<IRecurringJobManager>());
        }

        app.MapControllers();

        return app;
    }

    private static void UseLocalDocumentFiles(this WebApplication app)
    {
        var root = LocalFileSystemStorageService.ResolveRoot(app.Configuration, app.Environment.ContentRootPath);
        Directory.CreateDirectory(root);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = LocalFileSystemStorageService.RequestPath
        });
    }
}
