using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace ClaimsModule.Application.Documents.Commands.UploadClaimDocument;

public class UploadClaimDocumentCommandHandler(
    IClaimRepository claimRepository,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : IRequestHandler<UploadClaimDocumentCommand, DocumentDto>
{
    public async Task<DocumentDto> Handle(UploadClaimDocumentCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithDocumentsAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var fileName = DocumentPolicy.GetDisplayName(request.FileName);
        var contentType = DocumentPolicy.GetContentType(fileName)
            ?? throw new InvalidOperationException($"File type of {fileName} is not allowed.");

        await EnsureContentMatchesTypeAsync(request.Content, contentType, cancellationToken);

        var document = claim.AddDocument(fileName, contentType, request.Length, request.DocumentType, request.Notes, currentUserService.UserId, timeProvider.GetUtcNow());
        var storedFileName = DocumentPolicy.BuildStoredFileName(document.Id, fileName);

        document.BlobPath = await storageService.UploadAsync(claim.OrganisationId.ToString(), claim.Id.ToString(), storedFileName, request.Content, contentType, cancellationToken);

        auditLogService.Log(claim, AuditEventType.DOCUMENT_UPLOADED, $"{document.DocumentType} document {document.DocumentName} uploaded ({document.FileSizeBytes} bytes).", newValue: document.DocumentName, relatedEntityId: document.Id, relatedEntityType: nameof(ClaimDocument));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storageService.DeleteAsync(document.BlobPath, CancellationToken.None);
            throw;
        }

        var link = await storageService.GetDownloadUrlAsync(document.BlobPath, document.DocumentName, cancellationToken);

        return DocumentProjections.ToDto(document, link);
    }

    private static async Task EnsureContentMatchesTypeAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            throw new InvalidOperationException("Document content stream must be seekable.");
        }

        var header = new byte[DocumentPolicy.SignatureLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = 0;

        if (!DocumentPolicy.MatchesSignature(contentType, header.AsSpan(0, read)))
        {
            throw new ValidationException([new ValidationFailure("File", "The file content does not match its file type.")]);
        }
    }
}
