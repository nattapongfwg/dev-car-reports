using System.Globalization;
using CarReports.Web.Models;
using ClosedXML.Excel;
using HtmlAgilityPack;

namespace CarReports.Web.Excel;

public sealed class UploadedExcelReader : IUploadedExcelReader
{
    private const int HeaderRow = 3;
    private const int FirstDataRow = 4;
    private const int AmountColumn = 13;
    private const int RemarkColumn = 14;
    private const int ExpectedColumnCount = 14;

    public IReadOnlyList<StampUsageRow> Read(Stream uploadStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".xlsx" => ReadXlsx(uploadStream),
            ".xls"  => ReadHtmlXls(uploadStream),
            _ => throw new InvalidUploadException(
                $"Unsupported file type \"{extension}\". Upload .xlsx or .xls.")
        };
    }

    private static IReadOnlyList<StampUsageRow> ReadXlsx(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new InvalidUploadException("The uploaded file is not a valid .xlsx workbook.", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidUploadException("The uploaded workbook has no worksheets.");

            ValidateXlsxHeaderRow(sheet);

            var rows = new List<StampUsageRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            for (var rowNumber = FirstDataRow; rowNumber <= lastRow; rowNumber++)
            {
                var row = sheet.Row(rowNumber);
                if (row.IsEmpty()) continue;

                var remark = NormalizeRemark(row.Cell(RemarkColumn).GetString());
                var amountCell = row.Cell(AmountColumn);

                if (!TryReadDecimal(amountCell.GetString(), out var amount))
                {
                    throw new InvalidUploadException(
                        $"Row {rowNumber}: column M (Amount) is not a valid number — got \"{amountCell.GetString()}\".");
                }

                rows.Add(new StampUsageRow(rowNumber, remark, amount));
            }

            if (rows.Count == 0)
            {
                throw new InvalidUploadException("The uploaded workbook has no data rows.");
            }

            return rows;
        }
    }

    private static IReadOnlyList<StampUsageRow> ReadHtmlXls(Stream stream)
    {
        var doc = new HtmlDocument();
        try
        {
            doc.Load(stream);
        }
        catch (Exception ex)
        {
            throw new InvalidUploadException("The uploaded .xls file is not parseable as HTML.", ex);
        }

        var table = doc.DocumentNode.SelectSingleNode("//table")
            ?? throw new InvalidUploadException("The uploaded .xls file contains no <table>.");

        ValidateHtmlHeader(table);

        var rows = new List<StampUsageRow>();
        var sourceRowNumber = 0;

        foreach (var tr in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            sourceRowNumber++;
            var cells = tr.SelectNodes("./td");
            if (cells is null || cells.Count < ExpectedColumnCount) continue;

            var remark = NormalizeRemark(HtmlEntity.DeEntitize(cells[RemarkColumn - 1].InnerText));
            var amountText = HtmlEntity.DeEntitize(cells[AmountColumn - 1].InnerText).Trim();

            if (!TryReadDecimal(amountText, out var amount))
            {
                throw new InvalidUploadException(
                    $"Source row {sourceRowNumber}: column M (Amount) is not a valid number — got \"{amountText}\".");
            }

            rows.Add(new StampUsageRow(sourceRowNumber, remark, amount));
        }

        if (rows.Count == 0)
        {
            throw new InvalidUploadException("The uploaded .xls file has no data rows.");
        }

        return rows;
    }

    private static void ValidateXlsxHeaderRow(IXLWorksheet sheet)
    {
        var amountHeader = sheet.Row(HeaderRow).Cell(AmountColumn).GetString();
        var remarkHeader = sheet.Row(HeaderRow).Cell(RemarkColumn).GetString();
        ValidateHeaders(amountHeader, remarkHeader);
    }

    private static void ValidateHtmlHeader(HtmlNode table)
    {
        var headerCells = table.SelectNodes(".//thead//th") ?? table.SelectNodes(".//tr/th");
        if (headerCells is null || headerCells.Count < ExpectedColumnCount)
        {
            throw new InvalidUploadException(
                $"Header row not found or has fewer than {ExpectedColumnCount} columns.");
        }

        var amountHeader = HtmlEntity.DeEntitize(headerCells[AmountColumn - 1].InnerText);
        var remarkHeader = HtmlEntity.DeEntitize(headerCells[RemarkColumn - 1].InnerText);
        ValidateHeaders(amountHeader, remarkHeader);
    }

    private static void ValidateHeaders(string amountHeader, string remarkHeader)
    {
        var amountOk = amountHeader.Contains("จำนวนเงิน") ||
                       amountHeader.Contains("Amount", StringComparison.OrdinalIgnoreCase);
        var remarkOk = remarkHeader.Contains("หมายเหตุ") ||
                       remarkHeader.Contains("Remark", StringComparison.OrdinalIgnoreCase);

        if (!amountOk || !remarkOk)
        {
            throw new InvalidUploadException(
                "Header doesn't match the expected stamp report template. " +
                "Expected column M = \"จำนวนเงิน / Amount\" and column N = \"หมายเหตุ / Remark\".");
        }
    }

    private static string NormalizeRemark(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return string.Empty;

        var sb = new System.Text.StringBuilder(trimmed.Length);
        var lastWasSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    private static bool TryReadDecimal(string raw, out decimal value)
    {
        var cleaned = raw.Trim().Replace(",", string.Empty);
        if (cleaned.Length == 0)
        {
            value = 0m;
            return true;
        }
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
