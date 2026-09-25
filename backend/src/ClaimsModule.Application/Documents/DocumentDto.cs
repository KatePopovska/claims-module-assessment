namespace ClaimsModule.Application.Documents;

public record DocumentDto(Guid Id, string DocumentType, string DocumentName, string ContentType, long FileSizeBytes, DateTimeOffset UploadedAt, Guid? UploadedByUserId, string? Notes, string DownloadUrl, DateTimeOffset? DownloadUrlExpiresAt);
