namespace CarReports.Web.Models;

public sealed record SalaryDeductionRow(
    string EmployeeCode,
    string FullNameThName,
    string SalaryCode,
    string CompanyName,
    string DepartmentName,
    string SectionName,
    string? CostCenter,
    string? Email,
    string? PayrollCode,
    string? VehicleType,
    decimal HourlyTotal,
    string? PhoneNo = null,
    decimal PhoneExcess = 0m,
    decimal PhoneService = 0m,
    decimal MonthlyAmount = 0m);
