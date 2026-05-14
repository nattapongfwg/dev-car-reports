namespace CarReports.Web.Models;

public sealed record EmployeeVehicleMapping(
    Guid EmployeeId,
    string EmployeeCode,
    string FullNameTh,
    string FullNameThName,
    string SalaryCode,
    string CompanyName,
    string DepartmentName,
    string SectionName,
    string? CostCenter,
    string? Email,
    string? BusinessPhone,
    string? VehicleType,
    string? PayrollCode);
