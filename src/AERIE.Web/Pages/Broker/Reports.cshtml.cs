namespace AERIE.Web.Pages.Broker;

using System.Text;
using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using AERIE.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class ReportsModel : PageModel
{
    private readonly IApplicationDbContext _context;
    private readonly IBrokerService _brokerService;

    public ReportsModel(IApplicationDbContext context, IBrokerService brokerService)
    {
        _context = context;
        _brokerService = brokerService;
    }

    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }

    public int TotalDealsCount { get; set; }
    public decimal TotalTradedGross { get; set; }
    public decimal TotalCommissionEarned { get; set; }
    public decimal TotalFreightAttributed { get; set; }
    public decimal TotalFarmerPayouts { get; set; }
    public List<DealDto> Deals { get; set; } = [];

    public async Task OnGetAsync()
    {
        var from = FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = ToDate ?? DateTime.UtcNow.AddDays(1);

        var dealsQuery = _context.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Listing)
            .Include(d => d.Settlement)
            .Where(d => d.AcceptedAt >= from && d.AcceptedAt <= to);

        var list = await dealsQuery.OrderByDescending(d => d.AcceptedAt).ToListAsync();

        var tenantIds = list.Select(d => d.FarmerTenantId).Concat(list.Select(d => d.BrokerTenantId)).Distinct().ToList();
        var tenantNames = await _context.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        TotalDealsCount = list.Count;
        TotalTradedGross = list.Sum(d => d.Settlement?.GrossAmount ?? 0m);
        TotalCommissionEarned = list.Sum(d => d.Settlement?.CommissionAmount ?? 0m);
        TotalFreightAttributed = list.Sum(d => d.Settlement?.FreightAmount ?? 0m);
        TotalFarmerPayouts = list.Sum(d => d.Settlement?.NetPayableToFarmer ?? 0m);

        Deals = list.Select(d => new DealDto(
            d.Id,
            d.ProposalId,
            d.ListingId,
            d.Listing?.Title ?? "Flock Listing",
            d.FarmerTenantId,
            tenantNames.GetValueOrDefault(d.FarmerTenantId, "Farmer Partner"),
            d.BrokerTenantId,
            tenantNames.GetValueOrDefault(d.BrokerTenantId, "Punjab Traders"),
            d.QuantityBirds,
            d.AgreedPricePerKg,
            d.EstimatedWeightKg,
            d.ActualWeightKg,
            d.Status,
            d.AcceptedAt,
            d.ClosedAt,
            d.Notes,
            d.Settlement?.GrossAmount,
            d.Settlement?.CommissionAmount,
            d.Settlement?.FreightAmount,
            d.Settlement?.NetPayableToFarmer
        )).ToList();
    }

    public async Task<IActionResult> OnGetExportDealsCsvAsync()
    {
        var result = await _brokerService.GetDealsReportCsvAsync();
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var bytes = Encoding.UTF8.GetBytes(result.Value ?? "");
        var fileName = $"broker_trading_deals_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    public async Task<IActionResult> OnGetExportLedgerCsvAsync()
    {
        var result = await _brokerService.GetLedgerReportCsvAsync();
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var bytes = Encoding.UTF8.GetBytes(result.Value ?? "");
        var fileName = $"broker_financial_ledger_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }
}
