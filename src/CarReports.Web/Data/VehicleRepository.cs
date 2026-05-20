using CarReports.Web.Models;
using Dapper;

namespace CarReports.Web.Data;

public sealed class VehicleRepository : IVehicleRepository
{
    private const string AuditActor = "CarReports";

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

    public async Task<int> UpdatePlateAsync(
        string employeeCode,
        string vehicleType,
        string cardType,
        string oldPlate,
        string newPlate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ev
               SET ev.license_plate = @NewPlate,
                   ev.updated_by    = @Actor,
                   ev.updated_date  = SYSDATETIME()
              FROM dbo.employee_vehicles ev
              JOIN dbo.employees e ON e.id = ev.employee_id
             WHERE e.employee_code        = @EmployeeCode
               AND ev.vehicle_type        = @VehicleType
               AND ev.card_type           = @CardType
               AND LTRIM(RTRIM(ev.license_plate)) = @OldPlate
               AND ev.is_active           = 'Y';
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, new
        {
            EmployeeCode = employeeCode,
            VehicleType = vehicleType,
            CardType = cardType,
            OldPlate = oldPlate.Trim(),
            NewPlate = newPlate.Trim(),
            Actor = AuditActor,
        }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command);
    }

    public async Task<int> SoftDeleteAsync(
        string employeeCode,
        string vehicleType,
        string cardType,
        string plate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE ev
               SET ev.is_active    = 'N',
                   ev.updated_by   = @Actor,
                   ev.updated_date = SYSDATETIME()
              FROM dbo.employee_vehicles ev
              JOIN dbo.employees e ON e.id = ev.employee_id
             WHERE e.employee_code = @EmployeeCode
               AND ev.vehicle_type = @VehicleType
               AND ev.card_type    = @CardType
               AND LTRIM(RTRIM(ev.license_plate)) = @Plate
               AND ev.is_active    = 'Y';
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, new
        {
            EmployeeCode = employeeCode,
            VehicleType = vehicleType,
            CardType = cardType,
            Plate = plate.Trim(),
            Actor = AuditActor,
        }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command);
    }

    public async Task<int> InsertAsync(
        string employeeCode,
        string vehicleType,
        string cardType,
        string plate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.employee_vehicles
                (employee_id, vehicle_type, card_type, license_plate,
                 is_active, created_by, updated_by)
            SELECT TOP 1
                e.id, @VehicleType, @CardType, @Plate,
                'Y', @Actor, @Actor
              FROM dbo.employees e
             WHERE e.employee_code = @EmployeeCode;
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, new
        {
            EmployeeCode = employeeCode,
            VehicleType = vehicleType,
            CardType = cardType,
            Plate = plate.Trim(),
            Actor = AuditActor,
        }, cancellationToken: cancellationToken);
        return await connection.ExecuteAsync(command);
    }

    public async Task<bool> PlateExistsForEmployeeAsync(
        string employeeCode,
        string plate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 1 1
              FROM dbo.employee_vehicles ev
              JOIN dbo.employees e ON e.id = ev.employee_id
             WHERE e.employee_code = @EmployeeCode
               AND LTRIM(RTRIM(ev.license_plate)) = @Plate
               AND ev.is_active    = 'Y';
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, new
        {
            EmployeeCode = employeeCode,
            Plate = plate.Trim(),
        }, cancellationToken: cancellationToken);
        var result = await connection.ExecuteScalarAsync<int?>(command);
        return result.HasValue;
    }
}
