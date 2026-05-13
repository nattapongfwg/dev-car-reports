namespace CarReports.Web.Models;

public sealed record StampDetail(
    char VehicleTypePrefix,
    string RemarkName,
    decimal Amount);
