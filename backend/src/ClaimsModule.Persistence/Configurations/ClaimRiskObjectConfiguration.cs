using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimRiskObjectConfiguration : AuditableEntityConfiguration<ClaimRiskObject>
{
    public override void Configure(EntityTypeBuilder<ClaimRiskObject> builder)
    {
        base.Configure(builder);

        builder.ToTable("ClaimRiskObjects");

        builder.ConfigureSoftDelete();

        builder.Property(r => r.AssetType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(r => r.AssetDescription).HasMaxLength(500).IsRequired();
        builder.Property(r => r.DamageDescription).HasMaxLength(2000);
        builder.Property(r => r.IsPrimary).HasDefaultValue(false).IsRequired();
        builder.Property(r => r.AssetReference).HasMaxLength(255);

        builder.HasIndex(r => r.ClaimId);
    }
}
