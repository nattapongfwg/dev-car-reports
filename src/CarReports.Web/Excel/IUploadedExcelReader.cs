using CarReports.Web.Models;

namespace CarReports.Web.Excel;

public interface IUploadedExcelReader
{
    IReadOnlyList<StampUsageRow> Read(Stream uploadStream, string fileName);
}
