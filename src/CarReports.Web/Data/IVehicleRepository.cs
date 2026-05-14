using CarReports.Web.Models;

namespace CarReports.Web.Data;

public interface IVehicleRepository
{
    Task<IReadOnlyList<CarOwner>> GetCarOwnersAsync(CancellationToken cancellationToken);
}
