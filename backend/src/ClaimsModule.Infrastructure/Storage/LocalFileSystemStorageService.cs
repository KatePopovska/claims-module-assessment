using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ClaimsModule.Infrastructure.Storage;

public class LocalFileSystemStorageService(IConfiguration configuration) : IStorageService
{
    private readonly string _root = configuration["Storage:LocalPath"] ?? "uploads";

    public async Task<string> UploadAsync(string organisationId, string claimId, string sanitisedFileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(_root, organisationId, claimId);
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, sanitisedFileName);
        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, cancellationToken);

        return Path.Combine(organisationId, claimId, sanitisedFileName).Replace('\\', '/');
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"/uploads/{blobPath}");
    }
}
