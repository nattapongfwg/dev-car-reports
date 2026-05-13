namespace CarReports.Web.Models;

public sealed record SalaryReportData(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<SalaryDeductionRow> Rows);
