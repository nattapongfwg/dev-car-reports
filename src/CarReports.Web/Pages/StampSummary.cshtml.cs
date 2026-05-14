using CarReports.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarReports.Web.Pages;

[IgnoreAntiforgeryToken]
public sealed class StampSummaryModel : PageModel
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IExcelReportService _stampReports;
    private readonly ILogger<StampSummaryModel> _logger;
    private readonly long _maxBytes;
    private readonly string[] _allowedExtensions;

    public StampSummaryModel(
        IExcelReportService stampReports,
        IConfiguration configuration,
        ILogger<StampSummaryModel> logger)
    {
        _stampReports = stampReports;
        _logger = logger;
        _maxBytes = configuration.GetValue<long?>("Upload:MaxBytes") ?? 52_428_800L;
        _allowedExtensions = configuration.GetSection("Upload:AllowedExtensions").Get<string[]>()
            ?? new[] { ".xlsx", ".xls" };
    }

    [TempData] public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(IFormFile? upload, CancellationToken cancellationToken)
    {
        if (upload is null || upload.Length == 0)
        {
            ErrorMessage = "Please choose a file to upload.";
            return RedirectToPage();
        }
        if (upload.Length > _maxBytes)
        {
            ErrorMessage = $"File too large. Maximum allowed size is {_maxBytes / 1_048_576} MB.";
            return RedirectToPage();
        }
        var extension = Path.GetExtension(upload.FileName);
        if (!_allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            ErrorMessage = $"Only {string.Join(", ", _allowedExtensions)} files are allowed.";
            return RedirectToPage();
        }

        try
        {
            await using var stream = upload.OpenReadStream();
            var bytes = await _stampReports.GenerateAsync(stream, upload.FileName, cancellationToken);
            var downloadName = $"stamp-summary-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            return File(bytes, XlsxContentType, downloadName);
        }
        catch (InvalidUploadException ex)
        {
            _logger.LogWarning(ex, "Rejected stamp generation: {Reason}", ex.Message);
            ErrorMessage = ex.Message;
            return RedirectToPage();
        }
    }
}
