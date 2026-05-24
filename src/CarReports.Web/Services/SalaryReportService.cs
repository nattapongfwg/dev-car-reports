using CarReports.Web.Data;
using CarReports.Web.Excel;
using CarReports.Web.Models;

namespace CarReports.Web.Services;

public sealed class SalaryReportService : ISalaryReportService
{
    private readonly IStampDetailReader _stampReader;
    private readonly IPhoneBillReader _phoneReader;
    private readonly ISalaryRepository _repository;
    private readonly IBusinessPhoneRepository _businessPhoneRepository;
    private readonly ISalaryWorkbookBuilder _builder;
    private readonly ILogger<SalaryReportService> _logger;

    public SalaryReportService(
        IStampDetailReader stampReader,
        IPhoneBillReader phoneReader,
        ISalaryRepository repository,
        IBusinessPhoneRepository businessPhoneRepository,
        ISalaryWorkbookBuilder builder,
        ILogger<SalaryReportService> logger)
    {
        _stampReader = stampReader;
        _phoneReader = phoneReader;
        _repository = repository;
        _businessPhoneRepository = businessPhoneRepository;
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

        var monthlyFees = await _repository.GetVehicleMonthlyFeesAsync(cancellationToken);
        foreach (var code in new[] { "C", "M" })
        {
            if (!monthlyFees.ContainsKey(code))
            {
                _logger.LogWarning(
                    "m_cfg_lov has no VEHICLE_TYPE row for code {Code}; monthly fee treated as 0",
                    code);
            }
        }

        var vehicleRows = BuildVehicleRows(stamps.Rows, mappings, monthlyFees);
        var rows = await MergePhoneBillsAsync(vehicleRows, phoneBills, cancellationToken);
        rows = SortRows(rows);
        _logger.LogInformation("Built {Count} salary deduction rows", rows.Count);

        var data = new SalaryReportData(stamps.PeriodStart, stamps.PeriodEnd, rows);
        return _builder.Build(data);
    }

    // Emit one row per (employee, vehicle_type) that has any deductible amount.
    //   * HourlyTotal: sum of Report.xlsx stamps matching (vehicle_type, name).
    //                  Stamps are always card_type='H' so this is the hourly side.
    //   * MonthlyAmount: m_cfg_lov fee for the vehicle_type, applied only when
    //                    the employee owns at least one active card_type='M' vehicle
    //                    of that type (per dbo.v_employee_vehicle_mapping).
    //   * Row is skipped when both amounts are 0 — nothing to deduct.
    // Phone columns stay empty here; they are filled in MergePhoneBillsAsync.
    private static List<SalaryDeductionRow> BuildVehicleRows(
        IReadOnlyList<StampDetail> stamps,
        IReadOnlyList<EmployeeVehicleMapping> mappings,
        IReadOnlyDictionary<string, decimal> monthlyFees)
    {
        var stampTotals = stamps
            .Where(s => !string.IsNullOrWhiteSpace(s.RemarkName))
            .GroupBy(s => (s.VehicleTypePrefix, s.RemarkName))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // Group mapping rows by (employee, vehicle_type). The view emits one row
        // per (employee, vehicle_type, card_type), so a single employee with both
        // H and M cards of the same type yields two mapping rows that consolidate here.
        var grouped = mappings
            .Where(m => !string.IsNullOrEmpty(m.VehicleType))
            .GroupBy(m => (m.FullNameTh, VehicleType: m.VehicleType!));

        var rows = new List<SalaryDeductionRow>();
        foreach (var group in grouped)
        {
            var sample = group.First();
            var hasMonthlyCard = group.Any(m =>
                string.Equals(m.CardType, "M", StringComparison.Ordinal));

            var hourlyTotal = stampTotals.TryGetValue(
                (group.Key.VehicleType[0], group.Key.FullNameTh), out var t) ? t : 0m;

            var monthlyAmount = hasMonthlyCard && monthlyFees.TryGetValue(group.Key.VehicleType, out var fee)
                ? fee
                : 0m;

            if (hourlyTotal == 0m && monthlyAmount == 0m) continue;

            rows.Add(new SalaryDeductionRow(
                EmployeeCode: sample.EmployeeCode,
                FullNameThName: sample.FullNameThName,
                SalaryCode: sample.SalaryCode,
                CompanyName: sample.CompanyName,
                DepartmentName: sample.DepartmentName,
                SectionName: sample.SectionName,
                CostCenter: sample.CostCenter,
                Email: sample.Email,
                PayrollCode: sample.PayrollCode,
                VehicleType: group.Key.VehicleType,
                HourlyTotal: hourlyTotal,
                MonthlyAmount: monthlyAmount));
        }

        return rows;
    }

