using ClaimsModule.Application.Claims.Commands.CreateClaim;
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
}
