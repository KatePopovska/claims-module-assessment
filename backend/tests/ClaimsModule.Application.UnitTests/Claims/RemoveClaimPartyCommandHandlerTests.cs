using ClaimsModule.Application.Claims.Commands.RemoveClaimParty;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using NSubstitute;
using static ClaimsModule.Application.UnitTests.TestData;

namespace ClaimsModule.Application.UnitTests.Claims;

public class RemoveClaimPartyCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();

    private static ClaimParty AddClaimant(Claim claim)
    {
        var party = new ClaimParty { Id = Guid.NewGuid(), PartyRole = PartyRole.Claimant, PartyType = PartyType.Person, FirstName = "Bo", LastName = "Kim" };
        claim.Parties.Add(party);
        return party;
    }

    [Fact]
    public async Task Remove_LastActiveClaimant_IsRejected()
    {
        var claim = Claim();
        var only = claim.Parties.Single();
        var handler = new RemoveClaimPartyCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RemoveClaimPartyCommand(claim.Id, only.Id), CancellationToken.None));

        Assert.True(only.IsActive);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Remove_ClaimantWhenAnotherRemains_DeactivatesAndAuditsWithPartyId()
    {
        var claim = Claim();
        var second = AddClaimant(claim);
        var handler = new RemoveClaimPartyCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await handler.Handle(new RemoveClaimPartyCommand(claim.Id, second.Id), CancellationToken.None);

        Assert.False(second.IsActive);
        _audit.Received(1).Log(claim, AuditEventType.PARTY_REMOVED, "Claimant party Bo Kim removed.", "Claimant: Bo Kim", Arg.Any<string?>(), second.Id, nameof(ClaimParty));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_AlreadyInactiveParty_IsANoOp()
    {
        var claim = Claim();
        var inactive = AddClaimant(claim);
        inactive.IsActive = false;
        var handler = new RemoveClaimPartyCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await handler.Handle(new RemoveClaimPartyCommand(claim.Id, inactive.Id), CancellationToken.None);

        _audit.DidNotReceiveWithAnyArgs().Log(default!, default, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Remove_UnknownParty_ThrowsNotFound()
    {
        var claim = Claim();
        var handler = new RemoveClaimPartyCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RemoveClaimPartyCommand(claim.Id, Guid.NewGuid()), CancellationToken.None));
    }
}
