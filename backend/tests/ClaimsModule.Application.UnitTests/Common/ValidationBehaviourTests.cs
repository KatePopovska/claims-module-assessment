using ClaimsModule.Application.Claims.Commands.RemoveClaimParty;
using ClaimsModule.Application.Common.Behaviours;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.UnitTests.Common;

public class ValidationBehaviourTests
{
    [Fact]
    public async Task Handle_RunsValidatorsForCommandsWithoutAResponse()
    {
        var behaviour = new ValidationBehaviour<RemoveClaimPartyCommand, Unit>([new RemoveClaimPartyCommandValidator()]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behaviour.Handle(new RemoveClaimPartyCommand(Guid.NewGuid(), Guid.Empty), _ =>
            {
                nextCalled = true;
                return Task.FromResult(Unit.Value);
            }, CancellationToken.None));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(RemoveClaimPartyCommand.PartyId));
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Handle_CallsNextWhenValid()
    {
        var behaviour = new ValidationBehaviour<RemoveClaimPartyCommand, Unit>([new RemoveClaimPartyCommandValidator()]);
        var nextCalled = false;

        await behaviour.Handle(new RemoveClaimPartyCommand(Guid.NewGuid(), Guid.NewGuid()), _ =>
        {
            nextCalled = true;
            return Task.FromResult(Unit.Value);
        }, CancellationToken.None);

        Assert.True(nextCalled);
    }
}
