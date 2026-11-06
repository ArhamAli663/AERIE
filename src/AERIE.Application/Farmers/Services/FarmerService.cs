namespace AERIE.Application.Farmers.Services;

using System.Text;
using AERIE.Application.Common.Interfaces;
using AERIE.Application.Common.Models;
using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using AERIE.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class FarmerService : IFarmerService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<FarmerService> _logger;

    public FarmerService(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        ILogger<FarmerService> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ── 1. Farmer Dashboard & Operational Overview ─────────────────────────────

    public async Task<Result<FarmerDashboardSummaryDto>> GetFarmerDashboardSummaryAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<FarmerDashboardSummaryDto>.Failure("Unauthorized tenant context.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        // Active and ready-for-sale batches
        var batches = await _context.FlockBatches
            .Include(b => b.Farm)
            .Include(b => b.PoultryHouse)
            .Include(b => b.Breed)
            .OrderByDescending(b => b.PlacementDate)
            .ToListAsync(ct);

        var activeBatches = batches
            .Where(b => b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale)
            .ToList();

        var activeBatchIds = activeBatches.Select(b => b.Id).ToList();

        // Farm & house counts
        var totalFarms = await _context.Farms.CountAsync(f => f.IsActive, ct);
        var totalHouses = await _context.PoultryHouses.CountAsync(h => h.IsActive, ct);

        // Birds metrics
        var totalPlaced = activeBatches.Sum(b => b.InitialChickCount);
        var currentLiveBirds = activeBatches.Sum(b => b.CurrentAliveCount);
        var cumulativeDead = totalPlaced > currentLiveBirds ? totalPlaced - currentLiveBirds : 0;
        var cumulativeMortalityPct = totalPlaced > 0
            ? Math.Round(((decimal)cumulativeDead / totalPlaced) * 100m, 2)
            : 0m;

        // Daily records aggregation
        var recentLogs = await _context.DailyFlockRecords
            .Where(r => activeBatchIds.Contains(r.BatchId))
            .OrderByDescending(r => r.RecordDate)
            .Take(15)
            .ToListAsync(ct);

        var todayLogs = await _context.DailyFlockRecords
            .Where(r => activeBatchIds.Contains(r.BatchId) && r.RecordDate == today)
            .ToListAsync(ct);

        var todayMortality = todayLogs.Sum(r => r.DailyMortality);
        var feedTodayKg = todayLogs.Sum(r => r.FeedConsumedKg);
        var waterTodayLiters = todayLogs.Sum(r => r.WaterConsumedLiters);

        // Period consumption (last 30 days)
        var periodLogs = await _context.DailyFlockRecords
            .Where(r => activeBatchIds.Contains(r.BatchId) && r.RecordDate >= thirtyDaysAgo)
            .ToListAsync(ct);

        var feedPeriodKg = periodLogs.Sum(r => r.FeedConsumedKg);
        var waterPeriodLiters = periodLogs.Sum(r => r.WaterConsumedLiters);

        // Sale Readiness & Reservation breakdown
        var saleReadyBirds = activeBatches
            .Where(b => b.Status == BatchStatus.ReadyForSale)
            .Sum(b => b.CurrentAliveCount);

        // Calculate reserved birds in pending or active trade deals
        var activeDeals = await _context.Deals
            .Where(d => d.FarmerTenantId == tenantId &&
                        (d.Status == DealStatus.Accepted || d.Status == DealStatus.InTransit))
            .ToListAsync(ct);

        var reservedBirds = activeDeals.Sum(d => d.QuantityBirds);
        var uncommittedAvailableBirds = Math.Max(0, saleReadyBirds - reservedBirds);

        // Marketplace listings count
        var activeListingsCount = await _context.MarketplaceListings
            .CountAsync(l => l.Status == ListingStatus.Published, ct);

        // Received broker proposals count
        var myListingIds = await _context.MarketplaceListings.Select(l => l.Id).ToListAsync(ct);
        var pendingProposalsCount = await _context.DealProposals
            .IgnoreQueryFilters()
            .CountAsync(p => myListingIds.Contains(p.ListingId) && p.Status == DealStatus.Proposed, ct);

        // Generate Abnormal Mortality Alerts
        var alerts = new List<MortalityAlertDto>();
        foreach (var batch in activeBatches)
        {
            var batchLogs = await _context.DailyFlockRecords
                .Where(r => r.BatchId == batch.Id)
                .OrderByDescending(r => r.RecordDate)
                .Take(7)
                .ToListAsync(ct);

            foreach (var log in batchLogs)
            {
                if (batch.InitialChickCount > 0)
                {
                    var singleDayPct = ((decimal)log.DailyMortality / batch.InitialChickCount) * 100m;
                    if (singleDayPct >= 1.5m || log.IsAbnormalMortality)
                    {
                        alerts.Add(new MortalityAlertDto(
                            batch.Id,
                            batch.BatchCode,
                            batch.Farm.Name,
                            batch.PoultryHouse.Name,
                            log.RecordDate,
                            log.DailyMortality,
                            Math.Round(singleDayPct, 2),
                            1.5m,
                            log.MortalityAlertReason ?? $"Daily mortality spike ({log.DailyMortality} birds, {singleDayPct:F1}%) exceeded 1.5% warning threshold."
                        ));
                    }
                }
            }
        }

        // Generate Upcoming Broiler Milestones
        var upcomingMilestones = new List<FlockMilestoneDto>();
        foreach (var b in activeBatches)
        {
            var ageDays = Math.Max(0, today.DayNumber - b.PlacementDate.DayNumber);

            var schedules = new (int TargetDay, string Title, string Desc)[]
            {
                (7, "First Week Weight & Starter Evaluation", "Measure 5% flock weight sample to verify 160g-180g target."),
                (12, "Gumboro (IBD) Vaccine Administration", "Administer via drinking water with skim milk stabilizer."),
                (18, "Newcastle Disease (ND+IB) Booster", "Administer booster coarse spray or eye drop."),
                (21, "Grower Feed Transition Phase", "Shift flock gradually from Starter to Grower feed formulation."),
                (30, "Finisher Feed Switch", "Transition to high-energy Finisher feed ration."),
                (35, "Market Weight Verification (1.8kg+)", "Perform sample weighings across 100 birds per shed."),
                (42, "Target Sale Readiness Window", "Initiate broker listing for mature harvest weight (2.2kg-2.4kg).")
            };

            foreach (var s in schedules)
            {
                if (ageDays <= s.TargetDay + 2 && ageDays >= s.TargetDay - 5)
                {
                    upcomingMilestones.Add(new FlockMilestoneDto(
                        b.Id,
                        b.BatchCode,
                        b.PoultryHouse.Name,
                        ageDays,
                        s.TargetDay,
                        s.Title,
                        s.Desc,
                        IsDueToday: ageDays == s.TargetDay,
                        IsCompleted: ageDays > s.TargetDay
                    ));
                }
            }
        }

        // Batch DTOs
        var batchDtos = batches.Select(b =>
        {
            var ageDays = Math.Max(0, today.DayNumber - b.PlacementDate.DayNumber);
            return new FlockBatchDto(
                b.Id,
                b.FarmId,
                b.Farm.Name,
                b.PoultryHouseId,
                b.PoultryHouse.Name,
                b.BreedId,
                b.Breed.Name,
                b.BatchCode,
                b.PlacementDate,
                b.InitialChickCount,
                b.CurrentAliveCount,
                b.CostPerChick,
                b.Status,
                ageDays,
                b.CalculatedFcr,
                b.TotalMortality,
                b.LatestAvgWeightGrams,
                b.Notes
            );
        });

        var recentLogDtos = recentLogs.Select(r => new DailyFlockRecordDto(
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
        ));

        var summary = new FarmerDashboardSummaryDto(
            totalFarms,
            totalHouses,
            activeBatches.Count,
            totalPlaced,
            currentLiveBirds,
            todayMortality,
            cumulativeDead,
            cumulativeMortalityPct,
            feedTodayKg,
            feedPeriodKg,
            waterTodayLiters,
            waterPeriodLiters,
            saleReadyBirds,
            reservedBirds,
            uncommittedAvailableBirds,
            activeListingsCount,
            pendingProposalsCount,
            alerts.Take(5),
            upcomingMilestones.Take(8),
            recentLogDtos,
            batchDtos
        );

        return Result<FarmerDashboardSummaryDto>.Success(summary);
    }

    // ── 2. Farm Management ────────────────────────────────────────────────────

    public async Task<Result<FarmDto>> CreateFarmAsync(CreateFarmRequest request, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<FarmDto>.Failure("Unauthorized tenant context.");

        var farm = new Farm
        {
            TenantId = tenantId,
            Name = request.Name,
            Location = request.Location,
            City = request.City,
            Province = request.Province,
            TotalCapacity = request.TotalCapacity,
            ContactPhone = request.ContactPhone,
            Notes = request.Notes,
            IsActive = true
        };

        _context.Farms.Add(farm);
        await _context.SaveChangesAsync(ct);

        return Result<FarmDto>.Success(new FarmDto(
            farm.Id,
            farm.Name,
            farm.Location,
            farm.City,
            farm.Province,
            farm.TotalCapacity,
            farm.ContactPhone,
            farm.IsActive,
            HouseCount: 0,
            ActiveBatchCount: 0
        ));
    }

    public async Task<Result<FarmDto>> UpdateFarmAsync(Guid id, UpdateFarmRequest request, CancellationToken ct = default)
    {
        var farm = await _context.Farms
            .Include(f => f.PoultryHouses)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (farm == null)
            return Result<FarmDto>.Failure("Farm not found.");

        farm.Name = request.Name;
        farm.Location = request.Location;
        farm.City = request.City;
        farm.Province = request.Province;
        farm.TotalCapacity = request.TotalCapacity;
        farm.ContactPhone = request.ContactPhone;
        farm.Notes = request.Notes;
        farm.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);

        var activeBatchCount = await _context.FlockBatches
            .CountAsync(b => b.FarmId == farm.Id && (b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale), ct);

        return Result<FarmDto>.Success(new FarmDto(
            farm.Id,
            farm.Name,
            farm.Location,
            farm.City,
            farm.Province,
            farm.TotalCapacity,
            farm.ContactPhone,
            farm.IsActive,
            farm.PoultryHouses.Count,
            activeBatchCount
        ));
    }

    public async Task<Result<bool>> DeactivateFarmAsync(Guid id, CancellationToken ct = default)
    {
        var farm = await _context.Farms.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (farm == null) return Result<bool>.Failure("Farm not found.");

        // Safe deactivation check: cannot deactivate if active batches exist
        var hasActiveBatches = await _context.FlockBatches
            .AnyAsync(b => b.FarmId == id && (b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale), ct);

        if (hasActiveBatches)
            return Result<bool>.Failure("Cannot deactivate farm. There are active flock batches currently in production on this farm.");

        farm.IsActive = false;
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<IEnumerable<FarmDto>>> GetFarmsAsync(CancellationToken ct = default)
    {
        var farms = await _context.Farms
            .Include(f => f.PoultryHouses)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        var farmIds = farms.Select(f => f.Id).ToList();
        var batchCounts = await _context.FlockBatches
            .Where(b => farmIds.Contains(b.FarmId) && (b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale))
            .GroupBy(b => b.FarmId)
            .Select(g => new { FarmId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FarmId, x => x.Count, ct);

        var dtos = farms.Select(f => new FarmDto(
            f.Id,
            f.Name,
            f.Location,
            f.City,
            f.Province,
            f.TotalCapacity,
            f.ContactPhone,
            f.IsActive,
            f.PoultryHouses.Count,
            batchCounts.GetValueOrDefault(f.Id, 0)
        ));

        return Result<IEnumerable<FarmDto>>.Success(dtos);
    }

    public async Task<Result<FarmDto>> GetFarmByIdAsync(Guid id, CancellationToken ct = default)
    {
        var farm = await _context.Farms
            .Include(f => f.PoultryHouses)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (farm == null) return Result<FarmDto>.Failure("Farm not found.");

        var activeBatchCount = await _context.FlockBatches
            .CountAsync(b => b.FarmId == farm.Id && (b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale), ct);

        return Result<FarmDto>.Success(new FarmDto(
            farm.Id,
            farm.Name,
            farm.Location,
            farm.City,
            farm.Province,
            farm.TotalCapacity,
            farm.ContactPhone,
            farm.IsActive,
            farm.PoultryHouses.Count,
            activeBatchCount
        ));
    }

    // ── 3. Poultry House Management ───────────────────────────────────────────

    public async Task<Result<PoultryHouseDto>> CreatePoultryHouseAsync(CreatePoultryHouseRequest request, CancellationToken ct = default)
    {
        var farm = await _context.Farms.FirstOrDefaultAsync(f => f.Id == request.FarmId, ct);
        if (farm == null) return Result<PoultryHouseDto>.Failure("Associated farm not found.");

        var house = new PoultryHouse
        {
            TenantId = _tenantContext.TenantId,
            FarmId = request.FarmId,
            Name = request.Name,
            Capacity = request.Capacity,
            Description = request.Description,
            IsActive = true
        };

        _context.PoultryHouses.Add(house);
        await _context.SaveChangesAsync(ct);

        return Result<PoultryHouseDto>.Success(new PoultryHouseDto(
            house.Id,
            house.FarmId,
            farm.Name,
            house.Name,
            house.Capacity,
            house.IsActive,
            ActiveBatchCount: 0
        ));
    }

    public async Task<Result<PoultryHouseDto>> UpdatePoultryHouseAsync(Guid id, UpdatePoultryHouseRequest request, CancellationToken ct = default)
    {
        var house = await _context.PoultryHouses
            .Include(h => h.Farm)
            .FirstOrDefaultAsync(h => h.Id == id, ct);

        if (house == null) return Result<PoultryHouseDto>.Failure("Poultry house not found.");

        house.Name = request.Name;
        house.Capacity = request.Capacity;
        house.Description = request.Description;
        house.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);

        var activeBatchCount = await _context.FlockBatches
            .CountAsync(b => b.PoultryHouseId == id && (b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale), ct);

        return Result<PoultryHouseDto>.Success(new PoultryHouseDto(
            house.Id,
            house.FarmId,
            house.Farm.Name,
            house.Name,
            house.Capacity,
            house.IsActive,
            activeBatchCount
        ));
    }

    public async Task<Result<bool>> DeactivatePoultryHouseAsync(Guid id, CancellationToken ct = default)
    {
        var house = await _context.PoultryHouses.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (house == null) return Result<bool>.Failure("Poultry house not found.");

        var hasActiveBatches = await _context.FlockBatches
            .AnyAsync(b => b.PoultryHouseId == id && (b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale), ct);

        if (hasActiveBatches)
            return Result<bool>.Failure("Cannot deactivate poultry house with active flock batches.");

        house.IsActive = false;
        await _context.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    public async Task<Result<IEnumerable<PoultryHouseDto>>> GetPoultryHousesAsync(Guid? farmId = null, CancellationToken ct = default)
    {
        var query = _context.PoultryHouses.Include(h => h.Farm).AsQueryable();

        if (farmId.HasValue)
            query = query.Where(h => h.FarmId == farmId.Value);

        var houses = await query.OrderBy(h => h.Name).ToListAsync(ct);

        var houseIds = houses.Select(h => h.Id).ToList();
        var activeBatchCounts = await _context.FlockBatches
            .Where(b => houseIds.Contains(b.PoultryHouseId) && (b.Status == BatchStatus.Active || b.Status == BatchStatus.ReadyForSale))
            .GroupBy(b => b.PoultryHouseId)
            .Select(g => new { HouseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.HouseId, x => x.Count, ct);

        var dtos = houses.Select(h => new PoultryHouseDto(
            h.Id,
            h.FarmId,
            h.Farm.Name,
            h.Name,
            h.Capacity,
            h.IsActive,
            activeBatchCounts.GetValueOrDefault(h.Id, 0)
        ));

        return Result<IEnumerable<PoultryHouseDto>>.Success(dtos);
    }

    // ── 4. Flock Batch Lifecycle ──────────────────────────────────────────────

    public async Task<Result<FlockBatchDto>> CreateFlockBatchAsync(CreateFlockBatchRequest request, CancellationToken ct = default)
    {
        var farm = await _context.Farms.FirstOrDefaultAsync(f => f.Id == request.FarmId, ct);
        if (farm == null) return Result<FlockBatchDto>.Failure("Farm not found.");

        var house = await _context.PoultryHouses.FirstOrDefaultAsync(h => h.Id == request.PoultryHouseId && h.FarmId == request.FarmId, ct);
        if (house == null) return Result<FlockBatchDto>.Failure("Poultry house not found or does not belong to specified farm.");

        var breed = await _context.Breeds.FirstOrDefaultAsync(b => b.Id == request.BreedId, ct);
        if (breed == null) return Result<FlockBatchDto>.Failure("Breed not found.");

        if (request.InitialChickCount <= 0)
            return Result<FlockBatchDto>.Failure("Initial chick count must be greater than zero.");

        var countThisYear = await _context.FlockBatches
            .CountAsync(b => b.PlacementDate.Year == request.PlacementDate.Year, ct);
        var batchCode = $"BATCH-{request.PlacementDate.Year}-{(countThisYear + 1):D3}";

        var batch = new FlockBatch
        {
            TenantId = _tenantContext.TenantId,
            FarmId = request.FarmId,
            PoultryHouseId = request.PoultryHouseId,
            BreedId = request.BreedId,
            BatchCode = batchCode,
            PlacementDate = request.PlacementDate,
            InitialChickCount = request.InitialChickCount,
            CurrentAliveCount = request.InitialChickCount,
            CostPerChick = request.CostPerChick,
            SupplierReference = request.SupplierReference,
            Status = BatchStatus.Active,
            Notes = request.Notes
        };

        _context.FlockBatches.Add(batch);

        // Record chick placement
        var placement = new ChickPlacement
        {
            TenantId = _tenantContext.TenantId,
            BatchId = batch.Id,
            PlacementDate = request.PlacementDate,
            Quantity = request.InitialChickCount,
            CostPerChick = request.CostPerChick,
            SupplierName = request.SupplierReference,
            Notes = request.Notes
        };
        _context.ChickPlacements.Add(placement);

        await _context.SaveChangesAsync(ct);

        var ageDays = Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - request.PlacementDate.DayNumber);

        return Result<FlockBatchDto>.Success(new FlockBatchDto(
            batch.Id,
            batch.FarmId,
            farm.Name,
            batch.PoultryHouseId,
            house.Name,
            batch.BreedId,
            breed.Name,
            batch.BatchCode,
            batch.PlacementDate,
            batch.InitialChickCount,
            batch.CurrentAliveCount,
            batch.CostPerChick,
            batch.Status,
            ageDays,
            LatestFcr: null,
            MortalityRatePct: 0,
            LatestAvgWeightGrams: null,
            batch.Notes
        ));
    }

    public async Task<Result<IEnumerable<FlockBatchDto>>> GetFlockBatchesAsync(Guid? farmId = null, BatchStatus? status = null, CancellationToken ct = default)
    {
        var query = _context.FlockBatches
            .Include(b => b.Farm)
            .Include(b => b.PoultryHouse)
            .Include(b => b.Breed)
            .AsQueryable();

        if (farmId.HasValue)
            query = query.Where(b => b.FarmId == farmId.Value);

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var batches = await query.OrderByDescending(b => b.PlacementDate).ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dtos = batches.Select(b =>
        {
            var ageDays = Math.Max(0, today.DayNumber - b.PlacementDate.DayNumber);
            return new FlockBatchDto(
                b.Id,
                b.FarmId,
                b.Farm.Name,
                b.PoultryHouseId,
                b.PoultryHouse.Name,
                b.BreedId,
                b.Breed.Name,
                b.BatchCode,
                b.PlacementDate,
                b.InitialChickCount,
                b.CurrentAliveCount,
                b.CostPerChick,
                b.Status,
                ageDays,
                b.CalculatedFcr,
                b.TotalMortality,
                b.LatestAvgWeightGrams,
                b.Notes
            );
        });

        return Result<IEnumerable<FlockBatchDto>>.Success(dtos);
    }

    public async Task<Result<FlockBatchDto>> GetFlockBatchByIdAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _context.FlockBatches
            .Include(b => b.Farm)
            .Include(b => b.PoultryHouse)
            .Include(b => b.Breed)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (b == null) return Result<FlockBatchDto>.Failure("Batch not found.");

        var ageDays = Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - b.PlacementDate.DayNumber);

        return Result<FlockBatchDto>.Success(new FlockBatchDto(
            b.Id,
            b.FarmId,
            b.Farm.Name,
            b.PoultryHouseId,
            b.PoultryHouse.Name,
            b.BreedId,
            b.Breed.Name,
            b.BatchCode,
            b.PlacementDate,
            b.InitialChickCount,
            b.CurrentAliveCount,
            b.CostPerChick,
            b.Status,
            ageDays,
            b.CalculatedFcr,
            b.TotalMortality,
            b.LatestAvgWeightGrams,
            b.Notes
        ));
    }

    public async Task<Result<FlockBatchDto>> MarkBatchReadyForSaleAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _context.FlockBatches
            .Include(b => b.Farm)
            .Include(b => b.PoultryHouse)
            .Include(b => b.Breed)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch == null) return Result<FlockBatchDto>.Failure("Flock batch not found.");

        if (batch.Status != BatchStatus.Active && batch.Status != BatchStatus.PartialSold)
            return Result<FlockBatchDto>.Failure($"Cannot mark batch as ready for sale when status is {batch.Status}.");

        batch.Status = BatchStatus.ReadyForSale;
        await _context.SaveChangesAsync(ct);

        var ageDays = Math.Max(0, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - batch.PlacementDate.DayNumber);

        return Result<FlockBatchDto>.Success(new FlockBatchDto(
            batch.Id,
            batch.FarmId,
            batch.Farm.Name,
            batch.PoultryHouseId,
            batch.PoultryHouse.Name,
            batch.BreedId,
            batch.Breed.Name,
            batch.BatchCode,
            batch.PlacementDate,
            batch.InitialChickCount,
            batch.CurrentAliveCount,
            batch.CostPerChick,
            batch.Status,
            ageDays,
            batch.CalculatedFcr,
            batch.TotalMortality,
            batch.LatestAvgWeightGrams,
            batch.Notes
        ));
    }

    // ── 5. Daily Observations & Performance Metrics ───────────────────────────

    public async Task<Result<DailyFlockRecordDto>> RecordDailyDataAsync(CreateDailyRecordRequest request, CancellationToken ct = default)
    {
        var batch = await _context.FlockBatches.FirstOrDefaultAsync(b => b.Id == request.BatchId, ct);
        if (batch == null) return Result<DailyFlockRecordDto>.Failure("Flock batch not found.");

        // Idempotency check for mobile synchronization
        if (!string.IsNullOrEmpty(request.ClientRecordId))
        {
            var existing = await _context.DailyFlockRecords
                .FirstOrDefaultAsync(r => r.ClientRecordId == request.ClientRecordId, ct);
            if (existing != null)
            {
                _logger.LogInformation("Duplicate submission detected for ClientRecordId: {ClientRecordId}", request.ClientRecordId);
                return Result<DailyFlockRecordDto>.Success(new DailyFlockRecordDto(
                    existing.Id,
                    existing.BatchId,
                    existing.RecordDate,
                    existing.OpeningCount,
                    existing.AliveCount,
                    existing.ClosingCount,
                    existing.DailyMortality,
                    existing.FeedCategory ?? "Starter",
                    existing.FeedConsumedKg,
                    existing.WaterConsumedLiters,
                    existing.AverageWeightGrams,
                    existing.Notes,
                    existing.IsAbnormalMortality,
                    existing.MortalityAlertReason,
                    existing.CreatedAt,
                    existing.ClientRecordId
                ));
            }
        }

        // Duplicate entry check for same batch and date (when not a retry)
        var duplicate = await _context.DailyFlockRecords
            .FirstOrDefaultAsync(r => r.BatchId == request.BatchId && r.RecordDate == request.RecordDate, ct);
        if (duplicate != null && string.IsNullOrEmpty(request.ClientRecordId))
        {
            return Result<DailyFlockRecordDto>.Failure($"A record for date {request.RecordDate} already exists for this batch. Please use the edit adjustment function to correct it.");
        }

        // Opening / closing count tracking
        var openingCount = request.OpeningCount ?? batch.CurrentAliveCount;
        if (request.DailyMortality > openingCount)
        {
            return Result<DailyFlockRecordDto>.Failure($"Daily mortality ({request.DailyMortality}) cannot exceed the opening bird count ({openingCount}).");
        }

        var closingCount = request.AliveCount > 0 ? request.AliveCount : (openingCount - request.DailyMortality);

        // Abnormal mortality check (> 1.5% in a single day)
        bool isAbnormal = false;
        string? alertReason = null;
        if (batch.InitialChickCount > 0)
        {
            var dailyMortalityRate = ((decimal)request.DailyMortality / batch.InitialChickCount) * 100m;
            if (dailyMortalityRate >= 1.5m)
            {
                isAbnormal = true;
                alertReason = $"Single-day mortality of {request.DailyMortality} birds ({dailyMortalityRate:F1}%) exceeded 1.5% threshold.";
            }
        }

        var avgFeedPerBird = closingCount > 0
            ? Math.Round((request.FeedConsumedKg * 1000m) / closingCount, 2)
            : 0;

        var record = new DailyFlockRecord
        {
            TenantId = _tenantContext.TenantId,
            BatchId = request.BatchId,
            RecordDate = request.RecordDate,
            OpeningCount = openingCount,
            AliveCount = closingCount,
            ClosingCount = closingCount,
            DailyMortality = request.DailyMortality,
            FeedCategory = request.FeedCategory ?? "Starter",
            FeedConsumedKg = request.FeedConsumedKg,
            WaterConsumedLiters = request.WaterConsumedLiters,
            AverageWeightGrams = request.AverageWeightGrams,
            AverageFeedPerBirdGrams = avgFeedPerBird,
            Notes = request.Notes,
            IsAbnormalMortality = isAbnormal,
            MortalityAlertReason = alertReason,
            RecordedByUserId = _tenantContext.UserId != Guid.Empty ? _tenantContext.UserId : null,
            ClientRecordId = request.ClientRecordId,
            RowVersion = 1
        };

        _context.DailyFlockRecords.Add(record);

        // Deduct from feed stock inventory if tracked
        var feedStock = await _context.FeedInventories
            .FirstOrDefaultAsync(f => f.FarmId == batch.FarmId && f.FeedCategory == (request.FeedCategory ?? "Starter"), ct);
        if (feedStock != null)
        {
            feedStock.CurrentStockKg = Math.Max(0, feedStock.CurrentStockKg - request.FeedConsumedKg);
        }

        // Update batch live bird count
        batch.CurrentAliveCount = closingCount;
        if (request.AverageWeightGrams.HasValue)
        {
            batch.LatestAvgWeightGrams = request.AverageWeightGrams.Value;
        }

        await _context.SaveChangesAsync(ct);

        // Calculate and refresh metrics automatically
        await CalculateAndStoreMetricsAsync(batch.Id, ct);

        return Result<DailyFlockRecordDto>.Success(new DailyFlockRecordDto(
            record.Id,
            record.BatchId,
            record.RecordDate,
            record.OpeningCount,
            record.AliveCount,
            record.ClosingCount,
            record.DailyMortality,
            record.FeedCategory ?? "Starter",
            record.FeedConsumedKg,
            record.WaterConsumedLiters,
            record.AverageWeightGrams,
            record.Notes,
            record.IsAbnormalMortality,
            record.MortalityAlertReason,
            record.CreatedAt,
            record.ClientRecordId
        ));
    }

    public async Task<Result<DailyFlockRecordDto>> UpdateDailyDataAsync(UpdateDailyRecordRequest request, CancellationToken ct = default)
    {
        var record = await _context.DailyFlockRecords
            .Include(r => r.Batch)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);

        if (record == null) return Result<DailyFlockRecordDto>.Failure("Observation record not found.");

        var mortalityDelta = request.DailyMortality - record.DailyMortality;
        if (record.Batch.CurrentAliveCount - mortalityDelta < 0)
        {
            return Result<DailyFlockRecordDto>.Failure("Adjustment would result in negative bird count.");
        }

        record.DailyMortality = request.DailyMortality;
        record.AliveCount = request.AliveCount;
        record.ClosingCount = request.AliveCount;
        record.FeedConsumedKg = request.FeedConsumedKg;
        record.WaterConsumedLiters = request.WaterConsumedLiters;
        record.FeedCategory = request.FeedCategory ?? record.FeedCategory;
        record.AverageWeightGrams = request.AverageWeightGrams;
        record.Notes = $"{record.Notes} | Adjusted: {request.CorrectionReason}";

        record.Batch.CurrentAliveCount -= mortalityDelta;
        if (request.AverageWeightGrams.HasValue)
        {
            record.Batch.LatestAvgWeightGrams = request.AverageWeightGrams.Value;
        }

        // Log audit entry
        _context.AuditLogs.Add(new AuditLog
        {
            TenantId = _tenantContext.TenantId,
            ActorUserId = _tenantContext.UserId,
            Action = "UpdateDailyFlockRecord",
            EntityName = "DailyFlockRecord",
            EntityId = record.Id,
            OldValues = $"OldMortality: {record.DailyMortality}",
            NewValues = $"NewMortality: {request.DailyMortality}, Reason: {request.CorrectionReason}",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
        await CalculateAndStoreMetricsAsync(record.BatchId, ct);

        return Result<DailyFlockRecordDto>.Success(new DailyFlockRecordDto(
            record.Id,
            record.BatchId,
            record.RecordDate,
            record.OpeningCount,
            record.AliveCount,
            record.ClosingCount,
            record.DailyMortality,
            record.FeedCategory ?? "Starter",
            record.FeedConsumedKg,
            record.WaterConsumedLiters,
            record.AverageWeightGrams,
            record.Notes,
            record.IsAbnormalMortality,
            record.MortalityAlertReason,
            record.CreatedAt,
            record.ClientRecordId
        ));
    }

    public async Task<Result<IEnumerable<DailyFlockRecordDto>>> GetDailyRecordsAsync(Guid batchId, CancellationToken ct = default)
    {
        var records = await _context.DailyFlockRecords
            .Where(r => r.BatchId == batchId)
            .OrderByDescending(r => r.RecordDate)
            .ToListAsync(ct);

        var dtos = records.Select(r => new DailyFlockRecordDto(
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
        ));

        return Result<IEnumerable<DailyFlockRecordDto>>.Success(dtos);
    }

    public async Task<Result<ProductionMetricDto>> GetProductionMetricsAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _context.FlockBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null) return Result<ProductionMetricDto>.Failure("Batch not found.");

        var metric = await _context.ProductionMetrics
            .Where(m => m.BatchId == batchId)
            .OrderByDescending(m => m.CalculatedAt)
            .FirstOrDefaultAsync(ct);

        if (metric == null)
        {
            return await CalculateAndStoreMetricsAsync(batchId, ct);
        }

        return Result<ProductionMetricDto>.Success(new ProductionMetricDto(
            metric.BatchId,
            batch.BatchCode,
            metric.CalculatedAt,
            metric.AgeDays,
            metric.Fcr,
            metric.MortalityRatePct,
            metric.AdgGrams,
            metric.Epef,
            metric.TotalFeedKg,
            metric.TotalLiveWeightKg
        ));
    }

    public async Task<Result<ProductionMetricDto>> CalculateAndStoreMetricsAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await _context.FlockBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch == null) return Result<ProductionMetricDto>.Failure("Batch not found.");

        var records = await _context.DailyFlockRecords
            .Where(r => r.BatchId == batchId)
            .OrderBy(r => r.RecordDate)
            .ToListAsync(ct);

        var ageDays = Math.Max(1, DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - batch.PlacementDate.DayNumber);
        var totalFeedKg = records.Sum(r => r.FeedConsumedKg);
        var latestWeight = records.LastOrDefault()?.AverageWeightGrams ?? batch.LatestAvgWeightGrams ?? 40m;
        var totalLiveWeightKg = (batch.CurrentAliveCount * latestWeight) / 1000m;

        // Use Domain calculation service
        var (fcr, adg, mortalityPct, epef) = ProductionMetricsCalculator.CalculateAll(batch, records);

        // Update cached values on batch
        batch.CalculatedFcr = fcr;
        batch.TotalMortality = mortalityPct;
        batch.LatestAvgWeightGrams = latestWeight;

        var metric = new ProductionMetric
        {
            TenantId = batch.TenantId,
            BatchId = batch.Id,
            CalculatedAt = DateTime.UtcNow,
            AgeDays = ageDays,
            Fcr = fcr,
            MortalityRatePct = mortalityPct,
            AdgGrams = adg,
            Epef = epef,
            TotalFeedKg = totalFeedKg,
            TotalLiveWeightKg = totalLiveWeightKg,
            SampleSize = records.Count
        };

        _context.ProductionMetrics.Add(metric);
        await _context.SaveChangesAsync(ct);

        return Result<ProductionMetricDto>.Success(new ProductionMetricDto(
            metric.BatchId,
            batch.BatchCode,
            metric.CalculatedAt,
            metric.AgeDays,
            metric.Fcr,
            metric.MortalityRatePct,
            metric.AdgGrams,
            metric.Epef,
            metric.TotalFeedKg,
            metric.TotalLiveWeightKg
        ));
    }

    // ── 6. Feed Inventory Management ──────────────────────────────────────────

    public async Task<Result<IEnumerable<FeedInventoryDto>>> GetFeedInventoriesAsync(Guid? farmId = null, CancellationToken ct = default)
    {
        var query = _context.FeedInventories.Include(f => f.Farm).AsQueryable();
        if (farmId.HasValue) query = query.Where(f => f.FarmId == farmId.Value);

        var list = await query.OrderBy(f => f.FeedCategory).ToListAsync(ct);

        var dtos = list.Select(f => new FeedInventoryDto(
            f.Id,
            f.FarmId,
            f.Farm.Name,
            f.FeedCategory,
            f.CurrentStockKg,
            f.MinimumStockThresholdKg,
            f.UnitPricePerKg,
            IsLowStock: f.CurrentStockKg <= f.MinimumStockThresholdKg,
            f.SupplierName,
            f.LastRestockedAt
        ));

        return Result<IEnumerable<FeedInventoryDto>>.Success(dtos);
    }

    public async Task<Result<FeedInventoryDto>> RestockFeedAsync(RestockFeedRequest request, CancellationToken ct = default)
    {
        var farm = await _context.Farms.FirstOrDefaultAsync(f => f.Id == request.FarmId, ct);
        if (farm == null) return Result<FeedInventoryDto>.Failure("Farm not found.");

        var item = await _context.FeedInventories
            .FirstOrDefaultAsync(f => f.FarmId == request.FarmId && f.FeedCategory == request.FeedCategory, ct);

        if (item == null)
        {
            item = new FeedInventory
            {
                TenantId = _tenantContext.TenantId,
                FarmId = request.FarmId,
                FeedCategory = request.FeedCategory,
                CurrentStockKg = request.QuantityKg,
                MinimumStockThresholdKg = 500m,
                UnitPricePerKg = request.UnitPricePerKg,
                SupplierName = request.SupplierName,
                LastRestockedAt = DateTime.UtcNow
            };
            _context.FeedInventories.Add(item);
        }
        else
        {
            item.CurrentStockKg += request.QuantityKg;
            if (request.UnitPricePerKg.HasValue) item.UnitPricePerKg = request.UnitPricePerKg.Value;
            if (!string.IsNullOrEmpty(request.SupplierName)) item.SupplierName = request.SupplierName;
            item.LastRestockedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        return Result<FeedInventoryDto>.Success(new FeedInventoryDto(
            item.Id,
            item.FarmId,
            farm.Name,
            item.FeedCategory,
            item.CurrentStockKg,
            item.MinimumStockThresholdKg,
            item.UnitPricePerKg,
            IsLowStock: item.CurrentStockKg <= item.MinimumStockThresholdKg,
            item.SupplierName,
            item.LastRestockedAt
        ));
    }

    // ── 7. Marketplace Listings ───────────────────────────────────────────────

    public async Task<Result<ListingDto>> CreateListingAsync(CreateListingRequest request, CancellationToken ct = default)
    {
        var batch = await _context.FlockBatches
            .Include(b => b.Breed)
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, ct);

        if (batch == null) return Result<ListingDto>.Failure("Batch not found.");

        if (batch.Status != BatchStatus.ReadyForSale && batch.Status != BatchStatus.PartialSold && batch.Status != BatchStatus.Active)
            return Result<ListingDto>.Failure($"Batch status is {batch.Status}. Must be ReadyForSale or Active to list.");

        if (request.QuantityTotal > batch.CurrentAliveCount)
            return Result<ListingDto>.Failure($"Cannot list {request.QuantityTotal} birds. Current alive flock is {batch.CurrentAliveCount}.");

        var listing = new MarketplaceListing
        {
            TenantId = _tenantContext.TenantId,
            BatchId = request.BatchId,
            Title = request.Title,
            QuantityTotal = request.QuantityTotal,
            QuantityAvailable = request.QuantityTotal,
            AskingPricePerKg = request.AskingPricePerKg,
            NegotiableFloorPricePerKg = request.NegotiableFloorPricePerKg,
            LocationCity = request.LocationCity,
            LocationDetails = request.LocationDetails,
            ExpiresAt = request.ExpiresAt,
            Notes = request.Notes,
            Status = ListingStatus.Draft
        };

        _context.MarketplaceListings.Add(listing);
        await _context.SaveChangesAsync(ct);

        return Result<ListingDto>.Success(new ListingDto(
            listing.Id,
            listing.BatchId,
            batch.BatchCode,
            batch.Breed.Name,
            listing.Title,
            listing.QuantityTotal,
            listing.QuantityAvailable,
            listing.AskingPricePerKg,
            listing.NegotiableFloorPricePerKg,
            listing.LocationCity,
            listing.Status.ToString(),
            listing.PublishedAt,
            listing.ExpiresAt,
            listing.Notes
        ));
    }

    public async Task<Result<ListingDto>> PublishListingAsync(Guid listingId, CancellationToken ct = default)
    {
        var listing = await _context.MarketplaceListings
            .Include(l => l.Batch)
            .ThenInclude(b => b.Breed)
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);

        if (listing == null) return Result<ListingDto>.Failure("Listing not found.");

        if (listing.QuantityAvailable <= 0)
            return Result<ListingDto>.Failure("Cannot publish listing with zero available quantity.");

        listing.Status = ListingStatus.Published;
        listing.PublishedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Result<ListingDto>.Success(new ListingDto(
            listing.Id,
            listing.BatchId,
            listing.Batch.BatchCode,
            listing.Batch.Breed.Name,
            listing.Title,
            listing.QuantityTotal,
            listing.QuantityAvailable,
            listing.AskingPricePerKg,
            listing.NegotiableFloorPricePerKg,
            listing.LocationCity,
            listing.Status.ToString(),
            listing.PublishedAt,
            listing.ExpiresAt,
            listing.Notes
        ));
    }

    public async Task<Result<ListingDto>> CancelListingAsync(Guid listingId, CancellationToken ct = default)
    {
        var listing = await _context.MarketplaceListings
            .Include(l => l.Batch)
            .ThenInclude(b => b.Breed)
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);

        if (listing == null) return Result<ListingDto>.Failure("Listing not found.");

        listing.Status = ListingStatus.Cancelled;
        await _context.SaveChangesAsync(ct);

        return Result<ListingDto>.Success(new ListingDto(
            listing.Id,
            listing.BatchId,
            listing.Batch.BatchCode,
            listing.Batch.Breed.Name,
            listing.Title,
            listing.QuantityTotal,
            listing.QuantityAvailable,
            listing.AskingPricePerKg,
            listing.NegotiableFloorPricePerKg,
            listing.LocationCity,
            listing.Status.ToString(),
            listing.PublishedAt,
            listing.ExpiresAt,
            listing.Notes
        ));
    }

    public async Task<Result<IEnumerable<ListingDto>>> GetMyListingsAsync(ListingStatus? status = null, CancellationToken ct = default)
    {
        var query = _context.MarketplaceListings
            .Include(l => l.Batch)
            .ThenInclude(b => b.Breed)
            .AsQueryable();

        if (status.HasValue) query = query.Where(l => l.Status == status.Value);

        var listings = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);

        var dtos = listings.Select(l => new ListingDto(
            l.Id,
            l.BatchId,
            l.Batch.BatchCode,
            l.Batch.Breed.Name,
            l.Title,
            l.QuantityTotal,
            l.QuantityAvailable,
            l.AskingPricePerKg,
            l.NegotiableFloorPricePerKg,
            l.LocationCity,
            l.Status.ToString(),
            l.PublishedAt,
            l.ExpiresAt,
            l.Notes
        ));

        return Result<IEnumerable<ListingDto>>.Success(dtos);
    }

    public async Task<Result<IEnumerable<ListingDto>>> GetPublicMarketplaceListingsAsync(string? city = null, CancellationToken ct = default)
    {
        var query = _context.MarketplaceListings
            .IgnoreQueryFilters()
            .Include(l => l.Batch)
            .ThenInclude(b => b.Breed)
            .Where(l => l.Status == ListingStatus.Published && l.QuantityAvailable > 0);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(l => l.LocationCity.ToLower() == city.ToLower());

        var listings = await query.OrderByDescending(l => l.PublishedAt).ToListAsync(ct);

        var dtos = listings.Select(l => new ListingDto(
            l.Id,
            l.BatchId,
            l.Batch.BatchCode,
            l.Batch.Breed.Name,
            l.Title,
            l.QuantityTotal,
            l.QuantityAvailable,
            l.AskingPricePerKg,
            l.NegotiableFloorPricePerKg,
            l.LocationCity,
            l.Status.ToString(),
            l.PublishedAt,
            l.ExpiresAt,
            l.Notes
        ));

        return Result<IEnumerable<ListingDto>>.Success(dtos);
    }

    // ── 8. Farmer Financial View & Statements ──────────────────────────────────

    public async Task<Result<FarmerFinancialSummaryDto>> GetFarmerFinancialSummaryAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == Guid.Empty)
            return Result<FarmerFinancialSummaryDto>.Failure("Unauthorized tenant context.");

        // Deals where current tenant is the seller farmer
        var deals = await _context.Deals
            .IgnoreQueryFilters()
            .Where(d => d.FarmerTenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        var dealIds = deals.Select(d => d.Id).ToList();

        // Settlements for these deals
        var settlements = await _context.Settlements
            .IgnoreQueryFilters()
            .Where(s => dealIds.Contains(s.DealId))
            .ToListAsync(ct);

        // Invoices for these deals
        var invoices = await _context.Invoices
            .IgnoreQueryFilters()
            .Where(i => dealIds.Contains(i.DealId))
            .ToListAsync(ct);

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        // Payments for these invoices
        var payments = await _context.Payments
            .IgnoreQueryFilters()
            .Where(p => invoiceIds.Contains(p.InvoiceId))
            .ToListAsync(ct);

        // Broker tenant names map
        var brokerTenantIds = deals.Select(d => d.BrokerTenantId).Distinct().ToList();
        var brokerNames = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => brokerTenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var farmerDealDtos = new List<FarmerDealDto>();
        decimal totalGrossSales = 0m;
        decimal totalCommission = 0m;
        decimal totalFreight = 0m;
        decimal totalNetPayout = 0m;
        decimal totalPaid = 0m;

        foreach (var deal in deals)
        {
            var settlement = settlements.FirstOrDefault(s => s.DealId == deal.Id);
            var dealInvoices = invoices.Where(i => i.DealId == deal.Id).ToList();
            var dealPaid = dealInvoices.Sum(i => i.PaidAmount);

            var gross = settlement?.GrossAmount ?? 0m;
            var comm = settlement?.CommissionAmount ?? 0m;
            var freight = settlement?.FreightAmount ?? 0m;
            var net = settlement?.NetPayableToFarmer ?? 0m;

            totalGrossSales += gross;
            totalCommission += comm;
            totalFreight += freight;
            totalNetPayout += net;
            totalPaid += dealPaid;

            var dealCode = $"DEAL-{deal.Id.ToString()[..8].ToUpper()}";

            farmerDealDtos.Add(new FarmerDealDto(
                deal.Id,
                dealCode,
                deal.ProposalId,
                brokerNames.GetValueOrDefault(deal.BrokerTenantId, "Trading Broker"),
                deal.AgreedPricePerKg,
                deal.QuantityBirds,
                deal.ActualWeightKg,
                deal.Status.ToString(),
                gross,
                net,
                dealPaid,
                Math.Max(0, net - dealPaid),
                deal.CreatedAt,
                deal.AcceptedAt
            ));
        }

        var invoiceDtos = invoices.Select(i =>
        {
            var deal = deals.FirstOrDefault(d => d.Id == i.DealId);
            var dealCode = deal != null ? $"DEAL-{deal.Id.ToString()[..8].ToUpper()}" : "DEAL";
            return new FarmerInvoiceDto(
                i.Id,
                i.InvoiceNumber,
                dealCode,
                brokerNames.GetValueOrDefault(deal?.BrokerTenantId ?? Guid.Empty, "Trading Partner"),
                DateOnly.FromDateTime(i.IssuedAt),
                i.DueDate.HasValue ? DateOnly.FromDateTime(i.DueDate.Value) : DateOnly.FromDateTime(i.IssuedAt.AddDays(7)),
                i.TotalAmount,
                i.PaidAmount,
                i.Status.ToString()
            );
        });

        var paymentDtos = payments.Select(p =>
        {
            var inv = invoices.FirstOrDefault(i => i.Id == p.InvoiceId);
            return new FarmerPaymentDto(
                p.Id,
                inv?.InvoiceNumber ?? "INV",
                p.Amount,
                p.Method.ToString(),
                p.ReferenceNumber,
                p.RecordedAt
            );
        });

        var summary = new FarmerFinancialSummaryDto(
            totalGrossSales,
            totalCommission,
            totalFreight,
            totalNetPayout,
            totalPaid,
            OutstandingBalance: Math.Max(0, totalNetPayout - totalPaid),
            farmerDealDtos,
            invoiceDtos,
            paymentDtos
        );

        return Result<FarmerFinancialSummaryDto>.Success(summary);
    }

    // ── 9. Reports & Exports ──────────────────────────────────────────────────

    public async Task<Result<FlockProductionReportDto>> GetFlockProductionReportAsync(
        Guid batchId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var batch = await _context.FlockBatches
            .Include(b => b.Farm)
            .Include(b => b.PoultryHouse)
            .Include(b => b.Breed)
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch == null) return Result<FlockProductionReportDto>.Failure("Batch not found.");

        var query = _context.DailyFlockRecords
            .Where(r => r.BatchId == batchId)
            .AsQueryable();

        if (fromDate.HasValue) query = query.Where(r => r.RecordDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(r => r.RecordDate <= toDate.Value);

        var records = await query.OrderBy(r => r.RecordDate).ToListAsync(ct);

        var (fcr, adg, mortPct, epef) = ProductionMetricsCalculator.CalculateAll(batch, records);

        var rows = records.Select(r =>
        {
            var dayAge = Math.Max(1, r.RecordDate.DayNumber - batch.PlacementDate.DayNumber);
            var dailyGain = r.AverageWeightGrams.HasValue && r.AverageWeightGrams > 40m
                ? (r.AverageWeightGrams.Value - 40m) / dayAge
                : (decimal?)null;

            return new ProductionReportRowDto(
                r.RecordDate,
                dayAge,
                r.OpeningCount,
                r.DailyMortality,
                r.ClosingCount,
                r.FeedCategory ?? "Starter",
                r.FeedConsumedKg,
                r.WaterConsumedLiters,
                r.AverageWeightGrams,
                batch.CalculatedFcr,
                dailyGain.HasValue ? Math.Round(dailyGain.Value, 1) : null
            );
        });

        var report = new FlockProductionReportDto(
            batch.Id,
            batch.BatchCode,
            batch.Farm.Name,
            batch.PoultryHouse.Name,
            batch.Breed.Name,
            batch.PlacementDate,
            batch.InitialChickCount,
            batch.CurrentAliveCount,
            records.Sum(r => r.DailyMortality),
            mortPct,
            records.Sum(r => r.FeedConsumedKg),
            records.Sum(r => r.WaterConsumedLiters),
            records.LastOrDefault()?.AverageWeightGrams ?? batch.LatestAvgWeightGrams,
            fcr,
            adg,
            epef,
            rows
        );

        return Result<FlockProductionReportDto>.Success(report);
    }

    public async Task<Result<string>> GetProductionReportCsvAsync(
        Guid batchId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var reportResult = await GetFlockProductionReportAsync(batchId, fromDate, toDate, ct);
        if (!reportResult.IsSuccess) return Result<string>.Failure(reportResult.Error ?? "Failed to generate report.");

        var r = reportResult.Value!;
        var sb = new StringBuilder();
        sb.AppendLine($"AERIE POULTRY ERP — FLOCK PRODUCTION LIFECYCLE REPORT");
        sb.AppendLine($"Batch Code,{r.BatchCode}");
        sb.AppendLine($"Farm / House,{r.FarmName} / {r.HouseName}");
        sb.AppendLine($"Breed,{r.BreedName}");
        sb.AppendLine($"Placement Date,{r.PlacementDate:yyyy-MM-dd}");
        sb.AppendLine($"Initial Chicks Placed,{r.InitialChickCount}");
        sb.AppendLine($"Current Live Birds,{r.CurrentAliveCount}");
        sb.AppendLine($"Cumulative Mortality %,{r.MortalityRatePct:F2}%");
        sb.AppendLine($"Overall FCR,{r.OverallFcr?.ToString("F2") ?? "N/A"}");
        sb.AppendLine($"EPEF Index,{r.Epef?.ToString("F1") ?? "N/A"}");
        sb.AppendLine();
        sb.AppendLine("Date,AgeDays,OpeningBirds,Mortality,ClosingBirds,FeedCategory,FeedKg,WaterLiters,SampleWeightGrams");

        foreach (var row in r.DailyRows)
        {
            sb.AppendLine($"{row.Date:yyyy-MM-dd},{row.DayAge},{row.OpeningCount},{row.DailyMortality},{row.ClosingCount},{row.FeedCategory},{row.FeedConsumedKg:F1},{row.WaterConsumedLiters:F1},{row.AverageWeightGrams?.ToString("F0") ?? ""}");
        }

        return Result<string>.Success(sb.ToString());
    }

    public async Task<Result<string>> GetFarmerFinancialReportCsvAsync(CancellationToken ct = default)
    {
        var finResult = await GetFarmerFinancialSummaryAsync(ct);
        if (!finResult.IsSuccess) return Result<string>.Failure(finResult.Error ?? "Failed to load financial records.");

        var fin = finResult.Value!;
        var sb = new StringBuilder();
        sb.AppendLine("AERIE POULTRY ERP — FARMER FINANCIAL & TRADE CONTRACT STATEMENT");
        sb.AppendLine($"Total Gross Sales (PKR),{fin.TotalGrossSales:F2}");
        sb.AppendLine($"Total Broker Commission Deducted (PKR),{fin.TotalBrokerCommissions:F2}");
        sb.AppendLine($"Total Freight Deducted (PKR),{fin.TotalFreightDeductions:F2}");
        sb.AppendLine($"Net Farmer Receivable (PKR),{fin.NetFarmerReceivables:F2}");
        sb.AppendLine($"Total Received Payments (PKR),{fin.TotalPaymentsReceived:F2}");
        sb.AppendLine($"Outstanding Balance (PKR),{fin.OutstandingBalance:F2}");
        sb.AppendLine();
        sb.AppendLine("DealNumber,BrokerBuyer,AgreedRatePerKg,QuantityBirds,LiftedWeightKg,Status,GrossAmountPKR,NetPayoutPKR,AmountPaidPKR,BalanceDuePKR");

        foreach (var d in fin.Deals)
        {
            sb.AppendLine($"{d.DealNumber},{d.BrokerTenantName},{d.AgreedPricePerKg:F2},{d.RequestedQuantity},{d.LiftedWeightKg?.ToString("F2") ?? ""},{d.Status},{d.GrossAmount:F2},{d.NetPayout:F2},{d.AmountPaid:F2},{d.BalanceDue:F2}");
        }

        return Result<string>.Success(sb.ToString());
    }
}
