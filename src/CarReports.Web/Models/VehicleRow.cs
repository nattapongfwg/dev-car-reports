namespace CarReports.Web.Models;

public sealed record VehicleRow(
    Guid Id,
    string EmployeeCode,
    string FullNameTh,
    string VehicleType,
    string? CardType,
    string LicensePlate,
    string? LicenseProvince,
    string? Brand,
    string? Model,
    string? Color);
