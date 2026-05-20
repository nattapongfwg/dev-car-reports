USE [DEV_HRDB]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER VIEW [dbo].[v_employee_vehicle_owner]
AS
    with slotted as (
        select
            ev.employee_id,
            ltrim(rtrim(ev.license_plate)) as plate,
            ev.vehicle_type,
            ev.card_type,
            row_number() over (
                partition by ev.employee_id, ev.vehicle_type, ev.card_type
                order by ev.license_plate
            ) as slot
        from dbo.employee_vehicles ev
        where ev.is_active = 'Y'
    )
    select
        e.employee_code,
        e.full_name_th,
        count(*)                                                                            as total_vehicles,
        max(case when s.vehicle_type='C' and s.card_type='M' and s.slot=1 then s.plate end) as [C-M 1],
        max(case when s.vehicle_type='C' and s.card_type='M' and s.slot=2 then s.plate end) as [C-M 2],
        max(case when s.vehicle_type='C' and s.card_type='M' and s.slot=3 then s.plate end) as [C-M 3],
        max(case when s.vehicle_type='C' and s.card_type='M' and s.slot=4 then s.plate end) as [C-M 4],
        max(case when s.vehicle_type='M' and s.card_type='M' and s.slot=1 then s.plate end) as [M-M 1],
        max(case when s.vehicle_type='M' and s.card_type='M' and s.slot=2 then s.plate end) as [M-M 2],
        max(case when s.vehicle_type='M' and s.card_type='M' and s.slot=3 then s.plate end) as [M-M 3],
        max(case when s.vehicle_type='C' and s.card_type='H' and s.slot=1 then s.plate end) as [C-H 1],
        max(case when s.vehicle_type='C' and s.card_type='H' and s.slot=2 then s.plate end) as [C-H 2],
        max(case when s.vehicle_type='C' and s.card_type='H' and s.slot=3 then s.plate end) as [C-H 3],
        max(case when s.vehicle_type='M' and s.card_type='H' and s.slot=1 then s.plate end) as [M-H 1],
        max(case when s.vehicle_type='M' and s.card_type='H' and s.slot=2 then s.plate end) as [M-H 2],
        max(case when s.vehicle_type='M' and s.card_type='H' and s.slot=3 then s.plate end) as [M-H 3]
    from dbo.employees e
    join slotted s on s.employee_id = e.id
    group by e.employee_code, e.full_name_th;
GO
