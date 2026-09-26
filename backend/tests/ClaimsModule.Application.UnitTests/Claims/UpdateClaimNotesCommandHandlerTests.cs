using ClaimsModule.Application.Claims.Commands.UpdateClaimNotes;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Enums;
using NSubstitute;
using static ClaimsModule.Application.UnitTests.TestData;

namespace ClaimsModule.Application.UnitTests.Claims;

public class UpdateClaimNotesCommandHandlerTests
{
    private readonly IClaimRepository _repository = Substitute.For<IClaimRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();

    private UpdateClaimNotesCommandHandler Handler() => new(_repository, _unitOfWork, _audit);

    [Fact]
    public async Task Update_ChangedNotes_UpdatesAndAuditsOldAndNewValues()
    {
        var claim = Claim();
        claim.Notes = "Old note";
        _repository.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);

        await Handler().Handle(new UpdateClaimNotesCommand(claim.Id, "  New note  "), CancellationToken.None);

        Assert.Equal("New note", claim.Notes);
        _audit.Received(1).Log(claim, AuditEventType.CLAIM_NOTES_UPDATED, Arg.Any<string>(), "Old note", "New note", Arg.Any<Guid?>(), Arg.Any<string?>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_BlankNotes_ClearsThem()
    {
        var claim = Claim();
        claim.Notes = "Old note";
        _repository.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);

        await Handler().Handle(new UpdateClaimNotesCommand(claim.Id, "   "), CancellationToken.None);

        Assert.Null(claim.Notes);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_UnchangedNotes_IsANoOp()
    {
        var claim = Claim();
        claim.Notes = "Same";
        _repository.GetByIdAsync(claim.Id, Arg.Any<CancellationToken>()).Returns(claim);

        await Handler().Handle(new UpdateClaimNotesCommand(claim.Id, "Same"), CancellationToken.None);

        _audit.DidNotReceiveWithAnyArgs().Log(default!, default, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Update_UnknownClaim_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(new UpdateClaimNotesCommand(Guid.NewGuid(), "Note"), CancellationToken.None));
    }
}
