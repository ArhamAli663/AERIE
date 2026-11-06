namespace AERIE.Web.Pages.Farmer;

using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class FinancialsModel : PageModel
{
    private readonly IFarmerService _farmerService;

    public FinancialsModel(IFarmerService farmerService)
    {
        _farmerService = farmerService;
    }

    public FarmerFinancialSummaryDto? Financials { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var result = await _farmerService.GetFarmerFinancialSummaryAsync();
        if (result.IsSuccess)
        {
            Financials = result.Data;
        }
        else
        {
            ErrorMessage = result.Error ?? "Failed to load financial statement.";
        }
    }
}
