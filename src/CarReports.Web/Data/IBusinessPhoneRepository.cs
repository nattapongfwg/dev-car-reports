namespace CarReports.Web.Data;

public enum EnsurePhoneResult
{
    EmployeeNotFound,
    AlreadyActive,
    Inserted,
}

public interface IBusinessPhoneRepository
{
    Task<EnsurePhoneResult> EnsurePhoneAsync(string employeeCode, string phoneNo, CancellationToken cancellationToken);
}
