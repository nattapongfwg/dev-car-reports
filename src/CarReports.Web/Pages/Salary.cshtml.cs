using CarReports.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarReports.Web.Pages;

[IgnoreAntiforgeryToken]
public sealed class SalaryModel : PageModel
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ISalaryReportService _salaryReports;
    private readonly ILogger<SalaryModel> _logger;
    private readonly long _maxBytes;

    public SalaryModel(
        ISalaryReportService salaryReports,
        IConfiguration configuration,
        ILogger<SalaryModel> logger)
    {
        _salaryReports = salaryReports;
        _logger = logger;
        _maxBytes = configuration.GetValue<long?>("Upload:MaxBytes") ?? 52_428_800L;
    }

    [TempData] public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostExportAsync(
        IFormFile? vehicleReport,
        IFormFile? phoneReport,
        CancellationToken cancellationToken)
    {
        if (vehicleReport is null || vehicleReport.Length == 0 ||
            phoneReport is null || phoneReport.Length == 0)
        {
            ErrorMessage = "Both files are required.";
            return RedirectToPage();
        }

        if (vehicleReport.Length > _maxBytes || phoneReport.Length > _maxBytes)
        {
            ErrorMessage = $"File too large. Maximum allowed size is {_maxBytes / 1_048_576} MB.";
            return RedirectToPage();
        }

        try
        {
            await using var vehicleStream = vehicleReport.OpenReadStream();
            await using var phoneStream = phoneReport.OpenReadStream();
            var bytes = await _salaryReports.GenerateAsync(
                vehicleStream, vehicleReport.FileName,
                phoneStream, phoneReport.FileName,
                cancellationToken);

            var downloadName = $"Salary_{DateTime.Now:ddMMyyyyHHmmss}.xlsx";
            return File(bytes, XlsxContentType, downloadName);
        }
        catch (InvalidUploadException ex)
        {
            _logger.LogWarning(ex, "Rejected salary generation: {Reason}", ex.Message);
            ErrorMessage = ex.Message;
            return RedirectToPage();
        }
    }
}
