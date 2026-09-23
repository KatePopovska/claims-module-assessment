using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Reference;
using ClaimsModule.Domain.Reserves;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Claim> Claims { get; }
    DbSet<LossEvent> LossEvents { get; }
    DbSet<ClaimParty> ClaimParties { get; }
    DbSet<ClaimRiskObject> ClaimRiskObjects { get; }
    DbSet<ClaimReserveComponent> ClaimReserveComponents { get; }
    DbSet<ReserveHistory> ReserveHistory { get; }
    DbSet<ClaimDocument> ClaimDocuments { get; }
    DbSet<ClaimAuditLog> ClaimAuditLog { get; }
    DbSet<CauseOfLossCode> CauseOfLossCodes { get; }
    DbSet<Policy> Policies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
