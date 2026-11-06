namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;

/// <summary>
/// A physical farm owned by a tenant (farmer company).
/// A farm has one or many poultry houses.
/// </summary>
public class Farm : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Province { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int TotalCapacity { get; set; }           // Total bird capacity
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<PoultryHouse> PoultryHouses { get; set; } = new List<PoultryHouse>();
    public ICollection<FarmStaff> Staff { get; set; } = new List<FarmStaff>();
}
