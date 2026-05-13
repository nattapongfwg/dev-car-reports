using System.Globalization;
using System.Text.RegularExpressions;
using CarReports.Web.Models;
using ClosedXML.Excel;
using HtmlAgilityPack;

namespace CarReports.Web.Excel;

public sealed class StampDetailReader : IStampDetailReader
{
    private const int CardCodeColumn = 2;
    private const int AmountColumn = 13;
    private const int RemarkColumn = 14;
    private const int ExpectedColumnCount = 14;

    private static readonly Regex PeriodPattern =
        new(@"(\d{1,2})[-/](\w{3,})[-/](\d{4}).*?(\d{1,2})[-/](\w{3,})[-/](\d{4})", RegexOptions.Compiled);

    public StampReportDetails Read(Stream uploadStream, string fileName)
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

    private static StampReportDetails ReadXlsx(Stream stream)
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

            var periodText = sheet.Row(2).Cell(1).GetString();
            var (start, end) = ParsePeriod(periodText);

            var details = new List<StampDetail>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            for (var rowNumber = 4; rowNumber <= lastRow; rowNumber++)
            {
                var row = sheet.Row(rowNumber);
                if (row.IsEmpty()) continue;

                var cardCode = row.Cell(CardCodeColumn).GetString().Trim();
                var remark = NormalizeText(row.Cell(RemarkColumn).GetString());
                var amountText = row.Cell(AmountColumn).GetString();

                if (cardCode.Length == 0) continue;
                if (!TryReadDecimal(amountText, out var amount)) continue;

                details.Add(new StampDetail(char.ToUpperInvariant(cardCode[0]), remark, amount));
            }

            if (details.Count == 0)
            {
                throw new InvalidUploadException("The uploaded workbook has no parseable data rows.");
            }

            return new StampReportDetails(start, end, details);
        }
    }

    private static StampReportDetails ReadHtmlXls(Stream stream)
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

        var periodText = ExtractPeriodText(table);
        var (start, end) = ParsePeriod(periodText);

        var details = new List<StampDetail>();
        foreach (var tr in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = tr.SelectNodes("./td");
            if (cells is null || cells.Count < ExpectedColumnCount) continue;

            var cardCode = HtmlEntity.DeEntitize(cells[CardCodeColumn - 1].InnerText).Trim();
            var remark = NormalizeText(HtmlEntity.DeEntitize(cells[RemarkColumn - 1].InnerText));
            var amountText = HtmlEntity.DeEntitize(cells[AmountColumn - 1].InnerText).Trim();

            if (cardCode.Length == 0) continue;
            if (!TryReadDecimal(amountText, out var amount)) continue;

            details.Add(new StampDetail(char.ToUpperInvariant(cardCode[0]), remark, amount));
        }

        if (details.Count == 0)
        {
            throw new InvalidUploadException("The uploaded .xls file has no parseable data rows.");
        }

        return new StampReportDetails(start, end, details);
    }

    private static string ExtractPeriodText(HtmlNode table)
    {
        foreach (var th in table.SelectNodes(".//th") ?? Enumerable.Empty<HtmlNode>())
        {
            var text = HtmlEntity.DeEntitize(th.InnerText);
            if (PeriodPattern.IsMatch(text)) return text;
        }
        throw new InvalidUploadException(
            "Could not find the report period (e.g. \"วันที่ 01-Apr-2026 ถึง 30-Apr-2026\") in the uploaded file's header.");
    }

    private static (DateOnly Start, DateOnly End) ParsePeriod(string text)
    {
        var match = PeriodPattern.Match(text ?? string.Empty);
        if (!match.Success)
        {
            throw new InvalidUploadException(
                $"Could not parse the report period from \"{text}\". Expected two dates like dd-MMM-yyyy.");
        }

        if (!TryParseDate(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, out var start) ||
            !TryParseDate(match.Groups[4].Value, match.Groups[5].Value, match.Groups[6].Value, out var end))
        {
            throw new InvalidUploadException(
                $"Could not parse start/end dates from \"{text}\".");
        }

        return (start, end);
    }

    private static bool TryParseDate(string day, string month, string year, out DateOnly value)
    {
        var monthNumber = ParseMonth(month);
        if (monthNumber == 0 ||
            !int.TryParse(day, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ||
            !int.TryParse(year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            value = default;
            return false;
        }
        try
        {
            value = new DateOnly(y, monthNumber, d);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static int ParseMonth(string token)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 12)
        {
            return n;
        }

        var t = token.Trim().ToLowerInvariant();
        return t switch
        {
            "jan" or "january"   => 1,
            "feb" or "february"  => 2,
            "mar" or "march"     => 3,
            "apr" or "april"     => 4,
            "may"                => 5,
            "jun" or "june"      => 6,
            "jul" or "july"      => 7,
            "aug" or "august"    => 8,
            "sep" or "sept" or "september" => 9,
            "oct" or "october"   => 10,
            "nov" or "november"  => 11,
            "dec" or "december"  => 12,
            _ => 0
        };
    }

    private static string NormalizeText(string raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
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
        var cleaned = (raw ?? string.Empty).Trim().Replace(",", string.Empty);
        if (cleaned.Length == 0)
        {
            value = 0m;
            return false;
        }
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
