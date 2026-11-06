namespace AERIE.Application.Farmers.DTOs;

using AERIE.Domain.Enums;

// ── Farm DTOs ──────────────────────────────────────────────────────────────

public record CreateFarmRequest(
    string Name,
    string Location,
    string? City,
    string? Province,
    int TotalCapacity,
    string? ContactPhone,
    string? Notes
);

public record UpdateFarmRequest(
    string Name,
    string Location,
    string? City,
    string? Province,
    int TotalCapacity,
    string? ContactPhone,
    string? Notes,
    bool IsActive
);

public record FarmDto(
    Guid Id,
    string Name,
    string Location,
    string? City,
    string? Province,
    int TotalCapacity,
    string? ContactPhone,
    bool IsActive,
    int HouseCount,
    int ActiveBatchCount
);

// ── PoultryHouse DTOs ──────────────────────────────────────────────────────

public record CreatePoultryHouseRequest(
    Guid FarmId,
    string Name,
    int Capacity,
    string? Description
);

public record PoultryHouseDto(
    Guid Id,
    Guid FarmId,
    string FarmName,
    string Name,
    int Capacity,
    bool IsActive,
    int ActiveBatchCount
);

// ── FlockBatch DTOs ────────────────────────────────────────────────────────

public record CreateFlockBatchRequest(
    Guid FarmId,
    Guid PoultryHouseId,
    Guid BreedId,
    DateOnly PlacementDate,
    int InitialChickCount,
    decimal CostPerChick,
    string? SupplierReference,
    string? Notes
);

public record FlockBatchDto(
    Guid Id,
    Guid FarmId,
    string FarmName,
    Guid PoultryHouseId,
    string HouseName,
    Guid BreedId,
    string BreedName,
    string BatchCode,
    DateOnly PlacementDate,
    int InitialChickCount,
    int CurrentAliveCount,
    decimal CostPerChick,
    BatchStatus Status,
    int AgeDays,
    decimal? LatestFcr,
    decimal? MortalityRatePct,
    decimal? LatestAvgWeightGrams,
    string? Notes
);

// ── DailyFlockRecord DTOs ──────────────────────────────────────────────────

public record CreateDailyRecordRequest(
    Guid BatchId,
    DateOnly RecordDate,
    int AliveCount,
    int DailyMortality,
    decimal FeedConsumedKg,
    decimal WaterConsumedLiters,
    decimal? AverageWeightGrams,
    string? Notes,
    string? ClientRecordId = null,     // Stable UUID from mobile for idempotency
    string? FeedCategory = "Starter",
    int? OpeningCount = null
);

public record DailyFlockRecordDto(
    Guid Id,
    Guid BatchId,
    DateOnly RecordDate,
    int OpeningCount,
    int AliveCount,
    int ClosingCount,
    int DailyMortality,
    string FeedCategory,
    decimal FeedConsumedKg,
    decimal WaterConsumedLiters,
    decimal? AverageWeightGrams,
    string? Notes,
    bool IsAbnormalMortality,
    string? MortalityAlertReason,
    DateTime CreatedAt,
    string? ClientRecordId
);

// ── ProductionMetric DTOs ──────────────────────────────────────────────────

public record ProductionMetricDto(
    Guid BatchId,
    string BatchCode,
    DateTime CalculatedAt,
    int AgeDays,
    decimal? Fcr,
    decimal? MortalityRatePct,
    decimal? AdgGrams,
    decimal? Epef,
    decimal? TotalFeedKg,
    decimal? TotalLiveWeightKg
);

// ── MarketplaceListing DTOs ────────────────────────────────────────────────

public record CreateListingRequest(
    Guid BatchId,
    string Title,
    int QuantityTotal,
    decimal AskingPricePerKg,
    decimal? NegotiableFloorPricePerKg,
    string LocationCity,
    string? LocationDetails,
    DateTime? ExpiresAt,
    string? Notes
);

public record ListingDto(
    Guid Id,
    Guid BatchId,
    string BatchCode,
    string BreedName,
    string Title,
    int QuantityTotal,
    int QuantityAvailable,
    decimal AskingPricePerKg,
    decimal? NegotiableFloorPricePerKg,
    string LocationCity,
    string Status,
    DateTime? PublishedAt,
    DateTime? ExpiresAt,
    string? Notes
);

// ── Farmer Dashboard & Alert DTOs ──────────────────────────────────────────

public record MortalityAlertDto(
    Guid BatchId,
    string BatchCode,
    string FarmName,
    string HouseName,
    DateOnly Date,
    int DailyMortality,
    decimal MortalityPct,
    decimal ThresholdPct,
    string TriggerReason
);

