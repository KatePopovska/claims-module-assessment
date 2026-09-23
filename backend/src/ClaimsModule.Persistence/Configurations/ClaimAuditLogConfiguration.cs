using ClaimsModule.Domain.Audit;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimAuditLogConfiguration : AuditableEntityConfiguration<ClaimAuditLog>
{
    public override void Configure(EntityTypeBuilder<ClaimAuditLog> builder)
    {
        base.Configure(builder);

        builder.ToTable("ClaimAuditLog");

        builder.Property(a => a.EventType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.RelatedEntityType).HasMaxLength(100);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);

        builder.HasIndex(a => a.ClaimId);
    }
}
