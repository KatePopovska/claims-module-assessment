using ClaimsModule.Application.Reference.Queries.GetPolicyCoverage;
using ClaimsModule.Application.Reference.Queries.SearchPolicies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize]
[Route("api/policies")]
public class PoliciesController(ISender sender) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery(Name = "q")] string? searchTerm, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SearchPoliciesQuery(searchTerm), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/coverage")]
    public async Task<IActionResult> GetCoverage(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPolicyCoverageQuery(id), cancellationToken);
        return Ok(result);
    }
}
