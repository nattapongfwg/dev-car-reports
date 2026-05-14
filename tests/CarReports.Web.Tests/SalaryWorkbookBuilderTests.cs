using CarReports.Web.Excel;
using CarReports.Web.Models;
using Xunit;

namespace CarReports.Web.Tests;

public sealed class SalaryWorkbookBuilderTests
{
    [Fact]
    public void Build_WithNoRows_ProducesValidXlsx()
    {
        var builder = new SalaryWorkbookBuilder();
        var data = new SalaryReportData(
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 30),
            Array.Empty<SalaryDeductionRow>());

        var bytes = builder.Build(data);

        Assert.NotEmpty(bytes);
        // xlsx is a ZIP container: first four bytes must be the local-file-header signature.
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, bytes.AsSpan(0, 4).ToArray());
    }
}
