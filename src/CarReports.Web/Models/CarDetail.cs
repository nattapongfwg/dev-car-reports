namespace CarReports.Web.Models;

public sealed record CarDetail(
    string Vin,
    string Make,
    string Model,
    int Year,
    string OwnerName,
    decimal LastServiceCostThb,
    DateTime LastServiceDate);
