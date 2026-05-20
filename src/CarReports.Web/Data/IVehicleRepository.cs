using CarReports.Web.Models;

namespace CarReports.Web.Data;

public interface IVehicleRepository
{
    Task<IReadOnlyList<CarOwner>> GetCarOwnersAsync(CancellationToken cancellationToken);

    Task<int> UpdatePlateAsync(
        string employeeCode,
        string vehicleType,
        string cardType,
        string oldPlate,
        string newPlate,
        CancellationToken cancellationToken);

    Task<int> SoftDeleteAsync(
        string employeeCode,
        string vehicleType,
        string cardType,
        string plate,
        CancellationToken cancellationToken);

    Task<int> InsertAsync(
        string employeeCode,
        string vehicleType,
        string cardType,
        string plate,
        CancellationToken cancellationToken);

    Task<bool> PlateExistsForEmployeeAsync(
        string employeeCode,
        string plate,
        CancellationToken cancellationToken);
}
