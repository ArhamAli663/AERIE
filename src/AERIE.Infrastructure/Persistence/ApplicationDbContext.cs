namespace AERIE.Infrastructure.Persistence;

using AERIE.Application.Common.Interfaces;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Central EF Core DbContext with:
/// - Multi-tenant global query filters (set via ITenantContext injected at request scope)
/// - Soft-delete filters
/// - Concurrency token for MarketplaceListing
/// - Row-version conflict detection
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    // ─── Shared / Reference ─────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public new DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Breed> Breeds => Set<Breed>();
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<PoultryShop> PoultryShops => Set<PoultryShop>();

    // ─── Farmer Module ───────────────────────────────────────────────────────
    public DbSet<Farm> Farms => Set<Farm>();
    public DbSet<PoultryHouse> PoultryHouses => Set<PoultryHouse>();
    public DbSet<FarmStaff> FarmStaff => Set<FarmStaff>();
    public DbSet<FlockBatch> FlockBatches => Set<FlockBatch>();
    public DbSet<ChickPlacement> ChickPlacements => Set<ChickPlacement>();
    public DbSet<DailyFlockRecord> DailyFlockRecords => Set<DailyFlockRecord>();
    public DbSet<MortalityRecord> MortalityRecords => Set<MortalityRecord>();
    public DbSet<ProductionMetric> ProductionMetrics => Set<ProductionMetric>();
    public DbSet<FeedInventory> FeedInventories => Set<FeedInventory>();

    // ─── Marketplace ─────────────────────────────────────────────────────────
    public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();

    // ─── Broker Module ───────────────────────────────────────────────────────
    public DbSet<DealProposal> DealProposals => Set<DealProposal>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DealStatusHistory> DealStatusHistories => Set<DealStatusHistory>();
    public DbSet<TransportRecord> TransportRecords => Set<TransportRecord>();

    // ─── Financial ───────────────────────────────────────────────────────────
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();
    public DbSet<DealExpense> DealExpenses => Set<DealExpense>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    // ─── System ──────────────────────────────────────────────────────────────
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ── Rename Identity tables to snake_case / shorter names ──────────
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<ApplicationRole>().ToTable("roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");

        // ── Tenant ───────────────────────────────────────────────────────
        builder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasIndex(t => t.Slug).IsUnique();
        });

        // ── ApplicationUser ───────────────────────────────────────────────
        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Tenant)
             .WithMany(t => t.Users)
             .HasForeignKey(u => u.TenantId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Breeds (global reference, no tenant filter) ───────────────────
        builder.Entity<Breed>(e => e.ToTable("breeds"));

        // ── Farm (tenant-scoped) ──────────────────────────────────────────
        builder.Entity<Farm>(e =>
        {
            e.ToTable("farms");
            e.HasQueryFilter(f => f.TenantId == _tenantContext.TenantId && !f.IsDeleted);
            e.HasIndex(f => f.TenantId);
            e.Property(f => f.Name).HasMaxLength(200);
        });

        // ── PoultryHouse ──────────────────────────────────────────────────
        builder.Entity<PoultryHouse>(e =>
        {
            e.ToTable("poultry_houses");
            e.HasQueryFilter(h => h.TenantId == _tenantContext.TenantId && !h.IsDeleted);
            e.HasOne(h => h.Farm).WithMany(f => f.PoultryHouses)
             .HasForeignKey(h => h.FarmId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── FarmStaff ─────────────────────────────────────────────────────
        builder.Entity<FarmStaff>(e =>
        {
            e.ToTable("farm_staff");
            e.HasQueryFilter(s => s.TenantId == _tenantContext.TenantId);
        });

        // ── FlockBatch ────────────────────────────────────────────────────
        builder.Entity<FlockBatch>(e =>
        {
            e.ToTable("flock_batches");
            e.HasQueryFilter(b => b.TenantId == _tenantContext.TenantId && !b.IsDeleted);
            e.HasIndex(b => new { b.TenantId, b.BatchCode }).IsUnique();
            e.HasOne(b => b.Farm).WithMany().HasForeignKey(b => b.FarmId);
            e.HasOne(b => b.PoultryHouse).WithMany(h => h.Batches).HasForeignKey(b => b.PoultryHouseId);
            e.HasOne(b => b.Breed).WithMany(br => br.Batches).HasForeignKey(b => b.BreedId);
            e.Property(b => b.CostPerChick).HasColumnType("numeric(10,4)");
        });

        // ── ChickPlacement ────────────────────────────────────────────────
        builder.Entity<ChickPlacement>(e =>
        {
            e.ToTable("chick_placements");
            e.HasQueryFilter(cp => cp.TenantId == _tenantContext.TenantId);
            e.Property(cp => cp.CostPerChick).HasColumnType("numeric(10,4)");
        });

        // ── DailyFlockRecord ──────────────────────────────────────────────
        builder.Entity<DailyFlockRecord>(e =>
        {
            e.ToTable("daily_flock_records");
            e.HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);
            // Idempotency: unique client record ID prevents duplicate sync submissions
            e.HasIndex(r => r.ClientRecordId).IsUnique().HasFilter("\"ClientRecordId\" IS NOT NULL");
            e.HasIndex(r => new { r.BatchId, r.RecordDate });
            e.Property(r => r.FeedConsumedKg).HasColumnType("numeric(10,3)");
            e.Property(r => r.WaterConsumedLiters).HasColumnType("numeric(10,3)");
            e.Property(r => r.AverageWeightGrams).HasColumnType("numeric(10,2)");
        });

        // ── MortalityRecord ───────────────────────────────────────────────
        builder.Entity<MortalityRecord>(e =>
        {
            e.ToTable("mortality_records");
            e.HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);
        });

        // ── ProductionMetric ──────────────────────────────────────────────
        builder.Entity<ProductionMetric>(e =>
        {
            e.ToTable("production_metrics");
            e.HasQueryFilter(m => m.TenantId == _tenantContext.TenantId);
            e.Property(m => m.Fcr).HasColumnType("numeric(6,3)");
            e.Property(m => m.Epef).HasColumnType("numeric(8,2)");
        });

        // ── FeedInventory ─────────────────────────────────────────────────
        builder.Entity<FeedInventory>(e =>
        {
            e.ToTable("feed_inventories");
            e.HasQueryFilter(fi => fi.TenantId == _tenantContext.TenantId);
            e.Property(fi => fi.CurrentStockKg).HasColumnType("numeric(10,2)");
            e.Property(fi => fi.MinimumStockThresholdKg).HasColumnType("numeric(10,2)");
            e.Property(fi => fi.UnitPricePerKg).HasColumnType("numeric(10,2)");
        });

        // ── MarketplaceListing ─────────────────────────────────────────────
        builder.Entity<MarketplaceListing>(e =>
        {
            e.ToTable("marketplace_listings");
            // IMPORTANT: Farmer sees only OWN listings (tenant filter).
            // Brokers see Published listings via special cross-tenant query (filter bypassed in service).
            e.HasQueryFilter(l => l.TenantId == _tenantContext.TenantId && !l.IsDeleted);
            e.Property(l => l.AskingPricePerKg).HasColumnType("numeric(10,2)");
            e.Property(l => l.NegotiableFloorPricePerKg).HasColumnType("numeric(10,2)");
            e.Property<byte[]>("RowVersion").IsRowVersion();  // Concurrency token
        });

        // ── DealProposal ──────────────────────────────────────────────────
        builder.Entity<DealProposal>(e =>
        {
            e.ToTable("deal_proposals");
            e.HasQueryFilter(p => p.TenantId == _tenantContext.TenantId);
            e.Property(p => p.OfferedPricePerKg).HasColumnType("numeric(10,2)");
        });

        // ── Deal (cross-tenant — NO global filter) ────────────────────────
        builder.Entity<Deal>(e =>
        {
            e.ToTable("deals");
            // No global query filter — deal is shared between farmer & broker tenants.
            // Authorization is enforced in application layer by checking FarmerTenantId || BrokerTenantId.
            e.HasIndex(d => d.FarmerTenantId);
            e.HasIndex(d => d.BrokerTenantId);
            e.Property(d => d.AgreedPricePerKg).HasColumnType("numeric(10,2)");
            e.Property(d => d.ActualWeightKg).HasColumnType("numeric(10,3)");
            e.HasOne(d => d.Proposal).WithOne(p => p.Deal)
             .HasForeignKey<Deal>(d => d.ProposalId);
        });

        // ── DealStatusHistory ─────────────────────────────────────────────
        builder.Entity<DealStatusHistory>(e => e.ToTable("deal_status_history"));

        // ── TransportRecord ────────────────────────────────────────────────
        builder.Entity<TransportRecord>(e =>
        {
            e.ToTable("transport_records");
            e.Property(t => t.WeightLiftedKg).HasColumnType("numeric(10,3)");
            e.Property(t => t.WeightDeliveredKg).HasColumnType("numeric(10,3)");
            e.Ignore(t => t.ShrinkageKg); // Computed property
        });

        // ── Financial entities ─────────────────────────────────────────────
        builder.Entity<CommissionRule>(e =>
        {
            e.ToTable("commission_rules");
            e.HasQueryFilter(r => r.TenantId == _tenantContext.TenantId);
            e.Property(r => r.Rate).HasColumnType("numeric(8,4)");
        });

        builder.Entity<Settlement>(e =>
        {
            e.ToTable("settlements");
            e.Property(s => s.GrossAmount).HasColumnType("numeric(12,2)");
            e.Property(s => s.CommissionAmount).HasColumnType("numeric(12,2)");
            e.Property(s => s.FreightAmount).HasColumnType("numeric(12,2)");
            e.Property(s => s.NetPayableToFarmer).HasColumnType("numeric(12,2)");
            e.Property(s => s.NetPayableToBroker).HasColumnType("numeric(12,2)");
        });

        builder.Entity<Invoice>(e =>
        {
            e.ToTable("invoices");
            e.HasQueryFilter(i => i.TenantId == _tenantContext.TenantId);
            e.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
            e.Property(i => i.TotalAmount).HasColumnType("numeric(12,2)");
            e.Property(i => i.PaidAmount).HasColumnType("numeric(12,2)");
            e.Ignore(i => i.BalanceDue);
        });

        builder.Entity<InvoiceItem>(e =>
        {
            e.ToTable("invoice_items");
            e.Ignore(i => i.Amount);
        });

        builder.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.Property(p => p.Amount).HasColumnType("numeric(12,2)");
        });

        builder.Entity<LedgerEntry>(e =>
        {
            e.ToTable("ledger_entries");
            e.HasQueryFilter(l => l.TenantId == _tenantContext.TenantId);
            e.Property(l => l.Amount).HasColumnType("numeric(12,2)");
            e.Property(l => l.RunningBalance).HasColumnType("numeric(14,2)");
        });

        builder.Entity<MarketPrice>(e =>
        {
            e.ToTable("market_prices");
            e.Property(mp => mp.PricePerKg).HasColumnType("numeric(10,2)");
            e.Property(mp => mp.PreviousPricePerKg).HasColumnType("numeric(10,2)");
        });

        builder.Entity<PoultryShop>(e =>
        {
            e.ToTable("poultry_shops");
            e.Property(s => s.DailyCapacityKg).HasColumnType("numeric(10,2)");
            e.Property(s => s.CurrentBroilerMeatRatePerKg).HasColumnType("numeric(10,2)");
            e.Property(s => s.CurrentLiveBroilerBuyRatePerKg).HasColumnType("numeric(10,2)");
        });

        // ── Notifications & Audit ─────────────────────────────────────────
        builder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasQueryFilter(n => n.TenantId == _tenantContext.TenantId);
        });

        builder.Entity<AuditLog>(e => e.ToTable("audit_logs"));

        // ── DealExpense ────────────────────────────────────────────────────
        builder.Entity<DealExpense>(e =>
        {
            e.ToTable("deal_expenses");
            e.Property(d => d.Amount).HasColumnType("numeric(12,2)");
        });
    }

    /// <summary>
    /// Auto-sets CreatedAt/UpdatedAt timestamps on save.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Domain.Common.BaseEntity entity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entity.CreatedAt = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entity.UpdatedAt = DateTime.UtcNow;
                        break;
                }
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Extended Identity role with tenant support.
/// </summary>
public class ApplicationRole : Microsoft.AspNetCore.Identity.IdentityRole<Guid>
{
    public string? Description { get; set; }
}
