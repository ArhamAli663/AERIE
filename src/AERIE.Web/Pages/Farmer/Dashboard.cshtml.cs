namespace AERIE.Web.Pages.Farmer;

using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class DashboardModel : PageModel
{
    private readonly IFarmerService _farmerService;

    public DashboardModel(IFarmerService farmerService)
    {
        _farmerService = farmerService;
    }

    public FarmerDashboardSummaryDto? Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        var result = await _farmerService.GetFarmerDashboardSummaryAsync();
        if (result.IsSuccess)
        {
            Summary = result.Data;
        }
        else
        {
            ErrorMessage = result.Error ?? "Failed to load dashboard summary.";
        }
    }

    public async Task<IActionResult> OnPostRecordObservationAsync(
        Guid batchId,
        DateOnly recordDate,
        int aliveCount,
        int dailyMortality,
        string feedCategory,
        decimal feedConsumedKg,
        decimal waterConsumedLiters,
        decimal? averageWeightGrams,
        string? notes)
    {
        var request = new CreateDailyRecordRequest(
            batchId,
            recordDate,
            aliveCount,
            dailyMortality,
            feedConsumedKg,
            waterConsumedLiters,
            averageWeightGrams,
            notes,
            ClientRecordId: Guid.NewGuid().ToString(),
            FeedCategory: feedCategory
        );

        var result = await _farmerService.RecordDailyDataAsync(request);
        if (result.IsSuccess)
        {
            SuccessMessage = $"Observation successfully recorded for {recordDate}. Production KPIs updated.";
        }
        else
        {
            ErrorMessage = result.Error ?? "Failed to record observation.";
        }

        await OnGetAsync();
        return Page();
    }
}
