namespace AERIE.Web.Pages;

using AERIE.Application.Common.Interfaces;
using AERIE.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public class PoultryShopsModel : PageModel
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PoultryShopsModel> _logger;

    public PoultryShopsModel(IApplicationDbContext context, ILogger<PoultryShopsModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public List<PoultryShop> Shops { get; set; } = [];
    [BindProperty(SupportsGet = true)] public string? SearchQuery { get; set; }
    [BindProperty(SupportsGet = true)] public string? SelectedCity { get; set; }
    [BindProperty(SupportsGet = true)] public string? SelectedType { get; set; }

    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    // Analysis Metrics
    public int TotalShopsCount { get; set; }
    public decimal AvgBroilerMeatRate { get; set; }
    public decimal AvgLiveBroilerBuyRate { get; set; }
    public decimal TotalDailyCapacityKg { get; set; }
    public List<string> AvailableCities { get; set; } = [];

    public async Task OnGetAsync()
    {
        var query = _context.PoultryShops.AsQueryable();

        AvailableCities = await _context.PoultryShops
            .Select(s => s.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(SelectedCity) && SelectedCity != "All")
        {
            query = query.Where(s => s.City.ToLower() == SelectedCity.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(SelectedType) && SelectedType != "All")
        {
            query = query.Where(s => s.ShopType.ToLower() == SelectedType.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var filter = SearchQuery.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(filter) 
                                  || s.City.ToLower().Contains(filter) 
                                  || s.MarketName.ToLower().Contains(filter)
                                  || s.Address.ToLower().Contains(filter)
                                  || s.ContactPerson.ToLower().Contains(filter));
        }

        Shops = await query.OrderByDescending(s => s.GoogleRating).ThenByDescending(s => s.DailyCapacityKg).ToListAsync();

        // Calculate Aggregate Analytics
        var allShops = await _context.PoultryShops.ToListAsync();
        TotalShopsCount = allShops.Count;
        if (allShops.Any())
        {
            AvgBroilerMeatRate = Math.Round(allShops.Average(s => s.CurrentBroilerMeatRatePerKg), 1);
            AvgLiveBroilerBuyRate = Math.Round(allShops.Average(s => s.CurrentLiveBroilerBuyRatePerKg), 1);
            TotalDailyCapacityKg = allShops.Sum(s => s.DailyCapacityKg);
        }
    }

    public async Task<IActionResult> OnPostAddShopAsync(
        string name,
        string city,
        string province,
        string marketName,
        string address,
        string shopType,
        string contactPerson,
        string contactPhone,
        decimal dailyCapacityKg,
        decimal currentBroilerMeatRatePerKg,
        decimal currentLiveBroilerBuyRatePerKg,
        string? specialties,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(contactPhone))
        {
            ErrorMessage = "Shop name, city, and contact phone are required.";
            return RedirectToPage();
        }

        var shop = new PoultryShop
        {
            Name = name.Trim(),
            City = city.Trim(),
            Province = string.IsNullOrWhiteSpace(province) ? "Punjab" : province.Trim(),
            MarketName = string.IsNullOrWhiteSpace(marketName) ? "Local Mandi" : marketName.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? $"{city} Main Market" : address.Trim(),
            ShopType = string.IsNullOrWhiteSpace(shopType) ? "Wholesale Mandi Shop" : shopType.Trim(),
            ContactPerson = string.IsNullOrWhiteSpace(contactPerson) ? "Shop Owner" : contactPerson.Trim(),
            ContactPhone = contactPhone.Trim(),
            DailyCapacityKg = dailyCapacityKg > 0 ? dailyCapacityKg : 2000m,
            CurrentBroilerMeatRatePerKg = currentBroilerMeatRatePerKg > 0 ? currentBroilerMeatRatePerKg : 560m,
            CurrentLiveBroilerBuyRatePerKg = currentLiveBroilerBuyRatePerKg > 0 ? currentLiveBroilerBuyRatePerKg : 345m,
            GoogleRating = 4.7,
            GoogleReviewsCount = 12,
            IsGoogleVerified = true,
            Specialties = specialties?.Trim() ?? "Live Bird Cutting, Daily Crates",
            Notes = notes?.Trim()
        };

        _context.PoultryShops.Add(shop);
        await _context.SaveChangesAsync();

        StatusMessage = $"Poultry Shop '{name}' successfully registered in {city} directory!";
        return RedirectToPage();
    }
}
