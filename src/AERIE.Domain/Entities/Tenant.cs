namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using AERIE.Domain.Enums;

/// <summary>
/// Represents a registered company/organization in the multi-tenant system.
/// This is the root of all tenant-scoped data.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;          // URL-safe unique identifier
    public string? LogoUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public TenantPlan Plan { get; set; } = TenantPlan.Starter;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Farm> Farms { get; set; } = new List<Farm>();
}
