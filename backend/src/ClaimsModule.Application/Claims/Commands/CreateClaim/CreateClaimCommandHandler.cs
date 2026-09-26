using ClaimsModule.Application.Common.Extensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reference;
using ClaimsModule.Domain.Reserves;
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
    ReserveTransactionSubmitter reserveSubmitter,
    TimeProvider timeProvider) : IRequestHandler<CreateClaimCommand, ClaimCreatedDto>
{
    public async Task<ClaimCreatedDto> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var policy = request.PolicyId.HasValue
            ? await context.Policies.FirstOrDefaultAsync(p => p.Id == request.PolicyId.Value, cancellationToken)
            : null;

        var claimNumber = await claimNumberGenerator.GenerateAsync(cancellationToken);

        var claim = BuildClaim(request, claimNumber, policy);

        if (request.InitialReserve is { } initialReserve)
        {
            ReserveRules.CheckNewReserve(claim, initialReserve.Component, initialReserve.Amount).ThrowIfAny();
        }

        ReserveHistory? reserveTransaction = null;
        IReadOnlyList<string> reserveWarnings = [];

        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                claimRepository.Add(claim);
                auditLogService.Log(claim, AuditEventType.CLAIM_CREATED, $"Claim {claimNumber} created via FNOL intake.");
                await unitOfWork.SaveChangesAsync(cancellationToken);

                if (request.InitialReserve is { } reserve)
                {
                    var component = claim.OpenReserveComponent(reserve.Component);
                    reserveWarnings = ReserveRules.GetSubmissionWarnings(claim, reserve.Amount);
                    reserveTransaction = reserveSubmitter.Record(claim, component, reserve.Amount, reserve.ChangeReason, reserveWarnings);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        if (reserveTransaction is not null)
        {
            reserveSubmitter.EnqueueGlPostingIfAutoApproved(reserveTransaction);
        }

        var initialReserveResult = reserveTransaction is null ? null : new ReserveSubmissionResultDto(ReserveProjections.ToDto(reserveTransaction), reserveWarnings);

        return new ClaimCreatedDto(claim.Id, claim.ClaimNumber, claim.Status, claim.ReportedDate, initialReserveResult);
    }

    private Claim BuildClaim(CreateClaimCommand request, string claimNumber, Policy? policy)
    {
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

        return claim;
    }
}
