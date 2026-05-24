USE [DEV_HRDB]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- New normalised table for employee business phone numbers.
-- One row per (employee, phone). Replaces the comma-delimited employees.business_phone column
-- so an employee can hold an arbitrary number of business numbers.
--
-- Conventions follow employee_vehicles:
--   * id: uniqueidentifier PK (NEWID default)
--   * employee_id: nvarchar(50) holding employees.id (no enforced FK, matches existing tables)
--   * is_active: char(1) Y/N
--   * created_by / updated_by: nvarchar(50); seed rows use 'system'
IF OBJECT_ID('dbo.employee_business_phone', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.employee_business_phone
    (
        id            uniqueidentifier NOT NULL CONSTRAINT DF_employee_business_phone_id DEFAULT (NEWID()),
        employee_id   nvarchar(50)     NOT NULL,
        phone_no      nvarchar(50)     NOT NULL,
        is_active     char(1)          NOT NULL CONSTRAINT DF_employee_business_phone_is_active DEFAULT ('Y'),
        created_date  datetime2        NOT NULL CONSTRAINT DF_employee_business_phone_created_date DEFAULT (SYSDATETIME()),
        created_by    nvarchar(50)     NOT NULL,
        updated_date  datetime2        NOT NULL CONSTRAINT DF_employee_business_phone_updated_date DEFAULT (SYSDATETIME()),
        updated_by    nvarchar(50)     NOT NULL,
        CONSTRAINT PK_employee_business_phone PRIMARY KEY NONCLUSTERED (id)
    );

    CREATE INDEX IX_employee_business_phone_employee_id
        ON dbo.employee_business_phone (employee_id)
        INCLUDE (phone_no, is_active);
END
GO

-- Migration: copy employees.business_phone into the new table.
-- Splits on ',' so multi-phone rows (e.g. employee 0468 = '0859800919,0891719955')
-- produce one row per phone. Trims whitespace and skips empty fragments.
IF NOT EXISTS (SELECT 1 FROM dbo.employee_business_phone)
BEGIN
    INSERT INTO dbo.employee_business_phone (employee_id, phone_no, is_active, created_by, updated_by)
    SELECT
        e.id,
        LTRIM(RTRIM(s.value)) AS phone_no,
        'Y',
        'system',
        'system'
    FROM dbo.employees e
    CROSS APPLY STRING_SPLIT(ISNULL(e.business_phone, ''), ',') s
    WHERE LTRIM(RTRIM(s.value)) <> '';
END
GO
