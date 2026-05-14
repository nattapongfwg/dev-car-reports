using CarReports.Web.Data;
using CarReports.Web.Excel;
using CarReports.Web.Models;

namespace CarReports.Web.Services;

public sealed class SalaryReportService : ISalaryReportService
{
    private const string CarVehicleCode = "C";

    private readonly IStampDetailReader _stampReader;
    private readonly IPhoneBillReader _phoneReader;
    private readonly ISalaryRepository _repository;
    private readonly ISalaryWorkbookBuilder _builder;
    private readonly ILogger<SalaryReportService> _logger;

    public SalaryReportService(
        IStampDetailReader stampReader,
        IPhoneBillReader phoneReader,
        ISalaryRepository repository,
        ISalaryWorkbookBuilder builder,
        ILogger<SalaryReportService> logger)
    {
        _stampReader = stampReader;
        _phoneReader = phoneReader;
        _repository = repository;
        _builder = builder;
        _logger = logger;
    }

    public async Task<byte[]> GenerateAsync(
        Stream vehicleStream, string vehicleFileName,
        Stream phoneStream, string phoneFileName,
        CancellationToken cancellationToken)
    {
        var stamps = _stampReader.Read(vehicleStream, vehicleFileName);
        _logger.LogInformation(
            "Parsed {Count} stamp rows for period {Start:yyyy-MM-dd}..{End:yyyy-MM-dd}",
            stamps.Rows.Count, stamps.PeriodStart, stamps.PeriodEnd);

        var phoneBills = _phoneReader.Read(phoneStream, phoneFileName);
        _logger.LogInformation("Parsed {Count} phone-bill entries", phoneBills.Count);

        var mappings = await _repository.GetMappingsAsync(cancellationToken);
        _logger.LogInformation("Fetched {Count} employee-vehicle mappings", mappings.Count);

        var rows = BuildVehicleRows(stamps.Rows, mappings);
        rows = await MergePhoneBillsAsync(rows, phoneBills, cancellationToken);
        rows = SortRows(rows);
        _logger.LogInformation("Built {Count} salary deduction rows", rows.Count);

        var data = new SalaryReportData(stamps.PeriodStart, stamps.PeriodEnd, rows);
        return _builder.Build(data);
    }

    private static List<SalaryDeductionRow> BuildVehicleRows(
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
            if (string.IsNullOrEmpty(m.VehicleType)) continue;
            mappingLookup.TryAdd((m.FullNameTh, m.VehicleType), m);
        }

        var rows = new List<SalaryDeductionRow>();
        foreach (var ((prefix, name), total) in stampTotals)
        {
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

        return rows;
    }

    private async Task<List<SalaryDeductionRow>> MergePhoneBillsAsync(
        List<SalaryDeductionRow> rows,
        IReadOnlyList<PhoneBill> phoneBills,
        CancellationToken cancellationToken)
    {
        foreach (var bill in phoneBills)
        {
            if (bill.Excess == 0m && bill.Service == 0m) continue;

            var matches = rows
                .Select((row, index) => (row, index))
                .Where(t => string.Equals(t.row.EmployeeCode, bill.EmployeeCode, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                var mapping = await _repository.GetByEmployeeCodeAsync(bill.EmployeeCode, cancellationToken);
                if (mapping is null)
                {
                    _logger.LogWarning(
                        "Phone bill for ID {EmployeeCode} skipped: no salary row and no DB match",
                        bill.EmployeeCode);
                    continue;
                }

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
                    VehicleType: null,
                    HourlyTotal: 0m,
                    PhoneExcess: bill.Excess,
                    PhoneService: bill.Service));
                continue;
            }

            // If the employee has rows for different vehicle types, attribute phone
            // amounts to the Car row only — phone is per-employee, not per-vehicle.
            var target = matches.FirstOrDefault(t => t.row.VehicleType == CarVehicleCode);
            if (target == default) target = matches[0];

            rows[target.index] = target.row with
            {
                PhoneExcess = bill.Excess,
                PhoneService = bill.Service
            };
        }

        return rows;
    }

    private static List<SalaryDeductionRow> SortRows(List<SalaryDeductionRow> rows)
    {
        var thaiSort = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("th-TH"), ignoreCase: false);
        return rows
            .OrderBy(r => r.FullNameThName, thaiSort)
            .ThenBy(r => r.VehicleType ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }
}
