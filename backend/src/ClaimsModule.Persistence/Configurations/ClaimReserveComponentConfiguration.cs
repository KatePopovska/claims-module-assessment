using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimReserveComponentConfiguration : AuditableEntityConfiguration<ClaimReserveComponent>
{
    public override void Configure(EntityTypeBuilder<ClaimReserveComponent> builder)
    {
        base.Configure(builder);

        builder.ToTable("ClaimReserveComponents");

        builder.ConfigureSoftDelete();
        builder.ConfigureRowVersion();

        builder.Property(r => r.Component).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(r => r.CurrentAmount).HasPrecision(19, 4).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(2000);

        builder.HasIndex(r => new { r.ClaimId, r.Component }).IsUnique();

        builder.HasMany(r => r.History)
            .WithOne(h => h.ReserveComponent)
            .HasForeignKey(h => h.ReserveComponentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
