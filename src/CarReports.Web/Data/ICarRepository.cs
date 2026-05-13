using CarReports.Web.Models;

namespace CarReports.Web.Data;

public interface ICarRepository
{
    Task<IReadOnlyList<CarDetail>> GetCarsByVinAsync(
        IEnumerable<string> vins,
        CancellationToken cancellationToken);
}
