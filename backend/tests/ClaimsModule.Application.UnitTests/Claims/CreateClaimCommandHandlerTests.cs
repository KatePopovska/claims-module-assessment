using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reference;
using FluentValidation;
using NSubstitute;
using static ClaimsModule.Application.UnitTests.TestData;

namespace ClaimsModule.Application.UnitTests.Claims;

public class CreateClaimCommandHandlerTests
{
    private static readonly Guid PolicyId = Guid.NewGuid();
    private static readonly Guid DatabaseGeneratedClaimId = Guid.NewGuid();

    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();
    private readonly IClaimRepository _repository = Substitute.For<IClaimRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUnitOfWorkTransaction _transaction = Substitute.For<IUnitOfWorkTransaction>();
    private readonly IClaimNumberGenerator _claimNumbers = Substitute.For<IClaimNumberGenerator>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IGlPostingScheduler _scheduler = Substitute.For<IGlPostingScheduler>();
    private readonly ICurrentUserService _user = User("handler", HandlerId);

    private Claim? _addedClaim;

    public CreateClaimCommandHandlerTests()
    {
        var policies = AsyncQueryable.DbSetOf(new Policy { Id = PolicyId, PolicyNumber = "POL-1", ClientName = "Acme" });
        _context.Policies.Returns(policies);
        _claimNumbers.GenerateAsync(Arg.Any<CancellationToken>()).Returns("CLM-2026-0000042");
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);
        _repository.When(r => r.Add(Arg.Any<Claim>())).Do(call => _addedClaim = call.Arg<Claim>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (_addedClaim is { Id: var id } && id == Guid.Empty)
            {
                _addedClaim.Id = DatabaseGeneratedClaimId;
            }
            return 1;
        });
    }

    private CreateClaimCommandHandler Handler() =>
        new(_context, _repository, _unitOfWork, _claimNumbers, _user,new ReserveTransactionSubmitter(_unitOfWork, _audit, _scheduler, _user), new FixedTimeProvider(Now));

    private static CreateClaimCommand Command(CreateClaimInitialReserveDto? initialReserve = null, Guid? policyId = null) =>
        new(policyId ?? PolicyId, Now.AddDays(-1), "Water leak damaged the kitchen floor.", null, "WATER", null, null,
            [new CreateClaimPartyDto(PartyRole.Claimant, PartyType.Person, "Ann", "Lee", null, null, null)], [], initialReserve);

    [Fact]
    public async Task Create_WithoutInitialReserve_SavesOnceAndCommits()
    {
        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.Equal(DatabaseGeneratedClaimId, result.Id);
        Assert.Equal("CLM-2026-0000042", result.ClaimNumber);
        Assert.Null(result.InitialReserve);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _scheduler.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public async Task Create_WithAutoApprovedReserve_UsesDatabaseGeneratedClaimIdAndPostsAfterCommit()
    {
        var result = await Handler().Handle(Command(new CreateClaimInitialReserveDto(ReserveComponentType.Indemnity, 5000, "Initial")), CancellationToken.None);

        Assert.NotNull(result.InitialReserve);
        Assert.Equal(ReserveApprovalStatus.AutoApproved, result.InitialReserve.Transaction.ApprovalStatus);
        var history = _addedClaim!.ReserveComponents.Single().History.Single();
        Assert.Equal(DatabaseGeneratedClaimId, history.ClaimId);
        Assert.Equal(DatabaseGeneratedClaimId, _addedClaim.ReserveComponents.Single().ClaimId);
        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _transaction.CommitAsync(Arg.Any<CancellationToken>());
            _scheduler.Enqueue(history);
        });
    }

    [Fact]
    public async Task Create_WithReserveAboveAutoApprovalLimit_IsPendingAndNotPosted()
    {
        var result = await Handler().Handle(Command(new CreateClaimInitialReserveDto(ReserveComponentType.Indemnity, 25_000, null)), CancellationToken.None);

        Assert.Equal(ReserveApprovalStatus.PendingApproval, result.InitialReserve!.Transaction.ApprovalStatus);
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _scheduler.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public async Task Create_WhenReserveSaveFails_RollsBackAndDoesNotPost()
    {
        var saves = 0;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (++saves == 2)
            {
                throw new InvalidOperationException("Database failure");
            }
            _addedClaim!.Id = DatabaseGeneratedClaimId;
            return 1;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(Command(new CreateClaimInitialReserveDto(ReserveComponentType.Indemnity, 5000, null)), CancellationToken.None));

        await _transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceiveWithAnyArgs().CommitAsync(default);
        _scheduler.DidNotReceiveWithAnyArgs().Enqueue(default!);
    }

    [Fact]
    public async Task Create_WithInvalidReserveAmount_ThrowsBeforeStartingTransaction()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Handler().Handle(Command(new CreateClaimInitialReserveDto(ReserveComponentType.Indemnity, 0, null)), CancellationToken.None));

        await _unitOfWork.DidNotReceiveWithAnyArgs().BeginTransactionAsync(default);
        _repository.DidNotReceiveWithAnyArgs().Add(default!);
    }
}
