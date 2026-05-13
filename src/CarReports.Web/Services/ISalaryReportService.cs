namespace CarReports.Web.Services;

public interface ISalaryReportService
{
    Task<byte[]> GenerateAsync(Stream uploadStream, string fileName, CancellationToken cancellationToken);
}
