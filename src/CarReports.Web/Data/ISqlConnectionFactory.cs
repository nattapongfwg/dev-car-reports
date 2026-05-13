using System.Data;

namespace CarReports.Web.Data;

public interface ISqlConnectionFactory
{
    IDbConnection Create();
}
