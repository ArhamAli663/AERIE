namespace AERIE.Web.Pages;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class MarketRatesModel : PageModel
{
    private readonly IBrokerService _brokerService;
    private readonly IDailyMarketRateService _dailyMarketRateService;

    public MarketRatesModel(
        IBrokerService brokerService,
        IDailyMarketRateService dailyMarketRateService)
    {
        _brokerService = brokerService;
        _dailyMarketRateService = dailyMarketRateService;
    }

    public List<MarketPriceDto> MarketPrices { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? SelectedRegion { get; set; }
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostRefreshDailyRatesAsync()
    {
        var result = await _dailyMarketRateService.RefreshDailyMarketPricesAsync(forceNewFluctuation: true);
        if (result.IsSuccess)
        {
            StatusMessage = $"Mandi market rates rozana ke hisab se automatically update ho gaye hain! All {result.Value} Pakistan cities refreshed with live daily price fluctuation.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage(new { selectedRegion = SelectedRegion });
    }

    public async Task OnGetAsync()
    {
        var result = await _brokerService.GetLatestMarketPricesAsync(SelectedRegion);
        if (result.IsSuccess && result.Value != null)
        {
            MarketPrices = result.Value.OrderByDescending(p => p.PriceDate).ToList();
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }

    public async Task<IActionResult> OnPostRecordRateAsync(
        string region,
        decimal broilerRatePerKg,
        decimal cullRatePerKg,
        decimal dayOldChickRate,
        string marketName,
        string? source)
    {
        if (broilerRatePerKg <= 0)
        {
            ErrorMessage = "Broiler rate per kg must be greater than zero.";
            return RedirectToPage();
        }

        var result = await _brokerService.RecordMarketPriceAsync(new RecordMarketPriceRequest(
            Region: region,
            BroilerRatePerKg: broilerRatePerKg,
            CullRatePerKg: cullRatePerKg,
            DayOldChickRate: dayOldChickRate,
            PriceDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Source: string.IsNullOrWhiteSpace(source) ? "Official Mandi Verification" : source,
            MarketName: string.IsNullOrWhiteSpace(marketName) ? "Main Wholesale Mandi" : marketName
        ));

        if (result.IsSuccess)
        {
            StatusMessage = $"Market rate for {region} updated successfully to Rs. {broilerRatePerKg:N2}/kg.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }
}
