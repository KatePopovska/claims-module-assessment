using ClaimsModule.Application.Documents.Commands.UploadClaimDocument;
using ClaimsModule.Application.Documents.Queries.GetClaimDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/claims/{id:guid}/documents")]
public class DocumentsController(ISender sender) : ControllerBase
{
    private const long MaxRequestBodyBytes = 55L * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClaimDocumentsQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBodyBytes)]
    public async Task<IActionResult> Upload(Guid id, [FromForm] UploadClaimDocumentRequest request, CancellationToken cancellationToken)
    {
        await using var content = request.File.OpenReadStream();

        var result = await sender.Send(new UploadClaimDocumentCommand(id, DecodeFormFileName(request.File.FileName), request.File.Length, content, request.DocumentType, request.Notes), cancellationToken);

        return Created($"api/claims/{id}/documents", result);
    }

    private static string DecodeFormFileName(string fileName) =>
        fileName.Replace("%22", "\"").Replace("%0D", string.Empty).Replace("%0A", string.Empty);
}

public record UploadClaimDocumentRequest(IFormFile File, string? DocumentType, string? Notes);
