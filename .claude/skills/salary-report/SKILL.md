---
name: salary-report
description: Context and conventions for the Employee Salary Excel report flow (Pages/Salary.cshtml → SalaryReportService → SalaryWorkbookBuilder). Use when continuing work on the salary deduction workbook, the phone-bill merge, the per-employee 400 baht subsidy spillover, or the m_cfg_lov-driven monthly parking fees.
---

# Employee Salary report — working notes

Last session ended after wiring the 400-baht subsidy spillover (per-employee, Car-row → Motorcycle-row) into the workbook formulas.

## Entry points

- **UI**: `src/CarReports.Web/Pages/Salary.cshtml` (Razor page, two file uploads: vehicle stamps `Report.xlsx` and phone bill `Report_Phone.xlsx`).
- **Service**: `src/CarReports.Web/Services/SalaryReportService.cs` — orchestrates readers → repo → builder.
- **Workbook**: `src/CarReports.Web/Excel/SalaryWorkbookBuilder.cs` — writes the `Report_Salary` sheet.
- **Phone-bill reader**: `src/CarReports.Web/Excel/PhoneBillReader.cs` — Column F = ID (`employees.employee_code`), Column D = `หมายเลข`, N = excess, O = service.

## Data flow

1. `StampDetailReader.Read` parses `Report.xlsx` → period + per-vehicle stamp rows (all `card_type='H'`).
2. `PhoneBillReader.Read` parses `Report_Phone.xlsx` → `PhoneBill(employee_code, phone_no, excess, service)`, summed per `(employee, phone)`.
3. `ISalaryRepository.GetMappingsAsync` returns `v_employee_vehicle_mapping` rows (one per `(employee, vehicle_type, card_type)` — note: card_type is now selected, see `SalaryRepository.SelectColumns`).
4. `ISalaryRepository.GetVehicleMonthlyFeesAsync` returns `m_cfg_lov.lov_val1` keyed by `lov_code` for `lov_type='VEHICLE_TYPE'` (today: `C=1500`, `M=150`). `lov_val1` is `nvarchar` — parsed to decimal in C#, codes that don't parse are dropped, and a warning logs if `C` or `M` is missing.
5. `BuildVehicleRows` groups mappings by `(FullNameTh, VehicleType)` so an employee's H+M cards of the same vehicle type collapse into **one** row. Per group:
   - `HourlyTotal` = stamp sum from `Report.xlsx` for `(vehicle_type, name)`, else 0.
   - `MonthlyAmount` = fee from step 4 **only if** any group member has `CardType="M"`.
   - Skip the row when both are 0.
6. `MergePhoneBillsAsync` emits one standalone phone row per non-zero `(employee, phone)` bill, **and** calls `IBusinessPhoneRepository.EnsurePhoneAsync` to register any new business phone in `dbo.employee_business_phone` with `created_by=updated_by='sys_emp_salary'`.
7. Sorting (Thai collation): name → vehicle-rows-before-phone → `C` before `M` → phone number.

So one employee produces at most **2 vehicle rows** (C and M) plus N phone rows.

## Workbook layout (sheet `Report_Salary`)

| Col | Letter | Header | Source / formula |
|---|---|---|---|
| 1 | A | ID | `EmployeeCode` |
| 2 | B | ชื่อ - นามสกุล | `FullNameThName` |
| 3 | C | Salary Code | mapping |
| 4 | D | Company | mapping |
| 5 | E | Department | mapping |
| 6 | F | Section | mapping |
| 7 | G | Cost Center | mapping |
| 8 | H | Vehicle Type | `C → Car`, `M → Motorcycle`, null → blank |
| 9 | I | Phone No. | `PhoneBill.PhoneNo` (only on phone rows) |
| 10 | J | หมายเหตุ (phone) | blank |
| 11 | K | ค่าโทรเกิน | `PhoneExcess` |
| 12 | L | บริการเสริม | `PhoneService` |
| 13 | M | รวม (phone) | `=K+L` |
| 14 | N | ค่าจอดรถรายเดือน | `MonthlyAmount` when > 0 (else blank) |
| 15 | O | ลุมพินี ทาวเวอร์ | `HourlyTotal` |
| 16 | P | True Digital Park | blank (manual) |
| 17 | Q | รวม (hourly group) | `=N+O+P` |
| 18 | R | หักช่วยเหลือ 400 | **spillover-aware** (see below) |
| 19 | S | คงเหลือ | `=IF(R<0,0,R)` |
| 20 | T | ค่าโทรศัพท์ (deduct) | `=M` |
| 21 | U | ค่าที่จอดรถ | `=S` (was `=S+N`; N is now inside R) |
| 22 | V | รวม (deduct) | `=T+U` |
| 23 | W | หมายเหตุ | blank |
| 24 | X | Email | mapping |
| 25 | Y | Summary phone | `=T` |
| 26 | Z | Summary park | `=O` (raw hourly, was `=U`) |
| 27 | AA | Summary total | `=V` |
| 28 | AB | Payroll Code | mapping |

