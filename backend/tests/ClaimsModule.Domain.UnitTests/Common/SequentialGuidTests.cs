using System.Data.SqlTypes;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.UnitTests.Common;

public class SequentialGuidTests
{
    [Fact]
    public void NewGuid_ProducesUniqueValues()
    {
        var guids = Enumerable.Range(0, 10_000).Select(_ => SequentialGuid.NewGuid()).ToList();

        Assert.Equal(guids.Count, guids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, guids);
    }

    [Fact]
    public void NewGuid_IsAscendingInSqlServerOrder()
    {
        var guids = Enumerable.Range(0, 1_000).Select(_ => SequentialGuid.NewGuid()).ToList();

        var sqlOrdered = guids.OrderBy(g => new SqlGuid(g)).ToList();

        Assert.Equal(guids, sqlOrdered);
    }
}
