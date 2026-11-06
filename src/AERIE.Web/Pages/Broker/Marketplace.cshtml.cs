namespace AERIE.Web.Pages.Broker;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using AERIE.Application.Common.Interfaces;
using AERIE.Application.Farmers.DTOs;
using AERIE.Domain.Entities;
using AERIE.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class MarketplaceModel : PageModel
{
    private readonly IApplicationDbContext _context;
    private readonly IBrokerService _brokerService;

    public MarketplaceModel(IApplicationDbContext context, IBrokerService brokerService)
    {
        _context = context;
        _brokerService = brokerService;
    }

    public List<ListingDto> Listings { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? SelectedCity { get; set; }
    [TempData] public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var query = _context.MarketplaceListings
            .IgnoreQueryFilters()
            .Include(l => l.Batch)
            .ThenInclude(b => b.Breed)
            .Where(l => l.Status == ListingStatus.Published && l.QuantityAvailable > 0);

        if (!string.IsNullOrWhiteSpace(SelectedCity))
        {
            query = query.Where(l => l.LocationCity.ToLower() == SelectedCity.ToLower());
        }

        var list = await query.OrderByDescending(l => l.PublishedAt).ToListAsync();

        Listings = list.Select(l => new ListingDto(
            l.Id,
            l.BatchId,
            l.Batch?.BatchCode ?? "BATCH",
            l.Batch?.Breed?.Name ?? "Broiler",
            l.Title,
            l.QuantityTotal,
            l.QuantityAvailable,
            l.AskingPricePerKg,
            l.NegotiableFloorPricePerKg,
            l.LocationCity,
            l.Status.ToString(),
            l.PublishedAt,
            l.ExpiresAt,
            l.Notes
        )).ToList();
    }

    public async Task<IActionResult> OnPostSubmitProposalAsync(
        Guid listingId, int quantityRequested, decimal offeredPricePerKg, string? transportArrangement, string? notes)
    {
        var brokerTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Slug == "punjab-traders")
            ?? await _context.Tenants.FirstOrDefaultAsync();

        var brokerTenantId = brokerTenant?.Id ?? Guid.NewGuid();

        var result = await _brokerService.CreateProposalAsync(new CreateProposalRequest(
            ListingId: listingId,
            QuantityRequested: quantityRequested,
            OfferedPricePerKg: offeredPricePerKg,
            TransportArrangement: transportArrangement,
            Notes: notes
        ));

        if (result.IsSuccess)
        {
            StatusMessage = $"Purchase proposal for {quantityRequested:N0} birds @ Rs. {offeredPricePerKg}/kg submitted to the farmer successfully!";
            return RedirectToPage("/Broker/Deals");
        }
        else
        {
            StatusMessage = $"Failed to submit proposal: {result.Error}";
            return RedirectToPage();
        }
    }
}
