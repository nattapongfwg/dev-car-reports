using System.Globalization;
using CarReports.Web.Models;
using ClosedXML.Excel;

namespace CarReports.Web.Excel;

public sealed class SalaryWorkbookBuilder : ISalaryWorkbookBuilder
{
    private const string SheetName = "Report_Salary";
    private const int GroupHeaderRow = 3;
    private const int SubHeaderRow = 4;
    private const int FirstDataRow = 5;
    private const int LastCol = 27;

    // Column positions (1-based)
    private const int ColId           = 1;   // A
    private const int ColName         = 2;   // B
    private const int ColSalaryCode   = 3;   // C
    private const int ColCompany     = 4;   // D
    private const int ColDepartment   = 5;   // E
    private const int ColSection     = 6;   // F
    private const int ColCostCenter   = 7;   // G
    private const int ColVehicleType  = 8;   // H  (NEW — Vehicle Type)

    // I-L : ค่าโทรศัพท์ส่วนเกิน
    private const int ColPhoneRemark  = 9;   // I
    private const int ColPhoneExceed  = 10;  // J
    private const int ColPhoneService = 11;  // K
    private const int ColPhoneTotal   = 12;  // L  (formula =J+K)

    private const int ColMonthlyPark  = 13;  // M

    // N-P : ค่าจอดรถรายชั่วโมง
    private const int ColLumpini      = 14;  // N  (hourly total)
    private const int ColTrueDigital  = 15;  // O
    private const int ColHourlyTotal  = 16;  // P  (formula =N+O)

    // Q-R : ค่าจอดรถส่วนที่บริษัทช่วยเหลือ
    private const int ColMinus400     = 17;  // Q  (formula =N-400)
    private const int ColRemaining    = 18;  // R  (formula =IF(Q<0,0,Q))

    // S-U : ยอดหักเงินเดือน {month}
    private const int ColPhoneDeduct  = 19;  // S  (formula =L)
    private const int ColParkDeduct   = 20;  // T  (formula =R)
    private const int ColTotalDeduct  = 21;  // U  (formula =S+T)

    private const int ColNote         = 22;  // V
    private const int ColEmail        = 23;  // W

    // X-Z : สรุปเฉพาะที่ต้องทำบันทึกหัก
    private const int ColSummaryPhone = 24;  // X  (formula =S)
    private const int ColSummaryPark  = 25;  // Y  (formula =T)
    private const int ColSummaryTotal = 26;  // Z  (formula =U)

    private const int ColPayroll      = 27;  // AA

    private static readonly XLColor GroupHeaderFill = XLColor.FromHtml("#D9E1F2");
    private static readonly XLColor SubHeaderFill = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor BandFill = XLColor.FromHtml("#F2F2F2");

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

        for (var i = 0; i < data.Rows.Count; i++)
        {
            WriteDataRow(sheet, FirstDataRow + i, data.Rows[i]);
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
        SetMerged(sheet, GroupHeaderRow, ColPhoneRemark,  GroupHeaderRow, ColPhoneTotal,    "ค่าโทรศัพท์ส่วนเกิน");
        sheet.Cell(GroupHeaderRow, ColMonthlyPark).Value = "ค่าจอดรถรายเดือน";
        SetMerged(sheet, GroupHeaderRow, ColLumpini,      GroupHeaderRow, ColHourlyTotal,   $"ค่าจอดรถรายชั่วโมง\n{periodText}");
        SetMerged(sheet, GroupHeaderRow, ColMinus400,     GroupHeaderRow, ColRemaining,     "ค่าจอดรถส่วนที่บริษัทช่วยเหลือ");
        // S3:U3 month-label placeholder filled later in WriteMonthLabel.

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

    private static void WriteDataRow(IXLWorksheet sheet, int r, SalaryDeductionRow row)
    {
        sheet.Cell(r, ColId).Value          = row.EmployeeCode;
        sheet.Cell(r, ColName).Value        = row.FullNameThName;
        sheet.Cell(r, ColSalaryCode).Value  = row.SalaryCode;
        sheet.Cell(r, ColCompany).Value     = row.CompanyName;
        sheet.Cell(r, ColDepartment).Value  = row.DepartmentName;
        sheet.Cell(r, ColSection).Value     = row.SectionName;
        sheet.Cell(r, ColCostCenter).Value  = row.CostCenter ?? string.Empty;
        sheet.Cell(r, ColVehicleType).Value = MapVehicleType(row.VehicleType);

        // I, J, K left blank; L = J + K formula.
        var colJ = XLHelper.GetColumnLetterFromNumber(ColPhoneExceed);
        var colK = XLHelper.GetColumnLetterFromNumber(ColPhoneService);
        sheet.Cell(r, ColPhoneTotal).FormulaA1 = $"={colJ}{r}+{colK}{r}";

        // M (ค่าจอดรายเดือน) left blank.

        sheet.Cell(r, ColLumpini).Value      = row.HourlyTotal;
        // O (True Digital Park) blank.

        var colL = XLHelper.GetColumnLetterFromNumber(ColPhoneTotal);
        var colN = XLHelper.GetColumnLetterFromNumber(ColLumpini);
        var colO = XLHelper.GetColumnLetterFromNumber(ColTrueDigital);
        var colQ = XLHelper.GetColumnLetterFromNumber(ColMinus400);
        var colR = XLHelper.GetColumnLetterFromNumber(ColRemaining);

        sheet.Cell(r, ColHourlyTotal).FormulaA1 = $"={colN}{r}+{colO}{r}";
        sheet.Cell(r, ColMinus400).FormulaA1    = $"={colN}{r}-400";
        sheet.Cell(r, ColRemaining).FormulaA1   = $"=IF({colQ}{r}<0,0,{colQ}{r})";

        sheet.Cell(r, ColPhoneDeduct).FormulaA1 = $"={colL}{r}";
        sheet.Cell(r, ColParkDeduct).FormulaA1  = $"={colR}{r}";

        var colS = XLHelper.GetColumnLetterFromNumber(ColPhoneDeduct);
        var colT = XLHelper.GetColumnLetterFromNumber(ColParkDeduct);
        sheet.Cell(r, ColTotalDeduct).FormulaA1 = $"={colS}{r}+{colT}{r}";

        // V (หมายเหตุ) blank.
        sheet.Cell(r, ColEmail).Value = row.Email ?? string.Empty;

        var colU = XLHelper.GetColumnLetterFromNumber(ColTotalDeduct);
        sheet.Cell(r, ColSummaryPhone).FormulaA1 = $"={colS}{r}";
        sheet.Cell(r, ColSummaryPark).FormulaA1  = $"={colT}{r}";
        sheet.Cell(r, ColSummaryTotal).FormulaA1 = $"={colU}{r}";

        sheet.Cell(r, ColPayroll).Value = row.PayrollCode ?? string.Empty;

        sheet.Range(r, ColPhoneRemark, r, ColTotalDeduct).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(r, ColSummaryPhone, r, ColSummaryTotal).Style.NumberFormat.Format = "#,##0.00";

        var dataRange = sheet.Range(r, 1, r, LastCol);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Cell(r, ColVehicleType).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        if ((r - FirstDataRow) % 2 == 1)
        {
            dataRange.Style.Fill.BackgroundColor = BandFill;
        }
    }

    private static string MapVehicleType(string code) => code switch
    {
        "C" => "Car",
        "M" => "Motorcycle",
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
