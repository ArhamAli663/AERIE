namespace AERIE.Web.Pages.Farmer;

using AERIE.Application.Farmers.DTOs;
using AERIE.Application.Farmers.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class FarmsModel : PageModel
{
    private readonly IFarmerService _farmerService;

    public FarmsModel(IFarmerService farmerService)
    {
        _farmerService = farmerService;
    }

    public List<FarmDto> Farms { get; set; } = [];
    public List<PoultryHouseDto> Houses { get; set; } = [];
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var farmsRes = await _farmerService.GetFarmsAsync();
        if (farmsRes.IsSuccess)
        {
            Farms = farmsRes.Data?.ToList() ?? [];
        }

        var housesRes = await _farmerService.GetPoultryHousesAsync();
        if (housesRes.IsSuccess)
        {
            Houses = housesRes.Data?.ToList() ?? [];
        }
    }

    public async Task<IActionResult> OnPostCreateFarmAsync(
        string name, string location, string? city, string? province, int totalCapacity, string? contactPhone, string? notes)
    {
        var req = new CreateFarmRequest(name, location, city, province, totalCapacity, contactPhone, notes);
        var result = await _farmerService.CreateFarmAsync(req);
        if (result.IsSuccess)
        {
            StatusMessage = $"Farm '{name}' registered successfully.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateFarmAsync(
        Guid id, string name, string location, string? city, string? province, int totalCapacity, string? contactPhone, string? notes, bool isActive)
    {
        var req = new UpdateFarmRequest(name, location, city, province, totalCapacity, contactPhone, notes, isActive);
        var result = await _farmerService.UpdateFarmAsync(id, req);
        if (result.IsSuccess)
        {
            StatusMessage = $"Farm '{name}' updated successfully.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateFarmAsync(Guid farmId)
    {
        var result = await _farmerService.DeactivateFarmAsync(farmId);
        if (result.IsSuccess)
        {
            StatusMessage = "Farm safely deactivated. Historical records and batches preserved.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateHouseAsync(Guid farmId, string name, int capacity, string? description)
    {
        var req = new CreatePoultryHouseRequest(farmId, name, capacity, description);
        var result = await _farmerService.CreatePoultryHouseAsync(req);
        if (result.IsSuccess)
        {
            StatusMessage = $"Shed '{name}' registered successfully.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateHouseAsync(Guid houseId, string name, int capacity, string? description, bool isActive)
    {
        var req = new UpdatePoultryHouseRequest(name, capacity, description, isActive);
        var result = await _farmerService.UpdatePoultryHouseAsync(houseId, req);
        if (result.IsSuccess)
        {
            StatusMessage = $"Shed '{name}' updated successfully.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateHouseAsync(Guid houseId)
    {
        var result = await _farmerService.DeactivatePoultryHouseAsync(houseId);
        if (result.IsSuccess)
        {
            StatusMessage = "Shed safely deactivated.";
        }
        else
        {
            ErrorMessage = result.Error;
        }

        return RedirectToPage();
    }
}
