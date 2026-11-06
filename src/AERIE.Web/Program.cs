using AERIE.Application;
using AERIE.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Register Infrastructure and Application services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices();

// Authentication for Razor Pages
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.Cookie.Name = "AERIE.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.Cookie.Name = "AERIE.Auth";
});

builder.Services.AddAuthorization();
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

var app = builder.Build();

// Seed initial demonstrator ERP data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AERIE.Infrastructure.Persistence.ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await AERIE.Infrastructure.Persistence.AerieDatabaseSeeder.SeedAsync(context, scope.ServiceProvider, logger);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Enforce strict portal separation between Farmer and Broker
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (path.StartsWith("/Broker", StringComparison.OrdinalIgnoreCase) && 
            !path.StartsWith("/Broker/MarketRates", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "Broker", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Auth/AccessDenied?message=" + Uri.EscapeDataString("Access Restricted: You are signed in as a Poultry Farmer. Broker trading modules are only accessible to verified Brokers."));
            return;
        }

        if (path.StartsWith("/Farmer", StringComparison.OrdinalIgnoreCase) && 
            !string.Equals(role, "Farmer", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Auth/AccessDenied?message=" + Uri.EscapeDataString("Access Restricted: You are signed in as a Poultry Broker. Farm shed management & production records are private to Farm Tenants."));
            return;
        }
    }
    else
    {
        // Unauthenticated access to /Farmer/* or /Broker/* (except shared rates)
        if ((path.StartsWith("/Farmer", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/Broker", StringComparison.OrdinalIgnoreCase)) &&
            !path.StartsWith("/Broker/MarketRates", StringComparison.OrdinalIgnoreCase))
        {
            var targetRole = path.StartsWith("/Broker", StringComparison.OrdinalIgnoreCase) ? "Broker" : "Farmer";
            context.Response.Redirect($"/Auth/Login?role={targetRole}&returnUrl={Uri.EscapeDataString(path)}");
            return;
        }
    }

    await next();
});

app.MapRazorPages();

app.Run();
