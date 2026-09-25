using ClaimsModule.Domain.Documents;
using ClaimsModule.Persistence.Configurations.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public class ClaimDocumentConfiguration : AuditableEntityConfiguration<ClaimDocument>
{
    public override void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        base.Configure(builder);

        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.ToTable("ClaimDocuments");

        builder.ConfigureSoftDelete();

        builder.Property(d => d.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.DocumentName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.BlobPath).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(d => d.UploadedAt).HasPrecision(7).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(2000);

        builder.HasIndex(d => d.ClaimId);
    }
}
