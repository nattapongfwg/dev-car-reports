using CarReports.Web.Models;
using ClosedXML.Excel;

namespace CarReports.Web.Excel;

public sealed class ReportWorkbookBuilder : IReportWorkbookBuilder
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F4E78");
    private static readonly XLColor HeaderFont = XLColor.White;
    private static readonly XLColor BandFill   = XLColor.FromHtml("#F2F2F2");
    private static readonly XLColor TotalFill  = XLColor.FromHtml("#FCE4D6");

    public byte[] Build(ReportData data)
    {
        using var workbook = new XLWorkbook();
        BuildSummarySheet(workbook, data);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildSummarySheet(XLWorkbook workbook, ReportData data)
    {
        var sheet = workbook.Worksheets.Add("Summary");

        sheet.Cell(1, 1).Value = "Stamp Usage Summary";
        var title = sheet.Range(1, 1, 1, 3);
        title.Merge();
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 16;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        title.Style.Fill.BackgroundColor = HeaderFill;
        title.Style.Font.FontColor = HeaderFont;

        sheet.Cell(2, 1).Value = "Generated (UTC):";
        sheet.Cell(2, 2).Value = data.GeneratedAtUtc;
        sheet.Cell(2, 2).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm:ss";
        sheet.Cell(3, 1).Value = "Source rows:";
        sheet.Cell(3, 2).Value = data.Rows.Count;
        sheet.Range(2, 1, 3, 1).Style.Font.Bold = true;

        const int headerRow = 5;
        sheet.Cell(headerRow, 1).Value = "Remark (หมายเหตุ)";
        sheet.Cell(headerRow, 2).Value = "Total Amount (จำนวนเงิน)";
        sheet.Cell(headerRow, 3).Value = "Row Count";
        StyleHeader(sheet.Range(headerRow, 1, headerRow, 3));

        var groups = data.Rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Remark) ? "(blank)" : r.Remark,
                     StringComparer.Ordinal)
            .Select(g => new
            {
                Remark = g.Key,
                Total = g.Sum(r => r.Amount),
                Count = g.Count()
            })
            .OrderByDescending(g => g.Total)
            .ThenBy(g => g.Remark, StringComparer.Ordinal)
            .ToList();

        var rowNumber = headerRow + 1;
        foreach (var group in groups)
        {
            sheet.Cell(rowNumber, 1).Value = group.Remark;
            sheet.Cell(rowNumber, 2).Value = group.Total;
            sheet.Cell(rowNumber, 3).Value = group.Count;
            rowNumber++;
        }

        var dataFirst = headerRow + 1;
        var dataLast = rowNumber - 1;

        if (groups.Count > 0)
        {
            var totalRow = rowNumber;
            sheet.Cell(totalRow, 1).Value = "TOTAL";
            sheet.Cell(totalRow, 2).FormulaA1 = $"=SUM(B{dataFirst}:B{dataLast})";
            sheet.Cell(totalRow, 3).FormulaA1 = $"=SUM(C{dataFirst}:C{dataLast})";

            var totalRange = sheet.Range(totalRow, 1, totalRow, 3);
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Fill.BackgroundColor = TotalFill;
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;

            sheet.Range(dataFirst, 2, totalRow, 2).Style.NumberFormat.Format = "#,##0.00";
            sheet.Range(dataFirst, 3, totalRow, 3).Style.NumberFormat.Format = "#,##0";

            ApplyBanding(sheet.Range(dataFirst, 1, dataLast, 3));
        }
        else
        {
            sheet.Cell(dataFirst, 1).Value = "(no data rows found)";
            sheet.Range(dataFirst, 1, dataFirst, 3).Merge();
            sheet.Cell(dataFirst, 1).Style.Font.Italic = true;
        }

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns().AdjustToContents();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = HeaderFont;
        range.Style.Fill.BackgroundColor = HeaderFill;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void ApplyBanding(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;

        var firstRow = range.FirstRow().RowNumber();
        var lastRow = range.LastRow().RowNumber();
        var firstCol = range.FirstColumn().ColumnNumber();
        var lastCol = range.LastColumn().ColumnNumber();

        for (var r = firstRow; r <= lastRow; r++)
        {
            if ((r - firstRow) % 2 == 1)
            {
                range.Worksheet.Range(r, firstCol, r, lastCol)
                    .Style.Fill.BackgroundColor = BandFill;
            }
        }
    }
}
