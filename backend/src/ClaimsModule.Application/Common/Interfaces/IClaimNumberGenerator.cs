namespace ClaimsModule.Application.Common.Interfaces;

public interface IClaimNumberGenerator
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
