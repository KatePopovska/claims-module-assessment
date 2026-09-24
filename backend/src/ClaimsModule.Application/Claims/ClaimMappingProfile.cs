using AutoMapper;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Queries.GetClaimDetail;
using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Claims;

public class ClaimMappingProfile : Profile
{
    public ClaimMappingProfile()
    {
        CreateMap<Claim, ClaimCreatedDto>();
        CreateMap<ClaimParty, ClaimPartyDto>();
    }
}
