namespace AERIE.Application.Sync.Interfaces;

using AERIE.Application.Common.Models;
using AERIE.Application.Sync.DTOs;

public interface ISyncService
{
    Task<Result<SyncPushResponse>> PushSyncAsync(SyncPushRequest request, CancellationToken ct = default);
    Task<Result<SyncPullResponse>> PullSyncAsync(DateTime? since = null, CancellationToken ct = default);
}
