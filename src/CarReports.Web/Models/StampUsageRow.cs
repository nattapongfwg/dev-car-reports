namespace CarReports.Web.Models;

public sealed record StampUsageRow(
    int RowNumber,
    string Remark,
    decimal Amount);
