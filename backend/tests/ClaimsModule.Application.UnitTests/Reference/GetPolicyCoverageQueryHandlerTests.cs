using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reference.Queries.GetPolicyCoverage;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reference;
using NSubstitute;

namespace ClaimsModule.Application.UnitTests.Reference;

public class GetPolicyCoverageQueryHandlerTests
{
    private static readonly Policy Policy = new()
    {
        Id = Guid.NewGuid(),
        PolicyNumber = "POL-2024-001001",
        ClientName = "Meridian Transport LLC",
        EffectiveDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ExpirationDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
        Status = PolicyStatus.Active,
        CoverageTypes = "Vehicle, Cargo"
    };

    private static GetPolicyCoverageQueryHandler Handler()
    {
        var context = Substitute.For<IApplicationDbContext>();
        var policies = AsyncQueryable.DbSetOf(Policy);
        context.Policies.Returns(policies);
        return new GetPolicyCoverageQueryHandler(context);
    }

    [Fact]
    public async Task KnownPolicy_ReturnsDetailsAndTrimmedCoverageTypes()
    {
        var result = await Handler().Handle(new GetPolicyCoverageQuery(Policy.Id), CancellationToken.None);

        Assert.Equal((Policy.Id, "POL-2024-001001", PolicyStatus.Active), (result.PolicyId, result.PolicyNumber, result.Status));
        Assert.Equal((Policy.EffectiveDate, Policy.ExpirationDate), (result.EffectiveDate, result.ExpirationDate));
        Assert.Equal(["Vehicle", "Cargo"], result.CoverageTypes);
    }

    [Fact]
    public async Task UnknownPolicy_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(new GetPolicyCoverageQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
