namespace AERIE.Web.Pages.Broker;

using AERIE.Application.Brokers.DTOs;
using AERIE.Application.Brokers.Interfaces;
using AERIE.Application.Common.Interfaces;
using AERIE.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class DealsModel : PageModel
{
    private readonly IApplicationDbContext _context;
    private readonly IBrokerService _brokerService;

    public DealsModel(IApplicationDbContext context, IBrokerService brokerService)
    {
        _context = context;
        _brokerService = brokerService;
    }

    public List<DealDto> Deals { get; set; } = [];
    public List<DealProposalDto> Proposals { get; set; } = [];
    [TempData] public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var deals = await _context.Deals
            .IgnoreQueryFilters()
            .Include(d => d.Listing)
            .Include(d => d.Settlement)
            .OrderByDescending(d => d.AcceptedAt)
            .ToListAsync();

        var tenantIds = deals.Select(d => d.FarmerTenantId).Concat(deals.Select(d => d.BrokerTenantId)).Distinct().ToList();
        var tenantNames = await _context.Tenants
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        Deals = deals.Select(d => new DealDto(
            d.Id,
            d.ProposalId,
            d.ListingId,
            d.Listing?.Title ?? "Listing",
            d.FarmerTenantId,
            tenantNames.GetValueOrDefault(d.FarmerTenantId, "Al-Madina Poultry Farms"),
            d.BrokerTenantId,
            tenantNames.GetValueOrDefault(d.BrokerTenantId, "Punjab Poultry Traders"),
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

        // Also fetch active proposals
        var proposals = await _context.DealProposals
            .IgnoreQueryFilters()
            .Include(p => p.Listing)
            .Include(p => p.Deal)
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

    public async Task<IActionResult> OnPostRecordLiftingAsync(
        Guid dealId, decimal actualWeightKg, int birdsLoaded, string vehicleNumber, string? weighbridgeTicketNumber, string? driverName, string? driverPhone)
    {
        var result = await _brokerService.RecordLiftingAsync(dealId, new RecordLiftingRequest(
            ActualWeightKg: actualWeightKg,
            BirdsLoaded: birdsLoaded,
            MortalityDuringTransit: 0,
            VehicleNumber: vehicleNumber,
            DriverName: driverName,
            DriverPhone: driverPhone,
            WeighbridgeTicketNumber: weighbridgeTicketNumber,
            Notes: "Recorded via Web UI"
        ));

        StatusMessage = result.IsSuccess
            ? $"Lifting recorded! Net weight: {actualWeightKg:N1} kg verified from weighbridge. Deal status is now In Transit."
            : $"Failed to record lifting: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRecordDeliveryAsync(
        Guid dealId, int birdsDelivered, int mortalityDuringTransit, decimal weightDeliveredKg, string? destinationLocation, decimal? freightCost, string? notes)
    {
        var result = await _brokerService.RecordDeliveryAsync(dealId, new RecordDeliveryRequest(
            BirdsDelivered: birdsDelivered,
            MortalityDuringTransit: mortalityDuringTransit,
            WeightDeliveredKg: weightDeliveredKg,
            DestinationLocation: destinationLocation,
            FreightCost: freightCost,
            Notes: notes
        ));

        StatusMessage = result.IsSuccess
            ? $"Delivery confirmation recorded! Delivered: {birdsDelivered:N0} birds ({weightDeliveredKg:N1} kg). Shrinkage: {result.Value?.ShrinkageKg:N1} kg ({result.Value?.ShrinkagePct:N2}%). Deal status is now Delivered."
            : $"Failed to record delivery: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCalculateSettlementAsync(
        Guid dealId, decimal commissionRate, CommissionRuleType commissionType, decimal freightAmount, string? notes)
    {
        var result = await _brokerService.CreateSettlementAsync(dealId, new CreateSettlementRequest(
            CommissionRate: commissionRate,
            CommissionType: commissionType,
            FreightAmount: freightAmount,
            Deductions: 0m,
            Additions: 0m,
            Notes: notes
        ));

        StatusMessage = result.IsSuccess
            ? $"Settlement calculated! Gross: Rs. {result.Data!.TotalGrossAmount:N2}, Comm: Rs. {result.Data.CommissionAmount:N2}, Net Farmer Payable: Rs. {result.Data.NetFarmerPayable:N2}."
            : $"Failed to calculate settlement: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostGenerateInvoiceAsync(Guid dealId, decimal totalAmount)
    {
        var deal = await _context.Deals.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == dealId);
        if (deal == null)
        {
            StatusMessage = "Deal not found.";
            return RedirectToPage();
        }

        var result = await _brokerService.CreateInvoiceAsync(new CreateInvoiceRequest(
            DealId: dealId,
            RecipientTenantId: deal.BrokerTenantId,
            RecipientName: "Trading Partner",
            RecipientAddress: "Wholesale Mandi",
            SubTotal: totalAmount,
            TaxAmount: 0m,
            TotalAmount: totalAmount,
            DueDate: DateTime.UtcNow.AddDays(7),
            Notes: "Generated via Web UI"
        ));

        StatusMessage = result.IsSuccess
            ? $"Invoice #{result.Data!.InvoiceNumber} generated for amount Rs. {result.Data.TotalAmount:N2}!"
            : $"Failed to generate invoice: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRecordPaymentAsync(
        Guid dealId, decimal amount, PaymentMethod method, string? referenceNumber, string? bankName)
    {
        var result = await _brokerService.RecordPaymentAsync(new RecordPaymentRequest(
            DealId: dealId,
            InvoiceId: null,
            Amount: amount,
            Method: method,
            ReferenceNumber: referenceNumber,
            BankName: bankName,
            Notes: "Payment confirmed via Web UI"
        ));

        StatusMessage = result.IsSuccess
            ? $"Payment of Rs. {amount:N2} confirmed! Double-entry ledger audit posted. Deal status updated to Paid."
            : $"Failed to record payment: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCloseDealAsync(Guid dealId)
    {
        var result = await _brokerService.UpdateDealStatusAsync(dealId, new UpdateDealStatusRequest(
            NewStatus: DealStatus.Closed,
            Notes: "Deal finalized, payments confirmed, and closed."
        ));

        StatusMessage = result.IsSuccess
            ? "Trading deal successfully closed and archived."
            : $"Failed to close deal: {result.Error}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostWithdrawProposalAsync(Guid proposalId, string? reason)
    {
        var result = await _brokerService.WithdrawProposalAsync(proposalId, new WithdrawProposalRequest(reason));

        StatusMessage = result.IsSuccess
            ? "Purchase proposal withdrawn successfully. Reserved birds released."
            : $"Failed to withdraw proposal: {result.Error}";

        return RedirectToPage();
    }
}
