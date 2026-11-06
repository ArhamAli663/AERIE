namespace AERIE.Web.Pages.Auth;

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class AccessDeniedModel : PageModel
{
    public string UserRole { get; set; } = string.Empty;
    public string? Message { get; set; }

    public void OnGet(string? message)
    {
        UserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        Message = message;
    }
}
