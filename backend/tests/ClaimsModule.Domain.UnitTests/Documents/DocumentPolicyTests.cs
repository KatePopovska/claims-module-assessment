using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Domain.UnitTests.Documents;

public class DocumentPolicyTests
{
    [Theory]
    [InlineData("report.pdf", "application/pdf")]
    [InlineData("photo.JPG", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("scan.png", "image/png")]
    [InlineData("letter.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("estimate.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("items.csv", "text/csv")]
    public void GetContentType_MapsAllowedExtensions(string fileName, string expected)
    {
        Assert.Equal(expected, DocumentPolicy.GetContentType(fileName));
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("page.html")]
    [InlineData("legacy.doc")]
    [InlineData("archive.zip")]
    [InlineData("no-extension")]
    [InlineData("report.pdf.exe")]
    public void GetContentType_ReturnsNullForDisallowedTypes(string fileName)
    {
        Assert.Null(DocumentPolicy.GetContentType(fileName));
    }

    [Fact]
    public void MatchesSignature_AcceptsRealFileHeaders()
    {
        Assert.True(DocumentPolicy.MatchesSignature("application/pdf", "%PDF-1.7\n"u8));
        Assert.True(DocumentPolicy.MatchesSignature("image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]));
        Assert.True(DocumentPolicy.MatchesSignature("image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.True(DocumentPolicy.MatchesSignature("application/vnd.openxmlformats-officedocument.wordprocessingml.document", "PK\u0003\u0004rest"u8));
    }

    [Fact]
    public void MatchesSignature_RejectsMismatchedOrTruncatedContent()
    {
        Assert.False(DocumentPolicy.MatchesSignature("application/pdf", "MZ\u0090\u0000"u8));
        Assert.False(DocumentPolicy.MatchesSignature("image/png", [0x89, 0x50]));
        Assert.False(DocumentPolicy.MatchesSignature("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "not a zip"u8));
    }

    [Fact]
    public void MatchesSignature_TextTypesHaveNoSignature()
    {
        Assert.True(DocumentPolicy.MatchesSignature("text/plain", "anything"u8));
        Assert.True(DocumentPolicy.MatchesSignature("text/csv", "a,b,c"u8));
    }

    [Theory]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData(@"..\..\windows\system32\config.csv", "config.csv")]
    [InlineData("C:\\Users\\ann\\Desktop\\claim form.pdf", "claim_form.pdf")]
    [InlineData("invoice (final) #2.pdf", "invoice__final___2.pdf")]
    [InlineData("zażółć.pdf", "za.pdf")]
    [InlineData(".hidden.txt", "hidden.txt")]
    [InlineData("***.pdf", "document.pdf")]
    public void SanitiseFileName_RemovesPathsAndUnsafeCharacters(string input, string expected)
    {
        Assert.Equal(expected, DocumentPolicy.SanitiseFileName(input));
    }

    [Theory]
    [InlineData("../../../evil.txt", "evil.txt")]
    [InlineData(@"C:\Users\ann\Desktop\Claim Form (1).pdf", "Claim Form (1).pdf")]
    [InlineData("zażółć gęślą.pdf", "zażółć gęślą.pdf")]
    public void GetDisplayName_StripsPathButKeepsOriginalCharacters(string input, string expected)
    {
        Assert.Equal(expected, DocumentPolicy.GetDisplayName(input));
    }

    [Fact]
    public void SanitiseFileName_TruncatesLongNamesButKeepsExtension()
    {
        var sanitised = DocumentPolicy.SanitiseFileName(new string('a', 300) + ".pdf");

        Assert.Equal(100, sanitised.Length);
        Assert.EndsWith(".pdf", sanitised);
    }

    [Fact]
    public void BuildStoredFileName_PrefixesDocumentIdSoNamesNeverCollide()
    {
        var id = Guid.Parse("0a1b2c3d-0000-0000-0000-000000000001");

        Assert.Equal("0a1b2c3d000000000000000000000001-invoice.pdf", DocumentPolicy.BuildStoredFileName(id, "invoice.pdf"));
        Assert.NotEqual(DocumentPolicy.BuildStoredFileName(Guid.NewGuid(), "invoice.pdf"), DocumentPolicy.BuildStoredFileName(Guid.NewGuid(), "invoice.pdf"));
    }
}
