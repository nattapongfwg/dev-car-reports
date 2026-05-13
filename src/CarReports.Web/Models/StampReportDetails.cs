namespace CarReports.Web.Models;

public sealed record StampReportDetails(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<StampDetail> Rows);
