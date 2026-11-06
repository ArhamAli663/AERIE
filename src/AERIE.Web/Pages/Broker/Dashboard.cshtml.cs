namespace AERIE.Web.Pages.Broker;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class DashboardModel : PageModel
{
    private readonly IBrokerService _brokerService;

    public DashboardModel(IBrokerService brokerService)
    {
        _brokerService = brokerService;
    }

    public BrokerDashboardSummaryDto? Summary { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await _brokerService.GetBrokerDashboardSummaryAsync();
        if (result.IsSuccess)
        {
            Summary = result.Value;
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return Page();
    }
}
