using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Documents;

internal static class DocumentProjections
{
    public static DocumentDto ToDto(ClaimDocument document, DownloadLink link) => new(
        document.Id,
        document.DocumentType,
        document.DocumentName,
        document.ContentType,
        document.FileSizeBytes,
        document.UploadedAt,
        document.UploadedByUserId,
        document.Notes,
        link.Url,
        link.ExpiresAt);
}
