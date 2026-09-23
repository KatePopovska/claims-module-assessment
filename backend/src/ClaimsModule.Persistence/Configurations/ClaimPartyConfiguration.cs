using ClaimsModule.Domain.Claims;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimPartyConfiguration : AuditableEntityConfiguration<ClaimParty>
{
    public override void Configure(EntityTypeBuilder<ClaimParty> builder)
    {
        base.Configure(builder);

        builder.ToTable("ClaimParties");

        builder.Property(p => p.IsActive).HasDefaultValue(true).IsRequired();

        builder.Property(p => p.PartyRole).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.PartyType).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(p => p.FirstName).HasMaxLength(255);
        builder.Property(p => p.LastName).HasMaxLength(255);
        builder.Property(p => p.CompanyName).HasMaxLength(255);
        builder.Property(p => p.Email).HasMaxLength(255);
        builder.Property(p => p.Phone).HasMaxLength(50);

        builder.HasIndex(p => p.ClaimId);
    }
}
