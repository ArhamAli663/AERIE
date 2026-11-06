namespace AERIE.Application.Brokers.Services;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using AERIE.Application.Common.Interfaces;
using AERIE.Application.Common.Models;
using AERIE.Domain.Common;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

public class BrokerService : IBrokerService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IDailyMarketRateService _dailyMarketRateService;
    private readonly ILogger<BrokerService> _logger;

    public BrokerService(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        ILogger<BrokerService> logger)
        : this(context, tenantContext, new DailyMarketRateService(context, new Microsoft.Extensions.Logging.Abstractions.NullLogger<DailyMarketRateService>()), logger)
    {
    }

    public BrokerService(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        IDailyMarketRateService dailyMarketRateService,
        ILogger<BrokerService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _dailyMarketRateService = dailyMarketRateService;
        _logger = logger;
    }

    // ── Broker Dashboard ──────────────────────────────────────────────────────

    public async Task<Result<BrokerDashboardSummaryDto>> GetBrokerDashboardSummaryAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<BrokerDashboardSummaryDto>.Failure("Unauthorized broker tenant context.");

        var publishedListings = await _context.MarketplaceListings
            .IgnoreQueryFilters()
            .Where(l => l.Status == ListingStatus.Published)
            .CountAsync(ct);

        var myProposals = await _context.DealProposals
            .Include(p => p.Listing)
            .Include(p => p.Deal)
            .Where(p => p.BrokerTenantId == tenantId)
            .OrderByDescending(p => p.ProposedAt)
            .ToListAsync(ct);

        var pendingProposals = myProposals.Where(p => p.Status == DealStatus.Proposed || p.Status == DealStatus.Reviewed).ToList();

        var deals = await _context.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Listing)
            .Include(d => d.Settlement)
            .Where(d => d.BrokerTenantId == tenantId)
            .OrderByDescending(d => d.AcceptedAt)
            .ToListAsync(ct);

        var activeDeals = deals.Where(d => d.Status != DealStatus.Closed && d.Status != DealStatus.Cancelled).ToList();
        var committedBirds = activeDeals.Sum(d => d.QuantityBirds);

        var today = DateTime.UtcNow.Date;
        var todayPurchasedBirds = deals.Where(d => d.AcceptedAt.Date == today).Sum(d => d.QuantityBirds);
        var todaySoldBirds = deals.Where(d => d.Status == DealStatus.Delivered || d.Status == DealStatus.Settled || d.Status == DealStatus.Invoiced || d.Status == DealStatus.Paid)
                                  .Where(d => d.AcceptedAt.Date == today).Sum(d => d.QuantityBirds);

        decimal totalDealValue = deals.Sum(d => (d.ActualWeightKg ?? (d.QuantityBirds * 2.0m)) * d.AgreedPricePerKg);
        decimal expectedCommission = deals.Sum(d => d.Settlement?.CommissionAmount ?? BrokerFinancialCalculator.CalculateCommission((d.ActualWeightKg ?? (d.QuantityBirds * 2.0m)) * d.AgreedPricePerKg, d.ActualWeightKg ?? (d.QuantityBirds * 2.0m), d.QuantityBirds, CommissionRuleType.Percentage, 2.0m));
        decimal realizedCommission = deals.Where(d => d.Settlement != null).Sum(d => d.Settlement!.CommissionAmount);
        decimal totalTransportCosts = deals.Where(d => d.Settlement != null).Sum(d => d.Settlement!.FreightAmount);
        decimal totalPayableToFarmers = deals.Where(d => d.Settlement != null).Sum(d => d.Settlement!.NetPayableToFarmer);
        decimal totalReceivableFromBuyers = deals.Where(d => d.Settlement != null).Sum(d => d.Settlement!.GrossAmount + d.Settlement!.CommissionAmount);

        var pendingLifting = deals.Count(d => d.Status == DealStatus.Accepted);
        var pendingDelivery = deals.Count(d => d.Status == DealStatus.InTransit);

        var invoices = await _context.Invoices
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(ct);

        var unpaidInvoices = invoices.Count(i => i.Status == "Unpaid" || i.Status == "PartiallyPaid");
        var outstandingSettlements = deals.Count(d => (d.Status == DealStatus.Delivered || d.Status == DealStatus.InTransit) && d.Settlement == null);

        var recentPricesResult = await GetLatestMarketPricesAsync(null, ct);
        var recentPrices = recentPricesResult.IsSuccess ? recentPricesResult.Data! : Enumerable.Empty<MarketPriceDto>();

        var tenantNames = await _context.Tenants
            .Where(t => deals.Select(d => d.FarmerTenantId).Distinct().Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var brokerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var brokerName = brokerTenant?.Name ?? "My Brokerage";

        var activeDealDtos = activeDeals.Take(10).Select(d => new DealDto(
            d.Id,
            d.ProposalId,
            d.ListingId,
            d.Listing?.Title ?? "Listing",
            d.FarmerTenantId,
            tenantNames.GetValueOrDefault(d.FarmerTenantId, "Farmer"),
            d.BrokerTenantId,
            brokerName,
            d.QuantityBirds,
            d.AgreedPricePerKg,
            d.EstimatedWeightKg,
            d.ActualWeightKg,
            d.Status,
            d.AcceptedAt,
            d.ClosedAt,
            d.Notes,
            d.Settlement?.GrossAmount,
            d.Settlement?.CommissionAmount,
            d.Settlement?.FreightAmount,
            d.Settlement?.NetPayableToFarmer
        )).ToList();

        var pendingProposalDtos = pendingProposals.Take(10).Select(p => new DealProposalDto(
            p.Id,
            p.ListingId,
            p.Listing?.Title ?? "Listing",
            p.BrokerTenantId,
            p.BrokerCompanyName,
            p.ProposedByUserId,
            p.QuantityRequested,
            p.OfferedPricePerKg,
            p.TransportArrangement,
            p.Notes,
            p.Status,
            p.ProposedAt,
            p.FarmerResponse,
            p.RespondedAt,
            p.Deal?.Id
        )).ToList();

        var summary = new BrokerDashboardSummaryDto(
            TotalPublishedListingsAvailable: publishedListings,
            NewFarmerOffersCount: publishedListings,
            PendingProposalsCount: pendingProposals.Count,
            ActiveDealsCount: activeDeals.Count,
            CommittedBirdsCount: committedBirds,
            TodayPurchasedBirdsCount: todayPurchasedBirds,
            TodaySoldBirdsCount: todaySoldBirds,
            TotalDealValue: Math.Round(totalDealValue, 2),
            ExpectedCommission: Math.Round(expectedCommission, 2),
            RealizedCommission: Math.Round(realizedCommission, 2),
            TotalTransportCosts: Math.Round(totalTransportCosts, 2),
            TotalAmountsPayableToFarmers: Math.Round(totalPayableToFarmers, 2),
            TotalAmountsReceivableFromBuyers: Math.Round(totalReceivableFromBuyers, 2),
            PendingLiftingCount: pendingLifting,
            PendingDeliveryCount: pendingDelivery,
            UnpaidInvoicesCount: unpaidInvoices,
            OutstandingSettlementsCount: outstandingSettlements,
            RecentPrices: recentPrices,
            ActiveDeals: activeDealDtos,
            PendingProposals: pendingProposalDtos
        );

        return Result<BrokerDashboardSummaryDto>.Success(summary);
    }

    // ── Proposals ─────────────────────────────────────────────────────────────

    public async Task<Result<DealProposalDto>> CreateProposalAsync(CreateProposalRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<DealProposalDto>.Failure("Unauthorized broker tenant context.");

        // Query listing with IgnoreQueryFilters since it belongs to a farmer tenant
        var listing = await _context.MarketplaceListings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, ct);

        if (listing == null)
            return Result<DealProposalDto>.Failure("Marketplace listing not found.");

        if (listing.Status != ListingStatus.Published)
            return Result<DealProposalDto>.Failure("Listing is not available for proposals.");

        if (request.QuantityRequested > listing.QuantityAvailable)
            return Result<DealProposalDto>.Failure($"Requested quantity ({request.QuantityRequested}) exceeds available quantity ({listing.QuantityAvailable}).");

        if (request.OfferedPricePerKg <= 0)
            return Result<DealProposalDto>.Failure("Offered price per kg must be greater than zero.");

        var brokerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        var companyName = brokerTenant?.Name ?? "Brokerage Partner";

        var proposal = new DealProposal
        {
            TenantId = tenantId,
            BrokerTenantId = tenantId,
            ListingId = request.ListingId,
            BrokerCompanyName = companyName,
            ProposedByUserId = _tenantContext.UserId,
            QuantityRequested = request.QuantityRequested,
            OfferedPricePerKg = request.OfferedPricePerKg,
            TransportArrangement = request.TransportArrangement,
            Notes = request.Notes,
            Status = DealStatus.Proposed,
            ProposedAt = DateTime.UtcNow
        };

        _context.DealProposals.Add(proposal);

        // Persist real-time notification for farmer tenant
        _context.Notifications.Add(new Notification
        {
            TenantId = listing.TenantId,
            Type = NotificationType.DealProposed,
            Title = "New Purchase Proposal Received",
            Message = $"Broker '{companyName}' submitted a proposal for {proposal.QuantityRequested:N0} birds @ Rs. {proposal.OfferedPricePerKg:N2}/kg on listing '{listing.Title}'.",
            RelatedEntityId = proposal.Id,
            IsRead = false,
            ActionUrl = "/Farmer/Listings"
        });

        await _context.SaveChangesAsync(ct);

        return Result<DealProposalDto>.Success(new DealProposalDto(
            proposal.Id,
            proposal.ListingId,
            listing.Title,
            proposal.BrokerTenantId,
            proposal.BrokerCompanyName,
            proposal.ProposedByUserId,
            proposal.QuantityRequested,
            proposal.OfferedPricePerKg,
            proposal.TransportArrangement,
            proposal.Notes,
            proposal.Status,
            proposal.ProposedAt,
            proposal.FarmerResponse,
            proposal.RespondedAt,
            DealId: null
        ));
    }

    public async Task<Result<IEnumerable<DealProposalDto>>> GetMyProposalsAsync(CancellationToken ct = default)
    {
        var proposals = await _context.DealProposals
            .Include(p => p.Listing)
            .Include(p => p.Deal)
            .OrderByDescending(p => p.ProposedAt)
            .ToListAsync(ct);

        var dtos = proposals.Select(p => new DealProposalDto(
            p.Id,
            p.ListingId,
            p.Listing?.Title ?? "Listing",
            p.BrokerTenantId,
            p.BrokerCompanyName,
            p.ProposedByUserId,
            p.QuantityRequested,
            p.OfferedPricePerKg,
            p.TransportArrangement,
            p.Notes,
            p.Status,
            p.ProposedAt,
            p.FarmerResponse,
            p.RespondedAt,
            p.Deal?.Id
        ));

        return Result<IEnumerable<DealProposalDto>>.Success(dtos);
    }

    public async Task<Result<IEnumerable<DealProposalDto>>> GetProposalsForListingAsync(Guid listingId, CancellationToken ct = default)
    {
        // Verified by listing ownership
        var listing = await _context.MarketplaceListings.FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing == null)
            return Result<IEnumerable<DealProposalDto>>.Failure("Listing not found or not owned by current tenant.");

        var proposals = await _context.DealProposals
            .IgnoreQueryFilters()
            .Include(p => p.Deal)
            .Where(p => p.ListingId == listingId)
            .OrderByDescending(p => p.ProposedAt)
            .ToListAsync(ct);

        var dtos = proposals.Select(p => new DealProposalDto(
            p.Id,
            p.ListingId,
            listing.Title,
            p.BrokerTenantId,
            p.BrokerCompanyName,
            p.ProposedByUserId,
            p.QuantityRequested,
            p.OfferedPricePerKg,
            p.TransportArrangement,
            p.Notes,
            p.Status,
            p.ProposedAt,
            p.FarmerResponse,
            p.RespondedAt,
            p.Deal?.Id
        ));

        return Result<IEnumerable<DealProposalDto>>.Success(dtos);
    }

    public async Task<Result<DealDto>> ReviewProposalAsync(Guid proposalId, ReviewProposalRequest request, CancellationToken ct = default)
    {
        var proposal = await _context.DealProposals
            .IgnoreQueryFilters()
            .Include(p => p.Listing)
            .ThenInclude(l => l.Batch)
            .FirstOrDefaultAsync(p => p.Id == proposalId, ct);

        if (proposal == null)
            return Result<DealDto>.Failure("Proposal not found.");

        // Tenant authorization: only the farmer who owns the listing can review
        if (proposal.Listing.TenantId != _tenantContext.TenantId)
            return Result<DealDto>.Failure("Unauthorized. You do not own the listing associated with this proposal.");

        if (proposal.Status != DealStatus.Proposed && proposal.Status != DealStatus.Reviewed)
            return Result<DealDto>.Failure($"Cannot review proposal in status '{proposal.Status}'.");

        proposal.RespondedAt = DateTime.UtcNow;
        proposal.FarmerResponse = request.ResponseNotes;

        if (!request.Accept)
        {
            proposal.Status = DealStatus.Rejected;

            _context.Notifications.Add(new Notification
            {
                TenantId = proposal.BrokerTenantId,
                Type = NotificationType.DealRejected,
                Title = "Purchase Proposal Declined",
                Message = $"Farmer declined proposal for {proposal.QuantityRequested:N0} birds on '{proposal.Listing.Title}'. Reason: {request.ResponseNotes ?? "No reason given."}",
                RelatedEntityId = proposal.Id,
                IsRead = false,
                ActionUrl = "/Broker/Deals"
            });

            await _context.SaveChangesAsync(ct);
            return Result<DealDto>.Failure("Proposal was rejected.");
        }

        // Concurrency and overselling prevention check
        if (proposal.Listing.QuantityAvailable < proposal.QuantityRequested)
        {
            return Result<DealDto>.Failure($"Cannot accept proposal. Available quantity is {proposal.Listing.QuantityAvailable}, but proposal requests {proposal.QuantityRequested}.");
        }

        // Atomic reservation
        proposal.Listing.QuantityAvailable -= proposal.QuantityRequested;
        if (proposal.Listing.QuantityAvailable == 0)
        {
            proposal.Listing.Status = ListingStatus.Reserved;
        }

        // Update flock batch counts
        var batch = proposal.Listing.Batch;
        batch.CurrentAliveCount = Math.Max(0, batch.CurrentAliveCount - proposal.QuantityRequested);
        batch.Status = batch.CurrentAliveCount == 0 ? BatchStatus.FullySold : BatchStatus.PartialSold;

        proposal.Status = DealStatus.Accepted;

        var farmerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == proposal.Listing.TenantId, ct);
        var brokerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == proposal.BrokerTenantId, ct);

        // Create unified Deal referencing the exact listing and batch
        var deal = new Deal
        {
            ProposalId = proposal.Id,
            ListingId = proposal.ListingId,
            FarmerTenantId = proposal.Listing.TenantId,
            BrokerTenantId = proposal.BrokerTenantId,
            QuantityBirds = proposal.QuantityRequested,
            AgreedPricePerKg = proposal.OfferedPricePerKg,
            Status = DealStatus.Accepted,
            AcceptedAt = DateTime.UtcNow,
            Notes = request.ResponseNotes
        };

        _context.Deals.Add(deal);

        // Status history audit record
        var history = new DealStatusHistory
        {
            DealId = deal.Id,
            Status = DealStatus.Accepted,
            ChangedByUserId = _tenantContext.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = "Proposal accepted by farmer. Flock reserved."
        };
        _context.DealStatusHistories.Add(history);

        // Notify Broker Tenant of acceptance
        _context.Notifications.Add(new Notification
        {
            TenantId = proposal.BrokerTenantId,
            Type = NotificationType.DealAccepted,
            Title = "Purchase Proposal Accepted!",
            Message = $"Farmer accepted your proposal for {proposal.QuantityRequested:N0} birds on '{proposal.Listing.Title}' @ Rs. {proposal.OfferedPricePerKg:N2}/kg. Deal #{deal.Id.ToString("N")[..8]} created.",
            RelatedEntityId = deal.Id,
            IsRead = false,
            ActionUrl = "/Broker/Deals"
        });

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deal {DealId} accepted. {Quantity} birds reserved from listing {ListingId}", deal.Id, deal.QuantityBirds, deal.ListingId);

        return Result<DealDto>.Success(new DealDto(
            deal.Id,
            deal.ProposalId,
            deal.ListingId,
            proposal.Listing.Title,
            deal.FarmerTenantId,
            farmerTenant?.Name ?? "Farmer Partner",
            deal.BrokerTenantId,
            brokerTenant?.Name ?? "Broker Partner",
            deal.QuantityBirds,
            deal.AgreedPricePerKg,
            deal.EstimatedWeightKg,
            deal.ActualWeightKg,
            deal.Status,
            deal.AcceptedAt,
            deal.ClosedAt,
            deal.Notes,
            TotalAmount: null,
            CommissionAmount: null,
            FreightAmount: null,
            NetPayableToFarmer: null
        ));
    }

    public async Task<Result<bool>> WithdrawProposalAsync(Guid proposalId, WithdrawProposalRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var proposal = await _context.DealProposals
            .FirstOrDefaultAsync(p => p.Id == proposalId && p.BrokerTenantId == tenantId, ct);

        if (proposal == null)
            return Result<bool>.Failure("Proposal not found or unauthorized.");

        if (proposal.Status != DealStatus.Proposed && proposal.Status != DealStatus.Reviewed)
            return Result<bool>.Failure($"Cannot withdraw proposal with status '{proposal.Status}'. Only pending proposals can be withdrawn.");

        proposal.Status = DealStatus.Withdrawn;
        proposal.Notes = string.IsNullOrWhiteSpace(request.Reason)
            ? proposal.Notes
            : $"{proposal.Notes} [Withdrawn: {request.Reason}]";

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Proposal {ProposalId} withdrawn by broker tenant {TenantId}", proposalId, tenantId);

        return Result<bool>.Success(true);
    }

    // ── Deals ─────────────────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<DealDto>>> GetDealsAsync(DealStatus? status = null, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<IEnumerable<DealDto>>.Failure("Unauthorized tenant context.");

        // Deal is shared between FarmerTenantId and BrokerTenantId
        var query = _context.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Listing)
            .Include(d => d.Settlement)
            .Where(d => d.FarmerTenantId == tenantId || d.BrokerTenantId == tenantId);

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        var deals = await query.OrderByDescending(d => d.AcceptedAt).ToListAsync(ct);

        var tenantIds = deals.Select(d => d.FarmerTenantId).Concat(deals.Select(d => d.BrokerTenantId)).Distinct().ToList();
        var tenantNames = await _context.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var dtos = deals.Select(d => new DealDto(
            d.Id,
            d.ProposalId,
            d.ListingId,
            d.Listing?.Title ?? "Listing",
            d.FarmerTenantId,
            tenantNames.GetValueOrDefault(d.FarmerTenantId, "Farmer"),
            d.BrokerTenantId,
            tenantNames.GetValueOrDefault(d.BrokerTenantId, "Broker"),
            d.QuantityBirds,
            d.AgreedPricePerKg,
            d.EstimatedWeightKg,
            d.ActualWeightKg,
            d.Status,
            d.AcceptedAt,
            d.ClosedAt,
            d.Notes,
            d.Settlement?.GrossAmount,
            d.Settlement?.CommissionAmount,
            d.Settlement?.FreightAmount,
            d.Settlement?.NetPayableToFarmer
        ));

        return Result<IEnumerable<DealDto>>.Success(dtos);
    }

    public async Task<Result<DealDto>> GetDealByIdAsync(Guid dealId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Listing)
            .Include(d => d.Settlement)
            .FirstOrDefaultAsync(d => d.Id == dealId, ct);

        if (deal == null)
            return Result<DealDto>.Failure("Deal not found.");

        if (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId)
            return Result<DealDto>.Failure("Unauthorized. You are not a party to this deal.");

        var farmerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == deal.FarmerTenantId, ct);
        var brokerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == deal.BrokerTenantId, ct);

        return Result<DealDto>.Success(new DealDto(
            deal.Id,
            deal.ProposalId,
            deal.ListingId,
            deal.Listing?.Title ?? "Listing",
            deal.FarmerTenantId,
            farmerTenant?.Name ?? "Farmer",
            deal.BrokerTenantId,
            brokerTenant?.Name ?? "Broker",
            deal.QuantityBirds,
            deal.AgreedPricePerKg,
            deal.EstimatedWeightKg,
            deal.ActualWeightKg,
            deal.Status,
            deal.AcceptedAt,
            deal.ClosedAt,
            deal.Notes,
            deal.Settlement?.GrossAmount,
            deal.Settlement?.CommissionAmount,
            deal.Settlement?.FreightAmount,
            deal.Settlement?.NetPayableToFarmer
        ));
    }

    public async Task<Result<DealDto>> UpdateDealStatusAsync(Guid dealId, UpdateDealStatusRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Listing)
            .Include(d => d.Settlement)
            .FirstOrDefaultAsync(d => d.Id == dealId, ct);

        if (deal == null)
            return Result<DealDto>.Failure("Deal not found.");

        if (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId)
            return Result<DealDto>.Failure("Unauthorized to modify this deal.");

        deal.Status = request.NewStatus;
        if (request.NewStatus == DealStatus.Closed)
        {
            deal.ClosedAt = DateTime.UtcNow;
        }

        var history = new DealStatusHistory
        {
            DealId = deal.Id,
            Status = request.NewStatus,
            ChangedByUserId = _tenantContext.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = request.Notes
        };
        _context.DealStatusHistories.Add(history);

        await _context.SaveChangesAsync(ct);

        var farmerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == deal.FarmerTenantId, ct);
        var brokerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == deal.BrokerTenantId, ct);

        return Result<DealDto>.Success(new DealDto(
            deal.Id,
            deal.ProposalId,
            deal.ListingId,
            deal.Listing?.Title ?? "Listing",
            deal.FarmerTenantId,
            farmerTenant?.Name ?? "Farmer",
            deal.BrokerTenantId,
            brokerTenant?.Name ?? "Broker",
            deal.QuantityBirds,
            deal.AgreedPricePerKg,
            deal.EstimatedWeightKg,
            deal.ActualWeightKg,
            deal.Status,
            deal.AcceptedAt,
            deal.ClosedAt,
            deal.Notes,
            deal.Settlement?.GrossAmount,
            deal.Settlement?.CommissionAmount,
            deal.Settlement?.FreightAmount,
            deal.Settlement?.NetPayableToFarmer
        ));
    }

    // ── Transport / Lifting ───────────────────────────────────────────────────

    public async Task<Result<TransportRecordDto>> RecordLiftingAsync(Guid dealId, RecordLiftingRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == dealId, ct);

        if (deal == null)
            return Result<TransportRecordDto>.Failure("Deal not found.");

        if (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId)
            return Result<TransportRecordDto>.Failure("Unauthorized for this deal.");

        var record = new TransportRecord
        {
            DealId = dealId,
            VehicleNumber = request.VehicleNumber,
            DriverName = request.DriverName,
            DriverPhone = request.DriverPhone,
            DepartureAt = DateTime.UtcNow,
            WeightLiftedKg = request.ActualWeightKg,
            BirdsLoaded = request.BirdsLoaded,
            MortalityDuringTransit = request.MortalityDuringTransit,
            Notes = request.Notes
        };

        _context.TransportRecords.Add(record);

        // Update deal weight and advance status to InTransit
        deal.ActualWeightKg = request.ActualWeightKg;
        if (deal.Status == DealStatus.Accepted)
        {
            deal.Status = DealStatus.InTransit;
            _context.DealStatusHistories.Add(new DealStatusHistory
            {
                DealId = deal.Id,
                Status = DealStatus.InTransit,
                ChangedByUserId = _tenantContext.UserId,
                ChangedAt = DateTime.UtcNow,
                Note = $"Lifting recorded: {request.ActualWeightKg} kg, {request.BirdsLoaded} birds."
            });
        }

        await _context.SaveChangesAsync(ct);

        return Result<TransportRecordDto>.Success(new TransportRecordDto(
            record.Id,
            record.DealId,
            record.VehicleNumber,
            record.DriverName,
            record.DriverPhone,
            request.BirdsLoaded,
            request.MortalityDuringTransit,
            record.WeightLiftedKg ?? 0,
            request.WeighbridgeTicketNumber,
            record.DepartureAt ?? DateTime.UtcNow,
            record.ArrivalAt,
            record.Notes
        ));
    }

    public async Task<Result<TransportDiscrepancyDto>> RecordDeliveryAsync(Guid dealId, RecordDeliveryRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == dealId, ct);

        if (deal == null || (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId))
            return Result<TransportDiscrepancyDto>.Failure("Deal not found or unauthorized.");

        var record = await _context.TransportRecords
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.DealId == dealId, ct);

        if (record == null)
        {
            record = new TransportRecord
            {
                DealId = dealId,
                BirdsLoaded = deal.QuantityBirds,
                WeightLiftedKg = deal.ActualWeightKg ?? (deal.QuantityBirds * 2.0m),
                DepartureAt = DateTime.UtcNow.AddHours(-4)
            };
            _context.TransportRecords.Add(record);
        }

        record.BirdsDelivered = request.BirdsDelivered;
        record.MortalityDuringTransit = request.MortalityDuringTransit;
        record.WeightDeliveredKg = request.WeightDeliveredKg;
        record.DestinationLocation = request.DestinationLocation ?? record.DestinationLocation;
        record.FreightCost = request.FreightCost ?? record.FreightCost;
        record.ArrivalAt = DateTime.UtcNow;
        record.DeliveryStatus = "Delivered";
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            record.Notes = $"{record.Notes}; {request.Notes}".TrimStart(';', ' ');
        }

        var (shrinkageKg, shrinkagePct) = BrokerFinancialCalculator.CalculateShrinkage(
            record.WeightLiftedKg ?? request.WeightDeliveredKg,
            request.WeightDeliveredKg);

        var (mortality, missingBirds) = BrokerFinancialCalculator.CalculateDiscrepancy(
            record.BirdsLoaded,
            request.BirdsDelivered,
            request.MortalityDuringTransit);

        deal.ActualWeightKg = request.WeightDeliveredKg;
        deal.Status = DealStatus.Delivered;

        _context.DealStatusHistories.Add(new DealStatusHistory
        {
            DealId = deal.Id,
            Status = DealStatus.Delivered,
            ChangedByUserId = _tenantContext.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = $"Delivery confirmed: {request.BirdsDelivered} birds ({request.WeightDeliveredKg:N1} kg), Shrinkage: {shrinkageKg:N1} kg ({shrinkagePct:F2}%), Transit Mortality: {mortality}, Missing: {missingBirds}"
        });

        await _context.SaveChangesAsync(ct);

        return Result<TransportDiscrepancyDto>.Success(new TransportDiscrepancyDto(
            deal.Id,
            record.BirdsLoaded,
            record.BirdsDelivered,
            record.MortalityDuringTransit,
            missingBirds,
            record.WeightLiftedKg ?? request.WeightDeliveredKg,
            record.WeightDeliveredKg.Value,
            shrinkageKg,
            shrinkagePct,
            record.DeliveryStatus,
            record.Notes
        ));
    }

    public async Task<Result<IEnumerable<TransportRecordDto>>> GetTransportRecordsAsync(Guid dealId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == dealId, ct);

        if (deal == null || (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId))
            return Result<IEnumerable<TransportRecordDto>>.Failure("Deal not found or unauthorized.");

        var records = await _context.TransportRecords
            .Where(t => t.DealId == dealId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var dtos = records.Select(r => new TransportRecordDto(
            r.Id,
            r.DealId,
            r.VehicleNumber,
            r.DriverName,
            r.DriverPhone,
            BirdsLoaded: 0,
            MortalityDuringTransit: 0,
            r.WeightLiftedKg ?? 0,
            WeighbridgeTicketNumber: null,
            r.DepartureAt ?? r.CreatedAt,
            r.ArrivalAt,
            r.Notes
        ));

        return Result<IEnumerable<TransportRecordDto>>.Success(dtos);
    }

    // ── Settlement ────────────────────────────────────────────────────────────

    public async Task<Result<SettlementDto>> CreateSettlementAsync(Guid dealId, CreateSettlementRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == dealId, ct);

        if (deal == null)
            return Result<SettlementDto>.Failure("Deal not found.");

        if (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId)
            return Result<SettlementDto>.Failure("Unauthorized to settle this deal.");

        var weight = deal.ActualWeightKg ?? deal.EstimatedWeightKg ?? (deal.QuantityBirds * 2.0m);
        var grossAmount = Math.Round(weight * deal.AgreedPricePerKg, 2);

        decimal commissionAmount = request.CommissionType switch
        {
            CommissionRuleType.Percentage => Math.Round(grossAmount * (request.CommissionRate / 100m), 2),
            CommissionRuleType.FlatPerBird => Math.Round(deal.QuantityBirds * request.CommissionRate, 2),
            CommissionRuleType.FlatPerKg => Math.Round(weight * request.CommissionRate, 2),
            _ => 0m
        };

        var otherNet = request.Deductions - request.Additions;
        var netFarmerPayable = grossAmount - commissionAmount - request.FreightAmount - otherNet;
        var netBrokerReceivable = commissionAmount;

        var existingSettlement = await _context.Settlements.FirstOrDefaultAsync(s => s.DealId == dealId, ct);
        Settlement settlement;

        if (existingSettlement != null)
        {
            settlement = existingSettlement;
            settlement.GrossAmount = grossAmount;
            settlement.CommissionAmount = commissionAmount;
            settlement.FreightAmount = request.FreightAmount;
            settlement.OtherExpenses = otherNet;
            settlement.NetPayableToFarmer = netFarmerPayable;
            settlement.NetPayableToBroker = netBrokerReceivable;
            settlement.Notes = request.Notes;
            settlement.SettledAt = DateTime.UtcNow;
            settlement.SettledByUserId = _tenantContext.UserId;
        }
        else
        {
            settlement = new Settlement
            {
                DealId = dealId,
                GrossAmount = grossAmount,
                CommissionAmount = commissionAmount,
                FreightAmount = request.FreightAmount,
                OtherExpenses = otherNet,
                NetPayableToFarmer = netFarmerPayable,
                NetPayableToBroker = netBrokerReceivable,
                Notes = request.Notes,
                SettledAt = DateTime.UtcNow,
                SettledByUserId = _tenantContext.UserId
            };
            _context.Settlements.Add(settlement);
        }

        deal.Status = DealStatus.Settled;
        _context.DealStatusHistories.Add(new DealStatusHistory
        {
            DealId = deal.Id,
            Status = DealStatus.Settled,
            ChangedByUserId = _tenantContext.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = $"Settlement calculated: Gross {grossAmount:N2}, Comm {commissionAmount:N2}, Freight {request.FreightAmount:N2}, Net Farmer {netFarmerPayable:N2}"
        });

        await _context.SaveChangesAsync(ct);

        return Result<SettlementDto>.Success(new SettlementDto(
            settlement.Id,
            settlement.DealId,
            settlement.GrossAmount,
            settlement.CommissionAmount,
            settlement.FreightAmount,
            request.Deductions,
            request.Additions,
            settlement.NetPayableToFarmer,
            settlement.NetPayableToBroker,
            IsFinalized: true,
            settlement.SettledAt,
            settlement.Notes
        ));
    }

    public async Task<Result<SettlementDto>> GetSettlementByDealIdAsync(Guid dealId, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == dealId, ct);

        if (deal == null || (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId))
            return Result<SettlementDto>.Failure("Deal not found or unauthorized.");

        var s = await _context.Settlements.FirstOrDefaultAsync(s => s.DealId == dealId, ct);
        if (s == null)
            return Result<SettlementDto>.Failure("Settlement has not been created yet for this deal.");

        return Result<SettlementDto>.Success(new SettlementDto(
            s.Id,
            s.DealId,
            s.GrossAmount,
            s.CommissionAmount,
            s.FreightAmount,
            s.OtherExpenses > 0 ? s.OtherExpenses : 0,
            s.OtherExpenses < 0 ? -s.OtherExpenses : 0,
            s.NetPayableToFarmer,
            s.NetPayableToBroker,
            IsFinalized: true,
            s.SettledAt,
            s.Notes
        ));
    }

    // ── Invoices & Payments ───────────────────────────────────────────────────

    public async Task<Result<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == request.DealId, ct);

        if (deal == null)
            return Result<InvoiceDto>.Failure("Deal not found.");

        if (deal.FarmerTenantId != tenantId && deal.BrokerTenantId != tenantId)
            return Result<InvoiceDto>.Failure("Unauthorized to issue invoice for this deal.");

        var count = await _context.Invoices.CountAsync(i => i.IssuedAt.Year == DateTime.UtcNow.Year, ct);
        var invoiceNumber = $"INV-{DateTime.UtcNow.Year}-{(count + 1):D4}";

        var invoice = new Invoice
        {
            TenantId = tenantId,
            DealId = request.DealId,
            InvoiceNumber = invoiceNumber,
            IssuedAt = DateTime.UtcNow,
            DueDate = request.DueDate,
            TotalAmount = request.TotalAmount,
            PaidAmount = 0m,
            Status = "Unpaid",
            Notes = request.Notes
        };

        invoice.Items.Add(new InvoiceItem
        {
            Invoice = invoice,
            Description = $"Poultry Trading Deal: {deal.QuantityBirds} birds @ Rs. {deal.AgreedPricePerKg}/kg",
            Quantity = deal.ActualWeightKg ?? (deal.QuantityBirds * 2.0m),
            UnitPrice = deal.AgreedPricePerKg
        });

        _context.Invoices.Add(invoice);

        deal.Status = DealStatus.Invoiced;
        _context.DealStatusHistories.Add(new DealStatusHistory
        {
            DealId = deal.Id,
            Status = DealStatus.Invoiced,
            ChangedByUserId = _tenantContext.UserId,
            ChangedAt = DateTime.UtcNow,
            Note = $"Invoice {invoiceNumber} generated for amount Rs. {request.TotalAmount:N2}"
        });

        await _context.SaveChangesAsync(ct);

        return Result<InvoiceDto>.Success(new InvoiceDto(
            invoice.Id,
            invoice.DealId,
            invoice.InvoiceNumber,
            invoice.TenantId,
            request.RecipientTenantId,
            request.RecipientName,
            request.SubTotal,
            request.TaxAmount,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.BalanceDue,
            invoice.Status,
            invoice.IssuedAt,
            invoice.DueDate ?? DateTime.UtcNow.AddDays(7),
            invoice.Notes
        ));
    }

    public async Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Deal)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (invoice == null)
            return Result<InvoiceDto>.Failure("Invoice not found.");

        var recipientName = "Trading Partner";
        return Result<InvoiceDto>.Success(new InvoiceDto(
            invoice.Id,
            invoice.DealId,
            invoice.InvoiceNumber,
            invoice.TenantId,
            invoice.Deal.BrokerTenantId,
            recipientName,
            invoice.TotalAmount,
            TaxAmount: 0m,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.BalanceDue,
            invoice.Status,
            invoice.IssuedAt,
            invoice.DueDate ?? invoice.IssuedAt.AddDays(7),
            invoice.Notes
        ));
    }

    public async Task<Result<IEnumerable<InvoiceDto>>> GetInvoicesByDealIdAsync(Guid dealId, CancellationToken ct = default)
    {
        var invoices = await _context.Invoices
            .Include(i => i.Deal)
            .Where(i => i.DealId == dealId)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync(ct);

        var dtos = invoices.Select(i => new InvoiceDto(
            i.Id,
            i.DealId,
            i.InvoiceNumber,
            i.TenantId,
            i.Deal.BrokerTenantId,
            "Trading Partner",
            i.TotalAmount,
            0m,
            i.TotalAmount,
            i.PaidAmount,
            i.BalanceDue,
            i.Status,
            i.IssuedAt,
            i.DueDate ?? i.IssuedAt.AddDays(7),
            i.Notes
        ));

        return Result<IEnumerable<InvoiceDto>>.Success(dtos);
    }

    public async Task<Result<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return Result<PaymentDto>.Failure("Payment amount must be greater than zero.");

        var deal = await _context.Deals
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == request.DealId, ct);

        if (deal == null)
            return Result<PaymentDto>.Failure("Deal not found.");

        Invoice? invoice = null;
        if (request.InvoiceId.HasValue)
        {
            invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId.Value, ct);
        }
        else
        {
            invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.DealId == request.DealId, ct);
        }

        if (invoice == null)
            return Result<PaymentDto>.Failure("No valid invoice associated with this deal to apply payment.");

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Amount = request.Amount,
            Method = request.Method,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes,
            RecordedByUserId = _tenantContext.UserId,
            RecordedAt = DateTime.UtcNow,
            ConfirmedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);

        invoice.PaidAmount += request.Amount;
        if (invoice.BalanceDue <= 0)
        {
            invoice.Status = "Paid";
        }
        else
        {
            invoice.Status = "PartiallyPaid";
        }

        // Financial Ledger Entries (Double-Entry Bookkeeping audit)
        // 1. Debit Entry for Broker Tenant (Disbursement / Settlement)
        var debitLedger = new LedgerEntry
        {
            TenantId = deal.BrokerTenantId,
            DealId = deal.Id,
            EntryType = LedgerEntryType.Debit,
            Amount = request.Amount,
            Description = $"Payment disbursed via {request.Method} for Invoice {invoice.InvoiceNumber}. Ref: {request.ReferenceNumber}",
            EntryDate = DateTime.UtcNow,
            RecordedByUserId = _tenantContext.UserId
        };
        _context.LedgerEntries.Add(debitLedger);

        // 2. Credit Entry for Farmer Tenant (Trade proceed receipt)
        var creditLedger = new LedgerEntry
        {
            TenantId = deal.FarmerTenantId,
            DealId = deal.Id,
            EntryType = LedgerEntryType.Credit,
            Amount = request.Amount,
            Description = $"Payment received via {request.Method} for Invoice {invoice.InvoiceNumber}. Ref: {request.ReferenceNumber}",
            EntryDate = DateTime.UtcNow,
            RecordedByUserId = _tenantContext.UserId
        };
        _context.LedgerEntries.Add(creditLedger);

        // 3. Notification for Farmer Tenant
        _context.Notifications.Add(new Notification
        {
            TenantId = deal.FarmerTenantId,
            Type = NotificationType.PaymentReceived,
            Title = "Trade Payment Received",
            Message = $"Payment of Rs. {request.Amount:N2} received via {request.Method} for deal #{deal.Id.ToString("N")[..8]}.",
            RelatedEntityId = deal.Id,
            IsRead = false,
            ActionUrl = "/Farmer/Financials"
        });

        if (invoice.Status == "Paid")
        {
            deal.Status = DealStatus.Paid;
            _context.DealStatusHistories.Add(new DealStatusHistory
            {
                DealId = deal.Id,
                Status = DealStatus.Paid,
                ChangedByUserId = _tenantContext.UserId,
                ChangedAt = DateTime.UtcNow,
                Note = $"Full payment of Rs. {request.Amount:N2} confirmed. Invoice {invoice.InvoiceNumber} settled."
            });
        }

        await _context.SaveChangesAsync(ct);

        return Result<PaymentDto>.Success(new PaymentDto(
            payment.Id,
            deal.Id,
            invoice.Id,
            payment.ReferenceNumber ?? payment.Id.ToString("N")[..8],
            payment.Amount,
            payment.Method,
            payment.ReferenceNumber,
            request.BankName,
            Status: "Confirmed",
            payment.RecordedAt,
            payment.Notes
        ));
    }

    // ── Market Prices ─────────────────────────────────────────────────────────

    public async Task<Result<MarketPriceDto>> RecordMarketPriceAsync(RecordMarketPriceRequest request, CancellationToken ct = default)
    {
        var price = new MarketPrice
        {
            City = request.Region,
            MarketName = string.IsNullOrWhiteSpace(request.MarketName) ? "Main Wholesale Mandi" : request.MarketName,
            PricePerKg = request.BroilerRatePerKg,
            PreviousPricePerKg = request.PreviousRatePerKg,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Official Mandi Verification" : request.Source,
            IsVerified = true,
            RecordedAt = DateTime.UtcNow,
            RecordedByUserId = _tenantContext.UserId
        };

        _context.MarketPrices.Add(price);

        // Synchronize city shops if present
        var shops = await _context.PoultryShops
            .Where(s => s.City.ToLower() == request.Region.ToLower())
            .ToListAsync(ct);

        foreach (var shop in shops)
        {
            shop.CurrentLiveBroilerBuyRatePerKg = request.BroilerRatePerKg;
            shop.CurrentBroilerMeatRatePerKg = Math.Round(request.BroilerRatePerKg * 1.62m, 0);
        }

        await _context.SaveChangesAsync(ct);

        var docRate = request.DayOldChickRate > 0 
            ? request.DayOldChickRate 
            : Math.Clamp(Math.Round(82m + (price.PricePerKg - 340m) * 0.35m, 0), 75m, 95m);

        return Result<MarketPriceDto>.Success(new MarketPriceDto(
            price.Id,
            price.City,
            price.PricePerKg,
            CullRatePerKg: request.CullRatePerKg > 0 ? request.CullRatePerKg : price.PricePerKg * 0.7m,
            DayOldChickRate: docRate,
            DateOnly.FromDateTime(price.RecordedAt),
            price.Source,
            price.PreviousPricePerKg,
            price.MarketName,
            price.IsVerified
        ));
    }

    public async Task<Result<IEnumerable<MarketPriceDto>>> GetLatestMarketPricesAsync(string? city = null, CancellationToken ct = default)
    {
        // Guarantee that today's daily market rates exist and are up to date
        await _dailyMarketRateService.EnsureDailyRatesCurrentAsync(ct);

        var query = _context.MarketPrices.AsQueryable();

        if (!string.IsNullOrWhiteSpace(city))
        {
            var filter = city.Trim().ToLower();
            query = query.Where(p => p.City.ToLower().Contains(filter) || p.MarketName.ToLower().Contains(filter));
        }

        var prices = await query.OrderByDescending(p => p.RecordedAt).Take(200).ToListAsync(ct);

        var latestPerCity = prices
            .GroupBy(p => p.City, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.RecordedAt).First())
            .OrderBy(p => p.City)
            .ToList();

        var dtos = latestPerCity.Select(p => {
            var docRate = Math.Clamp(Math.Round(82m + (p.PricePerKg - 340m) * 0.35m, 0), 75m, 95m);
            return new MarketPriceDto(
                p.Id,
                p.City,
                p.PricePerKg,
                Math.Round(p.PricePerKg * 0.7m, 2),
                docRate,
                DateOnly.FromDateTime(p.RecordedAt),
                p.Source,
                p.PreviousPricePerKg,
                p.MarketName,
                p.IsVerified
            );
        });

        return Result<IEnumerable<MarketPriceDto>>.Success(dtos);
    }

    // ── Market Arbitrage Comparison ───────────────────────────────────────────

    public async Task<Result<MarketComparisonDto>> CompareMarketsAsync(MarketComparisonRequest request, CancellationToken ct = default)
    {
        if (request.QuantityKg <= 0)
            return Result<MarketComparisonDto>.Failure("Trading quantity must be greater than zero.");

        var sourcePrice = await _context.MarketPrices
            .Where(p => p.City.ToLower() == request.SourceCity.ToLower())
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync(ct);

        var destPrice = await _context.MarketPrices
            .Where(p => p.City.ToLower() == request.DestinationCity.ToLower())
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync(ct);

        decimal sourceRate = sourcePrice?.PricePerKg ?? 340.0m;
        decimal destRate = destPrice?.PricePerKg ?? 360.0m;
        decimal freightRate = request.EstimatedFreightPerKg ?? 8.5m;

        var spread = destRate - sourceRate;
        var netMargin = BrokerFinancialCalculator.CalculateArbitrageMargin(sourceRate, destRate, freightRate);
        var totalProfit = Math.Round(netMargin * request.QuantityKg, 2);

        var sourceFreshness = sourcePrice != null
            ? $"Recorded on {sourcePrice.RecordedAt:dd MMM yyyy HH:mm} ({sourcePrice.Source ?? "Verified Market Association"})"
            : "Regional baseline reference rate";

        var destFreshness = destPrice != null
            ? $"Recorded on {destPrice.RecordedAt:dd MMM yyyy HH:mm} ({destPrice.Source ?? "Verified Market Association"})"
            : "Regional baseline reference rate";

        var dto = new MarketComparisonDto(
            SourceCity: request.SourceCity,
            DestinationCity: request.DestinationCity,
            SourcePricePerKg: sourceRate,
            DestinationPricePerKg: destRate,
            SourceFreshness: sourceFreshness,
            DestinationFreshness: destFreshness,
            PriceSpreadPerKg: spread,
            EstimatedFreightPerKg: freightRate,
            NetArbitrageMarginPerKg: netMargin,
            EstimatedTotalProfit: totalProfit,
            QuantityKg: request.QuantityKg,
            Disclaimer: "Arbitrage margin estimate based on recorded market prices. Subject to transit shrinkage, local weighing tare, and market price fluctuations. Not a guaranteed profit."
        );

        return Result<MarketComparisonDto>.Success(dto);
    }

    // ── Financial Ledger Entries ──────────────────────────────────────────────

    public async Task<Result<IEnumerable<FinancialLedgerEntryDto>>> GetLedgerEntriesAsync(Guid? dealId = null, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.LedgerEntries.Where(l => l.TenantId == tenantId);

        if (dealId.HasValue)
            query = query.Where(l => l.DealId == dealId.Value);

        var entries = await query.OrderByDescending(l => l.EntryDate).ToListAsync(ct);

        decimal running = 0m;
        var dtos = entries.Select(e =>
        {
            running += e.EntryType == LedgerEntryType.Debit ? e.Amount : -e.Amount;
            return new FinancialLedgerEntryDto(
                e.Id,
                e.DealId,
                e.EntryType.ToString(),
                e.Amount,
                e.Description,
                e.EntryDate,
                running
            );
        });

        return Result<IEnumerable<FinancialLedgerEntryDto>>.Success(dtos);
    }

    // ── Reports & CSV Exports ─────────────────────────────────────────────────

    public async Task<Result<string>> GetDealsReportCsvAsync(CancellationToken ct = default)
    {
        var dealsRes = await GetDealsAsync(null, ct);
        if (!dealsRes.IsSuccess) return Result<string>.Failure(dealsRes.Error ?? "Failed to load deals.");

        var sb = new StringBuilder();
        sb.AppendLine("AERIE POULTRY ERP — BROKER TRADING DEALS REPORT");
        sb.AppendLine($"GeneratedAt,{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("DealId,ListingTitle,FarmerCompany,BrokerCompany,QuantityBirds,AgreedRateRsPerKg,ActualWeightKg,Status,AcceptedAt,GrossAmountPKR,CommissionPKR,FreightPKR,NetPayableToFarmerPKR");

        foreach (var d in dealsRes.Data!)
        {
            sb.AppendLine($"{d.Id},{EscapeCsv(d.ListingTitle)},{EscapeCsv(d.FarmerCompanyName)},{EscapeCsv(d.BrokerCompanyName)},{d.QuantityBirds},{d.AgreedPricePerKg:F2},{d.ActualWeightKg?.ToString("F2") ?? ""},{d.Status},{d.AcceptedAt:yyyy-MM-dd HH:mm},{d.TotalAmount?.ToString("F2") ?? ""},{d.CommissionAmount?.ToString("F2") ?? ""},{d.FreightAmount?.ToString("F2") ?? ""},{d.NetPayableToFarmer?.ToString("F2") ?? ""}");
        }

        return Result<string>.Success(sb.ToString());
    }

    public async Task<Result<string>> GetLedgerReportCsvAsync(CancellationToken ct = default)
    {
        var ledgerRes = await GetLedgerEntriesAsync(null, ct);
        if (!ledgerRes.IsSuccess) return Result<string>.Failure(ledgerRes.Error ?? "Failed to load ledger.");

        var sb = new StringBuilder();
        sb.AppendLine("AERIE POULTRY ERP — BROKER FINANCIAL LEDGER STATEMENT");
        sb.AppendLine($"GeneratedAt,{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("EntryId,DealId,EntryDate,EntryType,AmountPKR,RunningBalancePKR,Description");

        foreach (var e in ledgerRes.Data!)
        {
            sb.AppendLine($"{e.Id},{e.DealId?.ToString() ?? ""},{e.EntryDate:yyyy-MM-dd HH:mm},{e.EntryType},{e.Amount:F2},{e.RunningBalance:F2},{EscapeCsv(e.Description)}");
        }

        return Result<string>.Success(sb.ToString());
    }

    private static string EscapeCsv(string? val) => string.IsNullOrEmpty(val) ? "" : $"\"{val.Replace("\"", "\"\"")}\"";
}
