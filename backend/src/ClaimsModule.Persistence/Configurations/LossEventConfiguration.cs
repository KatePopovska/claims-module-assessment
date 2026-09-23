using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class LossEventConfiguration : AuditableEntityConfiguration<LossEvent>
{
    public override void Configure(EntityTypeBuilder<LossEvent> builder)
    {
        base.Configure(builder);

        builder.ToTable("LossEvents");

        builder.ConfigureSoftDelete();

        builder.Property(le => le.LossDate).HasPrecision(7).IsRequired();
        builder.Property(le => le.LossDescription).IsRequired();
        builder.Property(le => le.LossLocation).HasMaxLength(500);
        builder.Property(le => le.CauseOfLossCode).HasMaxLength(50).IsRequired();
        builder.Property(le => le.EstimatedLossAmount).HasPrecision(19, 4);
        builder.Property(le => le.ReportDate).HasPrecision(7).IsRequired();
        builder.Property(le => le.PoliceReportNumber).HasMaxLength(50);

        builder.HasIndex(le => le.ClaimId).IsUnique();

        builder.HasOne(le => le.CauseOfLossCodeReference)
            .WithMany()
            .HasForeignKey(le => le.CauseOfLossCode)
            .HasPrincipalKey(c => c.Code)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
