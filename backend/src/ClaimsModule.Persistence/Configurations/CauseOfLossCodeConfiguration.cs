using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Reference;
using ClaimsModule.Persistence.Configurations.Common;
using ClaimsModule.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class CauseOfLossCodeConfiguration : AuditableEntityConfiguration<CauseOfLossCode>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override void Configure(EntityTypeBuilder<CauseOfLossCode> builder)
    {
        base.Configure(builder);

        builder.ToTable("CauseOfLossCodes");

        builder.ConfigureSoftDelete();

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.HasAlternateKey(c => c.Code);

        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        builder.Property(c => c.PerilCategory).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.IsActive).HasDefaultValue(true).IsRequired();

        builder.HasData(
            Seed("10000000-0000-0000-0000-000000000001", "COL-FIRE", "Fire", PerilCategory.Property, 1),
            Seed("10000000-0000-0000-0000-000000000002", "COL-FLOOD", "Flood", PerilCategory.Weather, 2),
            Seed("10000000-0000-0000-0000-000000000003", "COL-THEFT", "Theft", PerilCategory.Crime, 3),
            Seed("10000000-0000-0000-0000-000000000004", "COL-VEH-COL", "Vehicle Collision", PerilCategory.Auto, 4),
            Seed("10000000-0000-0000-0000-000000000005", "COL-VEH-COMP", "Vehicle Comprehensive", PerilCategory.Auto, 5),
            Seed("10000000-0000-0000-0000-000000000006", "COL-LIAB", "Third Party Liability", PerilCategory.Liability, 6),
            Seed("10000000-0000-0000-0000-000000000007", "COL-EQUIP", "Equipment Breakdown", PerilCategory.Equipment, 7),
            Seed("10000000-0000-0000-0000-000000000008", "COL-WIND", "Wind / Storm", PerilCategory.Weather, 8),
            Seed("10000000-0000-0000-0000-000000000009", "COL-INJURY", "Bodily Injury", PerilCategory.Liability, 9),
            Seed("10000000-0000-0000-0000-000000000010", "COL-OTHER", "Other / Unknown", PerilCategory.General, 10));
    }

    private static CauseOfLossCode Seed(string id, string code, string name, PerilCategory perilCategory, int sortOrder) => new()
    {
        Id = Guid.Parse(id),
        OrganisationId = SeedConstants.OrganisationId,
        CreatedAt = SeededAt,
        Code = code,
        Name = name,
        PerilCategory = perilCategory,
        IsActive = true,
        SortOrder = sortOrder,
        IsDeleted = false
    };
}
