using CarReports.Web.Excel;
using CarReports.Web.Models;

namespace CarReports.Web.Services;

public sealed class ExcelReportService : IExcelReportService
{
    private readonly IUploadedExcelReader _reader;
    private readonly IReportWorkbookBuilder _builder;
    private readonly ILogger<ExcelReportService> _logger;

    public ExcelReportService(
        IUploadedExcelReader reader,
        IReportWorkbookBuilder builder,
        ILogger<ExcelReportService> logger)
    {
        _reader = reader;
        _builder = builder;
        _logger = logger;
    }

    public Task<byte[]> GenerateAsync(Stream uploadStream, string fileName, CancellationToken cancellationToken)
    {
        var rows = _reader.Read(uploadStream, fileName);
        _logger.LogInformation("Parsed {Count} data rows from {FileName}", rows.Count, fileName);

        var data = new ReportData(rows, DateTime.UtcNow);
        var bytes = _builder.Build(data);
        return Task.FromResult(bytes);
    }
}
