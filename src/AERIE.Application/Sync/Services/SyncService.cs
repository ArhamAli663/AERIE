namespace AERIE.Application.Sync.Services;

using AERIE.Application.Common.Interfaces;
using AERIE.Application.Common.Models;
using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using AERIE.Application.Sync.DTOs;
using AERIE.Application.Sync.Interfaces;
using AERIE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class SyncService : ISyncService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IFarmerService _farmerService;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        IFarmerService farmerService,
        ILogger<SyncService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _farmerService = farmerService;
        _logger = logger;
    }

    public async Task<Result<SyncPushResponse>> PushSyncAsync(SyncPushRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<SyncPushResponse>.Failure("Unauthorized tenant context for synchronization.");

        var results = new List<SyncItemResult>();
        var modifiedBatchIds = new HashSet<Guid>();

        foreach (var item in request.Records)
        {
            try
            {
                // Validate batch exists and belongs to tenant
                var batch = await _context.FlockBatches.FirstOrDefaultAsync(b => b.Id == item.BatchId, ct);
                if (batch == null)
                {
                    results.Add(new SyncItemResult(
                        item.ClientRecordId,
                        ServerRecordId: null,
                        Success: false,
                        ErrorMessage: "Batch not found for tenant.",
                        ServerVersion: 0
                    ));
                    continue;
                }

                // Check for idempotent duplicate submission
                var existing = await _context.DailyFlockRecords
                    .FirstOrDefaultAsync(r => r.ClientRecordId == item.ClientRecordId, ct);

                if (existing != null)
                {
                    // Existing record: version conflict resolution
                    if (item.ClientVersion < existing.RowVersion)
                    {
                        // Server has newer version: server-wins reconciliation policy
                        results.Add(new SyncItemResult(
                            item.ClientRecordId,
                            existing.Id,
                            Success: true,
                            ErrorMessage: "Server contains a newer version. Local record superseded.",
                            ServerVersion: existing.RowVersion
                        ));
                    }
                    else
                    {
                        // Idempotent retry: confirm success
                        results.Add(new SyncItemResult(
                            item.ClientRecordId,
                            existing.Id,
                            Success: true,
                            ErrorMessage: null,
                            ServerVersion: existing.RowVersion
                        ));
                    }
                    continue;
                }

                // New record insertion
                var avgFeedPerBird = item.AliveCount > 0
                    ? Math.Round((item.FeedConsumedKg * 1000m) / item.AliveCount, 2)
                    : 0;

                var newRecord = new DailyFlockRecord
                {
                    TenantId = tenantId,
                    BatchId = item.BatchId,
                    RecordDate = item.RecordDate,
                    AliveCount = item.AliveCount,
                    DailyMortality = item.DailyMortality,
                    FeedConsumedKg = item.FeedConsumedKg,
                    WaterConsumedLiters = item.WaterConsumedLiters,
                    AverageWeightGrams = item.AverageWeightGrams,
                    AverageFeedPerBirdGrams = avgFeedPerBird,
                    Notes = item.Notes,
                    RecordedByUserId = _tenantContext.UserId != Guid.Empty ? _tenantContext.UserId : null,
                    ClientRecordId = item.ClientRecordId,
                    RowVersion = 1
                };

                _context.DailyFlockRecords.Add(newRecord);

                // Update batch live counts
                batch.CurrentAliveCount = item.AliveCount;
                if (item.AverageWeightGrams.HasValue)
                {
                    batch.LatestAvgWeightGrams = item.AverageWeightGrams.Value;
                }

                modifiedBatchIds.Add(batch.Id);

                results.Add(new SyncItemResult(
                    item.ClientRecordId,
                    newRecord.Id,
                    Success: true,
                    ErrorMessage: null,
                    ServerVersion: 1
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sync item {ClientRecordId}", item.ClientRecordId);
                results.Add(new SyncItemResult(
                    item.ClientRecordId,
                    ServerRecordId: null,
                    Success: false,
                    ErrorMessage: ex.Message,
                    ServerVersion: 0
                ));
            }
        }

        await _context.SaveChangesAsync(ct);

        // Recalculate metrics for affected batches
        foreach (var batchId in modifiedBatchIds)
        {
            try
            {
                await _farmerService.CalculateAndStoreMetricsAsync(batchId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to recalculate metrics for batch {BatchId} during sync", batchId);
            }
        }

        return Result<SyncPushResponse>.Success(new SyncPushResponse(results, DateTime.UtcNow));
    }

    public async Task<Result<SyncPullResponse>> PullSyncAsync(DateTime? since = null, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<SyncPullResponse>.Failure("Unauthorized tenant context.");

        var recordsQuery = _context.DailyFlockRecords.AsQueryable();
        var batchesQuery = _context.FlockBatches
            .Include(b => b.Farm)
            .Include(b => b.PoultryHouse)
            .Include(b => b.Breed)
            .AsQueryable();

        if (since.HasValue)
        {
            recordsQuery = recordsQuery.Where(r => r.CreatedAt >= since.Value);
            batchesQuery = batchesQuery.Where(b => b.CreatedAt >= since.Value);
        }

        var records = await recordsQuery.OrderByDescending(r => r.RecordDate).Take(200).ToListAsync(ct);
        var batches = await batchesQuery.OrderByDescending(b => b.PlacementDate).Take(50).ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var recordDtos = records.Select(r => new DailyFlockRecordDto(
            r.Id,
            r.BatchId,
            r.RecordDate,
            r.OpeningCount,
            r.AliveCount,
            r.ClosingCount,
            r.DailyMortality,
            r.FeedCategory ?? "Starter",
            r.FeedConsumedKg,
            r.WaterConsumedLiters,
            r.AverageWeightGrams,
            r.Notes,
            r.IsAbnormalMortality,
            r.MortalityAlertReason,
            r.CreatedAt,
            r.ClientRecordId
        )).ToList();

        var batchDtos = batches.Select(b => new FlockBatchDto(
            b.Id,
            b.FarmId,
            b.Farm?.Name ?? "Farm",
            b.PoultryHouseId,
            b.PoultryHouse?.Name ?? "House",
            b.BreedId,
            b.Breed?.Name ?? "Broiler",
            b.BatchCode,
            b.PlacementDate,
            b.InitialChickCount,
            b.CurrentAliveCount,
            b.CostPerChick,
            b.Status,
            today.DayNumber - b.PlacementDate.DayNumber,
            b.CalculatedFcr,
            b.TotalMortality,
            b.LatestAvgWeightGrams,
            b.Notes
        )).ToList();

        return Result<SyncPullResponse>.Success(new SyncPullResponse(recordDtos, batchDtos, DateTime.UtcNow));
    }
}
