namespace AERIE.Web.Pages;

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
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public int TotalLiveBirds { get; set; } = 28450;
    public int ActiveBatchCount { get; set; } = 4;
    public int LiveListingCount { get; set; } = 2;
    public int ActiveDealCount { get; set; } = 3;

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.Equals(role, "Broker", StringComparison.OrdinalIgnoreCase))
                return RedirectToPage("/Broker/Dashboard");

            return RedirectToPage("/Farmer/Dashboard");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostQuickDemoAsync(string role)
    {
        bool isBroker = string.Equals(role, "Broker", StringComparison.OrdinalIgnoreCase);

        var demoEmail = isBroker ? "broker@aerie.pk" : "farmer@aerie.pk";
        var demoTenantId = isBroker ? AerieDemoConstants.BrokerTenantId : AerieDemoConstants.FarmerTenantId;
        var demoTenantName = isBroker ? "Punjab Poultry Traders" : "Al-Madina Poultry Farms";
        var demoName = isBroker ? "Mian Tariq Mehmood" : "Haji Muhammad Aslam";
        var targetRole = isBroker ? "Broker" : "Farmer";

        var user = await _userManager.FindByEmailAsync(demoEmail);
        if (user == null)
        {
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
            await _userManager.AddToRoleAsync(user, targetRole);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, targetRole),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("tenant_name", demoTenantName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        });

        return isBroker 
            ? RedirectToPage("/Broker/Dashboard") 
            : RedirectToPage("/Farmer/Dashboard");
    }
}
