namespace CarReports.Web.Services;

public interface IExcelReportService
{
    Task<byte[]> GenerateAsync(Stream uploadStream, string fileName, CancellationToken cancellationToken);
}
