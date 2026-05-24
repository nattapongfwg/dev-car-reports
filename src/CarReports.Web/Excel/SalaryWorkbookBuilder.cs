using System.Globalization;
using CarReports.Web.Models;
using ClosedXML.Excel;

namespace CarReports.Web.Excel;

public sealed class SalaryWorkbookBuilder : ISalaryWorkbookBuilder
{
    private const string SheetName     = "Report_Salary";
    private const int GroupHeaderRow   = 3;
    private const int SubHeaderRow     = 4;
    private const int FirstDataRow     = 5;
    private const int LastCol          = 28;  // through AB (was 27 before "Phone No." was inserted at I)

    // Column positions (1-based). NOTE: column "I = Phone No." was inserted
    // between "Vehicle Type" and the old phone-remark column, pushing every
    // later column one to the right.
    private const int ColId            = 1;   // A
    private const int ColName          = 2;   // B
    private const int ColSalaryCode    = 3;   // C
    private const int ColCompany       = 4;   // D
    private const int ColDepartment    = 5;   // E
    private const int ColSection       = 6;   // F
    private const int ColCostCenter    = 7;   // G
    private const int ColVehicleType   = 8;   // H

    // I-M : ค่าโทรศัพท์ส่วนเกิน (phone group, now 5 columns)
    private const int ColPhoneNo       = 9;   // I  Phone No.            (NEW)
    private const int ColPhoneRemark   = 10;  // J  หมายเหตุ              (blank)
    private const int ColPhoneExceed   = 11;  // K  ค่าโทรเกิน            (= input N)
    private const int ColPhoneService  = 12;  // L  บริการเสริม           (= input O)
    private const int ColPhoneTotal    = 13;  // M  รวม                  (= K + L)

    private const int ColMonthlyPark   = 14;  // N  ค่าจอดรถรายเดือน (blank)

    // O-Q : ค่าจอดรถรายชั่วโมง
    private const int ColLumpini       = 15;  // O  hourly total
    private const int ColTrueDigital   = 16;  // P
    private const int ColHourlyTotal   = 17;  // Q  = O + P

    // R-S : ค่าจอดรถส่วนที่บริษัทช่วยเหลือ
    private const int ColMinus400      = 18;  // R  = O - 400
    private const int ColRemaining     = 19;  // S  = IF(R<0,0,R)

    // T-V : ยอดหักเงินเดือน {month}
    private const int ColPhoneDeduct   = 20;  // T  = M  (phone deduction)
    private const int ColParkDeduct    = 21;  // U  = S  (parking deduction)
    private const int ColTotalDeduct   = 22;  // V  = T + U

    private const int ColNote          = 23;  // W  หมายเหตุ
    private const int ColEmail         = 24;  // X  Email

    // Y-AA : สรุปเฉพาะที่ต้องทำบันทึกหัก
    private const int ColSummaryPhone  = 25;  // Y  = T
    private const int ColSummaryPark   = 26;  // Z  = U
    private const int ColSummaryTotal  = 27;  // AA = V

    private const int ColPayroll       = 28;  // AB

    private static readonly XLColor GroupHeaderFill = XLColor.FromHtml("#D9E1F2");
    private static readonly XLColor SubHeaderFill   = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor BandFill        = XLColor.FromHtml("#F2F2F2");

    private static readonly string[] ThaiMonths =
    {
        "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
        "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
    };

    public byte[] Build(SalaryReportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);

        var periodText = FormatPeriod(data.PeriodStart, data.PeriodEnd);
        var monthLabel = $"ยอดหักเงินเดือน {ThaiMonths[data.PeriodStart.Month]} {data.PeriodStart.Year + 543}";

        WriteRow1Note(sheet);
        WriteGroupHeaders(sheet, periodText);
        WriteSubHeaders(sheet);
        WriteMonthLabel(sheet, monthLabel);

