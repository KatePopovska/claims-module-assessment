using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Application.Reserves.Commands.AdjustReserve;
using ClaimsModule.Application.Reserves.Commands.ApproveReserve;
using ClaimsModule.Application.Reserves.Commands.CreateReserve;
using ClaimsModule.Application.Reserves.Commands.RejectReserve;
using ClaimsModule.Application.Reserves.Commands.RetractReserve;
using ClaimsModule.Application.Reserves.Commands.SetReserveLimitOverride;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reserves;
using FluentValidation;
using NSubstitute;
using static ClaimsModule.Application.UnitTests.TestData;

namespace ClaimsModule.Application.UnitTests.Reserves;

public class ReserveCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IGlPostingScheduler _scheduler = Substitute.For<IGlPostingScheduler>();
    private readonly TimeProvider _time = new FixedTimeProvider(Now);

    private ReserveTransactionSubmitter Submitter(ICurrentUserService user) => new(_unitOfWork, _audit, _scheduler, user);

    private void AssertAudited(AuditEventType eventType, Guid? relatedEntityId = null) =>
        _audit.Received(1).Log(Arg.Any<Claim>(), eventType, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), relatedEntityId ?? Arg.Any<Guid?>(), Arg.Any<string?>());

    private void AssertNotAudited(AuditEventType eventType) =>
        _audit.DidNotReceive().Log(Arg.Any<Claim>(), eventType, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>());

    [Fact]
    public async Task Create_WithinAutoApprovalLimit_IsAutoApprovedAuditedAndPostedAfterSave()
    {
        var claim = Claim();
        var handler = new CreateReserveCommandHandler(RepositoryReturning(claim), Submitter(User("handler", HandlerId)));

        var result = await handler.Handle(new CreateReserveCommand(claim.Id, ReserveComponentType.Indemnity, 5000, "Initial"), CancellationToken.None);

        Assert.Equal(ReserveApprovalStatus.AutoApproved, result.Transaction.ApprovalStatus);
        Assert.Equal(ReserveComponentType.Indemnity, result.Transaction.Component);
        Assert.Empty(result.Warnings);
        AssertAudited(AuditEventType.RESERVE_CREATED, result.Transaction.Id);
        AssertAudited(AuditEventType.RESERVE_AUTO_APPROVED, result.Transaction.Id);
        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _scheduler.Enqueue(Arg.Is<ReserveHistory>(h => h.Id == result.Transaction.Id));
        });
    }

    [Fact]
    public async Task Create_AboveAutoApprovalLimit_IsPendingAndNotPosted()
    {
        var claim = Claim();
        var handler = new CreateReserveCommandHandler(RepositoryReturning(claim), Submitter(User("handler", HandlerId)));

        var result = await handler.Handle(new CreateReserveCommand(claim.Id, ReserveComponentType.Indemnity, 10_000.01m, null), CancellationToken.None);

        Assert.Equal(ReserveApprovalStatus.PendingApproval, result.Transaction.ApprovalStatus);
        AssertAudited(AuditEventType.RESERVE_CREATED);
        AssertNotAudited(AuditEventType.RESERVE_AUTO_APPROVED);
        _scheduler.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public async Task Create_OverTenMillion_ReturnsWarningAndIsNeverAutoApproved()
    {
        var claim = Claim();
        ApprovedTransaction(claim, 9_995_000);
        var handler = new CreateReserveCommandHandler(RepositoryReturning(claim), Submitter(User("handler", HandlerId)));

        var result = await handler.Handle(new CreateReserveCommand(claim.Id, ReserveComponentType.ALAE, 6000, null), CancellationToken.None);

        Assert.Equal(ReserveApprovalStatus.PendingApproval, result.Transaction.ApprovalStatus);
        Assert.Equal([ReserveRules.ReserveLimitWarning], result.Warnings);
        _scheduler.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public async Task Create_WithoutPolicy_ThrowsAndSavesNothing()
    {
        var claim = Claim();
        claim.PolicyId = null;
        var handler = new CreateReserveCommandHandler(RepositoryReturning(claim), Submitter(User("handler", HandlerId)));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new CreateReserveCommand(claim.Id, ReserveComponentType.Indemnity, 5000, null), CancellationToken.None));

        Assert.Empty(claim.ReserveComponents);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Create_UnknownClaim_ThrowsNotFound()
    {
        var handler = new CreateReserveCommandHandler(Substitute.For<IClaimRepository>(), Submitter(User("handler", HandlerId)));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CreateReserveCommand(Guid.NewGuid(), ReserveComponentType.Indemnity, 5000, null), CancellationToken.None));
    }

    [Fact]
    public async Task Adjust_UnknownComponent_ThrowsNotFound()
    {
        var claim = Claim();
        var handler = new AdjustReserveCommandHandler(RepositoryReturning(claim), Submitter(User("handler", HandlerId)));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new AdjustReserveCommand(claim.Id, Guid.NewGuid(), 100, null), CancellationToken.None));
    }

    [Fact]
    public async Task Adjust_AddsTransactionToExistingComponent()
    {
        var claim = Claim();
        var existing = ApprovedTransaction(claim, 5000);
        var handler = new AdjustReserveCommandHandler(RepositoryReturning(claim), Submitter(User("handler", HandlerId)));

        var result = await handler.Handle(new AdjustReserveCommand(claim.Id, existing.ReserveComponentId, -2000, "Recovery"), CancellationToken.None);

        Assert.Equal(ReserveTransactionType.Reverse, result.Transaction.TransactionType);
        Assert.Equal(5000, result.Transaction.PreviousBalance);
        Assert.Equal(3000, result.Transaction.NewBalance);
        Assert.Equal(2, existing.ReserveComponent.History.Count);
    }

    [Fact]
    public async Task Approve_BySupervisor_ApprovesRecalculatesAuditsAndPostsAfterSave()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, 50000);
        var handler = new ApproveReserveCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, _scheduler, User("supervisor", SupervisorId), _time);

        var result = await handler.Handle(new ApproveReserveCommand(claim.Id, pending.Id), CancellationToken.None);

        Assert.Equal(ReserveApprovalStatus.Approved, result.ApprovalStatus);
        Assert.Equal(SupervisorId, result.ApprovedByUserId);
        Assert.Equal(Now, result.ApprovedAt);
        Assert.Equal(50000, pending.ReserveComponent.CurrentAmount);
        AssertAudited(AuditEventType.RESERVE_APPROVED, pending.Id);
        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _scheduler.Enqueue(pending);
        });
    }

    [Fact]
    public async Task Approve_ByHandler_IsRejectedWithoutSavingOrPosting()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, 50000);
        var handler = new ApproveReserveCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, _scheduler, User("handler", HandlerId), _time);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ApproveReserveCommand(claim.Id, pending.Id), CancellationToken.None));

        Assert.Contains(exception.Errors, e => e.PropertyName == "Role");
        Assert.Equal(ReserveApprovalStatus.PendingApproval, pending.ApprovalStatus);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        _scheduler.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public async Task Approve_OwnSubmission_IsRejected()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, 50000, submittedBy: SupervisorId);
        var handler = new ApproveReserveCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, _scheduler, User("supervisor", SupervisorId), _time);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ApproveReserveCommand(claim.Id, pending.Id), CancellationToken.None));

        Assert.Contains(exception.Errors, e => e.ErrorMessage == "Self-approval is not permitted.");
    }

    [Fact]
    public async Task Approve_UnknownTransaction_ThrowsNotFound()
    {
        var claim = Claim();
        var handler = new ApproveReserveCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, _scheduler, User("manager", ManagerId), _time);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ApproveReserveCommand(claim.Id, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Reject_RecordsReasonInAuditOldValueAndDoesNotPost()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, 50000);
        var handler = new RejectReserveCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, User("supervisor", SupervisorId), _time);

        var result = await handler.Handle(new RejectReserveCommand(claim.Id, pending.Id, "Too high"), CancellationToken.None);

        Assert.Equal(ReserveApprovalStatus.Rejected, result.ApprovalStatus);
        Assert.Equal("Too high", result.RejectionReason);
        _audit.Received(1).Log(claim, AuditEventType.RESERVE_REJECTED, Arg.Any<string>(), "Too high", Arg.Any<string?>(), pending.Id, Arg.Any<string?>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _scheduler.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public async Task Retract_BySubmitter_CancelsTransaction()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, 50000, submittedBy: HandlerId);
        var handler = new RetractReserveCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, User("handler", HandlerId));

        var result = await handler.Handle(new RetractReserveCommand(claim.Id, pending.Id), CancellationToken.None);

        Assert.Equal(ReserveApprovalStatus.Cancelled, result.ApprovalStatus);
        AssertAudited(AuditEventType.RESERVE_RETRACTED, pending.Id);
    }

    [Fact]
    public async Task Retract_ByAnotherUser_IsRejected()
    {
        var claim = Claim();
        var pending = PendingTransaction(claim, 50000, submittedBy: HandlerId);
        var handler = new RetractReserveCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, User("supervisor", SupervisorId));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RetractReserveCommand(claim.Id, pending.Id), CancellationToken.None));

        Assert.Equal(ReserveApprovalStatus.PendingApproval, pending.ApprovalStatus);
    }

    [Fact]
    public async Task SetReserveLimitOverride_ByManager_SetsFlagAndAudits()
    {
        var claim = Claim();
        var handler = new SetReserveLimitOverrideCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, User("manager", ManagerId), _time);

        await handler.Handle(new SetReserveLimitOverrideCommand(claim.Id, "Catastrophic loss"), CancellationToken.None);

        Assert.True(claim.ReserveLimitOverride);
        Assert.Equal(ManagerId, claim.ReserveLimitOverrideByUserId);
        _audit.Received(1).Log(claim, AuditEventType.RESERVE_OVERRIDE_SET, Arg.Any<string>(), Arg.Any<string?>(), "Catastrophic loss", Arg.Any<Guid?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task SetReserveLimitOverride_BySupervisor_IsRejected()
    {
        var claim = Claim();
        var handler = new SetReserveLimitOverrideCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit, User("supervisor", SupervisorId), _time);

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new SetReserveLimitOverrideCommand(claim.Id, "Catastrophic loss"), CancellationToken.None));

        Assert.False(claim.ReserveLimitOverride);
    }
}
