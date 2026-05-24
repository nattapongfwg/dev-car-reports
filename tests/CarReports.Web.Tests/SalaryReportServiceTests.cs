using CarReports.Web.Data;
using CarReports.Web.Excel;
using CarReports.Web.Models;
using CarReports.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarReports.Web.Tests;

public sealed class SalaryReportServiceTests
{
    private static readonly IReadOnlyDictionary<string, decimal> DefaultFees =
        new Dictionary<string, decimal>(StringComparer.Ordinal) { ["C"] = 1500m, ["M"] = 150m };

    // Pins the "split car and phone into separate rows" contract:
    // an employee with one car stamp and two non-zero phone bills must
    // produce three rows — one car row, two phone rows — never sharing a row.
    [Fact]
    public async Task GenerateAsync_OneCarTwoPhones_ProducesThreeSplitRows()
    {
        var mapping = NewMapping("0468", "นาย เทสต์ มัลติโฟน", "C", "H");

        var stamps = new StampReportDetails(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 30),
            new[] { new StampDetail('C', "นาย เทสต์ มัลติโฟน", 195m) });

        var phoneBills = new[]
        {
            new PhoneBill("0468", "0859800919", Excess: 12m, Service: 0m),
            new PhoneBill("0468", "0891719955", Excess: 0m,  Service: 25m)
        };

        var rows = await RunAsync(stamps, phoneBills, new[] { mapping });

        Assert.Equal(3, rows.Count);

        Assert.Equal("C",          rows[0].VehicleType);
        Assert.Null(rows[0].PhoneNo);
        Assert.Equal(195m,         rows[0].HourlyTotal);
        Assert.Equal(0m,           rows[0].MonthlyAmount);

        Assert.Null(rows[1].VehicleType);
        Assert.Equal("0859800919", rows[1].PhoneNo);
        Assert.Equal(12m,          rows[1].PhoneExcess);

