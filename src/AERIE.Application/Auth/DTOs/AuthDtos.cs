namespace AERIE.Application.Auth.DTOs;

public record RegisterTenantRequest(
    string CompanyName,
    string CompanySlug,
    string ContactEmail,
    string? ContactPhone,
    string? Address,
    string UserFirstName,
    string UserLastName,
    string UserEmail,
    string Password,
    string Role       // "FarmOwner" or "BrokerOwner"
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    Guid TenantId,
    string TenantName
);

public record UserProfileDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    Guid TenantId,
    string TenantName,
    bool IsActive
);
