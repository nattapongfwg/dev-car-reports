using CarReports.Web.Data;
using CarReports.Web.Excel;
using CarReports.Web.Models;

namespace CarReports.Web.Services;

public sealed class SalaryReportService : ISalaryReportService
{
    private readonly IStampDetailReader _reader;
    private readonly ISalaryRepository _repository;
    private readonly ISalaryWorkbookBuilder _builder;
    private readonly ILogger<SalaryReportService> _logger;

    public SalaryReportService(
        IStampDetailReader reader,
        ISalaryRepository repository,
        ISalaryWorkbookBuilder builder,
        ILogger<SalaryReportService> logger)
    {
        _reader = reader;
        _repository = repository;
        _builder = builder;
        _logger = logger;
    }

    public async Task<byte[]> GenerateAsync(Stream uploadStream, string fileName, CancellationToken cancellationToken)
    {
        var stamps = _reader.Read(uploadStream, fileName);
        _logger.LogInformation(
            "Parsed {Count} stamp rows for period {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}",
            stamps.Rows.Count, stamps.PeriodStart, stamps.PeriodEnd);

        var mappings = await _repository.GetMappingsAsync(cancellationToken);
        _logger.LogInformation("Fetched {Count} employee-vehicle mappings", mappings.Count);

        var rows = BuildSalaryRows(stamps.Rows, mappings);
        _logger.LogInformation("Built {Count} salary deduction rows", rows.Count);

        var data = new SalaryReportData(stamps.PeriodStart, stamps.PeriodEnd, rows);
        return _builder.Build(data);
    }

    private static IReadOnlyList<SalaryDeductionRow> BuildSalaryRows(
        IReadOnlyList<StampDetail> stamps,
        IReadOnlyList<EmployeeVehicleMapping> mappings)
    {
        var stampTotals = stamps
            .Where(s => !string.IsNullOrWhiteSpace(s.RemarkName))
            .GroupBy(s => (s.VehicleTypePrefix, s.RemarkName))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var mappingLookup = new Dictionary<(string Name, string Vehicle), EmployeeVehicleMapping>();
        foreach (var m in mappings)
        {
            var key = (m.FullNameTh, m.VehicleType);
            mappingLookup.TryAdd(key, m);
        }

        var rows = new List<SalaryDeductionRow>();
        foreach (var ((prefix, name), total) in stampTotals)
        {
            if (total <= 0m) continue;

            var key = (name, prefix.ToString());
            if (!mappingLookup.TryGetValue(key, out var mapping)) continue;

            rows.Add(new SalaryDeductionRow(
                EmployeeCode: mapping.EmployeeCode,
                FullNameThName: mapping.FullNameThName,
                SalaryCode: mapping.SalaryCode,
                CompanyName: mapping.CompanyName,
                DepartmentName: mapping.DepartmentName,
                SectionName: mapping.SectionName,
                CostCenter: mapping.CostCenter,
                Email: mapping.Email,
                PayrollCode: mapping.PayrollCode,
                VehicleType: mapping.VehicleType,
                HourlyTotal: total));
        }

        return rows
            .OrderBy(r => r.CompanyName, StringComparer.Ordinal)
            .ThenBy(r => r.DepartmentName, StringComparer.Ordinal)
            .ThenBy(r => r.SectionName, StringComparer.Ordinal)
            .ThenBy(r => r.EmployeeCode, StringComparer.Ordinal)
            .ToList();
    }
}