### 400 baht subsidy spillover (R formula)

Subsidy is **per employee**, consumed first by the Car row, leftover spills to the Motorcycle row.

- **C row or any standalone row**: `R = O + N - 400`
- **M row with a paired C row at row `c`**: `R = O + N - MAX(0, 400 - O{c} - N{c})`

Worked examples (pinned in `SalaryWorkbookBuilderTests`):

- C-H=70, C-M=1100, M-H=10, M-M=150 → C.R=770 (full 400 consumed), M.R=160 (no leftover).
- C-H=10, C-M=200, M-H=20, M-M=150 → C.R=−190, M.R=−20 (leftover 190 spills to M).

`pairedCarRow` is computed up-front in `Build()` by indexing each employee's C row position, then passed into `WriteDataRow(sheet, r, row, pairedCarRow)`.

## DB references

- View `v_employee_vehicle_mapping` — joined `employees` + `employee_vehicles` (grouped by card_type) + `employee_business_phone`. **Note**: the view emits one row per `(employee, vehicle_type, card_type)`, so the C# side must consolidate. See `samples/queries/v_employee_vehicle_mapping.sql`.
- Table `dbo.employee_business_phone` — new normalised table, one row per `(employee, phone)`. Schema in `samples/queries/employee_business_phone.sql`. `employee_id` column stores `employees.id` (the GUID-as-nvarchar), **not** `employee_code`. The repository handles the join via `SELECT TOP 1 e.id FROM dbo.employees WHERE e.employee_code = @code`.
- Table `dbo.m_cfg_lov` — fees in `lov_val1` (nvarchar) under `lov_type='VEHICLE_TYPE'`.

## Logging

Serilog → console + rolling file at `{contentRoot}/logs/carreports-YYYYMMDD.log` (30-day retention). Warnings to grep for:

- `"no salary row and no DB match"` — employee_code from Column F not in the salary view (likely not in `employees` either).
- `"not in dbo.employees"` — defense-in-depth from `BusinessPhoneRepository.EnsurePhoneAsync` (theoretically unreachable today because the upstream check fires first).
- `"m_cfg_lov has no VEHICLE_TYPE row for code C/M"` — missing fee config; the row is treated as 0.
- `"Registered new business phone {PhoneNo} for ID {EmployeeCode}"` — info-level, fires when `EnsurePhoneAsync` inserts a new row.

`appsettings.json` sets Serilog default level to `Information`; check the `Serilog` section if a warning isn't showing up.

## Known open threads (potential next-session work)

- **Header labels stale**: R still says "หักช่วยเหลือ 400" and Z says "ค่าจอดรถนอกเหนือจากรายเดือน" — both technically still accurate but Z = raw O, no longer derived from U.
- **M-only employees with no C row** currently get the full 400 subsidy themselves (formula falls back to `=O+N-400`). The spec didn't address this case explicitly — confirm desired behavior if asked.
- **Phone-only rows write `R = -400`** because O=0, N=blank. Cosmetic only — S floors to 0 so no downstream effect. Was pre-existing.
- **U is now `=S`** (just floor-at-0 of the combined deduction). The sub-header label "(หักสวัสดิการ 400)" is now redundant — could simplify if user wants.

## Operational gotcha

When the local web app is running (e.g. PID 6608 last session), `CarReports.Web.exe` is locked and `dotnet build` fails with `MSB3027`. Ask the user to stop the app before building — do **not** kill the process unilaterally. Tests can run via `dotnet test --no-build` if you've already built once.

## Verification recipe

```
'/mnt/c/Program Files/dotnet/dotnet.exe' build CarReports.slnx -nologo -v minimal
'/mnt/c/Program Files/dotnet/dotnet.exe' test  CarReports.slnx --nologo --no-build -v minimal
```

(WSL setup — `dotnet` CLI is on the Windows side, not Linux.)

End state at last session: **11/11 tests green.**
