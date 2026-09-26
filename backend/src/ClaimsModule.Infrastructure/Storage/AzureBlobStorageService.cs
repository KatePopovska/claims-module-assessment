using System.Text;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ClaimsModule.Infrastructure.Storage;

public sealed class AzureBlobStorageService : IStorageService, IDisposable
{
    private const string ContainerName = "claim-documents";
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DelegationKeyLifetime = TimeSpan.FromDays(1);

    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _container;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _delegationKeyLock = new(1, 1);
    private UserDelegationKey? _delegationKey;

    public AzureBlobStorageService(IConfiguration configuration, TimeProvider timeProvider)
    {
        _serviceClient = CreateServiceClient(configuration);
        _container = _serviceClient.GetBlobContainerClient(ContainerName);
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

    public async Task<DownloadLink> GetDownloadUrlAsync(string blobPath, string downloadFileName, CancellationToken cancellationToken = default)
    {
        var blobClient = _container.GetBlobClient(blobPath);

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

        var url = blobClient.CanGenerateSasUri
            ? blobClient.GenerateSasUri(sasBuilder)
            : new BlobUriBuilder(blobClient.Uri)
            {
                Sas = sasBuilder.ToSasQueryParameters(await GetDelegationKeyAsync(now, cancellationToken), _serviceClient.AccountName)
            }.ToUri();

        return new DownloadLink(url.ToString(), expiresAt);
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) =>
        _container.GetBlobClient(blobPath).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    public void Dispose() => _delegationKeyLock.Dispose();

    private static BlobServiceClient CreateServiceClient(IConfiguration configuration)
    {
        var serviceUri = configuration["Storage:AzureBlob:ServiceUri"];
        if (!string.IsNullOrWhiteSpace(serviceUri))
        {
            return new BlobServiceClient(new Uri(serviceUri), new DefaultAzureCredential());
        }

        var connectionString = configuration["Storage:AzureBlob:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new BlobServiceClient(connectionString);
        }

        throw new InvalidOperationException("Configure Storage:AzureBlob:ServiceUri (managed identity) or Storage:AzureBlob:ConnectionString.");
    }

    private async Task<UserDelegationKey> GetDelegationKeyAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (IsUsable(_delegationKey, now))
        {
            return _delegationKey!;
        }

        await _delegationKeyLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsUsable(_delegationKey, now))
            {
                var response = await _serviceClient.GetUserDelegationKeyAsync(now - ClockSkewAllowance, now + DelegationKeyLifetime, cancellationToken);
                _delegationKey = response.Value;
            }

            return _delegationKey!;
        }
        finally
        {
            _delegationKeyLock.Release();
        }
    }

    private static bool IsUsable(UserDelegationKey? key, DateTimeOffset now) =>
        key is not null && key.SignedExpiresOn >= now + LinkLifetime + ClockSkewAllowance;

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
