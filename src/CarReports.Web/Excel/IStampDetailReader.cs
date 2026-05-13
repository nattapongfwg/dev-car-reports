using CarReports.Web.Models;

namespace CarReports.Web.Excel;

public interface IStampDetailReader
{
    StampReportDetails Read(Stream uploadStream, string fileName);
}
