using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ReserveHistoryConfiguration : AuditableEntityConfiguration<ReserveHistory>
{
    public override void Configure(EntityTypeBuilder<ReserveHistory> builder)
    {
        base.Configure(builder);

        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.ToTable("ReserveHistory");

        builder.Property(h => h.TransactionType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.Amount).HasPrecision(19, 4).IsRequired();
        builder.Property(h => h.PreviousBalance).HasPrecision(19, 4).IsRequired();
        builder.Property(h => h.NewBalance).HasPrecision(19, 4).IsRequired();

        builder.Property(h => h.ApprovalStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.ApprovedAt).HasPrecision(7);
        builder.Property(h => h.RejectedAt).HasPrecision(7);
        builder.Property(h => h.RejectionReason).HasMaxLength(1000);
        builder.Property(h => h.ChangeReason).HasMaxLength(1000);

        builder.Property(h => h.PostingStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.PostingJobId).HasMaxLength(100);

        builder.Property(h => h.IdempotencyKey).HasMaxLength(255).IsRequired();
        builder.HasIndex(h => h.IdempotencyKey).IsUnique();

        builder.HasIndex(h => h.ClaimId);
        builder.HasIndex(h => new { h.ReserveComponentId, h.ChangeSequence }).IsUnique();
    }
}
