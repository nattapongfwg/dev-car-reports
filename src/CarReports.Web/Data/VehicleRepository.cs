using CarReports.Web.Models;
using Dapper;

namespace CarReports.Web.Data;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public VehicleRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CarOwner>> GetCarOwnersAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                employee_code    AS EmployeeCode,
                full_name_th     AS FullNameTh,
                total_vehicles   AS TotalVehicles,
                [C-M 1] AS CM1, [C-M 2] AS CM2, [C-M 3] AS CM3, [C-M 4] AS CM4,
                [M-M 1] AS MM1, [M-M 2] AS MM2, [M-M 3] AS MM3,
                [C-H 1] AS CH1, [C-H 2] AS CH2, [C-H 3] AS CH3,
                [M-H 1] AS MH1, [M-H 2] AS MH2, [M-H 3] AS MH3
            FROM dbo.v_employee_vehicle_owner
            ORDER BY employee_code;
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<CarOwner>(command);
        return rows.ToList();
    }
}
