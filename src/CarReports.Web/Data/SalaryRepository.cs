using System.Globalization;
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
            payroll_code       AS PayrollCode,
            card_type          AS CardType
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

    public async Task<IReadOnlyDictionary<string, decimal>> GetVehicleMonthlyFeesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT lov_code AS Code, lov_val1 AS Value
              FROM dbo.m_cfg_lov
             WHERE lov_type = 'VEHICLE_TYPE';
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LovRow>(command);

        var fees = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Code) || row.Value is null) continue;
            if (decimal.TryParse(row.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var fee))
            {
                fees[row.Code] = fee;
            }
        }
        return fees;
    }

    private sealed record LovRow(string Code, string? Value);
}
