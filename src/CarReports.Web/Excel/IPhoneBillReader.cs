using CarReports.Web.Models;

namespace CarReports.Web.Excel;

public interface IPhoneBillReader
{
    IReadOnlyList<PhoneBill> Read(Stream uploadStream, string fileName);
}
