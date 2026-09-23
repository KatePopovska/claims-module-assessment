using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ClaimNumberGenerator(ClaimsDbContext context, TimeProvider timeProvider) : IClaimNumberGenerator
{
    public const string SequenceName = "ClaimNumberSequence";

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var nextValue = await context.Database
            .SqlQueryRaw<int>($"SELECT NEXT VALUE FOR {SequenceName}")
            .SingleAsync(cancellationToken);

        var year = timeProvider.GetUtcNow().Year;
        return $"CLM-{year}-{nextValue:D7}";
    }
}
