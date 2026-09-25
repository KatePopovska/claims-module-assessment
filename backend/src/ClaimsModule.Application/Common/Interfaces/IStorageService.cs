namespace ClaimsModule.Application.Common.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string organisationId, string claimId, string sanitisedFileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<DownloadLink> GetDownloadUrlAsync(string blobPath, string downloadFileName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}

public record DownloadLink(string Url, DateTimeOffset? ExpiresAt);
