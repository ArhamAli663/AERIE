namespace AERIE.Application.Brokers.Interfaces;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Common.Models;
using AERIE.Domain.Enums;

public interface IBrokerService
{
    // Dashboard
    Task<Result<BrokerDashboardSummaryDto>> GetBrokerDashboardSummaryAsync(CancellationToken ct = default);

    // Proposals
    Task<Result<DealProposalDto>> CreateProposalAsync(CreateProposalRequest request, CancellationToken ct = default);
    Task<Result<IEnumerable<DealProposalDto>>> GetMyProposalsAsync(CancellationToken ct = default);
    Task<Result<IEnumerable<DealProposalDto>>> GetProposalsForListingAsync(Guid listingId, CancellationToken ct = default);
    Task<Result<DealDto>> ReviewProposalAsync(Guid proposalId, ReviewProposalRequest request, CancellationToken ct = default);
    Task<Result<bool>> WithdrawProposalAsync(Guid proposalId, WithdrawProposalRequest request, CancellationToken ct = default);

    // Deals
    Task<Result<IEnumerable<DealDto>>> GetDealsAsync(DealStatus? status = null, CancellationToken ct = default);
    Task<Result<DealDto>> GetDealByIdAsync(Guid dealId, CancellationToken ct = default);
    Task<Result<DealDto>> UpdateDealStatusAsync(Guid dealId, UpdateDealStatusRequest request, CancellationToken ct = default);

    // Transport, Lifting & Delivery
    Task<Result<TransportRecordDto>> RecordLiftingAsync(Guid dealId, RecordLiftingRequest request, CancellationToken ct = default);
    Task<Result<TransportDiscrepancyDto>> RecordDeliveryAsync(Guid dealId, RecordDeliveryRequest request, CancellationToken ct = default);
    Task<Result<IEnumerable<TransportRecordDto>>> GetTransportRecordsAsync(Guid dealId, CancellationToken ct = default);

    // Settlement
    Task<Result<SettlementDto>> CreateSettlementAsync(Guid dealId, CreateSettlementRequest request, CancellationToken ct = default);
    Task<Result<SettlementDto>> GetSettlementByDealIdAsync(Guid dealId, CancellationToken ct = default);

    // Invoices, Payments & Ledgers
    Task<Result<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken ct = default);
    Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<Result<IEnumerable<InvoiceDto>>> GetInvoicesByDealIdAsync(Guid dealId, CancellationToken ct = default);
    Task<Result<PaymentDto>> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken ct = default);
    Task<Result<IEnumerable<FinancialLedgerEntryDto>>> GetLedgerEntriesAsync(Guid? dealId = null, CancellationToken ct = default);

    // Market Prices & Arbitrage Comparison
    Task<Result<MarketPriceDto>> RecordMarketPriceAsync(RecordMarketPriceRequest request, CancellationToken ct = default);
    Task<Result<IEnumerable<MarketPriceDto>>> GetLatestMarketPricesAsync(string? city = null, CancellationToken ct = default);
    Task<Result<MarketComparisonDto>> CompareMarketsAsync(MarketComparisonRequest request, CancellationToken ct = default);

    // Reports & Exports
    Task<Result<string>> GetDealsReportCsvAsync(CancellationToken ct = default);
    Task<Result<string>> GetLedgerReportCsvAsync(CancellationToken ct = default);
}
