namespace AERIE.Domain.Common;

/// <summary>
/// Tenant-scoped base entity — every business record that belongs to a specific tenant
/// inherits this. TenantId is NEVER set from client input; it is always stamped by
/// the application layer from validated JWT claims.
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}
