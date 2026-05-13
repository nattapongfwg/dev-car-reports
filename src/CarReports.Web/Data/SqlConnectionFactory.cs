using System.Data;
using Microsoft.Data.SqlClient;

namespace CarReports.Web.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CarReports")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:CarReports is not configured. " +
                "Set it in appsettings.Production.json on the client computer.");
    }

    public IDbConnection Create() => new SqlConnection(_connectionString);
}
