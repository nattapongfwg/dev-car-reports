using CarReports.Web.Models;

namespace CarReports.Web.Excel;

public interface IReportWorkbookBuilder
{
    byte[] Build(ReportData data);
}
