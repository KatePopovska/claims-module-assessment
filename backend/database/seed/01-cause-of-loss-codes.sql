SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @OrganisationId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SeededAt DATETIMEOFFSET(7) = '2026-01-01T00:00:00+00:00';

MERGE dbo.CauseOfLossCodes AS target
USING (VALUES
    ('10000000-0000-0000-0000-000000000001', N'COL-FIRE',     N'Fire',                  N'Property',  1),
    ('10000000-0000-0000-0000-000000000002', N'COL-FLOOD',    N'Flood',                 N'Weather',   2),
    ('10000000-0000-0000-0000-000000000003', N'COL-THEFT',    N'Theft',                 N'Crime',     3),
    ('10000000-0000-0000-0000-000000000004', N'COL-VEH-COL',  N'Vehicle Collision',     N'Auto',      4),
    ('10000000-0000-0000-0000-000000000005', N'COL-VEH-COMP', N'Vehicle Comprehensive', N'Auto',      5),
    ('10000000-0000-0000-0000-000000000006', N'COL-LIAB',     N'Third Party Liability', N'Liability', 6),
    ('10000000-0000-0000-0000-000000000007', N'COL-EQUIP',    N'Equipment Breakdown',   N'Equipment', 7),
    ('10000000-0000-0000-0000-000000000008', N'COL-WIND',     N'Wind / Storm',          N'Weather',   8),
    ('10000000-0000-0000-0000-000000000009', N'COL-INJURY',   N'Bodily Injury',         N'Liability', 9),
    ('10000000-0000-0000-0000-000000000010', N'COL-OTHER',    N'Other / Unknown',       N'General',   10)
) AS source (Id, Code, Name, PerilCategory, SortOrder)
ON target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET Name = source.Name, PerilCategory = source.PerilCategory, SortOrder = source.SortOrder,
               IsActive = 1, IsDeleted = 0, DeletedAt = NULL, UpdatedAt = SYSDATETIMEOFFSET()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Code, Name, PerilCategory, IsActive, SortOrder, IsDeleted, OrganisationId, CreatedAt)
    VALUES (source.Id, source.Code, source.Name, source.PerilCategory, 1, source.SortOrder, 0, @OrganisationId, @SeededAt);

COMMIT TRANSACTION;
