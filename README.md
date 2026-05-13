# Car Reports

Small internal web app: upload an `.xlsx`, the server joins it against SQL Server, and returns a generated `.xlsx` report.

- **Runtime**: .NET 10 (LTS)
- **Web**: ASP.NET Core Razor Pages (SSR)
- **Excel**: ClosedXML (MIT)
- **Data**: Dapper + Microsoft.Data.SqlClient
- **Host**: Kestrel as a Windows Service (no IIS required)

## Develop locally

```powershell
dotnet build
dotnet run --project src\CarReports.Web
```

Open <http://localhost:5000>.

The dev connection string lives in `src\CarReports.Web\appsettings.Development.json` (Trusted Connection to `(local)` by default). Adjust it for your dev DB.

## Build a deployable artifact

```powershell
dotnet publish src\CarReports.Web -c Release -o publish
```

Produces a self-contained, single-file Windows x64 build in `publish\`. The .NET runtime is bundled — the client computer does **not** need .NET installed.

## Deploy to a client computer

1. Copy the `publish\` folder to, e.g., `C:\Program Files\CarReports\`.
2. Edit `appsettings.Production.json`:
   - `ConnectionStrings:CarReports` — set the SQL Server connection string for that client.
   - `Kestrel:Endpoints:Http:Url` — change the port if `5000` is taken.
3. Open PowerShell **as Administrator** in the install folder and run:
   ```powershell
   .\install-service.ps1
   ```
4. Browse to <http://localhost:5000>.

To remove: `.\uninstall-service.ps1`.

### Service account & SQL access

By default the service runs as `LocalSystem`. If SQL Server requires a domain login, change the service "Log On" account in `services.msc`, or put SQL credentials directly in the connection string.

### Logs

Daily rolling log files are written to `logs\carreports-YYYYMMDD.log` **next to the .exe** (anchored to `AppContext.BaseDirectory`, so they don't end up in `C:\Windows\System32` when running as a service).

## Project layout

```
src/CarReports.Web/
├── Pages/         Razor Pages (HTTP layer only)
├── Services/      Use-case orchestration (no HTTP, no SQL, no Excel API leaks)
├── Excel/         ClosedXML reader and workbook builder
├── Data/          Dapper repository + SqlConnection factory
├── Models/        Immutable record DTOs
└── Program.cs     DI, Serilog, Kestrel, Windows Service host
```

## Customising for your schema

The repo ships with a sample schema (`dbo.Cars` with VIN, Make, Model, etc.) and a sample upload format (VIN / Plate Number / Report Date). To adapt to your real tables:

- `src/CarReports.Web/Data/CarRepository.cs` — change the SQL and column mappings.
- `src/CarReports.Web/Models/CarDetail.cs` — match the record shape to the SQL result.
- `src/CarReports.Web/Excel/UploadedExcelReader.cs` — change `RequiredHeaders` and the cell index reads.
- `src/CarReports.Web/Models/UploadedRow.cs` — match the uploaded row shape.
- `src/CarReports.Web/Excel/ReportWorkbookBuilder.cs` — adjust the three sheets (Summary / Detail / Missing) to your reporting needs.
