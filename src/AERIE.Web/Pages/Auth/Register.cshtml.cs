namespace AERIE.Web.Pages.Auth;

using System.Security.Claims;
using AERIE.Application.Auth.DTOs;
using AERIE.Application.Auth.Interfaces;
using AERIE.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class RegisterModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        IAuthService authService,
        UserManager<ApplicationUser> userManager,
        ILogger<RegisterModel> logger)
    {
        _authService = authService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Role { get; set; } = "Farmer";

    [BindProperty] public string CompanyName { get; set; } = string.Empty;
    [BindProperty] public string City { get; set; } = string.Empty;
    [BindProperty] public string ContactPhone { get; set; } = string.Empty;
    [BindProperty] public string FirstName { get; set; } = string.Empty;
    [BindProperty] public string LastName { get; set; } = string.Empty;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    [TempData] public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(currentRole, "Broker", StringComparison.OrdinalIgnoreCase))
                return RedirectToPage("/Broker/Dashboard");
            
            return RedirectToPage("/Farmer/Dashboard");
        }

        if (string.IsNullOrWhiteSpace(Role))
            Role = "Farmer";

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(CompanyName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Company Name, Email, and Password are required.";
            return Page();
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        bool isBroker = Role.Equals("Broker", StringComparison.OrdinalIgnoreCase);
        var targetRole = isBroker ? "Broker" : "Farmer";

        // Generate clean company slug
        var slug = CompanyName.ToLower().Trim()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "");
        if (slug.Length > 40) slug = slug[..40];
        slug = $"{slug}-{DateTime.UtcNow.Ticks % 10000}";

        var request = new RegisterTenantRequest(
            CompanyName: CompanyName.Trim(),
            CompanySlug: slug,
            ContactEmail: Email.Trim(),
            ContactPhone: ContactPhone?.Trim(),
            Address: City?.Trim(),
            UserFirstName: FirstName?.Trim() ?? "Enterprise",
            UserLastName: LastName?.Trim() ?? "User",
            UserEmail: Email.Trim(),
            Password: Password,
            Role: targetRole
        );

        var result = await _authService.RegisterTenantAsync(request);
        if (!result.IsSuccess || result.Data == null)
        {
            ErrorMessage = result.Error ?? "Registration failed.";
            return Page();
        }

        var authData = result.Data;

        // Sign in user with cookie authentication
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, authData.UserId.ToString()),
            new Claim(ClaimTypes.Name, authData.FullName),
            new Claim(ClaimTypes.Email, authData.Email),
            new Claim(ClaimTypes.Role, targetRole),
            new Claim("tenant_id", authData.TenantId.ToString()),
            new Claim("tenant_name", authData.TenantName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        });

        _logger.LogInformation("New {Role} company registered and signed in: {CompanyName}", targetRole, CompanyName);

        return isBroker 
            ? RedirectToPage("/Broker/Dashboard") 
            : RedirectToPage("/Farmer/Dashboard");
    }
}
