using MediatR;

namespace ClaimsModule.Application.Documents.Queries.GetClaimDocuments;

public record GetClaimDocumentsQuery(Guid ClaimId) : IRequest<IReadOnlyList<DocumentDto>>;
