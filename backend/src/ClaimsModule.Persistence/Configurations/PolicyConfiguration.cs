using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reference;
using ClaimsModule.Persistence.Configurations.Common;
using ClaimsModule.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class PolicyConfiguration : AuditableEntityConfiguration<Policy>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override void Configure(EntityTypeBuilder<Policy> builder)
    {
        base.Configure(builder);

        builder.ToTable("Policies");

        builder.ConfigureSoftDelete();

        builder.Property(p => p.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.PolicyNumber).IsUnique();

        builder.Property(p => p.ClientName).HasMaxLength(255).IsRequired();
        builder.Property(p => p.EffectiveDate).HasPrecision(7).IsRequired();
        builder.Property(p => p.ExpirationDate).HasPrecision(7).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.CoverageTypes).HasMaxLength(1000).IsRequired();

        builder.HasData(
            Seed("20000000-0000-0000-0000-000000000001", "POL-2024-001001", "Meridian Transport LLC",
                new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
                "Vehicle, Cargo", PolicyStatus.Active),
            Seed("20000000-0000-0000-0000-000000000002", "POL-2024-001002", "Harborview Properties Inc",
                new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero),
                "Property, Liability", PolicyStatus.Expired),
            Seed("20000000-0000-0000-0000-000000000003", "POL-2025-002001", "Coastal Builders Group",
                new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 2, 28, 0, 0, 0, TimeSpan.Zero),
                "Property, Equipment", PolicyStatus.Active),
            Seed("20000000-0000-0000-0000-000000000004", "POL-2025-002002", "Stanton Medical Group",
                new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
                "Liability, Vehicle", PolicyStatus.Active),
            Seed("20000000-0000-0000-0000-000000000005", "POL-2023-000099", "Archived Corp",
                new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2021, 12, 31, 0, 0, 0, TimeSpan.Zero),
                "Property", PolicyStatus.Expired));
    }

    private static Policy Seed(
        string id, string policyNumber, string clientName,
        DateTimeOffset effectiveDate, DateTimeOffset expirationDate,
        string coverageTypes, PolicyStatus status) => new()
    {
        Id = Guid.Parse(id),
        OrganisationId = SeedConstants.OrganisationId,
        CreatedAt = SeededAt,
        PolicyNumber = policyNumber,
        ClientName = clientName,
        EffectiveDate = effectiveDate,
        ExpirationDate = expirationDate,
        CoverageTypes = coverageTypes,
        Status = status,
        IsDeleted = false
    };
}
