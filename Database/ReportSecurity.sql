/*
    Reporting Suite report security baseline
    ----------------------------------------
    Uses the existing dbo.ReportRoles and dbo.RoleTypes tables.
    A report with no ReportRoles rows remains available to every authenticated user.
    Once one or more rows exist, access is limited to matching AD users/groups;
    Reporting Suite administrators retain access through the application.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ReportRoles', N'U') IS NULL OR OBJECT_ID(N'dbo.RoleTypes', N'U') IS NULL
BEGIN
    THROW 50001, 'dbo.ReportRoles and dbo.RoleTypes must exist before applying report security.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RoleTypes
    WHERE LOWER(LTRIM(RTRIM(RoleTypeName))) = 'active directory user'
)
BEGIN
    INSERT dbo.RoleTypes (RoleTypeName) VALUES ('Active Directory User');
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.RoleTypes
    WHERE LOWER(LTRIM(RTRIM(RoleTypeName))) = 'active directory group'
)
BEGIN
    INSERT dbo.RoleTypes (RoleTypeName) VALUES ('Active Directory Group');
END;

;WITH DuplicateRules AS
(
    SELECT ReportRoleID,
           ROW_NUMBER() OVER
           (
               PARTITION BY ReportID, RoleTypeID, UPPER(LTRIM(RTRIM(Grantee)))
               ORDER BY ReportRoleID
           ) AS DuplicateNumber
    FROM dbo.ReportRoles
    WHERE ReportID IS NOT NULL
      AND RoleTypeID IS NOT NULL
      AND NULLIF(LTRIM(RTRIM(Grantee)), '') IS NOT NULL
)
DELETE FROM DuplicateRules WHERE DuplicateNumber > 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.ReportRoles')
      AND name = N'UX_ReportRoles_Report_RoleType_Grantee'
)
BEGIN
    CREATE UNIQUE INDEX UX_ReportRoles_Report_RoleType_Grantee
        ON dbo.ReportRoles (ReportID, RoleTypeID, Grantee)
        WHERE ReportID IS NOT NULL
          AND RoleTypeID IS NOT NULL
          AND Grantee IS NOT NULL;
END;

COMMIT TRANSACTION;
