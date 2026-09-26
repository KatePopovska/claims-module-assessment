using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Common;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ClaimsModule.API.UnitTests.Persistence;

public class TenantQueryFilterTests
{
    private static ClaimsDbContext CreateContext()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.OrganisationId.Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        var options = new DbContextOptionsBuilder<ClaimsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True")
            .Options;

        return new ClaimsDbContext(options, user, TimeProvider.System);
    }

    [Fact]
    public void EveryAuditableEntity_HasAQueryFilter()
    {
        using var context = CreateContext();

        var unfiltered = context.Model.GetEntityTypes()
            .Where(t => typeof(BaseAuditableEntity).IsAssignableFrom(t.ClrType) && t.GetQueryFilter() is null)
            .Select(t => t.ClrType.Name)
            .ToList();

        Assert.Empty(unfiltered);
    }

    [Fact]
    public void SoftDeletableEntity_IsFilteredByTenantAndSoftDelete()
    {
        using var context = CreateContext();

        var sql = context.Claims.ToQueryString();

        Assert.Contains("[c].[OrganisationId] = @", sql);
        Assert.Contains("[c].[IsDeleted] = CAST(0 AS bit)", sql);
    }

    [Fact]
    public void NonSoftDeletableEntity_IsFilteredByTenantOnly()
    {
        using var context = CreateContext();

        var sql = context.ClaimAuditLog.ToQueryString();

        Assert.Contains("[OrganisationId] = @", sql);
        Assert.DoesNotContain("IsDeleted", sql);
    }
}
