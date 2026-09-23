using AutoMapper;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands.CreateClaim;

public class CreateClaimCommandHandler(
    IApplicationDbContext context,
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IClaimNumberGenerator claimNumberGenerator,
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    IMapper mapper) : IRequestHandler<CreateClaimCommand, ClaimCreatedDto>
{
    public async Task<ClaimCreatedDto> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var policy = request.PolicyId.HasValue
            ? await context.Policies.FirstOrDefaultAsync(p => p.Id == request.PolicyId.Value, cancellationToken)
            : null;

        var claimNumber = await claimNumberGenerator.GenerateAsync(cancellationToken);

        var claim = new Claim
        {
            ClaimNumber = claimNumber,
            PolicyId = policy?.Id,
            PolicyNumber = policy?.PolicyNumber,
            ClientName = policy?.ClientName,
            Status = ClaimStatus.Draft,
            ReportedDate = timeProvider.GetUtcNow(),
            AssignedHandlerId = currentUserService.UserId,
            LossEvent = new LossEvent
            {
                LossDate = request.LossDate,
                LossDescription = request.LossDescription,
                LossLocation = request.LossLocation,
                CauseOfLossCode = request.CauseOfLossCode,
                EstimatedLossAmount = request.EstimatedLossAmount,
                ReportDate = timeProvider.GetUtcNow(),
                PoliceReportNumber = request.PoliceReportNumber
            }
        };

        foreach (var party in request.Parties)
        {
            claim.Parties.Add(new ClaimParty
            {
                PartyRole = party.PartyRole,
                PartyType = party.PartyType,
                FirstName = party.FirstName,
                LastName = party.LastName,
                CompanyName = party.CompanyName,
                Email = party.Email,
                Phone = party.Phone
            });
        }

        foreach (var riskObject in request.RiskObjects)
        {
            claim.RiskObjects.Add(new ClaimRiskObject
            {
                AssetType = riskObject.AssetType,
                AssetDescription = riskObject.AssetDescription,
                DamageDescription = riskObject.DamageDescription,
                IsPrimary = riskObject.IsPrimary,
                AssetReference = riskObject.AssetReference
            });
        }

        claimRepository.Add(claim);

        auditLogService.Log(claim, AuditEventType.CLAIM_CREATED, $"Claim {claimNumber} created via FNOL intake.");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mapper.Map<ClaimCreatedDto>(claim);
    }
}
