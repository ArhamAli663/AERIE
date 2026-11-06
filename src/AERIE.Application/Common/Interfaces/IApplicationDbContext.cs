namespace AERIE.Application.Common.Interfaces;

using AERIE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core DbContext interface used by application layer.
/// Keeps Application layer free of EF Core implementation details.
/// </summary>
public interface IApplicationDbContext
{
    // Shared / Reference
    DbSet<Tenant> Tenants { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<Breed> Breeds { get; }
    DbSet<MarketPrice> MarketPrices { get; }
    DbSet<PoultryShop> PoultryShops { get; }

    // Farmer Module
    DbSet<Farm> Farms { get; }
    DbSet<PoultryHouse> PoultryHouses { get; }
    DbSet<FarmStaff> FarmStaff { get; }
    DbSet<FlockBatch> FlockBatches { get; }
    DbSet<ChickPlacement> ChickPlacements { get; }
    DbSet<DailyFlockRecord> DailyFlockRecords { get; }
    DbSet<MortalityRecord> MortalityRecords { get; }
    DbSet<ProductionMetric> ProductionMetrics { get; }
    DbSet<FeedInventory> FeedInventories { get; }

    // Marketplace
    DbSet<MarketplaceListing> MarketplaceListings { get; }

    // Broker Module
    DbSet<DealProposal> DealProposals { get; }
    DbSet<Deal> Deals { get; }
    DbSet<DealStatusHistory> DealStatusHistories { get; }
    DbSet<TransportRecord> TransportRecords { get; }

    // Financial
    DbSet<CommissionRule> CommissionRules { get; }
    DbSet<DealExpense> DealExpenses { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }

    // System
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
