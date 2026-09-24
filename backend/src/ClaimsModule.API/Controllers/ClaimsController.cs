using ClaimsModule.Application.Claims.Commands.AddClaimParty;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Claims.Commands.RemoveClaimParty;
using ClaimsModule.Application.Claims.Commands.UpdateClaimStatus;
using ClaimsModule.Application.Claims.Queries.GetClaimAuditLog;
using ClaimsModule.Application.Claims.Queries.GetClaimDetail;
using ClaimsModule.Application.Claims.Queries.ListClaims;
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
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListClaimsQuery query, CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClaimDetailQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> GetAuditLog(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetClaimAuditLogQuery(id, page, pageSize), cancellationToken);
        return Ok(result);
    }

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

    [HttpPost("{id:guid}/parties")]
    public async Task<IActionResult> AddParty(Guid id, AddClaimPartyRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AddClaimPartyCommand(id, request.PartyRole, request.PartyType, request.FirstName, request.LastName, request.CompanyName, request.Email, request.Phone, request.Notes), cancellationToken);

        return Created($"api/claims/{id}", result);
    }

    [HttpDelete("{id:guid}/parties/{partyId:guid}")]
    public async Task<IActionResult> RemoveParty(Guid id, Guid partyId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveClaimPartyCommand(id, partyId), cancellationToken);
        return NoContent();
    }
}

public record UpdateClaimStatusRequest(ClaimStatus TargetStatus, string? Reason, bool AcknowledgeWarnings = false);

public record AddClaimPartyRequest(PartyRole PartyRole, PartyType PartyType, string? FirstName, string? LastName, string? CompanyName, string? Email, string? Phone, string? Notes);
