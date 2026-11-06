namespace AERIE.Web.Pages.Auth;

using System.Security.Claims;
using AERIE.Domain.Constants;
using AERIE.Domain.Entities;
using AERIE.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

[IgnoreAntiforgeryToken]
public class LoginModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<LoginModel> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Role { get; set; } = "Farmer";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [TempData]
    public string? ErrorMessage { get; set; }

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

        // Pre-fill email hint depending on role for ease of use
        if (string.IsNullOrWhiteSpace(Email))
        {
            Email = Role.Equals("Broker", StringComparison.OrdinalIgnoreCase) 
                ? "broker@aerie.pk" 
                : "farmer@aerie.pk";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password.";
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Email.Trim());
        if (user == null || !user.IsActive)
        {
            ErrorMessage = "Invalid credentials or inactive account.";
            return Page();
        }

        var valid = await _userManager.CheckPasswordAsync(user, Password);
        if (!valid)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId);
        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? Role;

        await SignInUserAsync(user, tenant?.Name ?? "AERIE Enterprise", primaryRole);

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return LocalRedirect(ReturnUrl);

        return primaryRole.Equals("Broker", StringComparison.OrdinalIgnoreCase)
            ? RedirectToPage("/Broker/Dashboard")
            : RedirectToPage("/Farmer/Dashboard");
    }

    public async Task<IActionResult> OnPostDemoLoginAsync(string? targetRole, string? role)
    {
        var resolvedRole = !string.IsNullOrWhiteSpace(targetRole) ? targetRole : (!string.IsNullOrWhiteSpace(role) ? role : (!string.IsNullOrWhiteSpace(Role) ? Role : "Farmer"));
        bool isBroker = resolvedRole.Equals("Broker", StringComparison.OrdinalIgnoreCase);

        var demoEmail = isBroker ? "broker@aerie.pk" : "farmer@aerie.pk";
        var demoTenantId = isBroker ? AerieDemoConstants.BrokerTenantId : AerieDemoConstants.FarmerTenantId;
        var demoTenantName = isBroker ? "Punjab Poultry Traders" : "Al-Madina Poultry Farms";
        var demoUserName = isBroker ? "Mian Tariq Mehmood" : "Haji Muhammad Aslam";

        var user = await _userManager.FindByEmailAsync(demoEmail);
        if (user == null)
        {
            // Fallback user if seeder hasn't run
            user = new ApplicationUser
            {
                Id = isBroker ? AerieDemoConstants.BrokerUserId : AerieDemoConstants.FarmerUserId,
                UserName = demoEmail,
                Email = demoEmail,
                FirstName = isBroker ? "Mian Tariq" : "Haji Muhammad",
                LastName = isBroker ? "Mehmood" : "Aslam",
                TenantId = demoTenantId,
                IsActive = true,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(user, "Password123!");
            await _userManager.AddToRoleAsync(user, isBroker ? "Broker" : "Farmer");
        }

        await SignInUserAsync(user, demoTenantName, isBroker ? "Broker" : "Farmer");

        return isBroker 
            ? RedirectToPage("/Broker/Dashboard") 
            : RedirectToPage("/Farmer/Dashboard");
    }

    private async Task SignInUserAsync(ApplicationUser user, string tenantName, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("tenant_name", tenantName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
        _logger.LogInformation("User {Email} signed in as {Role} for tenant {TenantName}", user.Email, role, tenantName);
    }
}
