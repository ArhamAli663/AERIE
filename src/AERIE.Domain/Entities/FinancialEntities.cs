namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;

/// <summary>
/// Calculated settlement for a completed deal.
/// Commission, freight and net payable amounts.
/// NOT automatically created — created by authorized user triggering settlement.
/// </summary>
public class Settlement : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;

    public decimal GrossAmount { get; set; }            // AgreedPricePerKg * ActualWeightKg
    public decimal CommissionAmount { get; set; }       // Calculated from CommissionRule
    public decimal FreightAmount { get; set; }          // Transport cost
    public decimal OtherExpenses { get; set; } = 0m;
    public decimal NetPayableToFarmer { get; set; }     // Gross - Commission - Freight - Other
    public decimal NetPayableToBroker { get; set; }     // Commission - Freight (broker's earned portion)
    public string? Notes { get; set; }
    public DateTime SettledAt { get; set; } = DateTime.UtcNow;
    public Guid SettledByUserId { get; set; }

    // Navigation
    public ICollection<DealExpense> Expenses { get; set; } = new List<DealExpense>();
}

public class DealExpense : BaseEntity
{
    public Guid DealId { get; set; }
    public string Category { get; set; } = string.Empty;  // Freight, Loading, Other
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public Guid RecordedByUserId { get; set; }
}

public class CommissionRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public CommissionRuleType RuleType { get; set; } = CommissionRuleType.Percentage;
    public decimal Rate { get; set; }         // % or flat amount
    public bool IsDefault { get; set; } = false;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Invoice : TenantEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;
    public string InvoiceNumber { get; set; } = string.Empty;  // Auto-generated
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; } = 0m;
    public decimal BalanceDue => TotalAmount - PaidAmount;
    public string Status { get; set; } = "Unpaid";  // Unpaid, PartiallyPaid, Paid
    public string? Notes { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount => Quantity * UnitPrice;
}

public class Payment : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }   // Null until authorized user confirms
}

public class LedgerEntry : TenantEntity
{
    public Guid? DealId { get; set; }
    public Deal? Deal { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public decimal RunningBalance { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public Guid RecordedByUserId { get; set; }
}

public class MarketPrice : BaseEntity
{
    public Guid? BreedId { get; set; }
    public Breed? Breed { get; set; }
    public string City { get; set; } = string.Empty;
    public string MarketName { get; set; } = "Main Mandi";
    public string Category { get; set; } = "Broiler Live Bird";
    public string PricingUnit { get; set; } = "Rs/Kg";
    public decimal PricePerKg { get; set; }
    public decimal? PreviousPricePerKg { get; set; }
    public decimal PriceChange => PreviousPricePerKg.HasValue ? PricePerKg - PreviousPricePerKg.Value : 0m;
    public string? Source { get; set; }   // e.g., "Lahore Poultry Traders Association"
    public bool IsVerified { get; set; } = true;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Guid RecordedByUserId { get; set; }
}

public class Notification : TenantEntity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; } = false;
    public Guid? RelatedEntityId { get; set; }
}

public class AuditLog : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorUserId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;   // Created, Updated, Deleted, StatusChanged
    public string? OldValues { get; set; }   // JSON
    public string? NewValues { get; set; }   // JSON
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
