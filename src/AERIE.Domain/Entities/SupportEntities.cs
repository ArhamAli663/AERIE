namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;

public class DealStatusHistory : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;
    public DealStatus Status { get; set; }
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

public class TransportRecord : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;
    public string? VehicleNumber { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? PickupLocation { get; set; }
    public string? DestinationLocation { get; set; }
    public DateTime? DepartureAt { get; set; }
    public DateTime? ArrivalAt { get; set; }
    public int BirdsLoaded { get; set; }
    public int BirdsDelivered { get; set; }
    public int MortalityDuringTransit { get; set; }
    public int BirdCountDiscrepancy => Math.Max(0, BirdsLoaded - BirdsDelivered - MortalityDuringTransit);
    public decimal? WeightLiftedKg { get; set; }    // Weighbridge at farm
    public decimal? WeightDeliveredKg { get; set; } // Weighbridge at destination
    public decimal? ShrinkageKg => WeightLiftedKg.HasValue && WeightDeliveredKg.HasValue
        ? WeightLiftedKg.Value - WeightDeliveredKg.Value : null;
    public decimal? ShrinkagePct => WeightLiftedKg.HasValue && WeightLiftedKg.Value > 0 && ShrinkageKg.HasValue
        ? Math.Round((ShrinkageKg.Value / WeightLiftedKg.Value) * 100m, 2) : null;
    public decimal? FreightCost { get; set; }
    public string DeliveryStatus { get; set; } = "InTransit"; // InTransit, Delivered, DiscrepancyReported
    public string? Notes { get; set; }
}

public class FarmStaff : TenantEntity
{
    public Guid FarmId { get; set; }
    public Farm Farm { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Role { get; set; } = string.Empty;  // e.g., Manager, Labour, Supervisor
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