    // Emit one stand-alone phone row per (employee, phone) bill. Phone
    // rows never share their row with a vehicle: an employee with 1 car
    // and 2 phones produces 3 output rows — the car row plus two phone
    // rows — each with the vehicle/phone columns of the OTHER side blank.
    //
    // Rules:
    //   * Bills where excess AND service are both zero are skipped.
    //   * Employee info (name, salary code, etc.) is copied from an
    //     existing vehicle row if the employee has one; otherwise looked
    //     up via the repository.
    //   * Phone rows carry VehicleType = null and HourlyTotal = 0 so the
    //     parking columns stay blank.
    private async Task<List<SalaryDeductionRow>> MergePhoneBillsAsync(
        List<SalaryDeductionRow> rows,
        IReadOnlyList<PhoneBill> phoneBills,
        CancellationToken cancellationToken)
    {
        var employeeInfoCache = new Dictionary<string, EmployeeVehicleMapping?>(StringComparer.Ordinal);

        foreach (var bill in phoneBills)
        {
            if (bill.Excess == 0m && bill.Service == 0m) continue;

            var info = await ResolveEmployeeInfoAsync(rows, employeeInfoCache, bill.EmployeeCode, cancellationToken);
            if (info is null)
            {
                _logger.LogWarning(
                    "Phone bill for ID {EmployeeCode} skipped: no salary row and no DB match",
                    bill.EmployeeCode);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(bill.PhoneNo))
            {
                var result = await _businessPhoneRepository.EnsurePhoneAsync(
                    bill.EmployeeCode, bill.PhoneNo, cancellationToken);
                switch (result)
                {
                    case EnsurePhoneResult.Inserted:
                        _logger.LogInformation(
                            "Registered new business phone {PhoneNo} for ID {EmployeeCode}",
                            bill.PhoneNo, bill.EmployeeCode);
                        break;
                    case EnsurePhoneResult.EmployeeNotFound:
                        _logger.LogWarning(
                            "Phone {PhoneNo} not registered: ID {EmployeeCode} not in dbo.employees",
                            bill.PhoneNo, bill.EmployeeCode);
                        break;
                }
            }

            rows.Add(NewPhoneRow(info, bill));
        }

        return rows;
    }

    // Find the employee's display info from an existing vehicle row in the
    // working list (zero DB cost), or fall back to a repository lookup.
    // Cached so repeated phone bills for the same employee hit the DB once.
    private async Task<EmployeeVehicleMapping?> ResolveEmployeeInfoAsync(
        List<SalaryDeductionRow> rows,
        Dictionary<string, EmployeeVehicleMapping?> cache,
        string employeeCode,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(employeeCode, out var cached)) return cached;

        var vehicleRow = rows.FirstOrDefault(r =>
            string.Equals(r.EmployeeCode, employeeCode, StringComparison.Ordinal));

        if (vehicleRow is not null)
        {
            // Re-wrap the vehicle row's identity fields into an EmployeeVehicleMapping.
            // EmployeeId / FullNameTh / BusinessPhone / VehicleType aren't needed by NewPhoneRow,
            // so we plug in defaults — only the display fields matter for the output row.
            var fromRow = new EmployeeVehicleMapping(
                EmployeeId: Guid.Empty,
                EmployeeCode: vehicleRow.EmployeeCode,
                FullNameTh: string.Empty,
                FullNameThName: vehicleRow.FullNameThName,
                SalaryCode: vehicleRow.SalaryCode,
                CompanyName: vehicleRow.CompanyName,
                DepartmentName: vehicleRow.DepartmentName,
                SectionName: vehicleRow.SectionName,
                CostCenter: vehicleRow.CostCenter,
                Email: vehicleRow.Email,
                BusinessPhone: null,
                VehicleType: null,
                PayrollCode: vehicleRow.PayrollCode);

            cache[employeeCode] = fromRow;
            return fromRow;
        }

        var mapping = await _repository.GetByEmployeeCodeAsync(employeeCode, cancellationToken);
        cache[employeeCode] = mapping;
        return mapping;
    }

    private static SalaryDeductionRow NewPhoneRow(EmployeeVehicleMapping info, PhoneBill bill) =>
        new(
            EmployeeCode: info.EmployeeCode,
            FullNameThName: info.FullNameThName,
            SalaryCode: info.SalaryCode,
            CompanyName: info.CompanyName,
            DepartmentName: info.DepartmentName,
            SectionName: info.SectionName,
            CostCenter: info.CostCenter,
            Email: info.Email,
            PayrollCode: info.PayrollCode,
            VehicleType: null,
            HourlyTotal: 0m,
            PhoneNo: bill.PhoneNo,
            PhoneExcess: bill.Excess,
            PhoneService: bill.Service);

    // Sort rules:
    //   1. Employee name (Thai collation).
    //   2. Vehicle rows before phone-only rows for the same employee.
    //   3. Within the vehicle group, by VehicleType (C before M).
    //   4. Within the phone group, by PhoneNo.
    private static List<SalaryDeductionRow> SortRows(List<SalaryDeductionRow> rows)
    {
        var thaiSort = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("th-TH"), ignoreCase: false);
        return rows
            .OrderBy(r => r.FullNameThName, thaiSort)
            .ThenBy(r => string.IsNullOrEmpty(r.VehicleType) ? 1 : 0)
            .ThenBy(r => r.VehicleType ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(r => r.PhoneNo ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }
}
