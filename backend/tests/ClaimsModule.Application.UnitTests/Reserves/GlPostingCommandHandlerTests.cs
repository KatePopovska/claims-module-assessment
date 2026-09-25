using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.Commands.MarkGlPostingFailed;
using ClaimsModule.Application.Reserves.Commands.PostGlReserveChange;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using NSubstitute;
using static ClaimsModule.Application.UnitTests.TestData;

namespace ClaimsModule.Application.UnitTests.Reserves;

public class GlPostingCommandHandlerTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();

    [Fact]
    public async Task Post_ApprovedTransaction_WritesJournalAndMarksPosted()
    {
        var claim = Claim();
        var transaction = ApprovedTransaction(claim, 5000);
        var handler = new PostGlReserveChangeCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await handler.Handle(new PostGlReserveChangeCommand(transaction.Id, claim.Id, transaction.IdempotencyKey, "42"), CancellationToken.None);

        Assert.Equal(ReservePostingStatus.Posted, transaction.PostingStatus);
        Assert.Equal("42", transaction.PostingJobId);
        _audit.Received(1).Log(
            claim,
            AuditEventType.GL_POSTING_SIMULATED,
            "Simulated GL posting: DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = 5,000.00.",
            Arg.Any<string?>(),
            Arg.Is<string?>(json => json!.Contains("\"amount\":5000") && json.Contains(transaction.IdempotencyKey)),
            transaction.Id,
            "ReserveHistory");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Post_AlreadyPosted_IsANoOp()
    {
        var claim = Claim();
        var transaction = ApprovedTransaction(claim, 5000);
        transaction.MarkPosted("41");
        var handler = new PostGlReserveChangeCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await handler.Handle(new PostGlReserveChangeCommand(transaction.Id, claim.Id, transaction.IdempotencyKey, "42"), CancellationToken.None);

        Assert.Equal("41", transaction.PostingJobId);
        _audit.DidNotReceiveWithAnyArgs().Log(default!, default, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Post_MismatchedIdempotencyKey_Throws()
    {
        var claim = Claim();
        var transaction = ApprovedTransaction(claim, 5000);
        var handler = new PostGlReserveChangeCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PostGlReserveChangeCommand(transaction.Id, claim.Id, "Reserve:other:Change:1", null), CancellationToken.None));

        Assert.Equal(ReservePostingStatus.Pending, transaction.PostingStatus);
    }

    [Fact]
    public async Task Post_PendingTransaction_Throws()
    {
        var claim = Claim();
        var transaction = PendingTransaction(claim, 50000);
        var handler = new PostGlReserveChangeCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PostGlReserveChangeCommand(transaction.Id, claim.Id, transaction.IdempotencyKey, null), CancellationToken.None));
    }

    [Fact]
    public async Task MarkFailed_SetsFailedAndAuditsReason()
    {
        var claim = Claim();
        var transaction = ApprovedTransaction(claim, 5000);
        var handler = new MarkGlPostingFailedCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await handler.Handle(new MarkGlPostingFailedCommand(transaction.Id, claim.Id, "Ledger unavailable"), CancellationToken.None);

        Assert.Equal(ReservePostingStatus.Failed, transaction.PostingStatus);
        _audit.Received(1).Log(claim, AuditEventType.GL_POSTING_FAILED, Arg.Any<string>(), Arg.Any<string?>(), "Ledger unavailable", transaction.Id, "ReserveHistory");
    }

    [Theory]
    [InlineData(ReservePostingStatus.Posted)]
    [InlineData(ReservePostingStatus.Failed)]
    public async Task MarkFailed_AlreadyPostedOrFailed_IsANoOp(ReservePostingStatus status)
    {
        var claim = Claim();
        var transaction = ApprovedTransaction(claim, 5000);
        transaction.PostingStatus = status;
        var handler = new MarkGlPostingFailedCommandHandler(RepositoryReturning(claim), _unitOfWork, _audit);

        await handler.Handle(new MarkGlPostingFailedCommand(transaction.Id, claim.Id, "Ledger unavailable"), CancellationToken.None);

        Assert.Equal(status, transaction.PostingStatus);
        _audit.DidNotReceiveWithAnyArgs().Log(default!, default, default!);
    }
}
