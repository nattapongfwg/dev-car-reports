using Dapper;

namespace CarReports.Web.Data;

public sealed class BusinessPhoneRepository : IBusinessPhoneRepository
{
    private const string AuditActor = "sys_emp_salary";

    private readonly ISqlConnectionFactory _connectionFactory;

    public BusinessPhoneRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<EnsurePhoneResult> EnsurePhoneAsync(string employeeCode, string phoneNo, CancellationToken cancellationToken)
    {
        // One batch, three outcomes:
        //   0 = employee_code not in dbo.employees
        //   1 = phone already active for this employee
        //   2 = new row inserted
        const string sql = """
            DECLARE @empId nvarchar(50) =
                (SELECT TOP 1 e.id FROM dbo.employees e WHERE e.employee_code = @EmployeeCode);

            IF @empId IS NULL
                SELECT CAST(0 AS int);
            ELSE IF EXISTS (
                SELECT 1
                  FROM dbo.employee_business_phone p
                 WHERE p.employee_id = @empId
                   AND LTRIM(RTRIM(p.phone_no)) = @PhoneNo
                   AND p.is_active = 'Y'
            )
                SELECT CAST(1 AS int);
            ELSE
            BEGIN
                INSERT INTO dbo.employee_business_phone
                    (employee_id, phone_no, is_active, created_by, updated_by)
                VALUES (@empId, @PhoneNo, 'Y', @Actor, @Actor);
                SELECT CAST(2 AS int);
            END
            """;

        using var connection = _connectionFactory.Create();
        var command = new CommandDefinition(sql, new
        {
            EmployeeCode = employeeCode,
            PhoneNo = phoneNo.Trim(),
            Actor = AuditActor,
        }, cancellationToken: cancellationToken);
        var code = await connection.ExecuteScalarAsync<int>(command);
        return (EnsurePhoneResult)code;
    }
}
