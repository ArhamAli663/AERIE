namespace AERIE.Application.Common.Interfaces;

/// <summary>
/// Resolves the current authenticated user's tenant from JWT claims.
/// NEVER accepts TenantId from request body/query — always from validated claims.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string UserRole { get; }
    bool IsAuthenticated { get; }
}
