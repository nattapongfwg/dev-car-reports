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

    public IReadOnlyList<CarOwner> Owners { get; private set; } = Array.Empty<CarOwner>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Owners = await _repository.GetCarOwnersAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        [FromBody] UpdatePlateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { error = "Missing request body." });
        if (!TryNormalize(request.EmployeeCode, request.VehicleType, request.CardType, out var employeeCode, out var vehicleType, out var cardType, out var keyError))
            return BadRequest(new { error = keyError });
        if (!TryNormalizePlate(request.OldPlate, out var oldPlate, out var oldError))
            return BadRequest(new { error = "Original plate is invalid: " + oldError });
        if (!TryNormalizePlate(request.NewPlate, out var newPlate, out var newError))
            return BadRequest(new { error = newError });
        if (string.Equals(oldPlate, newPlate, StringComparison.Ordinal))
            return new JsonResult(new { ok = true, plate = newPlate, unchanged = true });

        if (await _repository.PlateExistsForEmployeeAsync(employeeCode, newPlate, cancellationToken))
            return BadRequest(new { error = "Employee already has a vehicle with this plate." });

        var affected = await _repository.UpdatePlateAsync(employeeCode, vehicleType, cardType, oldPlate, newPlate, cancellationToken);
        if (affected == 0)
            return NotFound(new { error = "No matching vehicle row found (already changed?)." });

        _logger.LogInformation("Updated plate {Old} -> {New} for {Employee} ({Bucket})",
            oldPlate, newPlate, employeeCode, vehicleType + "-" + cardType);
        return new JsonResult(new { ok = true, plate = newPlate });
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        [FromBody] DeletePlateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { error = "Missing request body." });
        if (!TryNormalize(request.EmployeeCode, request.VehicleType, request.CardType, out var employeeCode, out var vehicleType, out var cardType, out var keyError))
            return BadRequest(new { error = keyError });
        if (!TryNormalizePlate(request.Plate, out var plate, out var plateError))
            return BadRequest(new { error = plateError });

        var affected = await _repository.SoftDeleteAsync(employeeCode, vehicleType, cardType, plate, cancellationToken);
        if (affected == 0)
            return NotFound(new { error = "No matching vehicle row found." });

        _logger.LogInformation("Soft-deleted plate {Plate} for {Employee} ({Bucket})",
            plate, employeeCode, vehicleType + "-" + cardType);
        return new JsonResult(new { ok = true });
    }

    public async Task<IActionResult> OnPostInsertAsync(
        [FromBody] InsertPlateRequest request,
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

        _logger.LogInformation("Inserted plate {Plate} for {Employee} ({Bucket})",
            plate, employeeCode, vehicleType + "-" + cardType);
        return new JsonResult(new { ok = true, plate });
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

    public sealed record UpdatePlateRequest(string? EmployeeCode, string? VehicleType, string? CardType, string? OldPlate, string? NewPlate);
    public sealed record DeletePlateRequest(string? EmployeeCode, string? VehicleType, string? CardType, string? Plate);
    public sealed record InsertPlateRequest(string? EmployeeCode, string? VehicleType, string? CardType, string? Plate);
}
