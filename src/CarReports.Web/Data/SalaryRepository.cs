using CarReports.Web.Models;
using Dapper;

namespace CarReports.Web.Data;

public sealed class SalaryRepository : ISalaryRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SalaryRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<EmployeeVehicleMapping>> GetMappingsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT
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
                vehicle_type       AS VehicleType,
                payroll_code       AS PayrollCode
            FROM dbo.v_employee_vehicle_mapping;
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<EmployeeVehicleMapping>(command);
        return rows.ToList();
    }
}
