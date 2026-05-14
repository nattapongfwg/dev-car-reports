namespace CarReports.Web.Services;

public interface ISalaryReportService
{
    Task<byte[]> GenerateAsync(
        Stream vehicleStream, string vehicleFileName,
        Stream phoneStream, string phoneFileName,
        CancellationToken cancellationToken);
}
