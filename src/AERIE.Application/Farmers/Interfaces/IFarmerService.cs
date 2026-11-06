namespace AERIE.Application.Farmers.Interfaces;

using AERIE.Application.Common.Models;
using AERIE.Application.Farmers.DTOs;
using AERIE.Domain.Enums;

public interface IFarmerService
{
    // Dashboard & Overview
    Task<Result<FarmerDashboardSummaryDto>> GetFarmerDashboardSummaryAsync(CancellationToken ct = default);

    // Farms
    Task<Result<FarmDto>> CreateFarmAsync(CreateFarmRequest request, CancellationToken ct = default);
    Task<Result<FarmDto>> UpdateFarmAsync(Guid id, UpdateFarmRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeactivateFarmAsync(Guid id, CancellationToken ct = default);
    Task<Result<IEnumerable<FarmDto>>> GetFarmsAsync(CancellationToken ct = default);
    Task<Result<FarmDto>> GetFarmByIdAsync(Guid id, CancellationToken ct = default);

    // Poultry Houses
    Task<Result<PoultryHouseDto>> CreatePoultryHouseAsync(CreatePoultryHouseRequest request, CancellationToken ct = default);
    Task<Result<PoultryHouseDto>> UpdatePoultryHouseAsync(Guid id, UpdatePoultryHouseRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeactivatePoultryHouseAsync(Guid id, CancellationToken ct = default);
    Task<Result<IEnumerable<PoultryHouseDto>>> GetPoultryHousesAsync(Guid? farmId = null, CancellationToken ct = default);

    // Flock Batches
    Task<Result<FlockBatchDto>> CreateFlockBatchAsync(CreateFlockBatchRequest request, CancellationToken ct = default);
    Task<Result<IEnumerable<FlockBatchDto>>> GetFlockBatchesAsync(Guid? farmId = null, BatchStatus? status = null, CancellationToken ct = default);
    Task<Result<FlockBatchDto>> GetFlockBatchByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<FlockBatchDto>> MarkBatchReadyForSaleAsync(Guid batchId, CancellationToken ct = default);

    // Daily Records & Performance Metrics
    Task<Result<DailyFlockRecordDto>> RecordDailyDataAsync(CreateDailyRecordRequest request, CancellationToken ct = default);
    Task<Result<DailyFlockRecordDto>> UpdateDailyDataAsync(UpdateDailyRecordRequest request, CancellationToken ct = default);
    Task<Result<IEnumerable<DailyFlockRecordDto>>> GetDailyRecordsAsync(Guid batchId, CancellationToken ct = default);
    Task<Result<ProductionMetricDto>> GetProductionMetricsAsync(Guid batchId, CancellationToken ct = default);
    Task<Result<ProductionMetricDto>> CalculateAndStoreMetricsAsync(Guid batchId, CancellationToken ct = default);

    // Feed Inventory & Stock Management
    Task<Result<IEnumerable<FeedInventoryDto>>> GetFeedInventoriesAsync(Guid? farmId = null, CancellationToken ct = default);
    Task<Result<FeedInventoryDto>> RestockFeedAsync(RestockFeedRequest request, CancellationToken ct = default);

    // Marketplace Listings & Broker Interaction
    Task<Result<ListingDto>> CreateListingAsync(CreateListingRequest request, CancellationToken ct = default);
    Task<Result<ListingDto>> PublishListingAsync(Guid listingId, CancellationToken ct = default);
    Task<Result<ListingDto>> CancelListingAsync(Guid listingId, CancellationToken ct = default);
    Task<Result<IEnumerable<ListingDto>>> GetMyListingsAsync(ListingStatus? status = null, CancellationToken ct = default);
    Task<Result<IEnumerable<ListingDto>>> GetPublicMarketplaceListingsAsync(string? city = null, CancellationToken ct = default);

    // Financial View & Statements
    Task<Result<FarmerFinancialSummaryDto>> GetFarmerFinancialSummaryAsync(CancellationToken ct = default);

    // Reports & Export
    Task<Result<FlockProductionReportDto>> GetFlockProductionReportAsync(Guid batchId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);
    Task<Result<string>> GetProductionReportCsvAsync(Guid batchId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken ct = default);
    Task<Result<string>> GetFarmerFinancialReportCsvAsync(CancellationToken ct = default);
}
