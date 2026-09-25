using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ClaimsModule.Infrastructure.Storage;

public class AzureBlobStorageService : IStorageService
{
    private const string ContainerName = "claim-documents";
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

    private readonly BlobContainerClient _container;
    private readonly TimeProvider _timeProvider;

    public AzureBlobStorageService(IConfiguration configuration, TimeProvider timeProvider)
    {
        var connectionString = configuration["Storage:AzureBlob:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:AzureBlob:ConnectionString is not configured.");

        _container = new BlobContainerClient(connectionString, ContainerName);
        _timeProvider = timeProvider;
    }

    public async Task<string> UploadAsync(string organisationId, string claimId, string sanitisedFileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobPath = $"{organisationId}/{claimId}/{sanitisedFileName}";
        var blobClient = _container.GetBlobClient(blobPath);

        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

        return blobPath;
    }

    public Task<DownloadLink> GetDownloadUrlAsync(string blobPath, string downloadFileName, CancellationToken cancellationToken = default)
    {
        var blobClient = _container.GetBlobClient(blobPath);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("Blob client cannot generate a SAS URI with the configured credentials.");
        }

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now + LinkLifetime;

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b",
            StartsOn = now - ClockSkewAllowance,
            ExpiresOn = expiresAt,
            Protocol = _container.Uri.Scheme == Uri.UriSchemeHttps ? SasProtocol.Https : SasProtocol.HttpsAndHttp,
            ContentDisposition = BuildInlineContentDisposition(downloadFileName)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(new DownloadLink(blobClient.GenerateSasUri(sasBuilder).ToString(), expiresAt));
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) =>
        _container.GetBlobClient(blobPath).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private static string BuildInlineContentDisposition(string fileName)
    {
        var asciiName = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            asciiName.Append(char.IsAscii(character) && character is not ('"' or '\\') && !char.IsControl(character) ? character : '_');
        }

        return $"inline; filename=\"{asciiName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
