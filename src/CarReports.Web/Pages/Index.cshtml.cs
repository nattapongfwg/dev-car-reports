using CarReports.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarReports.Web.Pages;

// Localhost-only Windows Service; antiforgery adds no protection here and complicates scripting.
[IgnoreAntiforgeryToken]
public sealed class IndexModel : PageModel
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IExcelReportService _stampReports;
    private readonly ISalaryReportService _salaryReports;
    private readonly IUploadCache _cache;
    private readonly ILogger<IndexModel> _logger;
    private readonly long _maxBytes;
    private readonly string[] _allowedExtensions;

    public IndexModel(
        IExcelReportService stampReports,
        ISalaryReportService salaryReports,
        IUploadCache cache,
        IConfiguration configuration,
        ILogger<IndexModel> logger)
    {
        _stampReports = stampReports;
        _salaryReports = salaryReports;
        _cache = cache;
        _logger = logger;
        _maxBytes = configuration.GetValue<long?>("Upload:MaxBytes") ?? 52_428_800L;
        _allowedExtensions = configuration.GetSection("Upload:AllowedExtensions").Get<string[]>()
            ?? new[] { ".xlsx", ".xls" };
    }

    [TempData] public string? ErrorMessage { get; set; }
    [TempData] public string? UploadToken { get; set; }
    [TempData] public string? UploadedFileName { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? upload, CancellationToken cancellationToken)
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

        await using var stream = upload.OpenReadStream();
        var token = await _cache.StoreAsync(stream, upload.FileName, cancellationToken);

        UploadToken = token;
        UploadedFileName = upload.FileName;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStampAsync(string? token, CancellationToken cancellationToken)
    {
        if (!TryResolveUpload(token, out var filePath, out var originalName))
        {
            return RedirectToPage();
        }

        try
        {
            await using var stream = System.IO.File.OpenRead(filePath);
            var bytes = await _stampReports.GenerateAsync(stream, originalName, cancellationToken);
            PreserveToken(token!, originalName);
            var downloadName = $"stamp-summary-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
            return File(bytes, XlsxContentType, downloadName);
        }
        catch (InvalidUploadException ex)
        {
            _logger.LogWarning(ex, "Rejected stamp generation: {Reason}", ex.Message);
            ErrorMessage = ex.Message;
            PreserveToken(token!, originalName);
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostSalaryAsync(string? token, CancellationToken cancellationToken)
    {
        if (!TryResolveUpload(token, out var filePath, out var originalName))
        {
            return RedirectToPage();
        }

        try
        {
            await using var stream = System.IO.File.OpenRead(filePath);
            var bytes = await _salaryReports.GenerateAsync(stream, originalName, cancellationToken);
            PreserveToken(token!, originalName);
            var downloadName = $"Salary_{DateTime.Now:ddMMyyyyHHmmss}.xlsx";
            return File(bytes, XlsxContentType, downloadName);
        }
        catch (InvalidUploadException ex)
        {
            _logger.LogWarning(ex, "Rejected salary generation: {Reason}", ex.Message);
            ErrorMessage = ex.Message;
            PreserveToken(token!, originalName);
            return RedirectToPage();
        }
    }

    public IActionResult OnPostDiscard(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _cache.Remove(token);
        }
        return RedirectToPage();
    }

    private bool TryResolveUpload(string? token, out string filePath, out string originalName)
    {
        filePath = string.Empty;
        originalName = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            ErrorMessage = "Upload session expired. Please re-upload the file.";
            return false;
        }
        if (!_cache.TryGet(token, out filePath, out originalName))
        {
            ErrorMessage = "Upload session expired. Please re-upload the file.";
            return false;
        }
        return true;
    }

    private void PreserveToken(string token, string fileName)
    {
        UploadToken = token;
        UploadedFileName = fileName;
    }
}
