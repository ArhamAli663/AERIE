namespace AERIE.Web.Pages.Farmer;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using AERIE.Application.Common.Interfaces;
using AERIE.Application.Farmers.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class ListingsModel : PageModel
{
    private readonly IApplicationDbContext _context;
    private readonly IBrokerService _brokerService;
    private readonly ITenantContext _tenantContext;

    public ListingsModel(
        IApplicationDbContext context, 
        IBrokerService brokerService,
        ITenantContext tenantContext)
    {
        _context = context;
        _brokerService = brokerService;
        _tenantContext = tenantContext;
    }

    public List<ListingDto> Listings { get; set; } = [];
    public List<DealProposalDto> Proposals { get; set; } = [];
    [TempData] public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var tenantId = _tenantContext.TenantId;
        var listings = await _context.MarketplaceListings
            .Where(l => l.TenantId == tenantId)
            .Include(l => l.Batch)
            .ThenInclude(b => b.Breed)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        Listings = listings.Select(l => new ListingDto(
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

        var proposals = await _context.DealProposals
            .Include(p => p.Listing)
            .Include(p => p.Deal)
            .Where(p => p.Listing != null && p.Listing.TenantId == tenantId)
            .OrderByDescending(p => p.ProposedAt)
            .ToListAsync();

        Proposals = proposals.Select(p => new DealProposalDto(
            p.Id,
            p.ListingId,
            p.Listing?.Title ?? "Listing",
            p.BrokerTenantId,
            p.BrokerCompanyName,
            p.ProposedByUserId,
            p.QuantityRequested,
            p.OfferedPricePerKg,
            p.TransportArrangement,
            p.Notes,
            p.Status,
            p.ProposedAt,
            p.FarmerResponse,
            p.RespondedAt,
            p.Deal?.Id
        )).ToList();
    }

    public async Task<IActionResult> OnPostAcceptProposalAsync(Guid proposalId)
    {
        var result = await _brokerService.ReviewProposalAsync(proposalId, new ReviewProposalRequest(
            Accept: true,
            ResponseNotes: "Accepted via Farmer Portal. Deal initiated."
        ));

        if (result.IsSuccess)
        {
            StatusMessage = $"Proposal accepted! Active deal #{result.Data!.Id.ToString("N")[..8]} created and {result.Data.QuantityBirds:N0} birds reserved.";
        }
        else
        {
            StatusMessage = $"Failed to accept proposal: {result.Error}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectProposalAsync(Guid proposalId)
    {
        var result = await _brokerService.ReviewProposalAsync(proposalId, new ReviewProposalRequest(
            Accept: false,
            ResponseNotes: "Declined by farmer."
        ));

        StatusMessage = "Proposal declined.";
        return RedirectToPage();
    }
}
