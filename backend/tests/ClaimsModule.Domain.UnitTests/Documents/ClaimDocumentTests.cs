using static ClaimsModule.Domain.UnitTests.TestData;

namespace ClaimsModule.Domain.UnitTests.Documents;

public class ClaimDocumentTests
{
    [Fact]
    public void AddDocument_AssignsIdAndAttachesToClaim()
    {
        var claim = Claim();

        var document = claim.AddDocument("Police Report.pdf", "application/pdf", 2048, "PoliceReport", "Filed on site", HandlerId, Now);

        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Equal(claim.Id, document.ClaimId);
        Assert.Same(claim, document.Claim);
        Assert.Contains(document, claim.Documents);
        Assert.Equal("Police Report.pdf", document.DocumentName);
        Assert.Equal("PoliceReport", document.DocumentType);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(2048, document.FileSizeBytes);
        Assert.Equal(HandlerId, document.UploadedByUserId);
        Assert.Equal(Now, document.UploadedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void AddDocument_DefaultsDocumentTypeToOther(string? documentType)
    {
        var document = Claim().AddDocument("a.pdf", "application/pdf", 1, documentType, null, HandlerId, Now);

        Assert.Equal("Other", document.DocumentType);
    }
}
