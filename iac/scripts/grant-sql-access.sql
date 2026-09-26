IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(WebAppName)')
BEGIN
    CREATE USER [$(WebAppName)] FROM EXTERNAL PROVIDER WITH OBJECT_ID = '$(WebAppPrincipalId)';
END;

ALTER ROLE db_datareader ADD MEMBER [$(WebAppName)];
ALTER ROLE db_datawriter ADD MEMBER [$(WebAppName)];
ALTER ROLE db_ddladmin ADD MEMBER [$(WebAppName)];
GO

SELECT dp.name AS database_user, dp.type_desc AS principal_type, r.name AS role_name
FROM sys.database_role_members AS rm
JOIN sys.database_principals AS dp ON dp.principal_id = rm.member_principal_id
JOIN sys.database_principals AS r ON r.principal_id = rm.role_principal_id
WHERE dp.name = N'$(WebAppName)';
GO
