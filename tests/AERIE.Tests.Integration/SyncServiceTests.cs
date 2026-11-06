namespace AERIE.Tests.Integration;

using AERIE.Application.Common.Interfaces;
using AERIE.Application.Farmers.Services;
using AERIE.Application.Sync.DTOs;
using AERIE.Application.Sync.Services;
using AERIE.Domain.Entities;
using AERIE.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public class SyncServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private (ApplicationDbContext Db, SyncService SyncSvc) SetupEnvironment(string dbName)
    {
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.TenantId).Returns(_tenantId);
        tenantContextMock.Setup(t => t.UserId).Returns(_userId);
        tenantContextMock.Setup(t => t.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var db = new ApplicationDbContext(options, tenantContextMock.Object);
        var farmerLogger = NullLogger<FarmerService>.Instance;
        var syncLogger = NullLogger<SyncService>.Instance;

        var farmerSvc = new FarmerService(db, tenantContextMock.Object, farmerLogger);
        var syncSvc = new SyncService(db, tenantContextMock.Object, farmerSvc, syncLogger);

        return (db, syncSvc);
    }

    [Fact]
    public async Task PushSync_WithDuplicateClientRecordId_IsIdempotentAndDoesNotDuplicateRecords()
    {
        var dbName = Guid.NewGuid().ToString();
        var batchId = Guid.NewGuid();
        var clientRecordId = Guid.NewGuid().ToString();

        // Arrange: Batch exists
        {
            var (db, _) = SetupEnvironment(dbName);
            db.FlockBatches.Add(new FlockBatch
            {
                Id = batchId,
                TenantId = _tenantId,
                BatchCode = "BATCH-SYNC-01",
                InitialChickCount = 1000,
                CurrentAliveCount = 1000,
                PlacementDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
            });
            await db.SaveChangesAsync();
        }

        var syncItem = new DailyRecordSyncItem(
            ClientRecordId: clientRecordId,
            BatchId: batchId,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
            AliveCount: 990,
            DailyMortality: 10,
            FeedConsumedKg: 100m,
            WaterConsumedLiters: 200m,
            AverageWeightGrams: 500m,
            Notes: "Captured offline in Flutter app",
            ClientVersion: 1,
            ClientTimestamp: DateTime.UtcNow
        );

        // Act 1: First sync push
        {
            var (_, syncSvc1) = SetupEnvironment(dbName);
            var res1 = await syncSvc1.PushSyncAsync(new SyncPushRequest([syncItem]));
            res1.IsSuccess.Should().BeTrue();
            res1.Data!.Results.Should().HaveCount(1);
            res1.Data.Results[0].Success.Should().BeTrue();
            res1.Data.Results[0].ServerRecordId.Should().NotBeNull();
        }

        // Act 2: Second sync push (retry or network replay with identical ClientRecordId)
        {
            var (db2, syncSvc2) = SetupEnvironment(dbName);
            var res2 = await syncSvc2.PushSyncAsync(new SyncPushRequest([syncItem]));
            res2.IsSuccess.Should().BeTrue();
            res2.Data!.Results.Should().HaveCount(1);
            res2.Data.Results[0].Success.Should().BeTrue();

            // Assert: Exactly ONE record persisted in database!
            var count = await db2.DailyFlockRecords.CountAsync(r => r.BatchId == batchId);
            count.Should().Be(1);
        }
    }
}
