namespace AERIE.Web.Pages.Broker;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Common.Interfaces;
using AERIE.Domain.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class LedgerModel : PageModel
{
    private readonly IApplicationDbContext _context;

    public LedgerModel(IApplicationDbContext context)
    {
        _context = context;
    }

    public List<InvoiceDto> Invoices { get; set; } = [];
    public List<LedgerEntry> LedgerEntries { get; set; } = [];

    public async Task OnGetAsync()
    {
        var invoices = await _context.Invoices
            .IgnoreQueryFilters()
            .Include(i => i.Deal)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync();

        Invoices = invoices.Select(i => new InvoiceDto(
            i.Id,
            i.DealId,
            i.InvoiceNumber,
            i.TenantId,
            i.Deal?.BrokerTenantId ?? Guid.Empty,
            "Trading Partner",
            i.TotalAmount,
            0m,
            i.TotalAmount,
            i.PaidAmount,
            i.BalanceDue,
            i.Status,
            i.IssuedAt,
            i.DueDate ?? i.IssuedAt.AddDays(7),
            i.Notes
        )).ToList();

        LedgerEntries = await _context.LedgerEntries
            .IgnoreQueryFilters()
            .OrderByDescending(l => l.EntryDate)
            .Take(50)
            .ToListAsync();
    }
}
