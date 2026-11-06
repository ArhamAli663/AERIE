namespace AERIE.Web.Pages.Farmer;

using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class ReportsModel : PageModel
{
    private readonly IFarmerService _farmerService;

    public ReportsModel(IFarmerService farmerService)
    {
        _farmerService = farmerService;
    }

    public IEnumerable<FlockBatchDto> Batches { get; set; } = [];
    public FlockProductionReportDto? Report { get; set; }
    public Guid? SelectedBatchId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(Guid? batchId, DateOnly? fromDate, DateOnly? toDate)
    {
        var batchesRes = await _farmerService.GetFlockBatchesAsync();
        if (batchesRes.IsSuccess)
        {
            Batches = batchesRes.Data ?? [];
        }

        SelectedBatchId = batchId ?? Batches.FirstOrDefault()?.Id;
        FromDate = fromDate;
        ToDate = toDate;

        if (SelectedBatchId.HasValue)
        {
            var repRes = await _farmerService.GetFlockProductionReportAsync(SelectedBatchId.Value, fromDate, toDate);
            if (repRes.IsSuccess)
            {
                Report = repRes.Data;
            }
            else
            {
                ErrorMessage = repRes.Error;
            }
        }
    }
}
