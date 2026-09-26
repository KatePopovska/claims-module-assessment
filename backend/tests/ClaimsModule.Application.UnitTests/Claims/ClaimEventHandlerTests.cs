using ClaimsModule.Application.Claims.EventHandlers;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Enums;
using NSubstitute;
using static ClaimsModule.Application.UnitTests.TestData;

namespace ClaimsModule.Application.UnitTests.Claims;

public class ClaimEventHandlerTests
{
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();

    [Fact]
    public async Task ClaimCreated_LogsClaimCreatedAuditEntry()
    {
        var claim = Claim();

        await new ClaimCreatedEventHandler(_audit).Handle(new DomainEventNotification<ClaimCreatedEvent>(new ClaimCreatedEvent(claim, Now)), CancellationToken.None);

        _audit.Received(1).Log(claim, AuditEventType.CLAIM_CREATED, "Claim CLM-2026-0000001 created via FNOL intake.", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task StatusChangedToClosed_LogsStatusChangeWithWarningsAndReasonAndClaimClosed()
    {
        var claim = Claim();
        var domainEvent = new ClaimStatusChangedEvent(claim, ClaimStatus.Open, ClaimStatus.Closed, IsAutomatic: false, "Settled", ["Outside policy period."], Now);

        await new ClaimStatusChangedEventHandler(_audit).Handle(new DomainEventNotification<ClaimStatusChangedEvent>(domainEvent), CancellationToken.None);

        _audit.Received(1).Log(claim, AuditEventType.STATUS_CHANGED, "Status changed from Open to Closed. Acknowledged warnings: Outside policy period. Reason: Settled", "Open", "Closed", Arg.Any<Guid?>(), Arg.Any<string?>());
        _audit.Received(1).Log(claim, AuditEventType.CLAIM_CLOSED, "Claim closed.", Arg.Any<string?>(), "Settled", Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task AutomaticStatusChange_LogsAutomaticDescriptionOnly()
    {
        var claim = Claim();
        var domainEvent = new ClaimStatusChangedEvent(claim, ClaimStatus.Reopened, ClaimStatus.Open, IsAutomatic: true, null, [], Now);

        await new ClaimStatusChangedEventHandler(_audit).Handle(new DomainEventNotification<ClaimStatusChangedEvent>(domainEvent), CancellationToken.None);

        _audit.Received(1).Log(claim, AuditEventType.STATUS_CHANGED, "Status changed automatically from Reopened to Open.", "Reopened", "Open", Arg.Any<Guid?>(), Arg.Any<string?>());
        _audit.ReceivedWithAnyArgs(1).Log(default!, default, default!);
    }

    [Fact]
    public async Task StatusChangedToReopened_LogsClaimReopened()
    {
        var claim = Claim();
        var domainEvent = new ClaimStatusChangedEvent(claim, ClaimStatus.Closed, ClaimStatus.Reopened, IsAutomatic: false, "New evidence", [], Now);

        await new ClaimStatusChangedEventHandler(_audit).Handle(new DomainEventNotification<ClaimStatusChangedEvent>(domainEvent), CancellationToken.None);

        _audit.Received(1).Log(claim, AuditEventType.CLAIM_REOPENED, "Claim reopened.", Arg.Any<string?>(), "New evidence", Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public void DomainEventNotification_For_WrapsEventInTypedNotification()
    {
        var domainEvent = new ClaimCreatedEvent(Claim(), Now);

        var notification = DomainEventNotification.For(domainEvent);

        Assert.Same(domainEvent, Assert.IsType<DomainEventNotification<ClaimCreatedEvent>>(notification).DomainEvent);
    }
}
