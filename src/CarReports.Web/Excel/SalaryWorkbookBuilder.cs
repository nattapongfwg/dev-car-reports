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
        SetMerged(sheet, GroupHeaderRow, 1, GroupHeaderRow, 1, "ID");
        SetMerged(sheet, GroupHeaderRow, 2, GroupHeaderRow, 2, "ชื่อ - นามสกุล");
        SetMerged(sheet, GroupHeaderRow, 3, GroupHeaderRow, 3, "Salary Code");
        SetMerged(sheet, GroupHeaderRow, 4, GroupHeaderRow, 4, "Company");
        SetMerged(sheet, GroupHeaderRow, 5, GroupHeaderRow, 5, "Department");
        SetMerged(sheet, GroupHeaderRow, 6, GroupHeaderRow, 6, "Section");
        SetMerged(sheet, GroupHeaderRow, 7, GroupHeaderRow, 7, "Cost Center");

        SetMerged(sheet, GroupHeaderRow, 8, GroupHeaderRow, 11, "ค่าโทรศัพท์ส่วนเกิน");

        SetMerged(sheet, GroupHeaderRow, 12, GroupHeaderRow, 12, "ค่าจอดรถรายเดือน");

        SetMerged(sheet, GroupHeaderRow, 13, GroupHeaderRow, 15, $"ค่าจอดรถรายชั่วโมง\n{periodText}");

        SetMerged(sheet, GroupHeaderRow, 16, GroupHeaderRow, 17, "ค่าจอดรถส่วนที่บริษัทช่วยเหลือ");

        // R3:T3 month-label placeholder filled later in WriteMonthLabel.

        SetMerged(sheet, GroupHeaderRow, 21, GroupHeaderRow, 21, "หมายเหตุ");
        SetMerged(sheet, GroupHeaderRow, 22, GroupHeaderRow, 22, "Email");

        SetMerged(sheet, GroupHeaderRow, 23, GroupHeaderRow, 25, "สรุปเฉพาะที่ต้องทำบันทึกหัก");

        SetMerged(sheet, GroupHeaderRow, 26, GroupHeaderRow, 26, "Payroll Code");

        var fullHeader = sheet.Range(GroupHeaderRow, 1, GroupHeaderRow, 26);
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
        sheet.Cell(SubHeaderRow, 8).Value = "หมายเหตุ";
        sheet.Cell(SubHeaderRow, 9).Value = "ค่าโทรเกิน";
        sheet.Cell(SubHeaderRow, 10).Value = "บริการเสริม";
        sheet.Cell(SubHeaderRow, 11).Value = "รวม";

        sheet.Cell(SubHeaderRow, 13).Value = "ลุมพินี ทาวเวอร์";
        sheet.Cell(SubHeaderRow, 14).Value = "True Digital Park";
        sheet.Cell(SubHeaderRow, 15).Value = "รวม";

        sheet.Cell(SubHeaderRow, 16).Value = "หักส่วนที่บริษัทช่วยเหลือค่าที่จอดรถ 400 บาท";
        sheet.Cell(SubHeaderRow, 17).Value = "คงเหลือค่าที่จอดรถส่วนที่พนักงานชำระเอง";

        sheet.Cell(SubHeaderRow, 18).Value = "ค่าโทรศัพท์";
        sheet.Cell(SubHeaderRow, 19).Value = "ค่าที่จอดรถ\n(หักสวัสดิการ 400)";
        sheet.Cell(SubHeaderRow, 20).Value = "รวม";

        sheet.Cell(SubHeaderRow, 23).Value = "ค่าโทรศัพท์";
        sheet.Cell(SubHeaderRow, 24).Value = "ค่าจอดรถนอกเหนือจากรายเดือน";
        sheet.Cell(SubHeaderRow, 25).Value = "รวมหัก";

        // Merge vertical for single-cell group headers so column visually spans both rows.
        MergeVertical(sheet, 1, 1);   // A
        MergeVertical(sheet, 2, 2);   // B
        MergeVertical(sheet, 3, 3);   // C
        MergeVertical(sheet, 4, 4);   // D
        MergeVertical(sheet, 5, 5);   // E
        MergeVertical(sheet, 6, 6);   // F
        MergeVertical(sheet, 7, 7);   // G
        MergeVertical(sheet, 12, 12); // L
        MergeVertical(sheet, 21, 21); // U
        MergeVertical(sheet, 22, 22); // V
        MergeVertical(sheet, 26, 26); // Z

        var subRange = sheet.Range(SubHeaderRow, 1, SubHeaderRow, 26);
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
        SetMerged(sheet, GroupHeaderRow, 18, GroupHeaderRow, 20, monthLabel);
        var range = sheet.Range(GroupHeaderRow, 18, GroupHeaderRow, 20);
        range.Style.Font.Bold = true;
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Fill.BackgroundColor = GroupHeaderFill;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void WriteDataRow(IXLWorksheet sheet, int rowNumber, SalaryDeductionRow row)
    {
        sheet.Cell(rowNumber, 1).Value = row.EmployeeCode;
        sheet.Cell(rowNumber, 2).Value = row.FullNameThName;
        sheet.Cell(rowNumber, 3).Value = row.SalaryCode;
        sheet.Cell(rowNumber, 4).Value = row.CompanyName;
        sheet.Cell(rowNumber, 5).Value = row.DepartmentName;
        sheet.Cell(rowNumber, 6).Value = row.SectionName;
        sheet.Cell(rowNumber, 7).Value = row.CostCenter ?? string.Empty;

        // H, I, J left blank; K = I + J formula.
        sheet.Cell(rowNumber, 11).FormulaA1 = $"=I{rowNumber}+J{rowNumber}";

        // L blank (ค่าจอดรายเดือน).

        sheet.Cell(rowNumber, 13).Value = row.HourlyTotal;       // M ลุมพินี ทาวเวอร์
        // N True Digital Park blank.
        sheet.Cell(rowNumber, 15).Value = row.HourlyTotal;       // O รวม (value, not formula per spec)

        sheet.Cell(rowNumber, 16).FormulaA1 = $"=M{rowNumber}-400";       // P
        sheet.Cell(rowNumber, 17).FormulaA1 = $"=IF(P{rowNumber}<0,0,P{rowNumber})"; // Q

        var sValue = Math.Max(0m, row.HourlyTotal - 400m);
        sheet.Cell(rowNumber, 18).Value = 0m;        // R default 0.00
        sheet.Cell(rowNumber, 19).Value = sValue;    // S = Q value (no formula)
        sheet.Cell(rowNumber, 20).FormulaA1 = $"=R{rowNumber}+S{rowNumber}"; // T

        // U blank (หมายเหตุ).
        sheet.Cell(rowNumber, 22).Value = row.Email ?? string.Empty;

        sheet.Cell(rowNumber, 23).FormulaA1 = $"=R{rowNumber}"; // W
        sheet.Cell(rowNumber, 24).FormulaA1 = $"=S{rowNumber}"; // X
        sheet.Cell(rowNumber, 25).FormulaA1 = $"=T{rowNumber}"; // Y

        sheet.Cell(rowNumber, 26).Value = row.PayrollCode ?? string.Empty;

        var moneyRange = sheet.Range(rowNumber, 8, rowNumber, 20);
        moneyRange.Style.NumberFormat.Format = "#,##0.00";
        var summaryMoney = sheet.Range(rowNumber, 23, rowNumber, 25);
        summaryMoney.Style.NumberFormat.Format = "#,##0.00";

        var dataRange = sheet.Range(rowNumber, 1, rowNumber, 26);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        if ((rowNumber - FirstDataRow) % 2 == 1)
        {
            dataRange.Style.Fill.BackgroundColor = BandFill;
        }
    }

    private static void ApplyColumnWidths(IXLWorksheet sheet)
    {
        sheet.Column(1).Width = 8;    // A ID
        sheet.Column(2).Width = 28;   // B Name
        sheet.Column(3).Width = 10;   // C Salary Code
        sheet.Column(4).Width = 22;   // D Company
        sheet.Column(5).Width = 22;   // E Department
        sheet.Column(6).Width = 22;   // F Section
        sheet.Column(7).Width = 11;   // G Cost Center
        for (var c = 8; c <= 11; c++) sheet.Column(c).Width = 11;
        sheet.Column(12).Width = 11;
        for (var c = 13; c <= 15; c++) sheet.Column(c).Width = 12;
        for (var c = 16; c <= 17; c++) sheet.Column(c).Width = 14;
        for (var c = 18; c <= 20; c++) sheet.Column(c).Width = 12;
        sheet.Column(21).Width = 14;
        sheet.Column(22).Width = 28;
        for (var c = 23; c <= 25; c++) sheet.Column(c).Width = 12;
        sheet.Column(26).Width = 12;
    }

    private static void SetMerged(IXLWorksheet sheet, int r1, int c1, int r2, int c2, string text)
    {
        var range = sheet.Range(r1, c1, r2, c2);
        if (r1 != r2 || c1 != c2) range.Merge();
        sheet.Cell(r1, c1).Value = text;
    }

    private static void MergeVertical(IXLWorksheet sheet, int col, int colEnd)
    {
        sheet.Range(GroupHeaderRow, col, SubHeaderRow, colEnd).Merge();
    }

    private static string FormatPeriod(DateOnly start, DateOnly end)
    {
        return $"{start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} - " +
               $"{end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
    }
}
