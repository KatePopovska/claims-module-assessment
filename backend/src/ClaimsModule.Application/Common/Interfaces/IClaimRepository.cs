using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Common.Interfaces;

public interface IClaimRepository
{
    void Add(Claim claim);
}