        Assert.Null(rows[2].VehicleType);
        Assert.Equal("0891719955", rows[2].PhoneNo);
        Assert.Equal(25m,          rows[2].PhoneService);
    }

    // Card_type='M' alone (no hourly stamps) must still emit a vehicle row,
    // with the monthly fee from m_cfg_lov in MonthlyAmount and HourlyTotal = 0.
    [Fact]
    public async Task GenerateAsync_OnlyMonthlyCard_EmitsRowWithMonthlyOnly()
    {
        var mapping = NewMapping("0500", "นาย เทสต์ มอนธลี", "C", "M");

        var stamps = new StampReportDetails(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            Array.Empty<StampDetail>());

        var rows = await RunAsync(stamps, Array.Empty<PhoneBill>(), new[] { mapping });

        var row = Assert.Single(rows);
        Assert.Equal("C",     row.VehicleType);
        Assert.Equal(0m,      row.HourlyTotal);
        Assert.Equal(1500m,   row.MonthlyAmount);
    }

    // H and M of the same vehicle_type consolidate into ONE row: HourlyTotal
    // from stamps + MonthlyAmount from fees.
    [Fact]
    public async Task GenerateAsync_BothHAndMSameVehicleType_OneRowWithBoth()
    {
        var mappings = new[]
        {
            NewMapping("0501", "นาย เทสต์ บอท", "C", "H"),
            NewMapping("0501", "นาย เทสต์ บอท", "C", "M"),
        };

        var stamps = new StampReportDetails(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            new[] { new StampDetail('C', "นาย เทสต์ บอท", 220m) });

        var rows = await RunAsync(stamps, Array.Empty<PhoneBill>(), mappings);

        var row = Assert.Single(rows);
        Assert.Equal("C",     row.VehicleType);
        Assert.Equal(220m,    row.HourlyTotal);
        Assert.Equal(1500m,   row.MonthlyAmount);
    }

    // A motorcycle row uses fee for "M" (150). Combined with a car-H row
    // for the same employee, the employee shows ≤ 2 vehicle rows (one per type).
    [Fact]
    public async Task GenerateAsync_CarHourlyAndMotorcycleMonthly_TwoRows()
    {
        var mappings = new[]
        {
            NewMapping("0502", "นาย เทสต์ บอธ", "C", "H"),
            NewMapping("0502", "นาย เทสต์ บอธ", "M", "M"),
        };

        var stamps = new StampReportDetails(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            new[] { new StampDetail('C', "นาย เทสต์ บอธ", 80m) });

        var rows = await RunAsync(stamps, Array.Empty<PhoneBill>(), mappings);

        Assert.Equal(2, rows.Count);

        // Sort: "C" before "M".
        Assert.Equal("C",   rows[0].VehicleType);
        Assert.Equal(80m,   rows[0].HourlyTotal);
        Assert.Equal(0m,    rows[0].MonthlyAmount);

        Assert.Equal("M",   rows[1].VehicleType);
        Assert.Equal(0m,    rows[1].HourlyTotal);
        Assert.Equal(150m,  rows[1].MonthlyAmount);
    }

    // H-only card with no stamps for the period and no M card → no deduction → no row.
    [Fact]
    public async Task GenerateAsync_HourlyCardNoStamps_NoRow()
    {
        var mapping = NewMapping("0503", "นาย เทสต์ ว่าง", "C", "H");

        var stamps = new StampReportDetails(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            Array.Empty<StampDetail>());

        var rows = await RunAsync(stamps, Array.Empty<PhoneBill>(), new[] { mapping });

        Assert.Empty(rows);
    }

    // Each processed phone bill must hit EnsurePhoneAsync exactly once with the
    // employee_code from column F and the phone_no from column D. Zero-amount
    // bills are filtered out upstream and must not reach the repository.
    [Fact]
    public async Task GenerateAsync_CallsEnsurePhoneForEachProcessedBill()
    {
        var mapping = NewMapping("0468", "นาย เทสต์ มัลติโฟน", "C", "H");

        var stamps = new StampReportDetails(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            Array.Empty<StampDetail>());

        var phoneBills = new[]
        {
            new PhoneBill("0468", "0859800919", Excess: 12m, Service: 0m),
            new PhoneBill("0468", "0891719955", Excess: 0m,  Service: 25m),
            new PhoneBill("0468", "0000000000", Excess: 0m,  Service: 0m), // skipped
        };

        var phoneRepo = new RecordingBusinessPhoneRepository();
        await RunAsync(stamps, phoneBills, new[] { mapping }, phoneRepo);

        Assert.Equal(
            new[] { ("0468", "0859800919"), ("0468", "0891719955") },
            phoneRepo.Calls);
    }

    private static async Task<IReadOnlyList<SalaryDeductionRow>> RunAsync(
        StampReportDetails stamps,
        IReadOnlyList<PhoneBill> phoneBills,
        IReadOnlyList<EmployeeVehicleMapping> mappings,
        RecordingBusinessPhoneRepository? phoneRepo = null)
    {
        var capturingBuilder = new CapturingWorkbookBuilder();
        var service = new SalaryReportService(
            stampReader: new StubStampReader(stamps),
            phoneReader: new StubPhoneReader(phoneBills),
            repository:  new StubRepository(mappings, DefaultFees),
            businessPhoneRepository: phoneRepo ?? new RecordingBusinessPhoneRepository(),
            builder:     capturingBuilder,
            logger:      NullLogger<SalaryReportService>.Instance);

        await service.GenerateAsync(
            vehicleStream: Stream.Null, vehicleFileName: "Report.xls",
            phoneStream:   Stream.Null, phoneFileName:   "Report_Phone.xlsx",
            cancellationToken: CancellationToken.None);

        return capturingBuilder.LastData!.Rows;
    }

    private static EmployeeVehicleMapping NewMapping(string code, string fullNameTh, string? vehicleType, string? cardType = null) =>
        new(
            EmployeeId: Guid.NewGuid(),
            EmployeeCode: code,
            FullNameTh: fullNameTh,
            FullNameThName: "นาย " + fullNameTh,
            SalaryCode: "999999",
            CompanyName: "Freewill Comserv",
            DepartmentName: "Dept",
            SectionName: "Section",
            CostCenter: "10710101",
            Email: "test@example.com",
            BusinessPhone: null,
            VehicleType: vehicleType,
            PayrollCode: "P-001",
            CardType: cardType);

    private sealed class StubStampReader(StampReportDetails details) : IStampDetailReader
    {
        public StampReportDetails Read(Stream uploadStream, string fileName) => details;
    }

    private sealed class StubPhoneReader(IReadOnlyList<PhoneBill> bills) : IPhoneBillReader
    {
        public IReadOnlyList<PhoneBill> Read(Stream uploadStream, string fileName) => bills;
    }

    private sealed class StubRepository(
        IReadOnlyList<EmployeeVehicleMapping> mappings,
        IReadOnlyDictionary<string, decimal> fees) : ISalaryRepository
    {
        public Task<IReadOnlyList<EmployeeVehicleMapping>> GetMappingsAsync(CancellationToken cancellationToken)
            => Task.FromResult(mappings);

        public Task<EmployeeVehicleMapping?> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken)
            => Task.FromResult<EmployeeVehicleMapping?>(
                mappings.FirstOrDefault(m => string.Equals(m.EmployeeCode, employeeCode, StringComparison.Ordinal)));

        public Task<IReadOnlyDictionary<string, decimal>> GetVehicleMonthlyFeesAsync(CancellationToken cancellationToken)
            => Task.FromResult(fees);
    }

    private sealed class CapturingWorkbookBuilder : ISalaryWorkbookBuilder
    {
        public SalaryReportData? LastData { get; private set; }

        public byte[] Build(SalaryReportData data)
        {
            LastData = data;
            return Array.Empty<byte>();
        }
    }

    private sealed class RecordingBusinessPhoneRepository : IBusinessPhoneRepository
    {
        public List<(string EmployeeCode, string PhoneNo)> Calls { get; } = new();
        public EnsurePhoneResult Result { get; set; } = EnsurePhoneResult.Inserted;

        public Task<EnsurePhoneResult> EnsurePhoneAsync(string employeeCode, string phoneNo, CancellationToken cancellationToken)
        {
            Calls.Add((employeeCode, phoneNo));
            return Task.FromResult(Result);
        }
    }
}
