using CarReports.Web.Models;

namespace CarReports.Web.Data;

public interface ISalaryRepository
{
    Task<IReadOnlyList<EmployeeVehicleMapping>> GetMappingsAsync(CancellationToken cancellationToken);

    Task<EmployeeVehicleMapping?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken);

    // Returns monthly parking fees keyed by vehicle_type code (e.g. "C", "M")
    // from m_cfg_lov.lov_val1. Codes whose lov_val1 doesn't parse as a number are omitted.
    Task<IReadOnlyDictionary<string, decimal>> GetVehicleMonthlyFeesAsync(CancellationToken cancellationToken);
}
