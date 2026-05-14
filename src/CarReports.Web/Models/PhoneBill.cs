namespace CarReports.Web.Models;

public sealed record PhoneBill(
    string EmployeeCode,
    decimal Excess,
    decimal Service);
