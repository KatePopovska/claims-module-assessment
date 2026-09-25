using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Common.Interfaces;

public interface IClaimRepository
{
    void Add(Claim claim);
    Task<Claim?> GetByIdAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<Claim?> GetWithPartiesAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<Claim?> GetWithReservesAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<Claim?> GetWithDocumentsAsync(Guid claimId, CancellationToken cancellationToken = default);
}
