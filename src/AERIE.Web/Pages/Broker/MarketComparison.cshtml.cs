namespace AERIE.Web.Pages.Broker;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class MarketComparisonModel : PageModel
{
    private readonly IBrokerService _brokerService;

    public MarketComparisonModel(IBrokerService brokerService)
    {
        _brokerService = brokerService;
    }

    [BindProperty] public string SourceCity { get; set; } = "Faisalabad";
    [BindProperty] public string DestinationCity { get; set; } = "Lahore";
    [BindProperty] public decimal QuantityKg { get; set; } = 4000;
    [BindProperty] public decimal? EstimatedFreightPerKg { get; set; } = 3.50m;

    public MarketComparisonDto? Comparison { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var result = await _brokerService.CompareMarketsAsync(new MarketComparisonRequest(
            SourceCity,
            DestinationCity,
            QuantityKg,
            EstimatedFreightPerKg
        ));

        if (result.IsSuccess)
        {
            Comparison = result.Value;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceCity) || string.IsNullOrWhiteSpace(DestinationCity))
        {
            ErrorMessage = "Please select both source and destination markets.";
            return Page();
        }

        if (QuantityKg <= 0)
        {
            ErrorMessage = "Trading quantity must be greater than zero.";
            return Page();
        }

        var result = await _brokerService.CompareMarketsAsync(new MarketComparisonRequest(
            SourceCity,
            DestinationCity,
            QuantityKg,
            EstimatedFreightPerKg
        ));

        if (result.IsSuccess)
        {
            Comparison = result.Value;
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return Page();
    }
}
