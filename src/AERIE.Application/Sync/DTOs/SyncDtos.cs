namespace AERIE.Application.Sync.DTOs;

using AERIE.Application.Farmers.DTOs;

public record DailyRecordSyncItem(
    string ClientRecordId,
    Guid BatchId,
    DateOnly RecordDate,
    int AliveCount,
    int DailyMortality,
    decimal FeedConsumedKg,
    decimal WaterConsumedLiters,
    decimal? AverageWeightGrams,
    string? Notes,
    int ClientVersion,
    DateTime ClientTimestamp
);

public record SyncPushRequest(
    List<DailyRecordSyncItem> Records
);

public record SyncItemResult(
    string ClientRecordId,
    Guid? ServerRecordId,
    bool Success,
    string? ErrorMessage,
    int ServerVersion
);

public record SyncPushResponse(
    List<SyncItemResult> Results,
    DateTime SyncedAt
);

public record SyncPullResponse(
    List<DailyFlockRecordDto> DailyRecords,
    List<FlockBatchDto> Batches,
    DateTime ServerTimestamp
);
