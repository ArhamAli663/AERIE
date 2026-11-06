namespace AERIE.Domain.Enums;

public enum TenantPlan { Starter, Professional, Enterprise }
public enum BatchStatus { Active, ReadyForSale, PartialSold, FullySold, Closed }
public enum ListingStatus { Draft, Published, Reserved, Sold, Cancelled }
public enum DealStatus
{
    Proposed,       // Broker submitted proposal
    Reviewed,       // Farmer has seen it
    Accepted,       // Farmer accepted
    Rejected,       // Farmer rejected
    Withdrawn,      // Broker withdrew proposal
    InTransit,      // Transport/lifting started
    Delivered,      // Delivery confirmation & discrepancies recorded
    Settled,        // Commission & freight calculated
    Invoiced,       // Invoice generated
    Paid,           // Payment confirmed
    Closed,         // Deal fully closed
    Cancelled       // Deal cancelled
}
public enum PaymentMethod { Cash, BankTransfer, Cheque, OnlineTransfer }
public enum LedgerEntryType { Debit, Credit }
public enum CommissionRuleType { Percentage, FlatPerBird, FlatPerKg }
public enum FreightRuleType { PerKm, FlatRate }
public enum UserRole { Admin, FarmOwner, FarmManager, FarmLabour, BrokerOwner, BrokerDealer }
public enum SyncStatus { Pending, Syncing, Synced, Failed }
public enum NotificationType { DealProposed, DealAccepted, DealRejected, PaymentReceived, ListingPublished, MarketPriceUpdate }
