using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ClaimsModule.Infrastructure.Storage;

public class AzureBlobStorageService : IStorageService
{
    private const string ContainerName = "claim-documents";
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["Storage:AzureBlob:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:AzureBlob:ConnectionString is not configured.");

        _container = new BlobContainerClient(connectionString, ContainerName);
    }

    public async Task<string> UploadAsync(string organisationId, string claimId, string sanitisedFileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobPath = $"{organisationId}/{claimId}/{sanitisedFileName}";
        var blobClient = _container.GetBlobClient(blobPath);

        await blobClient.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

        return blobPath;
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var blobClient = _container.GetBlobClient(blobPath);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("Blob client cannot generate a SAS URI with the configured credentials.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }
}
