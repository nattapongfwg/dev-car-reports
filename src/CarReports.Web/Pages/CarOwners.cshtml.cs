using CarReports.Web.Data;
using CarReports.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarReports.Web.Pages;

public sealed class CarOwnersModel : PageModel
{
    private readonly IVehicleRepository _repository;

    public CarOwnersModel(IVehicleRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<CarOwner> Owners { get; private set; } = Array.Empty<CarOwner>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Owners = await _repository.GetCarOwnersAsync(cancellationToken);
    }
}
