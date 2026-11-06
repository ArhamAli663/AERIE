namespace AERIE.Domain.Entities;

using AERIE.Domain.Common;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Extends ASP.NET Core Identity IdentityUser with tenant and role metadata.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
}
