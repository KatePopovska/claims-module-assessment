using ClaimsModule.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ClaimsModule.Infrastructure.Storage;

public class LocalFileSystemStorageService(IConfiguration configuration, IHostEnvironment environment, IHttpContextAccessor httpContextAccessor) : IStorageService
{
    public const string RequestPath = "/uploads";

    private readonly string _root = ResolveRoot(configuration, environment.ContentRootPath);

    public static string ResolveRoot(IConfiguration configuration, string contentRootPath) =>
        Path.GetFullPath(configuration["Storage:LocalPath"] ?? "uploads", contentRootPath);

    public async Task<string> UploadAsync(string organisationId, string claimId, string sanitisedFileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var blobPath = $"{organisationId}/{claimId}/{sanitisedFileName}";
        var path = ResolveFilePath(blobPath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        return blobPath;
    }

    public Task<DownloadLink> GetDownloadUrlAsync(string blobPath, string downloadFileName, CancellationToken cancellationToken = default)
    {
        var relativeUrl = $"{RequestPath}/{string.Join('/', blobPath.Split('/').Select(Uri.EscapeDataString))}";
        var request = httpContextAccessor.HttpContext?.Request;
        var url = request is null ? relativeUrl : $"{request.Scheme}://{request.Host}{request.PathBase}{relativeUrl}";

        return Task.FromResult(new DownloadLink(url, null));
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        File.Delete(ResolveFilePath(blobPath));
        return Task.CompletedTask;
    }

    private string ResolveFilePath(string blobPath)
    {
        var path = Path.GetFullPath(Path.Combine(_root, blobPath));

        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Blob path {blobPath} resolves outside the storage root.");
        }

        return path;
    }
}
