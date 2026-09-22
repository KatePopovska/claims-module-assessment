namespace ClaimsModule.Application.Common.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string organisationId, string claimId, string sanitisedFileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<string> GetDownloadUrlAsync(string blobPath, CancellationToken cancellationToken = default);
}
