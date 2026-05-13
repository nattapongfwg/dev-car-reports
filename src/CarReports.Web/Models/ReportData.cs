namespace CarReports.Web.Models;

public sealed record ReportData(
    IReadOnlyList<StampUsageRow> Rows,
    DateTime GeneratedAtUtc);
