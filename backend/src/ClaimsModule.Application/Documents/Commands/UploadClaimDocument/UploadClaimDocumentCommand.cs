using MediatR;

namespace ClaimsModule.Application.Documents.Commands.UploadClaimDocument;

public record UploadClaimDocumentCommand(Guid ClaimId, string FileName, long Length, Stream Content, string? DocumentType, string? Notes) : IRequest<DocumentDto>;
