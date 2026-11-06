namespace AERIE.Api.Controllers;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using AERIE.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BrokerController : ControllerBase
{
    private readonly IBrokerService _brokerService;

    public BrokerController(IBrokerService brokerService)
    {
        _brokerService = brokerService;
    }

    // ── Proposals ─────────────────────────────────────────────────────────────

    [HttpPost("proposals")]
    public async Task<IActionResult> CreateProposal([FromBody] CreateProposalRequest request, CancellationToken ct)
    {
        var result = await _brokerService.CreateProposalAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("proposals/my")]
    public async Task<IActionResult> GetMyProposals(CancellationToken ct)
    {
        var result = await _brokerService.GetMyProposalsAsync(ct);
        return Ok(result);
    }

    [HttpGet("listings/{listingId:guid}/proposals")]
    public async Task<IActionResult> GetProposalsForListing(Guid listingId, CancellationToken ct)
    {
        var result = await _brokerService.GetProposalsForListingAsync(listingId, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("proposals/{id:guid}/review")]
    public async Task<IActionResult> ReviewProposal(Guid id, [FromBody] ReviewProposalRequest request, CancellationToken ct)
    {
        var result = await _brokerService.ReviewProposalAsync(id, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Deals ─────────────────────────────────────────────────────────────────

    [HttpGet("deals")]
    public async Task<IActionResult> GetDeals([FromQuery] DealStatus? status, CancellationToken ct)
    {
        var result = await _brokerService.GetDealsAsync(status, ct);
        return Ok(result);
    }

    [HttpGet("deals/{id:guid}")]
    public async Task<IActionResult> GetDeal(Guid id, CancellationToken ct)
    {
        var result = await _brokerService.GetDealByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpPut("deals/{id:guid}/status")]
    public async Task<IActionResult> UpdateDealStatus(Guid id, [FromBody] UpdateDealStatusRequest request, CancellationToken ct)
    {
        var result = await _brokerService.UpdateDealStatusAsync(id, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Transport / Lifting ───────────────────────────────────────────────────

    [HttpPost("deals/{dealId:guid}/lifting")]
    public async Task<IActionResult> RecordLifting(Guid dealId, [FromBody] RecordLiftingRequest request, CancellationToken ct)
    {
        var result = await _brokerService.RecordLiftingAsync(dealId, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("deals/{dealId:guid}/transport-records")]
    public async Task<IActionResult> GetTransportRecords(Guid dealId, CancellationToken ct)
    {
        var result = await _brokerService.GetTransportRecordsAsync(dealId, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Settlement ────────────────────────────────────────────────────────────

    [HttpPost("deals/{dealId:guid}/settlement")]
    public async Task<IActionResult> CreateSettlement(Guid dealId, [FromBody] CreateSettlementRequest request, CancellationToken ct)
    {
        var result = await _brokerService.CreateSettlementAsync(dealId, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("deals/{dealId:guid}/settlement")]
    public async Task<IActionResult> GetSettlement(Guid dealId, CancellationToken ct)
    {
        var result = await _brokerService.GetSettlementByDealIdAsync(dealId, ct);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    // ── Invoices ──────────────────────────────────────────────────────────────

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var result = await _brokerService.CreateInvoiceAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        var result = await _brokerService.GetInvoiceByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result) : NotFound(result);
    }

    [HttpGet("deals/{dealId:guid}/invoices")]
    public async Task<IActionResult> GetDealInvoices(Guid dealId, CancellationToken ct)
    {
        var result = await _brokerService.GetInvoicesByDealIdAsync(dealId, ct);
        return Ok(result);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _brokerService.GetBrokerDashboardSummaryAsync(ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("proposals/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawProposal(Guid id, [FromBody] WithdrawProposalRequest request, CancellationToken ct)
    {
        var result = await _brokerService.WithdrawProposalAsync(id, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("deals/{dealId:guid}/delivery")]
    public async Task<IActionResult> RecordDelivery(Guid dealId, [FromBody] RecordDeliveryRequest request, CancellationToken ct)
    {
        var result = await _brokerService.RecordDeliveryAsync(dealId, request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("market-comparison")]
    public async Task<IActionResult> CompareMarkets(
        [FromQuery] string sourceCity,
        [FromQuery] string destinationCity,
        [FromQuery] decimal quantityKg,
        [FromQuery] decimal? estimatedFreightPerKg,
        CancellationToken ct)
    {
        var req = new MarketComparisonRequest(sourceCity, destinationCity, quantityKg, estimatedFreightPerKg);
        var result = await _brokerService.CompareMarketsAsync(req, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("ledger/entries")]
    public async Task<IActionResult> GetLedgerEntries([FromQuery] Guid? dealId, CancellationToken ct)
    {
        var result = await _brokerService.GetLedgerEntriesAsync(dealId, ct);
        return Ok(result);
    }

    [HttpGet("reports/deals/csv")]
    public async Task<IActionResult> DownloadDealsCsv(CancellationToken ct)
    {
        var result = await _brokerService.GetDealsReportCsvAsync(ct);
        if (!result.IsSuccess) return BadRequest(result);
        return File(System.Text.Encoding.UTF8.GetBytes(result.Data!), "text/csv", $"broker_deals_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
    }

    [HttpGet("reports/ledger/csv")]
    public async Task<IActionResult> DownloadLedgerCsv(CancellationToken ct)
    {
        var result = await _brokerService.GetLedgerReportCsvAsync(ct);
        if (!result.IsSuccess) return BadRequest(result);
        return File(System.Text.Encoding.UTF8.GetBytes(result.Data!), "text/csv", $"broker_ledger_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
    }

    // ── Payments ──────────────────────────────────────────────────────────────

    [HttpPost("payments")]
    public async Task<IActionResult> RecordPayment([FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var result = await _brokerService.RecordPaymentAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // ── Market Prices ─────────────────────────────────────────────────────────

    [HttpPost("market-prices")]
    public async Task<IActionResult> RecordMarketPrice([FromBody] RecordMarketPriceRequest request, CancellationToken ct)
    {
        var result = await _brokerService.RecordMarketPriceAsync(request, ct);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("market-prices")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMarketPrices([FromQuery] string? city, CancellationToken ct)
    {
        var result = await _brokerService.GetLatestMarketPricesAsync(city, ct);
        return Ok(result);
    }
}
