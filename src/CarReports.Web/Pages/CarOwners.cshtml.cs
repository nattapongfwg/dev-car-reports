using CarReports.Web.Data;
using CarReports.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarReports.Web.Pages;

[IgnoreAntiforgeryToken]
public sealed class CarOwnersModel : PageModel
{
    private static readonly HashSet<string> AllowedVehicleTypes = new(StringComparer.Ordinal) { "C", "M" };
    private static readonly HashSet<string> AllowedCardTypes = new(StringComparer.Ordinal) { "M", "H" };

    private readonly IVehicleRepository _repository;
    private readonly ILogger<CarOwnersModel> _logger;

    public CarOwnersModel(IVehicleRepository repository, ILogger<CarOwnersModel> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public IReadOnlyList<VehicleRow> Vehicles { get; private set; } = Array.Empty<VehicleRow>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Vehicles = await _repository.GetVehiclesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        [FromBody] UpdatePlateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { error = "Missing or invalid id." });
        if (!TryNormalizePlate(request.NewPlate, out var newPlate, out var plateError))
            return BadRequest(new { error = plateError });

        var affected = await _repository.UpdatePlateByIdAsync(request.Id, newPlate, cancellationToken);
        if (affected == 0)
            return NotFound(new { error = "Vehicle not found (already deleted?)." });

        _logger.LogInformation("Updated plate {Id} -> {Plate}", request.Id, newPlate);
        return new JsonResult(new { ok = true, id = request.Id, plate = newPlate });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        [FromBody] DeleteRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.Id == Guid.Empty)
            return BadRequest(new { error = "Missing or invalid id." });

        var affected = await _repository.SoftDeleteByIdAsync(request.Id, cancellationToken);
        if (affected == 0)
            return NotFound(new { error = "Vehicle not found." });

        _logger.LogInformation("Soft-deleted vehicle {Id}", request.Id);
        return new JsonResult(new { ok = true, id = request.Id });
    }

    public async Task<IActionResult> OnPostInsertAsync(
        [FromBody] InsertRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { error = "Missing request body." });
        if (!TryNormalize(request.EmployeeCode, request.VehicleType, request.CardType, out var employeeCode, out var vehicleType, out var cardType, out var keyError))
            return BadRequest(new { error = keyError });
        if (!TryNormalizePlate(request.Plate, out var plate, out var plateError))
            return BadRequest(new { error = plateError });

        if (await _repository.PlateExistsForEmployeeAsync(employeeCode, plate, cancellationToken))
            return BadRequest(new { error = "Employee already has a vehicle with this plate." });

        var affected = await _repository.InsertAsync(employeeCode, vehicleType, cardType, plate, cancellationToken);
        if (affected == 0)
            return NotFound(new { error = "Employee not found." });

        _logger.LogInformation("Inserted plate {Plate} for {Employee} ({Bucket})", plate, employeeCode, vehicleType + "-" + cardType);
        return new JsonResult(new { ok = true });
    }

    private static bool TryNormalize(
        string? employeeCodeIn, string? vehicleTypeIn, string? cardTypeIn,
        out string employeeCode, out string vehicleType, out string cardType,
        out string error)
    {
        employeeCode = (employeeCodeIn ?? string.Empty).Trim();
        vehicleType = (vehicleTypeIn ?? string.Empty).Trim().ToUpperInvariant();
        cardType = (cardTypeIn ?? string.Empty).Trim().ToUpperInvariant();
        error = string.Empty;

        if (employeeCode.Length == 0) { error = "Employee code is required."; return false; }
        if (!AllowedVehicleTypes.Contains(vehicleType)) { error = "Vehicle type must be 'C' or 'M'."; return false; }
        if (!AllowedCardTypes.Contains(cardType)) { error = "Card type must be 'M' or 'H'."; return false; }
        return true;
    }

    private static bool TryNormalizePlate(string? input, out string plate, out string error)
    {
        plate = (input ?? string.Empty).Trim();
        error = string.Empty;
        if (plate.Length == 0) { error = "Plate is required."; return false; }
        if (plate.Length > 20) { error = "Plate must be 20 characters or fewer."; return false; }
        return true;
    }

    public sealed record UpdatePlateRequest(Guid Id, string? NewPlate);
    public sealed record DeleteRequest(Guid Id);
    public sealed record InsertRequest(string? EmployeeCode, string? VehicleType, string? CardType, string? Plate);
}
