namespace CarReports.Web.Models;

public sealed record CarOwner(
    string EmployeeCode,
    string FullNameTh,
    int TotalVehicles,
    string? CM1, string? CM2, string? CM3, string? CM4,
    string? MM1, string? MM2, string? MM3,
    string? CH1, string? CH2, string? CH3,
    string? MH1, string? MH2, string? MH3);
