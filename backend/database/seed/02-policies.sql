SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @OrganisationId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SeededAt DATETIMEOFFSET(7) = '2026-01-01T00:00:00+00:00';

MERGE dbo.Policies AS target
USING (VALUES
    ('20000000-0000-0000-0000-000000000001', N'POL-2024-001001', N'Meridian Transport LLC',    CAST('2024-01-01T00:00:00+00:00' AS DATETIMEOFFSET(7)), CAST('2026-12-31T00:00:00+00:00' AS DATETIMEOFFSET(7)), N'Vehicle, Cargo',      N'Active'),
    ('20000000-0000-0000-0000-000000000002', N'POL-2024-001002', N'Harborview Properties Inc', CAST('2024-06-01T00:00:00+00:00' AS DATETIMEOFFSET(7)), CAST('2026-05-31T00:00:00+00:00' AS DATETIMEOFFSET(7)), N'Property, Liability', N'Expired'),
    ('20000000-0000-0000-0000-000000000003', N'POL-2025-002001', N'Coastal Builders Group',    CAST('2025-03-01T00:00:00+00:00' AS DATETIMEOFFSET(7)), CAST('2027-02-28T00:00:00+00:00' AS DATETIMEOFFSET(7)), N'Property, Equipment', N'Active'),
    ('20000000-0000-0000-0000-000000000004', N'POL-2025-002002', N'Stanton Medical Group',     CAST('2025-01-01T00:00:00+00:00' AS DATETIMEOFFSET(7)), CAST('2026-12-31T00:00:00+00:00' AS DATETIMEOFFSET(7)), N'Liability, Vehicle',  N'Active'),
    ('20000000-0000-0000-0000-000000000005', N'POL-2023-000099', N'Archived Corp',             CAST('2020-01-01T00:00:00+00:00' AS DATETIMEOFFSET(7)), CAST('2021-12-31T00:00:00+00:00' AS DATETIMEOFFSET(7)), N'Property',            N'Expired')
) AS source (Id, PolicyNumber, ClientName, EffectiveDate, ExpirationDate, CoverageTypes, Status)
ON target.PolicyNumber = source.PolicyNumber
WHEN MATCHED THEN
    UPDATE SET ClientName = source.ClientName, EffectiveDate = source.EffectiveDate, ExpirationDate = source.ExpirationDate,
               CoverageTypes = source.CoverageTypes, Status = source.Status,
               IsDeleted = 0, DeletedAt = NULL, UpdatedAt = SYSDATETIMEOFFSET()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, PolicyNumber, ClientName, EffectiveDate, ExpirationDate, Status, CoverageTypes, IsDeleted, OrganisationId, CreatedAt)
    VALUES (source.Id, source.PolicyNumber, source.ClientName, source.EffectiveDate, source.ExpirationDate, source.Status, source.CoverageTypes, 0, @OrganisationId, @SeededAt);

COMMIT TRANSACTION;
