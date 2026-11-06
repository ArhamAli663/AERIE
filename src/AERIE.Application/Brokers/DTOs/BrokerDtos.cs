namespace AERIE.Application.Brokers.DTOs;

using AERIE.Domain.Enums;

// ── Deal Proposal DTOs ────────────────────────────────────────────────────────

public record CreateProposalRequest(
    Guid ListingId,
    int QuantityRequested,
    decimal OfferedPricePerKg,
    string? TransportArrangement,
    string? Notes
);

public record ReviewProposalRequest(
    bool Accept,
    string? ResponseNotes
);

public record DealProposalDto(
    Guid Id,
    Guid ListingId,
    string ListingTitle,
    Guid BrokerTenantId,
    string BrokerCompanyName,
    Guid ProposedByUserId,
    int QuantityRequested,
    decimal OfferedPricePerKg,
    string? TransportArrangement,
    string? Notes,
    DealStatus Status,
    DateTime ProposedAt,
    string? FarmerResponse,
    DateTime? RespondedAt,
    Guid? DealId
);

// ── Deal DTOs ─────────────────────────────────────────────────────────────────

public record DealDto(
    Guid Id,
    Guid ProposalId,
    Guid ListingId,
    string ListingTitle,
    Guid FarmerTenantId,
    string FarmerCompanyName,
    Guid BrokerTenantId,
    string BrokerCompanyName,
    int QuantityBirds,
    decimal AgreedPricePerKg,
    decimal? EstimatedWeightKg,
    decimal? ActualWeightKg,
    DealStatus Status,
    DateTime AcceptedAt,
    DateTime? ClosedAt,
    string? Notes,
    decimal? TotalAmount,
    decimal? CommissionAmount,
    decimal? FreightAmount,
    decimal? NetPayableToFarmer
);

public record UpdateDealStatusRequest(
    DealStatus NewStatus,
    string? Notes
);

// ── Transport / Lifting DTOs ──────────────────────────────────────────────────

public record RecordLiftingRequest(
    decimal ActualWeightKg,
    int BirdsLoaded,
    int MortalityDuringTransit,
    string? VehicleNumber,
    string? DriverName,
    string? DriverPhone,
    string? WeighbridgeTicketNumber,
    string? Notes
);

public record TransportRecordDto(
    Guid Id,
    Guid DealId,
    string? VehicleNumber,
    string? DriverName,
    string? DriverPhone,
    int BirdsLoaded,
    int MortalityDuringTransit,
    decimal NetWeightKg,
    string? WeighbridgeTicketNumber,
    DateTime DispatchTime,
    DateTime? ArrivalTime,
    string? Notes
);

// ── Settlement DTOs ───────────────────────────────────────────────────────────

public record CreateSettlementRequest(
    decimal CommissionRate,
    CommissionRuleType CommissionType,
    decimal FreightAmount,
    decimal Deductions,
    decimal Additions,
    string? Notes
);

public record SettlementDto(
    Guid Id,
    Guid DealId,
    decimal TotalGrossAmount,
    decimal CommissionAmount,
    decimal FreightAmount,
    decimal Deductions,
    decimal Additions,
    decimal NetFarmerPayable,
    decimal NetBrokerReceivable,
    bool IsFinalized,
    DateTime? FinalizedAt,
    string? Notes
);

// ── Invoice DTOs ──────────────────────────────────────────────────────────────

public record CreateInvoiceRequest(
    Guid DealId,
    Guid RecipientTenantId,
    string RecipientName,
    string? RecipientAddress,
    decimal SubTotal,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTime DueDate,
    string? Notes
);

public record InvoiceDto(
    Guid Id,
    Guid DealId,
    string InvoiceNumber,
    Guid IssuerTenantId,
    Guid RecipientTenantId,
    string RecipientName,
    decimal SubTotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceDue,
    string Status,
    DateTime IssueDate,
    DateTime DueDate,
    string? Notes
);

// ── Payment DTOs ──────────────────────────────────────────────────────────────

