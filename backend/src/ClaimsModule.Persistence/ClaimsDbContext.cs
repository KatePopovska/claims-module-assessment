using System.Linq.Expressions;
using System.Reflection;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Reference;
using ClaimsModule.Domain.Reserves;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public class ClaimsDbContext(
    DbContextOptions<ClaimsDbContext> options,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<LossEvent> LossEvents => Set<LossEvent>();
    public DbSet<ClaimParty> ClaimParties => Set<ClaimParty>();
    public DbSet<ClaimRiskObject> ClaimRiskObjects => Set<ClaimRiskObject>();
    public DbSet<ClaimReserveComponent> ClaimReserveComponents => Set<ClaimReserveComponent>();
    public DbSet<ReserveHistory> ReserveHistory => Set<ReserveHistory>();
    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();
    public DbSet<ClaimAuditLog> ClaimAuditLog => Set<ClaimAuditLog>();
    public DbSet<CauseOfLossCode> CauseOfLossCodes => Set<CauseOfLossCode>();
    public DbSet<Policy> Policies => Set<Policy>();

    private static readonly MethodInfo ApplyGlobalQueryFilterMethod =
        typeof(ClaimsDbContext).GetMethod(nameof(ApplyGlobalQueryFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private Guid CurrentOrganisationId => currentUserService.OrganisationId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(ClaimNumberGenerator.SequenceName).StartsAt(1).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(BaseAuditableEntity).IsAssignableFrom(t.ClrType)).ToList())
        {
            ApplyGlobalQueryFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyGlobalQueryFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : BaseAuditableEntity
    {
        Expression<Func<TEntity, bool>> filter = typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity))
            ? e => e.OrganisationId == CurrentOrganisationId && !((ISoftDelete)e).IsDeleted
            : e => e.OrganisationId == CurrentOrganisationId;

        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow();

        MarkParentClaimsAsUpdated();

        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.CreatedAt == default)
                    {
                        entry.Entity.CreatedAt = utcNow;
                    }
                    entry.Entity.UserCreated = currentUserService.UserId;
                    if (entry.Entity.OrganisationId == Guid.Empty)
                    {
                        entry.Entity.OrganisationId = currentUserService.OrganisationId;
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    entry.Entity.UserModified = currentUserService.UserId;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State != EntityState.Deleted)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = utcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private void MarkParentClaimsAsUpdated()
    {
        if (currentUserService.UserId is null)
        {
            return;
        }

        var changedClaimIds = ChangeTracker.Entries<IClaimChild>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.ClaimId)
            .ToHashSet();

        var claimsToTouch = ChangeTracker.Entries<Claim>()
            .Where(e => e.State == EntityState.Unchanged && changedClaimIds.Contains(e.Entity.Id))
            .ToList();

        foreach (var claimEntry in claimsToTouch)
        {
            claimEntry.Property(c => c.UpdatedAt).IsModified = true;
        }
    }
}
