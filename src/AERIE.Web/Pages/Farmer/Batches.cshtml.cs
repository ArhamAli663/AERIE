namespace AERIE.Web.Pages.Farmer;

using AERIE.Application.Common.Interfaces;
using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class BatchesModel : PageModel
{
    private readonly IApplicationDbContext _context;
    private readonly IFarmerService _farmerService;
    private readonly ITenantContext _tenantContext;

    public BatchesModel(
        IApplicationDbContext context, 
        IFarmerService farmerService,
        ITenantContext tenantContext)
    {
        _context = context;
        _farmerService = farmerService;
        _tenantContext = tenantContext;
    }

    public List<FlockBatchDto> Batches { get; set; } = [];
    public List<PoultryHouseDto> PoultryHouses { get; set; } = [];
    public List<Breed> Breeds { get; set; } = [];
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Strictly tenant-isolated batches
        var batchesRes = await _farmerService.GetFlockBatchesAsync();
        if (batchesRes.IsSuccess && batchesRes.Data != null)
        {
            Batches = batchesRes.Data.ToList();
        }

        // Strictly tenant-isolated poultry houses
        var housesRes = await _farmerService.GetPoultryHousesAsync();
        if (housesRes.IsSuccess && housesRes.Data != null)
        {
            PoultryHouses = housesRes.Data.ToList();
        }

        Breeds = await _context.Breeds.ToListAsync();
        if (!Breeds.Any())
        {
            var defaultBreed = new Breed
            {
                Name = "Cobb 500",
                Lineage = "Cobb",
                TargetFcr = 1.72m,
                TargetMarketWeightGrams = 2100m,
                TargetMarketAgedays = 35
            };
            _context.Breeds.Add(defaultBreed);
            await _context.SaveChangesAsync();
            Breeds.Add(defaultBreed);
        }
    }

    public async Task<IActionResult> OnPostCreateBatchAsync(
        Guid poultryHouseId, Guid breedId, int initialChickCount, decimal costPerChick, string? supplierReference)
    {
        if (initialChickCount <= 0)
        {
            ErrorMessage = "Chick count must be greater than zero.";
            return RedirectToPage();
        }

        var house = await _context.PoultryHouses.FirstOrDefaultAsync(h => h.Id == poultryHouseId);
        if (house == null)
        {
            ErrorMessage = "Selected shed was not found or does not belong to your farm. Please register a shed first.";
            return RedirectToPage();
        }

        var tenantId = _tenantContext.TenantId;
        var countThisYear = await _context.FlockBatches.CountAsync(b => b.TenantId == tenantId && b.PlacementDate.Year == DateTime.UtcNow.Year);
        var batchCode = $"BATCH-{DateTime.UtcNow.Year}-{(countThisYear + 1):D3}";

        var batch = new FlockBatch
        {
            TenantId = tenantId,
            FarmId = house.FarmId,
            PoultryHouseId = poultryHouseId,
            BreedId = breedId,
            BatchCode = batchCode,
            PlacementDate = DateOnly.FromDateTime(DateTime.UtcNow),
            InitialChickCount = initialChickCount,
            CurrentAliveCount = initialChickCount,
            CostPerChick = costPerChick,
            SupplierReference = supplierReference,
            Status = BatchStatus.Active
        };

        _context.FlockBatches.Add(batch);
        await _context.SaveChangesAsync();

        StatusMessage = $"Flock Batch '{batchCode}' successfully placed with {initialChickCount:N0} chicks!";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogDailyAsync(
        Guid batchId, int aliveCount, int dailyMortality, decimal feedConsumedKg, decimal waterConsumedLiters, decimal? averageWeightGrams, string? feedCategory)
    {
        var result = await _farmerService.RecordDailyDataAsync(new CreateDailyRecordRequest(
            BatchId: batchId,
            RecordDate: DateOnly.FromDateTime(DateTime.UtcNow),
            AliveCount: aliveCount,
            DailyMortality: dailyMortality,
            FeedConsumedKg: feedConsumedKg,
            WaterConsumedLiters: waterConsumedLiters,
            AverageWeightGrams: averageWeightGrams,
            Notes: "Captured via Web UI",
            ClientRecordId: Guid.NewGuid().ToString(),
            FeedCategory: string.IsNullOrWhiteSpace(feedCategory) ? "Starter" : feedCategory
        ));

        StatusMessage = result.IsSuccess
            ? "Daily observation log saved and performance KPIs recalculated!"
            : $"Failed to log daily observation: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAdjustDailyAsync(
        Guid recordId, int aliveCount, int dailyMortality, decimal feedConsumedKg, decimal waterConsumedLiters, decimal? averageWeightGrams, string? feedCategory, string reason)
    {
        var req = new UpdateDailyRecordRequest(
            Id: recordId,
            AliveCount: aliveCount,
            DailyMortality: dailyMortality,
            FeedCategory: string.IsNullOrWhiteSpace(feedCategory) ? "Starter" : feedCategory,
            FeedConsumedKg: feedConsumedKg,
            WaterConsumedLiters: waterConsumedLiters,
            AverageWeightGrams: averageWeightGrams,
            Notes: null,
            CorrectionReason: reason
        );

        var result = await _farmerService.UpdateDailyDataAsync(req);
        StatusMessage = result.IsSuccess
            ? "Daily record audited and adjusted successfully!"
            : $"Adjustment failed: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkReadyAsync(Guid batchId)
    {
        var batch = await _context.FlockBatches.FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch != null)
        {
            batch.Status = BatchStatus.ReadyForSale;
            await _context.SaveChangesAsync();
            StatusMessage = $"Batch {batch.BatchCode} marked as Ready for Sale.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateListingAsync(
        Guid batchId, string title, int quantityTotal, decimal askingPricePerKg, string locationCity)
    {
        var batch = await _context.FlockBatches.FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch == null)
        {
            ErrorMessage = "Batch not found or does not belong to your account.";
            return RedirectToPage();
        }

        var listing = new MarketplaceListing
        {
            TenantId = _tenantContext.TenantId,
            BatchId = batchId,
            Title = title,
            QuantityTotal = quantityTotal,
            QuantityAvailable = quantityTotal,
            AskingPricePerKg = askingPricePerKg,
            LocationCity = locationCity,
            Status = ListingStatus.Published,
            PublishedAt = DateTime.UtcNow
        };

        _context.MarketplaceListings.Add(listing);
        await _context.SaveChangesAsync();

        StatusMessage = $"Marketplace listing '{title}' published successfully! Brokers can now submit proposals.";
        return RedirectToPage("/Farmer/Listings");
    }
}
