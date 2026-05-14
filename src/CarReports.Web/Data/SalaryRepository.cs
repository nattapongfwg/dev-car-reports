using CarReports.Web.Models;
using Dapper;

namespace CarReports.Web.Data;

public sealed class SalaryRepository : ISalaryRepository
{
    private const string SelectColumns = """
        SELECT
            employee_id        AS EmployeeId,
            employee_code      AS EmployeeCode,
            full_name_th       AS FullNameTh,
            full_name_th_name  AS FullNameThName,
            salary_code        AS SalaryCode,
            company_name       AS CompanyName,
            department_name    AS DepartmentName,
            section_name       AS SectionName,
            cost_center        AS CostCenter,
            email              AS Email,
            business_phone     AS BusinessPhone,
            vehicle_type       AS VehicleType,
            payroll_code       AS PayrollCode
        FROM dbo.v_employee_vehicle_mapping
        """;

    private readonly ISqlConnectionFactory _connectionFactory;

    public SalaryRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<EmployeeVehicleMapping>> GetMappingsAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(SelectColumns + ";", cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<EmployeeVehicleMapping>(command);
        return rows.ToList();
    }

    public async Task<EmployeeVehicleMapping?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken)
    {
        const string filter = " WHERE employee_code = @employeeCode";
        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(SelectColumns + filter, new { employeeCode }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<EmployeeVehicleMapping>(command);
        return rows.FirstOrDefault();
    }
}
