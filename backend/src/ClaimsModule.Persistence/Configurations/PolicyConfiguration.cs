using ClaimsModule.Domain.Reference;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class PolicyConfiguration : AuditableEntityConfiguration<Policy>
{
    public override void Configure(EntityTypeBuilder<Policy> builder)
    {
        base.Configure(builder);

        builder.ToTable("Policies");

        builder.ConfigureSoftDelete();

        builder.Property(p => p.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => new { p.OrganisationId, p.PolicyNumber }).IsUnique();

        builder.Property(p => p.ClientName).HasMaxLength(255).IsRequired();
        builder.Property(p => p.EffectiveDate).HasPrecision(7).IsRequired();
        builder.Property(p => p.ExpirationDate).HasPrecision(7).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.CoverageTypes).HasMaxLength(1000).IsRequired();
    }
}
