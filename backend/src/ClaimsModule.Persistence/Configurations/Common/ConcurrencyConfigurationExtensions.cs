using ClaimsModule.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations.Common;

public static class ConcurrencyConfigurationExtensions
{
    public static void ConfigureRowVersion<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, IHasConcurrencyToken
    {
        builder.Property(e => e.RowVer).IsRowVersion();
    }
}
