using AutoMapper;
using ClaimsModule.Application.Claims.Queries.GetClaimDetail;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands.AddClaimParty;

public class AddClaimPartyCommandHandler(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IAuditLogService auditLogService,
    IMapper mapper) : IRequestHandler<AddClaimPartyCommand, ClaimPartyDto>
{
    public async Task<ClaimPartyDto> Handle(AddClaimPartyCommand request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetWithPartiesAsync(request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Claim), request.ClaimId);

        var party = new ClaimParty
        {
            PartyRole = request.PartyRole,
            PartyType = request.PartyType,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            Email = request.Email,
            Phone = request.Phone,
            Notes = request.Notes
        };

        claim.Parties.Add(party);

        auditLogService.Log(claim, AuditEventType.PARTY_ADDED, $"{party.PartyRole} party {party.GetDisplayName()} added.", newValue: $"{party.PartyRole}: {party.GetDisplayName()}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ClaimPartyDto>(party);
    }
}
