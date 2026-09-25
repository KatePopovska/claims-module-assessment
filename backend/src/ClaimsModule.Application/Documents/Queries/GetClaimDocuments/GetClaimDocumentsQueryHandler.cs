using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Documents.Queries.GetClaimDocuments;

public class GetClaimDocumentsQueryHandler(IApplicationDbContext context, IStorageService storageService)
    : IRequestHandler<GetClaimDocumentsQuery, IReadOnlyList<DocumentDto>>
{
    public async Task<IReadOnlyList<DocumentDto>> Handle(GetClaimDocumentsQuery request, CancellationToken cancellationToken)
    {
        if (!await context.Claims.AnyAsync(c => c.Id == request.ClaimId, cancellationToken))
        {
            throw new NotFoundException(nameof(Claim), request.ClaimId);
        }

        var documents = await context.ClaimDocuments
            .AsNoTracking()
            .Where(d => d.ClaimId == request.ClaimId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        var result = new List<DocumentDto>(documents.Count);

        foreach (var document in documents)
        {
            var link = await storageService.GetDownloadUrlAsync(document.BlobPath, document.DocumentName, cancellationToken);
            result.Add(DocumentProjections.ToDto(document, link));
        }

        return result;
    }
}