public record FlockMilestoneDto(
    Guid BatchId,
    string BatchCode,
    string HouseName,
    int CurrentAgeDays,
    int TargetDay,
    string MilestoneTitle,
    string Description,
    bool IsDueToday,
    bool IsCompleted
);

public record FarmerDashboardSummaryDto(
    int TotalFarms,
    int TotalHouses,
    int ActiveBatchesCount,
    int TotalChicksPlaced,
    int CurrentLiveBirdCount,
    int TodayMortality,
    int CumulativeMortality,
    decimal CumulativeMortalityPct,
    decimal FeedConsumedTodayKg,
    decimal FeedConsumedPeriodKg,
    decimal WaterConsumedTodayLiters,
    decimal WaterConsumedPeriodLiters,
    int SaleReadyBirdsCount,
    int ReservedBirdsCount,
    int UncommittedAvailableBirdsCount,
    int ActiveListingsCount,
    int PendingBrokerProposalsCount,
    IEnumerable<MortalityAlertDto> Alerts,
    IEnumerable<FlockMilestoneDto> UpcomingMilestones,
    IEnumerable<DailyFlockRecordDto> RecentDailyLogs,
    IEnumerable<FlockBatchDto> Batches
);

// ── Feed Inventory DTOs ────────────────────────────────────────────────────

public record FeedInventoryDto(
    Guid Id,
    Guid FarmId,
    string FarmName,
    string FeedCategory,
    decimal CurrentStockKg,
    decimal MinimumStockThresholdKg,
    decimal? UnitPricePerKg,
    bool IsLowStock,
    string? SupplierName,
    DateTime LastRestockedAt
);

public record RestockFeedRequest(
    Guid FarmId,
    string FeedCategory,
    decimal QuantityKg,
    decimal? UnitPricePerKg,
    string? SupplierName,
    string? Notes
);

public record UpdatePoultryHouseRequest(
    string Name,
    int Capacity,
    string? Description,
    bool IsActive
);

public record UpdateDailyRecordRequest(
    Guid Id,
    int AliveCount,
    int DailyMortality,
    string? FeedCategory,
    decimal FeedConsumedKg,
    decimal WaterConsumedLiters,
    decimal? AverageWeightGrams,
    string? Notes,
    string CorrectionReason
);

// ── Farmer Financial DTOs ──────────────────────────────────────────────────

public record FarmerDealDto(
    Guid DealId,
    string DealNumber,
    Guid ProposalId,
    string BrokerTenantName,
    decimal AgreedPricePerKg,
    int RequestedQuantity,
    decimal? LiftedWeightKg,
    string Status,
    decimal GrossAmount,
    decimal NetPayout,
    decimal AmountPaid,
    decimal BalanceDue,
    DateTime CreatedAt,
    DateTime? LiftingDate
);

public record FarmerInvoiceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    string DealNumber,
    string BuyerName,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal TotalAmount,
    decimal PaidAmount,
    string Status
);

public record FarmerPaymentDto(
    Guid PaymentId,
    string InvoiceNumber,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    DateTime PaidAt
);

public record FarmerFinancialSummaryDto(
    decimal TotalGrossSales,
    decimal TotalBrokerCommissions,
    decimal TotalFreightDeductions,
    decimal NetFarmerReceivables,
    decimal TotalPaymentsReceived,
    decimal OutstandingBalance,
    IEnumerable<FarmerDealDto> Deals,
    IEnumerable<FarmerInvoiceDto> Invoices,
    IEnumerable<FarmerPaymentDto> Payments
);

// ── Reports DTOs ───────────────────────────────────────────────────────────

public record ProductionReportRowDto(
    DateOnly Date,
    int DayAge,
    int OpeningCount,
    int DailyMortality,
    int ClosingCount,
    string FeedCategory,
    decimal FeedConsumedKg,
    decimal WaterConsumedLiters,
    decimal? AverageWeightGrams,
    decimal? DailyFcr,
    decimal? DailyAdg
);

public record FlockProductionReportDto(
    Guid BatchId,
    string BatchCode,
    string FarmName,
    string HouseName,
    string BreedName,
    DateOnly PlacementDate,
    int InitialChickCount,
    int CurrentAliveCount,
    int TotalMortalityCount,
    decimal MortalityRatePct,
    decimal TotalFeedConsumedKg,
    decimal TotalWaterConsumedLiters,
    decimal? LatestAvgWeightGrams,
    decimal? OverallFcr,
    decimal? OverallAdg,
    decimal? Epef,
    IEnumerable<ProductionReportRowDto> DailyRows
);
