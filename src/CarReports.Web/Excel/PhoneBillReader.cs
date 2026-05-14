using System.Globalization;
using CarReports.Web.Models;
using ClosedXML.Excel;

namespace CarReports.Web.Excel;

public sealed class PhoneBillReader : IPhoneBillReader
{
    private const int IdColumn = 2;
    private const int ExcessColumn = 3;
    private const int ServiceColumn = 4;

    public IReadOnlyList<PhoneBill> Read(Stream uploadStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension != ".xlsx")
        {
            throw new InvalidUploadException(
                $"Phone report must be .xlsx (got \"{extension}\").");
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(uploadStream);
        }
        catch (Exception ex)
        {
            throw new InvalidUploadException("The uploaded phone report is not a valid .xlsx workbook.", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidUploadException("The phone report has no worksheets.");

            var totals = new Dictionary<string, (decimal Excess, decimal Service)>(StringComparer.OrdinalIgnoreCase);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = sheet.Row(rowNumber);
                if (row.IsEmpty()) continue;

                var employeeCode = row.Cell(IdColumn).GetString().Trim();
                if (employeeCode.Length == 0) continue;

                var excess = ReadDecimal(row.Cell(ExcessColumn).GetString());
                var service = ReadDecimal(row.Cell(ServiceColumn).GetString());

                if (totals.TryGetValue(employeeCode, out var existing))
                {
                    totals[employeeCode] = (existing.Excess + excess, existing.Service + service);
                }
                else
                {
                    totals[employeeCode] = (excess, service);
                }
            }

            if (totals.Count == 0)
            {
                throw new InvalidUploadException("The phone report has no parseable rows.");
            }

            return totals
                .Select(kvp => new PhoneBill(kvp.Key, kvp.Value.Excess, kvp.Value.Service))
                .ToList();
        }
    }

    private static decimal ReadDecimal(string raw)
    {
        var cleaned = (raw ?? string.Empty).Trim().Replace(",", string.Empty);
        if (cleaned.Length == 0) return 0m;
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }
}
