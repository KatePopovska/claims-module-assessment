using ClaimsModule.Domain.Reference;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class CauseOfLossCodeConfiguration : AuditableEntityConfiguration<CauseOfLossCode>
{
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
    }
}
