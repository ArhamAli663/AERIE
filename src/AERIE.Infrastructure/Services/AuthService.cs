namespace AERIE.Infrastructure.Services;

using AERIE.Application.Auth.DTOs;
using AERIE.Application.Auth.Interfaces;
using AERIE.Application.Common.Interfaces;
using AERIE.Application.Common.Models;
using AERIE.Domain.Entities;
using AERIE.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IApplicationDbContext context,
        ITenantContext tenantContext,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct = default)
    {
        var existingSlug = await _context.Tenants.AnyAsync(t => t.Slug == request.CompanySlug, ct);
        if (existingSlug)
            return Result<AuthResponse>.Failure($"Company slug '{request.CompanySlug}' is already taken.");

        var existingUser = await _userManager.FindByEmailAsync(request.UserEmail);
        if (existingUser != null)
            return Result<AuthResponse>.Failure($"A user with email '{request.UserEmail}' already exists.");

        // Create Tenant
        var tenant = new Tenant
        {
            Name = request.CompanyName,
            Slug = request.CompanySlug,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            IsActive = true
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync(ct);

        // Ensure Role exists
        var roleName = string.IsNullOrWhiteSpace(request.Role) ? "FarmOwner" : request.Role;
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }

        // Create Admin User
        var user = new ApplicationUser
        {
            UserName = request.UserEmail,
            Email = request.UserEmail,
            FirstName = request.UserFirstName,
            LastName = request.UserLastName,
            TenantId = tenant.Id,
            IsActive = true,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<AuthResponse>.Failure($"User creation failed: {errors}");
        }

        await _userManager.AddToRoleAsync(user, roleName);

        var tokenInfo = GenerateJwtToken(user, tenant, roleName);

        return Result<AuthResponse>.Success(new AuthResponse(
            tokenInfo.Token,
            tokenInfo.ExpiresAt,
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            roleName,
            tenant.Id,
            tenant.Name
        ));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
            return Result<AuthResponse>.Failure("Invalid email or password.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
            return Result<AuthResponse>.Failure("Invalid email or password.");

        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId, ct);
        if (tenant == null || !tenant.IsActive)
            return Result<AuthResponse>.Failure("Associated tenant company is inactive.");

        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? "User";

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var tokenInfo = GenerateJwtToken(user, tenant, primaryRole);

        return Result<AuthResponse>.Success(new AuthResponse(
            tokenInfo.Token,
            tokenInfo.ExpiresAt,
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            primaryRole,
            tenant.Id,
            tenant.Name
        ));
    }

    public async Task<Result<UserProfileDto>> GetCurrentUserProfileAsync(CancellationToken ct = default)
    {
        var userId = _tenantContext.UserId;
        if (userId == Guid.Empty)
            return Result<UserProfileDto>.Failure("User not authenticated.");

        var user = await _context.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return Result<UserProfileDto>.Failure("User profile not found.");

        return Result<UserProfileDto>.Success(new UserProfileDto(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            _tenantContext.UserRole,
            user.TenantId,
            user.Tenant?.Name ?? "Tenant",
            user.IsActive
        ));
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(ApplicationUser user, Tenant tenant, string role)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "AERIE_Super_Secure_Secret_Key_For_Poultry_ERP_2026_Minimum_32_Chars!";
        var issuer = _configuration["Jwt:Issuer"] ?? "AERIE.Platform";
        var audience = _configuration["Jwt:Audience"] ?? "AERIE.Clients";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddDays(7);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, role),
            new("tenant_id", tenant.Id.ToString()),
            new("tenant_name", tenant.Name),
            new("tenant_slug", tenant.Slug)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
