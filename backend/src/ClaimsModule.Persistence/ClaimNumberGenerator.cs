using System.Data;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ClaimNumberGenerator(ClaimsDbContext context, TimeProvider timeProvider) : IClaimNumberGenerator
{
    public const string SequenceName = "ClaimNumberSequence";

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT NEXT VALUE FOR {SequenceName}";
        var nextValue = (int)(await command.ExecuteScalarAsync(cancellationToken))!;

        var year = timeProvider.GetUtcNow().Year;
        return $"CLM-{year}-{nextValue:D7}";
    }
}
