using ClaimsModule.Application.Reference.Queries.GetCauseOfLossCodes;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reference")]
public class ReferenceController(ISender sender) : ControllerBase
{
    [HttpGet("cause-of-loss-codes")]
    public async Task<IActionResult> GetCauseOfLossCodes([FromQuery] PerilCategory? perilCategory, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCauseOfLossCodesQuery(perilCategory), cancellationToken);
        return Ok(result);
    }
}
