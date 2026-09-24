using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Commands.UpdateClaimStatus;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/claims")]
public class ClaimsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateClaimCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return Created($"api/claims/{result.Id}", result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateClaimStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateClaimStatusCommand(id, request.TargetStatus, request.Reason, request.AcknowledgeWarnings), cancellationToken);
        return Ok(result);
    }
}

public record UpdateClaimStatusRequest(ClaimStatus TargetStatus, string? Reason, bool AcknowledgeWarnings = false);
