using CarReports.Web.Models;

namespace CarReports.Web.Excel;

public interface ISalaryWorkbookBuilder
{
    byte[] Build(SalaryReportData data);
}
