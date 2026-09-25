using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Documents.Commands.UploadClaimDocument;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using FluentValidation.TestHelper;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static ClaimsModule.Application.UnitTests.TestData;

namespace ClaimsModule.Application.UnitTests.Documents;

public class UploadClaimDocumentTests
{
    private static readonly DateTimeOffset LinkExpiry = Now.AddHours(1);

    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();

    public UploadClaimDocumentTests()
    {
        _storage.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => $"{call.ArgAt<string>(0)}/{call.ArgAt<string>(1)}/{call.ArgAt<string>(2)}");
        _storage.GetDownloadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new DownloadLink($"https://storage.test/{call.ArgAt<string>(0)}?sig=abc", LinkExpiry));
    }

    private UploadClaimDocumentCommandHandler Handler(Claim claim) =>
        new(RepositoryReturning(claim), _storage, _unitOfWork, _audit, User("handler", HandlerId), new FixedTimeProvider(Now));

    private static UploadClaimDocumentCommand Command(Guid claimId, string fileName, byte[] content) =>
        new(claimId, fileName, content.Length, new MemoryStream(content), "PoliceReport", null);

    private static readonly byte[] PdfContent = "%PDF-1.7\nbody"u8.ToArray();

    [Fact]
    public async Task Upload_StoresUnderDocumentIdAuditsAndReturnsSignedLink()
    {
        var claim = Claim();
        claim.OrganisationId = Guid.NewGuid();

        var result = await Handler(claim).Handle(Command(claim.Id, "Police Report.pdf", PdfContent), CancellationToken.None);

        var document = Assert.Single(claim.Documents);
        var expectedPath = $"{claim.OrganisationId}/{claim.Id}/{document.Id:N}-Police_Report.pdf";
        Assert.Equal(expectedPath, document.BlobPath);
        Assert.Equal("application/pdf", document.ContentType);
        await _storage.Received(1).UploadAsync(claim.OrganisationId.ToString(), claim.Id.ToString(), $"{document.Id:N}-Police_Report.pdf", Arg.Any<Stream>(), "application/pdf", Arg.Any<CancellationToken>());
        _audit.Received(1).Log(claim, AuditEventType.DOCUMENT_UPLOADED, Arg.Any<string>(), Arg.Any<string?>(), "Police Report.pdf", document.Id, nameof(ClaimDocument));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _storage.Received(1).GetDownloadUrlAsync(expectedPath, "Police Report.pdf", Arg.Any<CancellationToken>());
        Assert.Equal(document.Id, result.Id);
        Assert.Equal($"https://storage.test/{expectedPath}?sig=abc", result.DownloadUrl);
        Assert.Equal(LinkExpiry, result.DownloadUrlExpiresAt);
    }

    [Fact]
    public async Task Upload_PassesStreamFromTheStartAfterCheckingSignature()
    {
        var claim = Claim();
        long positionAtUpload = -1;
        _storage.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Do<Stream>(s => positionAtUpload = s.Position), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("path");

        await Handler(claim).Handle(Command(claim.Id, "report.pdf", PdfContent), CancellationToken.None);

        Assert.Equal(0, positionAtUpload);
    }

    [Fact]
    public async Task Upload_ContentNotMatchingType_IsRejectedBeforeStoring()
    {
        var claim = Claim();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Handler(claim).Handle(Command(claim.Id, "invoice.pdf", "MZ\u0090\u0000executable"u8.ToArray()), CancellationToken.None));

        Assert.Contains(exception.Errors, e => e.PropertyName == "File");
        Assert.Empty(claim.Documents);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default!, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Upload_SaveFails_DeletesTheStoredFile()
    {
        var claim = Claim();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(claim).Handle(Command(claim.Id, "notes.txt", "hello"u8.ToArray()), CancellationToken.None));

        var document = Assert.Single(claim.Documents);
        await _storage.Received(1).DeleteAsync(document.BlobPath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_FileNameWithPath_KeepsOnlyTheFileName()
    {
        var claim = Claim();

        var result = await Handler(claim).Handle(Command(claim.Id, "../../../evil.txt", "evil"u8.ToArray()), CancellationToken.None);

        Assert.Equal("evil.txt", result.DocumentName);
        Assert.EndsWith("-evil.txt", Assert.Single(claim.Documents).BlobPath);
    }

    [Fact]
    public async Task Upload_UnknownClaim_ThrowsNotFound()
    {
        var handler = new UploadClaimDocumentCommandHandler(Substitute.For<IClaimRepository>(), _storage, _unitOfWork, _audit, User("handler", HandlerId), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(Command(Guid.NewGuid(), "report.pdf", PdfContent), CancellationToken.None));
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("legacy.doc")]
    [InlineData("")]
    public void Validator_RejectsMissingOrDisallowedFileTypes(string fileName)
    {
        var result = new UploadClaimDocumentCommandValidator().TestValidate(new UploadClaimDocumentCommand(Guid.NewGuid(), fileName, 10, Stream.Null, null, null));

        result.ShouldHaveValidationErrorFor(c => c.FileName);
    }

    [Theory]
    [InlineData(0, "The file is empty.")]
    [InlineData(DocumentPolicy.MaxFileSizeBytes + 1, "The file exceeds the 50 MB limit.")]
    public void Validator_EnforcesSizeLimits(long length, string message)
    {
        var result = new UploadClaimDocumentCommandValidator().TestValidate(new UploadClaimDocumentCommand(Guid.NewGuid(), "report.pdf", length, Stream.Null, null, null));

        result.ShouldHaveValidationErrorFor(c => c.Length).WithErrorMessage(message);
    }

    [Fact]
    public void Validator_AcceptsExactlyFiftyMegabytes()
    {
        var result = new UploadClaimDocumentCommandValidator().TestValidate(new UploadClaimDocumentCommand(Guid.NewGuid(), "report.pdf", DocumentPolicy.MaxFileSizeBytes, Stream.Null, "Invoice", "Notes"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
