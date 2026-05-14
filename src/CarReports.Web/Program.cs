using CarReports.Web.Data;
using CarReports.Web.Excel;
using CarReports.Web.Services;
using Serilog;

var contentRoot = AppContext.BaseDirectory;
var logDirectory = Path.Combine(contentRoot, "logs");
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logDirectory, "carreports-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = contentRoot
    });

    builder.Host.UseWindowsService(options => options.ServiceName = "CarReports");
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(logDirectory, "carreports-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30));

    var uploadMaxBytes = builder.Configuration.GetValue<long?>("Upload:MaxBytes") ?? 52_428_800L;

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = uploadMaxBytes;
    });

    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = uploadMaxBytes;
    });

    builder.Services.AddRazorPages();
    builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
    builder.Services.AddSingleton<IUploadCache, UploadCache>();
    builder.Services.AddScoped<ICarRepository, CarRepository>();
    builder.Services.AddScoped<IUploadedExcelReader, UploadedExcelReader>();
    builder.Services.AddScoped<IStampDetailReader, StampDetailReader>();
    builder.Services.AddScoped<IPhoneBillReader, PhoneBillReader>();
    builder.Services.AddScoped<IReportWorkbookBuilder, ReportWorkbookBuilder>();
    builder.Services.AddScoped<ISalaryWorkbookBuilder, SalaryWorkbookBuilder>();
    builder.Services.AddScoped<ISalaryRepository, SalaryRepository>();
    builder.Services.AddScoped<IExcelReportService, ExcelReportService>();
    builder.Services.AddScoped<ISalaryReportService, SalaryReportService>();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.MapRazorPages();

    Log.Information("CarReports starting on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CarReports terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
