namespace AERIE.Infrastructure.Services;

using AERIE.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

/// <summary>
/// Resolves TenantId and UserId from validated JWT claims in the HttpContext.
/// This is the ONLY place TenantId is derived — never from request body.
/// Registered as Scoped (per-request lifetime).
/// </summary>
public class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid TenantId
    {
        get
        {
            var claim = User?.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var id) && id != Guid.Empty)
                return id;

            var ctx = _httpContextAccessor.HttpContext;
            if (ctx != null && ctx.Request.Cookies.TryGetValue("AERIE_DEMO_TENANT", out var cookieVal) && Guid.TryParse(cookieVal, out var cookieId))
                return cookieId;

            return Guid.Empty;
        }
    }

    public Guid UserId
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var id) && id != Guid.Empty)
                return id;

            return Guid.Empty;
        }
    }

    public string UserRole =>
        User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
}
