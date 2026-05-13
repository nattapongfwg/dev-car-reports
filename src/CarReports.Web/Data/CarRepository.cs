using CarReports.Web.Models;
using Dapper;

namespace CarReports.Web.Data;

public sealed class CarRepository : ICarRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CarRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CarDetail>> GetCarsByVinAsync(
        IEnumerable<string> vins,
        CancellationToken cancellationToken)
    {
        var vinList = vins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (vinList.Length == 0)
        {
            return Array.Empty<CarDetail>();
        }

        // Adjust column names / table name to match the real SQL Server schema.
        const string sql = """
            SELECT
                Vin                  AS Vin,
                Make                 AS Make,
                Model                AS Model,
                ModelYear            AS Year,
                OwnerName            AS OwnerName,
                LastServiceCostThb   AS LastServiceCostThb,
                LastServiceDate      AS LastServiceDate
            FROM dbo.Cars
            WHERE Vin IN @Vins;
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Vins = vinList },
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<CarDetail>(command);
        return rows.ToList();
    }
}
