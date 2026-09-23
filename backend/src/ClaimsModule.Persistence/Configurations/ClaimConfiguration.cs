using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimConfiguration : AuditableEntityConfiguration<Claim>
{
    public override void Configure(EntityTypeBuilder<Claim> builder)
    {
        base.Configure(builder);

        builder.ToTable("Claims");

        builder.ConfigureSoftDelete();
        builder.ConfigureRowVersion();

        builder.Property(c => c.ClaimNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => new { c.OrganisationId, c.ClaimNumber }).IsUnique();

        builder.Property(c => c.PolicyNumber).HasMaxLength(50);
        builder.Property(c => c.ClientName).HasMaxLength(255);

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.Severity).HasConversion<string>().HasMaxLength(50);

        builder.Property(c => c.ReportedDate).HasPrecision(7).IsRequired();
        builder.Property(c => c.ClosedAt).HasPrecision(7);
        builder.Property(c => c.ClosureReason).HasMaxLength(500);

        builder.Property(c => c.ReserveLimitOverride).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.ReserveLimitOverrideAt).HasPrecision(7);
        builder.Property(c => c.ReserveLimitOverrideReason).HasMaxLength(500);

        builder.HasOne(c => c.Policy)
            .WithMany()
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.LossEvent)
            .WithOne(le => le.Claim)
            .HasForeignKey<LossEvent>(le => le.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Parties)
            .WithOne(p => p.Claim)
            .HasForeignKey(p => p.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.RiskObjects)
            .WithOne(r => r.Claim)
            .HasForeignKey(r => r.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.ReserveComponents)
            .WithOne(r => r.Claim)
            .HasForeignKey(r => r.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Documents)
            .WithOne(d => d.Claim)
            .HasForeignKey(d => d.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.AuditLogEntries)
            .WithOne(a => a.Claim)
            .HasForeignKey(a => a.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