        // Pre-index each employee's Car-row position so a later Motorcycle row
        // can spill the 400 baht parking subsidy that the Car row left unused.
        var carRowByEmployee = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < data.Rows.Count; i++)
        {
            var dataRow = data.Rows[i];
            if (string.Equals(dataRow.VehicleType, "C", StringComparison.Ordinal))
            {
                carRowByEmployee[dataRow.EmployeeCode] = FirstDataRow + i;
            }
        }

        for (var i = 0; i < data.Rows.Count; i++)
        {
            var dataRow = data.Rows[i];
            int? pairedCarRow = null;
            if (string.Equals(dataRow.VehicleType, "M", StringComparison.Ordinal)
                && carRowByEmployee.TryGetValue(dataRow.EmployeeCode, out var carRow))
            {
                pairedCarRow = carRow;
            }
            WriteDataRow(sheet, FirstDataRow + i, dataRow, pairedCarRow);
        }

        ApplyColumnWidths(sheet);
        sheet.SheetView.FreezeRows(SubHeaderRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteRow1Note(IXLWorksheet sheet)
    {
        sheet.Cell(1, 1).Value = "เอาข้อมูลจาก \"Pivot Teble_รวมรายการหัก\" มาวางเป็น \"ค่า\"";
        sheet.Cell(1, 1).Style.Font.Italic = true;
        sheet.Cell(1, 1).Style.Font.FontColor = XLColor.DarkRed;
    }

    private static void WriteGroupHeaders(IXLWorksheet sheet, string periodText)
    {
        // Single-column headers (will be merged vertically with row 4).
        sheet.Cell(GroupHeaderRow, ColId).Value           = "ID";
        sheet.Cell(GroupHeaderRow, ColName).Value         = "ชื่อ - นามสกุล";
        sheet.Cell(GroupHeaderRow, ColSalaryCode).Value   = "Salary Code";
        sheet.Cell(GroupHeaderRow, ColCompany).Value      = "Company";
        sheet.Cell(GroupHeaderRow, ColDepartment).Value   = "Department";
        sheet.Cell(GroupHeaderRow, ColSection).Value      = "Section";
        sheet.Cell(GroupHeaderRow, ColCostCenter).Value   = "Cost Center";
        sheet.Cell(GroupHeaderRow, ColVehicleType).Value  = "Vehicle Type";

        // Merged group headers spanning multiple columns.
        SetMerged(sheet, GroupHeaderRow, ColPhoneNo,      GroupHeaderRow, ColPhoneTotal,    "ค่าโทรศัพท์ส่วนเกิน");
        sheet.Cell(GroupHeaderRow, ColMonthlyPark).Value = "ค่าจอดรถรายเดือน";
        SetMerged(sheet, GroupHeaderRow, ColLumpini,      GroupHeaderRow, ColHourlyTotal,   $"ค่าจอดรถรายชั่วโมง\n{periodText}");
        SetMerged(sheet, GroupHeaderRow, ColMinus400,     GroupHeaderRow, ColRemaining,     "ค่าจอดรถส่วนที่บริษัทช่วยเหลือ");
        // T3:V3 month-label placeholder filled later in WriteMonthLabel.

        sheet.Cell(GroupHeaderRow, ColNote).Value  = "หมายเหตุ";
        sheet.Cell(GroupHeaderRow, ColEmail).Value = "Email";

        SetMerged(sheet, GroupHeaderRow, ColSummaryPhone, GroupHeaderRow, ColSummaryTotal,  "สรุปเฉพาะที่ต้องทำบันทึกหัก");

        sheet.Cell(GroupHeaderRow, ColPayroll).Value = "Payroll Code";

        var fullHeader = sheet.Range(GroupHeaderRow, 1, GroupHeaderRow, LastCol);
        fullHeader.Style.Font.Bold = true;
        fullHeader.Style.Alignment.WrapText = true;
        fullHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        fullHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        fullHeader.Style.Fill.BackgroundColor = GroupHeaderFill;
        fullHeader.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        fullHeader.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Row(GroupHeaderRow).Height = 32;
    }

    private static void WriteSubHeaders(IXLWorksheet sheet)
    {
        sheet.Cell(SubHeaderRow, ColPhoneNo).Value      = "Phone No.";
        sheet.Cell(SubHeaderRow, ColPhoneRemark).Value  = "หมายเหตุ";
        sheet.Cell(SubHeaderRow, ColPhoneExceed).Value  = "ค่าโทรเกิน";
        sheet.Cell(SubHeaderRow, ColPhoneService).Value = "บริการเสริม";
        sheet.Cell(SubHeaderRow, ColPhoneTotal).Value   = "รวม";

        sheet.Cell(SubHeaderRow, ColLumpini).Value      = "ลุมพินี ทาวเวอร์";
        sheet.Cell(SubHeaderRow, ColTrueDigital).Value  = "True Digital Park";
        sheet.Cell(SubHeaderRow, ColHourlyTotal).Value  = "รวม";

        sheet.Cell(SubHeaderRow, ColMinus400).Value     = "หักส่วนที่บริษัทช่วยเหลือค่าที่จอดรถ 400 บาท";
        sheet.Cell(SubHeaderRow, ColRemaining).Value    = "คงเหลือค่าที่จอดรถส่วนที่พนักงานชำระเอง";

        sheet.Cell(SubHeaderRow, ColPhoneDeduct).Value  = "ค่าโทรศัพท์";
        sheet.Cell(SubHeaderRow, ColParkDeduct).Value   = "ค่าที่จอดรถ\n(หักสวัสดิการ 400)";
        sheet.Cell(SubHeaderRow, ColTotalDeduct).Value  = "รวม";

        sheet.Cell(SubHeaderRow, ColSummaryPhone).Value = "ค่าโทรศัพท์";
        sheet.Cell(SubHeaderRow, ColSummaryPark).Value  = "ค่าจอดรถนอกเหนือจากรายเดือน";
        sheet.Cell(SubHeaderRow, ColSummaryTotal).Value = "รวมหัก";

        // Merge vertical for all single-column headers so the column visually spans both rows.
        foreach (var col in new[]
        {
            ColId, ColName, ColSalaryCode, ColCompany, ColDepartment, ColSection,
            ColCostCenter, ColVehicleType, ColMonthlyPark, ColNote, ColEmail, ColPayroll
        })
        {
            sheet.Range(GroupHeaderRow, col, SubHeaderRow, col).Merge();
        }

        var subRange = sheet.Range(SubHeaderRow, 1, SubHeaderRow, LastCol);
        subRange.Style.Font.Bold = true;
        subRange.Style.Alignment.WrapText = true;
        subRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        subRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        subRange.Style.Fill.BackgroundColor = SubHeaderFill;
        subRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        subRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        sheet.Row(SubHeaderRow).Height = 45;
    }

    private static void WriteMonthLabel(IXLWorksheet sheet, string monthLabel)
    {
        SetMerged(sheet, GroupHeaderRow, ColPhoneDeduct, GroupHeaderRow, ColTotalDeduct, monthLabel);
        var range = sheet.Range(GroupHeaderRow, ColPhoneDeduct, GroupHeaderRow, ColTotalDeduct);
        range.Style.Font.Bold = true;
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Fill.BackgroundColor = GroupHeaderFill;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void WriteDataRow(IXLWorksheet sheet, int r, SalaryDeductionRow row, int? pairedCarRow)
    {
        // Employee identity columns.
        sheet.Cell(r, ColId).Value          = row.EmployeeCode;
        sheet.Cell(r, ColName).Value        = row.FullNameThName;
        sheet.Cell(r, ColSalaryCode).Value  = row.SalaryCode;
        sheet.Cell(r, ColCompany).Value     = row.CompanyName;
        sheet.Cell(r, ColDepartment).Value  = row.DepartmentName;
        sheet.Cell(r, ColSection).Value     = row.SectionName;
        sheet.Cell(r, ColCostCenter).Value  = row.CostCenter ?? string.Empty;
        sheet.Cell(r, ColVehicleType).Value = MapVehicleType(row.VehicleType);

        // Phone group: I = phone number, J = blank note,
        // K = excess, L = service, M = K + L.
        sheet.Cell(r, ColPhoneNo).Value      = row.PhoneNo ?? string.Empty;
        sheet.Cell(r, ColPhoneExceed).Value  = row.PhoneExcess;
        sheet.Cell(r, ColPhoneService).Value = row.PhoneService;

        var colK = XLHelper.GetColumnLetterFromNumber(ColPhoneExceed);
        var colL = XLHelper.GetColumnLetterFromNumber(ColPhoneService);
        sheet.Cell(r, ColPhoneTotal).FormulaA1 = $"={colK}{r}+{colL}{r}";

        // N (ค่าจอดรายเดือน): monthly parking fee, only when the employee owns
        // an active card_type='M' vehicle of this type. Blank otherwise.
        if (row.MonthlyAmount > 0m)
        {
            sheet.Cell(r, ColMonthlyPark).Value = row.MonthlyAmount;
        }

        // Hourly parking group: O = Lumpini total (the stamp total), P blank,
        // Q = N + O + P.
        // R = combined park deduction after the company's 400 baht subsidy is applied
        //     per employee. For a C row (or any standalone row), R = O + N - 400.
        //     For an M row that pairs with a C row, only the LEFTOVER subsidy applies:
        //     R = O + N - MAX(0, 400 - C.O - C.N).
        // S = IF(R<0,0,R) is the positive part employees actually owe.
        sheet.Cell(r, ColLumpini).Value      = row.HourlyTotal;

        var colN = XLHelper.GetColumnLetterFromNumber(ColMonthlyPark);
        var colO = XLHelper.GetColumnLetterFromNumber(ColLumpini);
        var colP = XLHelper.GetColumnLetterFromNumber(ColTrueDigital);
        var colR = XLHelper.GetColumnLetterFromNumber(ColMinus400);

        sheet.Cell(r, ColHourlyTotal).FormulaA1 = $"={colN}{r}+{colO}{r}+{colP}{r}";

        var rFormula = pairedCarRow.HasValue
            ? $"={colO}{r}+{colN}{r}-MAX(0,400-{colO}{pairedCarRow.Value}-{colN}{pairedCarRow.Value})"
            : $"={colO}{r}+{colN}{r}-400";
        sheet.Cell(r, ColMinus400).FormulaA1  = rFormula;
        sheet.Cell(r, ColRemaining).FormulaA1 = $"=IF({colR}{r}<0,0,{colR}{r})";

        // Monthly deduction group: T = phone total (M), U = remaining parking (S),
        // V = T + U. Monthly fee is already baked into R via the new formula, so U = S
        // (no longer S + N).
        var colM = XLHelper.GetColumnLetterFromNumber(ColPhoneTotal);
        var colS = XLHelper.GetColumnLetterFromNumber(ColRemaining);
        sheet.Cell(r, ColPhoneDeduct).FormulaA1 = $"={colM}{r}";
        sheet.Cell(r, ColParkDeduct).FormulaA1  = $"={colS}{r}";

        var colT = XLHelper.GetColumnLetterFromNumber(ColPhoneDeduct);
        var colU = XLHelper.GetColumnLetterFromNumber(ColParkDeduct);
        sheet.Cell(r, ColTotalDeduct).FormulaA1 = $"={colT}{r}+{colU}{r}";

        // W (หมายเหตุ) blank.
        sheet.Cell(r, ColEmail).Value = row.Email ?? string.Empty;

        // Summary block: Y = T (phone), Z = O (raw hourly only — informational
        // breakdown of parking excluding monthly), AA = V (true total deduction).
        var colV = XLHelper.GetColumnLetterFromNumber(ColTotalDeduct);
        sheet.Cell(r, ColSummaryPhone).FormulaA1 = $"={colT}{r}";
        sheet.Cell(r, ColSummaryPark).FormulaA1  = $"={colO}{r}";
        sheet.Cell(r, ColSummaryTotal).FormulaA1 = $"={colV}{r}";

        sheet.Cell(r, ColPayroll).Value = row.PayrollCode ?? string.Empty;

        // Number formatting: phone group + monthly deduction + summary.
        sheet.Range(r, ColPhoneExceed, r, ColTotalDeduct).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(r, ColSummaryPhone, r, ColSummaryTotal).Style.NumberFormat.Format = "#,##0.00";

        // Borders + zebra striping.
        var dataRange = sheet.Range(r, 1, r, LastCol);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Cell(r, ColVehicleType).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Cell(r, ColPhoneNo).Style.Alignment.Horizontal     = XLAlignmentHorizontalValues.Center;

        if ((r - FirstDataRow) % 2 == 1)
        {
            dataRange.Style.Fill.BackgroundColor = BandFill;
        }
    }

    private static string MapVehicleType(string? code) => code switch
    {
        "C" => "Car",
        "M" => "Motorcycle",
        null => string.Empty,
        _   => code
    };

    private static void ApplyColumnWidths(IXLWorksheet sheet)
    {
        sheet.Column(ColId).Width           = 8;
        sheet.Column(ColName).Width         = 28;
        sheet.Column(ColSalaryCode).Width   = 10;
        sheet.Column(ColCompany).Width      = 22;
        sheet.Column(ColDepartment).Width   = 22;
        sheet.Column(ColSection).Width      = 22;
        sheet.Column(ColCostCenter).Width   = 11;
        sheet.Column(ColVehicleType).Width  = 12;

        sheet.Column(ColPhoneNo).Width      = 14;
        sheet.Column(ColPhoneRemark).Width  = 11;
        sheet.Column(ColPhoneExceed).Width  = 11;
        sheet.Column(ColPhoneService).Width = 11;
        sheet.Column(ColPhoneTotal).Width   = 11;

        sheet.Column(ColMonthlyPark).Width  = 11;

        sheet.Column(ColLumpini).Width      = 12;
        sheet.Column(ColTrueDigital).Width  = 12;
        sheet.Column(ColHourlyTotal).Width  = 12;

        sheet.Column(ColMinus400).Width     = 14;
        sheet.Column(ColRemaining).Width    = 14;

        sheet.Column(ColPhoneDeduct).Width  = 12;
        sheet.Column(ColParkDeduct).Width   = 12;
        sheet.Column(ColTotalDeduct).Width  = 12;

        sheet.Column(ColNote).Width         = 14;
        sheet.Column(ColEmail).Width        = 28;

        sheet.Column(ColSummaryPhone).Width = 12;
        sheet.Column(ColSummaryPark).Width  = 12;
        sheet.Column(ColSummaryTotal).Width = 12;

        sheet.Column(ColPayroll).Width      = 12;
    }

    private static void SetMerged(IXLWorksheet sheet, int r1, int c1, int r2, int c2, string text)
    {
        var range = sheet.Range(r1, c1, r2, c2);
        if (r1 != r2 || c1 != c2) range.Merge();
        sheet.Cell(r1, c1).Value = text;
    }

    private static string FormatPeriod(DateOnly start, DateOnly end)
    {
        return $"{start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} - " +
               $"{end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
    }
}
