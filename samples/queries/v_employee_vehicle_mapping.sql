USE [DEV_HRDB]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Long-format mapping of (employee, vehicle_type, card_type) buckets, plus phone-only employees.
-- A row appears when the employee has at least one active vehicle OR a non-empty business_phone.
-- Phone-only employees have NULL vehicle_type / card_type / license_plates / vehicle_count.
CREATE OR ALTER VIEW [dbo].[v_employee_vehicle_mapping]
AS
    with deduped as (
        select distinct
            ev.employee_id,
            ev.vehicle_type,
            ev.card_type,
            ltrim(rtrim(ev.license_plate)) as plate
        from dbo.employee_vehicles ev
        where ev.is_active = 'Y'
    ),
    vehicle_groups as (
        select
            employee_id,
            vehicle_type,
            card_type,
            count(*)                  as vehicle_count,
            string_agg(plate, ', ')   as license_plates
        from deduped
        group by employee_id, vehicle_type, card_type
    )
    select
        e.id                                                        as employee_id,
        e.employee_code,
        e.full_name_th,
        e.full_name_en,
        lov_p.lov_val1 + ' ' + e.full_name_th                       as full_name_th_name,
        e.salary_code,
        c.company_name,
        dep.department_name,
        s.section_name,
        s.cost_center,
        e.email,
        nullif(ltrim(rtrim(e.business_phone)), '')                  as business_phone,
        vg.vehicle_type,
        lov_vt.lov_name                                             as vehicle_type_name,
        vg.card_type,
        lov_ct.lov_name                                             as card_type_name,
        vg.vehicle_count,
        vg.license_plates,
        e.payroll_code
    from dbo.employees e
    left join vehicle_groups            vg  on vg.employee_id = e.id
    left join dbo.m_departments         dep on dep.id         = e.department_id
    left join dbo.m_departments_section s   on s.id           = e.section_id
    left join dbo.m_company             c   on c.id           = dep.company_id
    left join dbo.m_cfg_lov lov_p  on lov_p.lov_type  = 'EMPLOYEE_PREFIX'    and lov_p.lov_code  = e.prefix
    left join dbo.m_cfg_lov lov_ct on lov_ct.lov_type = 'VEHICLE_CARD_TYPE'  and lov_ct.lov_code = vg.card_type
    left join dbo.m_cfg_lov lov_vt on lov_vt.lov_type = 'VEHICLE_TYPE'       and lov_vt.lov_code = vg.vehicle_type
    where vg.employee_id is not null
       or nullif(ltrim(rtrim(e.business_phone)), '') is not null;
GO
