using ClaimsModule.Application.Reserves.Commands.AdjustReserve;
using ClaimsModule.Application.Reserves.Commands.ApproveReserve;
using ClaimsModule.Application.Reserves.Commands.CreateReserve;
using ClaimsModule.Application.Reserves.Commands.RejectReserve;
using ClaimsModule.Application.Reserves.Commands.RetractReserve;
using ClaimsModule.Application.Reserves.Commands.SetReserveLimitOverride;
using ClaimsModule.Application.Reserves.Queries.GetClaimReserves;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/claims/{id:guid}")]
public class ReservesController(ISender sender) : ControllerBase
{
    [HttpGet("reserves")]
    public async Task<IActionResult> GetReserves(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetClaimReservesQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("reserves")]
    public async Task<IActionResult> Create(Guid id, CreateReserveRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateReserveCommand(id, request.Component, request.Amount, request.ChangeReason), cancellationToken);

        return Created($"api/claims/{id}/reserves", result);
    }

    [HttpPut("reserves/{reserveComponentId:guid}")]
    public async Task<IActionResult> Adjust(Guid id, Guid reserveComponentId, AdjustReserveRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new AdjustReserveCommand(id, reserveComponentId, request.Amount, request.ChangeReason), cancellationToken);

        return Created($"api/claims/{id}/reserves", result);
    }

    [HttpPost("reserves/{txnId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, Guid txnId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ApproveReserveCommand(id, txnId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("reserves/{txnId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, Guid txnId, RejectReserveRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RejectReserveCommand(id, txnId, request.RejectionReason), cancellationToken);
        return Ok(result);
    }

    [HttpPost("reserves/{txnId:guid}/retract")]
    public async Task<IActionResult> Retract(Guid id, Guid txnId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RetractReserveCommand(id, txnId), cancellationToken);
        return Ok(result);
    }

    [HttpPut("reserve-limit-override")]
    public async Task<IActionResult> SetReserveLimitOverride(Guid id, SetReserveLimitOverrideRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetReserveLimitOverrideCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }
}

public record CreateReserveRequest(ReserveComponentType Component, decimal Amount, string? ChangeReason);

public record AdjustReserveRequest(decimal Amount, string? ChangeReason);

public record RejectReserveRequest(string RejectionReason);

public record SetReserveLimitOverrideRequest(string Reason);
