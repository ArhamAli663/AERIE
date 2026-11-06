namespace AERIE.Api.Controllers;

using System.Text;
using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using AERIE.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FarmerController : ControllerBase
{
    private readonly IFarmerService _farmerService;

    public FarmerController(IFarmerService farmerService)
    {
        _farmerService = farmerService;
    }

    // ── Dashboard Overview ────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _farmerService.GetFarmerDashboardSummaryAsync(ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Farms ─────────────────────────────────────────────────────────────────

    [HttpPost("farms")]
    public async Task<IActionResult> CreateFarm([FromBody] CreateFarmRequest request, CancellationToken ct)
    {
        var result = await _farmerService.CreateFarmAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("farms/{id:guid}")]
    public async Task<IActionResult> UpdateFarm(Guid id, [FromBody] UpdateFarmRequest request, CancellationToken ct)
    {
        var result = await _farmerService.UpdateFarmAsync(id, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("farms/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateFarm(Guid id, CancellationToken ct)
    {
        var result = await _farmerService.DeactivateFarmAsync(id, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("farms")]
    public async Task<IActionResult> GetFarms(CancellationToken ct)
    {
        var result = await _farmerService.GetFarmsAsync(ct);
        return Ok(result);
    }

    [HttpGet("farms/{id:guid}")]
    public async Task<IActionResult> GetFarm(Guid id, CancellationToken ct)
    {
        var result = await _farmerService.GetFarmByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    // ── Poultry Houses ────────────────────────────────────────────────────────

    [HttpPost("houses")]
    public async Task<IActionResult> CreateHouse([FromBody] CreatePoultryHouseRequest request, CancellationToken ct)
    {
        var result = await _farmerService.CreatePoultryHouseAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("houses/{id:guid}")]
    public async Task<IActionResult> UpdateHouse(Guid id, [FromBody] UpdatePoultryHouseRequest request, CancellationToken ct)
    {
        var result = await _farmerService.UpdatePoultryHouseAsync(id, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("houses/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateHouse(Guid id, CancellationToken ct)
    {
        var result = await _farmerService.DeactivatePoultryHouseAsync(id, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("houses")]
    public async Task<IActionResult> GetHouses([FromQuery] Guid? farmId, CancellationToken ct)
    {
        var result = await _farmerService.GetPoultryHousesAsync(farmId, ct);
        return Ok(result);
    }

    // ── Flock Batches ─────────────────────────────────────────────────────────

    [HttpPost("batches")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateFlockBatchRequest request, CancellationToken ct)
    {
        var result = await _farmerService.CreateFlockBatchAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches([FromQuery] Guid? farmId, [FromQuery] BatchStatus? status, CancellationToken ct)
    {
        var result = await _farmerService.GetFlockBatchesAsync(farmId, status, ct);
        return Ok(result);
    }

    [HttpGet("batches/{id:guid}")]
    public async Task<IActionResult> GetBatch(Guid id, CancellationToken ct)
    {
        var result = await _farmerService.GetFlockBatchByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpPost("batches/{id:guid}/ready-for-sale")]
    public async Task<IActionResult> MarkReadyForSale(Guid id, CancellationToken ct)
    {
        var result = await _farmerService.MarkBatchReadyForSaleAsync(id, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Daily Records & Production Metrics ────────────────────────────────────

    [HttpPost("daily-records")]
    public async Task<IActionResult> RecordDailyData([FromBody] CreateDailyRecordRequest request, CancellationToken ct)
    {
        var result = await _farmerService.RecordDailyDataAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPut("daily-records/{id:guid}")]
    public async Task<IActionResult> UpdateDailyData(Guid id, [FromBody] UpdateDailyRecordRequest request, CancellationToken ct)
    {
        if (id != request.Id) return BadRequest("Mismatched identifier.");
        var result = await _farmerService.UpdateDailyDataAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("batches/{batchId:guid}/daily-records")]
    public async Task<IActionResult> GetDailyRecords(Guid batchId, CancellationToken ct)
    {
        var result = await _farmerService.GetDailyRecordsAsync(batchId, ct);
        return Ok(result);
    }

    [HttpGet("batches/{batchId:guid}/metrics")]
    public async Task<IActionResult> GetMetrics(Guid batchId, CancellationToken ct)
    {
        var result = await _farmerService.GetProductionMetricsAsync(batchId, ct);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpPost("batches/{batchId:guid}/calculate-metrics")]
    public async Task<IActionResult> CalculateMetrics(Guid batchId, CancellationToken ct)
    {
        var result = await _farmerService.CalculateAndStoreMetricsAsync(batchId, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Feed Inventory & Restock ──────────────────────────────────────────────

    [HttpGet("feed-inventory")]
    public async Task<IActionResult> GetFeedInventory([FromQuery] Guid? farmId, CancellationToken ct)
    {
        var result = await _farmerService.GetFeedInventoriesAsync(farmId, ct);
        return Ok(result);
    }

    [HttpPost("feed-inventory/restock")]
    public async Task<IActionResult> RestockFeed([FromBody] RestockFeedRequest request, CancellationToken ct)
    {
        var result = await _farmerService.RestockFeedAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Marketplace Listings ──────────────────────────────────────────────────

    [HttpPost("listings")]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingRequest request, CancellationToken ct)
    {
        var result = await _farmerService.CreateListingAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("listings/{id:guid}/publish")]
    public async Task<IActionResult> PublishListing(Guid id, CancellationToken ct)
    {
        var result = await _farmerService.PublishListingAsync(id, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("listings/{id:guid}/cancel")]
    public async Task<IActionResult> CancelListing(Guid id, CancellationToken ct)
    {
        var result = await _farmerService.CancelListingAsync(id, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("listings/my")]
    public async Task<IActionResult> GetMyListings([FromQuery] ListingStatus? status, CancellationToken ct)
    {
        var result = await _farmerService.GetMyListingsAsync(status, ct);
        return Ok(result);
    }

    [HttpGet("listings/marketplace")]
    public async Task<IActionResult> GetMarketplaceListings([FromQuery] string? city, CancellationToken ct)
    {
        var result = await _farmerService.GetPublicMarketplaceListingsAsync(city, ct);
        return Ok(result);
    }

    // ── Financial View ────────────────────────────────────────────────────────

    [HttpGet("financials")]
    public async Task<IActionResult> GetFinancials(CancellationToken ct)
    {
        var result = await _farmerService.GetFarmerFinancialSummaryAsync(ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Reports & CSV Exports ─────────────────────────────────────────────────

    [HttpGet("reports/production/{batchId:guid}")]
    public async Task<IActionResult> GetProductionReport(
        Guid batchId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        var result = await _farmerService.GetFlockProductionReportAsync(batchId, fromDate, toDate, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("reports/production/{batchId:guid}/csv")]
    public async Task<IActionResult> GetProductionReportCsv(
        Guid batchId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        CancellationToken ct)
    {
        var result = await _farmerService.GetProductionReportCsvAsync(batchId, fromDate, toDate, ct);
        if (!result.IsSuccess) return BadRequest(result);

        var bytes = Encoding.UTF8.GetBytes(result.Value!);
        return File(bytes, "text/csv", $"FlockReport_{batchId}_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("reports/financials/csv")]
    public async Task<IActionResult> GetFinancialReportCsv(CancellationToken ct)
    {
        var result = await _farmerService.GetFarmerFinancialReportCsvAsync(ct);
        if (!result.IsSuccess) return BadRequest(result);

        var bytes = Encoding.UTF8.GetBytes(result.Value!);
        return File(bytes, "text/csv", $"FarmerFinancialStatement_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