public record RecordPaymentRequest(
    Guid DealId,
    Guid? InvoiceId,
    decimal Amount,
    PaymentMethod Method,
    string? ReferenceNumber,
    string? BankName,
    string? Notes
);

public record PaymentDto(
    Guid Id,
    Guid DealId,
    Guid? InvoiceId,
    string PaymentReference,
    decimal Amount,
    PaymentMethod Method,
    string? ReferenceNumber,
    string? BankName,
    string Status,
    DateTime PaymentDate,
    string? Notes
);

// ── Market Price DTOs ─────────────────────────────────────────────────────────

public record MarketPriceDto(
    Guid Id,
    string Region,
    decimal BroilerRatePerKg,
    decimal CullRatePerKg,
    decimal DayOldChickRate,
    DateOnly PriceDate,
    string? Source,
    decimal? PreviousRatePerKg = null,
    string? MarketName = "Main Mandi",
    bool IsVerified = true
);

public record RecordMarketPriceRequest(
    string Region,
    decimal BroilerRatePerKg,
    decimal CullRatePerKg,
    decimal DayOldChickRate,
    DateOnly PriceDate,
    string? Source,
    string? MarketName = "Main Mandi",
    decimal? PreviousRatePerKg = null
);

// ── Proposal Withdrawal & Negotiation DTOs ─────────────────────────────────

public record WithdrawProposalRequest(
    string? Reason
);

// ── Delivery Confirmation & Discrepancy DTOs ───────────────────────────────

public record RecordDeliveryRequest(
    int BirdsDelivered,
    int MortalityDuringTransit,
    decimal WeightDeliveredKg,
    string? DestinationLocation,
    decimal? FreightCost,
    string? Notes
);

public record TransportDiscrepancyDto(
    Guid DealId,
    int BirdsLoaded,
    int BirdsDelivered,
    int MortalityDuringTransit,
    int MissingBirdsCount,
    decimal WeightLiftedKg,
    decimal WeightDeliveredKg,
    decimal ShrinkageKg,
    decimal ShrinkagePct,
    string DeliveryStatus,
    string? Notes
);

// ── Market Arbitrage Comparison DTOs ───────────────────────────────────────

public record MarketComparisonRequest(
    string SourceCity,
    string DestinationCity,
    decimal QuantityKg,
    decimal? EstimatedFreightPerKg = null
);

public record MarketComparisonDto(
    string SourceCity,
    string DestinationCity,
    decimal SourcePricePerKg,
    decimal DestinationPricePerKg,
    string SourceFreshness,
    string DestinationFreshness,
    decimal PriceSpreadPerKg,
    decimal EstimatedFreightPerKg,
    decimal NetArbitrageMarginPerKg,
    decimal EstimatedTotalProfit,
    decimal QuantityKg,
    string Disclaimer
);

// ── Broker Dashboard DTOs ──────────────────────────────────────────────────

public record BrokerDashboardSummaryDto(
    int TotalPublishedListingsAvailable,
    int NewFarmerOffersCount,
    int PendingProposalsCount,
    int ActiveDealsCount,
    int CommittedBirdsCount,
    int TodayPurchasedBirdsCount,
    int TodaySoldBirdsCount,
    decimal TotalDealValue,
    decimal ExpectedCommission,
    decimal RealizedCommission,
    decimal TotalTransportCosts,
    decimal TotalAmountsPayableToFarmers,
    decimal TotalAmountsReceivableFromBuyers,
    int PendingLiftingCount,
    int PendingDeliveryCount,
    int UnpaidInvoicesCount,
    int OutstandingSettlementsCount,
    IEnumerable<MarketPriceDto> RecentPrices,
    IEnumerable<DealDto> ActiveDeals,
    IEnumerable<DealProposalDto> PendingProposals
);

// ── Financial Ledger Entry DTO ─────────────────────────────────────────────

public record FinancialLedgerEntryDto(
    Guid Id,
    Guid? DealId,
    string EntryType, // Debit or Credit
    decimal Amount,
    string Description,
    DateTime EntryDate,
    decimal RunningBalance
);

