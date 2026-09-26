using ClaimsModule.Persistence.Configurations.Common;
using ClaimsModule.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class IdempotencyRecordConfiguration : AuditableEntityConfiguration<IdempotencyRecord>
{
    public override void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        base.Configure(builder);

        builder.ToTable("IdempotencyRecords");

        builder.Property(r => r.Key).HasMaxLength(200).IsRequired();
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(255);
        builder.Property(r => r.Location).HasMaxLength(2000);

        builder.HasIndex(r => new { r.OrganisationId, r.Key }).IsUnique();
        builder.HasIndex(r => r.CreatedAt);
    }
}
