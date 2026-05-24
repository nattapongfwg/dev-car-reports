using CarReports.Web.Excel;
using CarReports.Web.Models;
using ClosedXML.Excel;
using Xunit;

namespace CarReports.Web.Tests;

public sealed class SalaryWorkbookBuilderTests
{
    [Fact]
    public void Build_WithNoRows_ProducesValidXlsx()
    {
        var builder = new SalaryWorkbookBuilder();
        var data = new SalaryReportData(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 30),
            Array.Empty<SalaryDeductionRow>());

        var bytes = builder.Build(data);

        Assert.NotEmpty(bytes);
        // xlsx is a ZIP container: first four bytes must be the local-file-header signature.
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, bytes.AsSpan(0, 4).ToArray());
    }

    // Header row 4 must label column I as "Phone No.", K as "ค่าโทรเกิน",
    // and L as "บริการเสริม". These were shifted by +1 when "Phone No." was
    // inserted at column I.
    [Fact]
    public void Build_Headers_PlacePhoneNoAndAmountsInExpectedColumns()
    {
        var builder = new SalaryWorkbookBuilder();
        var data = new SalaryReportData(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 30),
            Array.Empty<SalaryDeductionRow>());

        var bytes = builder.Build(data);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheets.First();
        Assert.Equal("Phone No.",   sheet.Cell(4, 9).GetString());   // I
        Assert.Equal("ค่าโทรเกิน",   sheet.Cell(4, 11).GetString());  // K
        Assert.Equal("บริการเสริม", sheet.Cell(4, 12).GetString());  // L
        Assert.Equal("รวม",         sheet.Cell(4, 13).GetString());  // M (=K+L)
    }

    // When the input has two SalaryDeductionRows for the same employee with
    // different phone numbers, both rows must appear in the output workbook
    // with their own Phone No. / excess / service values. This documents the
    // "equal rows with different phone no" duplication contract.
    [Fact]
    public void Build_TwoRowsSameEmployeeDifferentPhones_BothAppear()
    {
        var sharedFields = new
        {
            EmployeeCode    = "0468",
            FullNameThName  = "Mr. Test Multi-Phone",
            SalaryCode      = "999999",
            CompanyName     = "Freewill Comserv",
            DepartmentName  = "Dept",
            SectionName     = "Section",
            CostCenter      = "10710101",
            Email           = "test@example.com",
            PayrollCode     = "P-001",
            VehicleType     = "C",
            HourlyTotal     = 195m
        };

        var rowA = new SalaryDeductionRow(
            sharedFields.EmployeeCode, sharedFields.FullNameThName, sharedFields.SalaryCode,
            sharedFields.CompanyName, sharedFields.DepartmentName, sharedFields.SectionName,
            sharedFields.CostCenter, sharedFields.Email, sharedFields.PayrollCode,
            sharedFields.VehicleType, sharedFields.HourlyTotal,
            PhoneNo: "0859800919", PhoneExcess: 12m, PhoneService: 0m);

        var rowB = rowA with { PhoneNo = "0891719955", PhoneExcess = 0m, PhoneService = 25m };

        var builder = new SalaryWorkbookBuilder();
        var data = new SalaryReportData(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 30),
            new[] { rowA, rowB });

        var bytes = builder.Build(data);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheets.First();

        const int colId = 1, colPhoneNo = 9, colExcess = 11, colService = 12;
        const int firstDataRow = 5;

        Assert.Equal("0468",       sheet.Cell(firstDataRow,     colId).GetString());
        Assert.Equal("0468",       sheet.Cell(firstDataRow + 1, colId).GetString());
        Assert.Equal("0859800919", sheet.Cell(firstDataRow,     colPhoneNo).GetString());
        Assert.Equal("0891719955", sheet.Cell(firstDataRow + 1, colPhoneNo).GetString());
        Assert.Equal(12m,          sheet.Cell(firstDataRow,     colExcess).GetValue<decimal>());
        Assert.Equal(0m,           sheet.Cell(firstDataRow + 1, colExcess).GetValue<decimal>());
        Assert.Equal(0m,           sheet.Cell(firstDataRow,     colService).GetValue<decimal>());
        Assert.Equal(25m,          sheet.Cell(firstDataRow + 1, colService).GetValue<decimal>());
    }

    // Subsidy fully consumed by the Car row → Motorcycle row gets no leftover.
    // Spec: C-H=70, C-M=1100; M-H=10, M-M=150.
    //   C: R = 70+1100-400 = 770; Z = 70 (raw hourly only).
    //   M: R = 10+150 - MAX(0, 400-70-1100) = 160 - 0 = 160; Z = 10.
    [Fact]
    public void Build_CarAndMotorcycle_SubsidyFullyConsumedByCar()
    {
        var bytes = BuildVehiclePair(carHourly: 70m, carMonthly: 1100m, moHourly: 10m, moMonthly: 150m);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheets.First();

        const int firstDataRow = 5;
        const int colR = 18, colS = 19, colU = 21, colV = 22, colZ = 26;

        Assert.Equal(770m, sheet.Cell(firstDataRow,     colR).GetValue<decimal>());
        Assert.Equal(770m, sheet.Cell(firstDataRow,     colS).GetValue<decimal>());
        Assert.Equal(770m, sheet.Cell(firstDataRow,     colU).GetValue<decimal>());
        Assert.Equal(770m, sheet.Cell(firstDataRow,     colV).GetValue<decimal>());
        Assert.Equal(70m,  sheet.Cell(firstDataRow,     colZ).GetValue<decimal>());

        Assert.Equal(160m, sheet.Cell(firstDataRow + 1, colR).GetValue<decimal>());
        Assert.Equal(160m, sheet.Cell(firstDataRow + 1, colS).GetValue<decimal>());
        Assert.Equal(160m, sheet.Cell(firstDataRow + 1, colU).GetValue<decimal>());
        Assert.Equal(10m,  sheet.Cell(firstDataRow + 1, colZ).GetValue<decimal>());
    }

    // Car row leaves subsidy unused → leftover spills to Motorcycle row, can drive R negative.
    // Spec: C-H=10, C-M=200; M-H=20, M-M=150.
    //   C: R = 10+200-400 = -190; S floors to 0.
    //   M: R = 20+150 - MAX(0, 400-10-200) = 170 - 190 = -20; S floors to 0.
    [Fact]
    public void Build_CarAndMotorcycle_UnusedSubsidySpillsToMotorcycle()
    {
        var bytes = BuildVehiclePair(carHourly: 10m, carMonthly: 200m, moHourly: 20m, moMonthly: 150m);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheets.First();

        const int firstDataRow = 5;
        const int colR = 18, colS = 19, colU = 21;

        Assert.Equal(-190m, sheet.Cell(firstDataRow,     colR).GetValue<decimal>());
        Assert.Equal(0m,    sheet.Cell(firstDataRow,     colS).GetValue<decimal>());
        Assert.Equal(0m,    sheet.Cell(firstDataRow,     colU).GetValue<decimal>());

        Assert.Equal(-20m,  sheet.Cell(firstDataRow + 1, colR).GetValue<decimal>());
        Assert.Equal(0m,    sheet.Cell(firstDataRow + 1, colS).GetValue<decimal>());
        Assert.Equal(0m,    sheet.Cell(firstDataRow + 1, colU).GetValue<decimal>());
    }

    private static byte[] BuildVehiclePair(decimal carHourly, decimal carMonthly, decimal moHourly, decimal moMonthly)
    {
        var carRow = MakeVehicleRow("0468", "C", carHourly, carMonthly);
        var moRow  = MakeVehicleRow("0468", "M", moHourly,  moMonthly);

        var builder = new SalaryWorkbookBuilder();
        var data = new SalaryReportData(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 30),
            new[] { carRow, moRow });
        return builder.Build(data);
    }

    private static SalaryDeductionRow MakeVehicleRow(string empCode, string vehicleType, decimal hourly, decimal monthly) =>
        new(
            EmployeeCode: empCode,
            FullNameThName: "Mr. Test",
            SalaryCode: "999999",
            CompanyName: "Freewill Comserv",
            DepartmentName: "Dept",
            SectionName: "Section",
            CostCenter: "10710101",
            Email: "test@example.com",
            PayrollCode: "P-001",
            VehicleType: vehicleType,
            HourlyTotal: hourly,
            MonthlyAmount: monthly);
}
