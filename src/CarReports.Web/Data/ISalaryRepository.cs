using CarReports.Web.Models;

namespace CarReports.Web.Data;

public interface ISalaryRepository
{
    Task<IReadOnlyList<EmployeeVehicleMapping>> GetMappingsAsync(CancellationToken cancellationToken);

    Task<EmployeeVehicleMapping?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken);
}
