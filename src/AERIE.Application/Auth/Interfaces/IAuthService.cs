namespace AERIE.Application.Auth.Interfaces;

using AERIE.Application.Auth.DTOs;
using AERIE.Application.Common.Models;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<UserProfileDto>> GetCurrentUserProfileAsync(CancellationToken ct = default);
}
