using System.Text;

namespace ClaimsModule.Domain.Documents;

public static class DocumentPolicy
{
    public const long MaxFileSizeBytes = 50L * 1024 * 1024;
    public const string DefaultDocumentType = "Other";

    private const int MaxStoredNameLength = 100;

    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv"
    };

    private static readonly Dictionary<string, byte[]> SignaturesByContentType = new()
    {
        ["application/pdf"] = "%PDF"u8.ToArray(),
        ["image/jpeg"] = [0xFF, 0xD8, 0xFF],
        ["image/png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [0x50, 0x4B, 0x03, 0x04],
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [0x50, 0x4B, 0x03, 0x04]
    };

    public static int SignatureLength => SignaturesByContentType.Values.Max(s => s.Length);

    public static string? GetContentType(string fileName) =>
        ContentTypesByExtension.GetValueOrDefault(Path.GetExtension(fileName));

    public static bool MatchesSignature(string contentType, ReadOnlySpan<byte> header) =>
        !SignaturesByContentType.TryGetValue(contentType, out var signature) || header.StartsWith(signature);

    public static string GetDisplayName(string fileName)
    {
        var name = fileName.Replace('\\', '/');
        return name[(name.LastIndexOf('/') + 1)..].Trim();
    }

    public static string SanitiseFileName(string fileName)
    {
        var name = GetDisplayName(fileName);

        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_');
        }

        var sanitised = builder.ToString().TrimStart('.');
        var extension = Path.GetExtension(sanitised);
        var stem = Path.GetFileNameWithoutExtension(sanitised).Trim('_', '.');

        if (stem.Length == 0)
        {
            stem = "document";
        }

        var maxStemLength = Math.Max(1, MaxStoredNameLength - extension.Length);
        return (stem.Length > maxStemLength ? stem[..maxStemLength] : stem) + extension;
    }

    public static string BuildStoredFileName(Guid documentId, string fileName) => $"{documentId:N}-{SanitiseFileName(fileName)}";
}
