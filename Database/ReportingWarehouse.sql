/*
    ReportingSuite ACoA warehouse deployment

    Run this script while connected to a separate warehouse database, for example:
        ReportingWarehouse

    Do not run it against the ReportingSuite operational database. Database creation,
    sizing, backup, recovery, encryption, and service-account permissions remain DBA tasks.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() = N'ReportingSuite'
    THROW 51000, 'Run this script in a separate warehouse database, not ReportingSuite.', 1;
GO

IF SCHEMA_ID(N'etl') IS NULL EXEC(N'CREATE SCHEMA [etl] AUTHORIZATION [dbo]');
IF SCHEMA_ID(N'dw') IS NULL EXEC(N'CREATE SCHEMA [dw] AUTHORIZATION [dbo]');
IF SCHEMA_ID(N'analytics') IS NULL EXEC(N'CREATE SCHEMA [analytics] AUTHORIZATION [dbo]');
GO

IF OBJECT_ID(N'[etl].[ACoAReportLoadBatch]', N'U') IS NULL
BEGIN
    CREATE TABLE [etl].[ACoAReportLoadBatch]
    (
        [LoadBatchID] bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT [PK_ACoAReportLoadBatch] PRIMARY KEY,
        [LoadRunID] uniqueidentifier NOT NULL
            CONSTRAINT [UQ_ACoAReportLoadBatch_LoadRunID] UNIQUE,
        [StartedAtUtc] datetime2(3) NOT NULL,
        [CompletedAtUtc] datetime2(3) NULL,
        [LoadStatus] varchar(20) NOT NULL,
        [RequestedBy] nvarchar(256) NULL,
        [SourceProcedure] nvarchar(256) NOT NULL,
        [SourceContractVersion] nvarchar(128) NOT NULL,
        [ReportID] bigint NOT NULL,
        [QuarterFrom] bigint NOT NULL,
        [QuarterTo] bigint NOT NULL,
        [QuarterSelection] nvarchar(max) NOT NULL,
        [YearFrom] int NOT NULL,
        [YearTo] int NOT NULL,
        [YearSelection] nvarchar(max) NOT NULL,
        [EntityType] nvarchar(4000) NOT NULL,
        [EntitySelection] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [ReportColumnSelection] nvarchar(max) NOT NULL,
        [ExtractedRowCount] int NULL,
        [LoadedRowCount] int NULL,
        [SkippedNullValueRowCount] int NULL,
        [RowCount] int NULL,
        [DurationMilliseconds] bigint NULL,
        [ErrorMessage] nvarchar(4000) NULL,
        CONSTRAINT [CK_ACoAReportLoadBatch_LoadStatus]
            CHECK ([LoadStatus] IN ('Running', 'Completed', 'Failed'))
    );
END;
GO

IF COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'ExtractedRowCount') IS NULL
    ALTER TABLE [etl].[ACoAReportLoadBatch] ADD [ExtractedRowCount] int NULL;
IF COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'LoadedRowCount') IS NULL
    ALTER TABLE [etl].[ACoAReportLoadBatch] ADD [LoadedRowCount] int NULL;
IF COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'SkippedNullValueRowCount') IS NULL
    ALTER TABLE [etl].[ACoAReportLoadBatch] ADD [SkippedNullValueRowCount] int NULL;
IF COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'RowCount') IS NULL
    ALTER TABLE [etl].[ACoAReportLoadBatch] ADD [RowCount] int NULL;
GO

IF OBJECT_ID(N'[dw].[FactACoAReportSnapshot]', N'U') IS NULL
BEGIN
    CREATE TABLE [dw].[FactACoAReportSnapshot]
    (
        [WarehouseRowID] bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT [PK_FactACoAReportSnapshot] PRIMARY KEY,
        [LoadBatchID] bigint NOT NULL,
        [Year] int NULL,
        [Quarter] nvarchar(50) NULL,
        [PeriodID] bigint NULL,
        [ReportID] bigint NULL,
        [Report_Name] nvarchar(512) NULL,
        [Report_Title] nvarchar(512) NULL,
        [Entity_GroupID] bigint NULL,
        [Report_Description] nvarchar(max) NULL,
        [Report_SubTitle] nvarchar(max) NULL,
        [Sort_Order] int NULL,
        [SectionID] bigint NULL,
        [ReportTypeID] bigint NULL,
        [ReportURL] nvarchar(2048) NULL,
        [Is_Dynamic] bit NULL,
        [LineItemTypeID] bigint NULL,
        [Is_Deleted] bit NULL,
        [Is_MulitpleEntities] bit NULL,
        [YearFrom] int NULL,
        [YearTo] int NULL,
        [QuarterFrom] int NULL,
        [QuarterTo] int NULL,
        [ReportCode] nvarchar(256) NULL,
        [Updated] bit NULL,
        [ShowMappedItems] bit NULL,
        [Report_LineItemID] bigint NULL,
        [Line_itemName] nvarchar(1024) NULL,
        [Is_Main] bit NULL,
        [Is_Title] bit NULL,
        [LineItemValue] decimal(38,10) NOT NULL,
        [Sort_Order2] int NULL,
        [Sort_Order3] int NULL,
        [Sort_Order4] int NULL,
        [Sort_Order5] int NULL,
        [EntityGroupName] nvarchar(512) NULL,
        [ColumnName] nvarchar(512) NULL,
        [EntityName] nvarchar(512) NULL,
        [LicenseNumber] nvarchar(256) NULL,
        [Status] int NULL,
        [ExtractedAtUtc] datetime2(3) NOT NULL
            CONSTRAINT [DF_FactACoAReportSnapshot_ExtractedAtUtc] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [FK_FactACoAReportSnapshot_LoadBatch]
            FOREIGN KEY ([LoadBatchID]) REFERENCES [etl].[ACoAReportLoadBatch]([LoadBatchID])
    );

    CREATE INDEX [IX_FactACoAReportSnapshot_Analytics]
        ON [dw].[FactACoAReportSnapshot]
           ([ReportID], [Year], [Quarter], [Entity_GroupID], [Status])
        INCLUDE ([Report_LineItemID], [ColumnName], [LineItemValue], [LicenseNumber]);

    CREATE INDEX [IX_FactACoAReportSnapshot_LoadBatchID]
        ON [dw].[FactACoAReportSnapshot]([LoadBatchID]);

    CREATE INDEX [IX_FactACoAReportSnapshot_Entity]
        ON [dw].[FactACoAReportSnapshot]([LicenseNumber], [Year], [Quarter]);
END;
GO

IF OBJECT_ID(N'[dw].[CK_FactACoAReportSnapshot_LineItemValue]', N'C') IS NULL
BEGIN
    ALTER TABLE [dw].[FactACoAReportSnapshot] WITH NOCHECK
        ADD CONSTRAINT [CK_FactACoAReportSnapshot_LineItemValue]
        CHECK ([LineItemValue] IS NOT NULL);
END;
GO

/*
    Shared Adhoc report warehouse

    Every source row is stored as one JSON document so reports with unrelated
    result schemas can share a single physical fact table. Column metadata is
    versioned separately and load batches provide traceability.
*/
IF OBJECT_ID(N'[etl].[AdhocReportLoadBatch]', N'U') IS NULL
BEGIN
    CREATE TABLE [etl].[AdhocReportLoadBatch]
    (
        [LoadBatchID] bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT [PK_AdhocReportLoadBatch] PRIMARY KEY,
        [LoadRunID] uniqueidentifier NOT NULL
            CONSTRAINT [UQ_AdhocReportLoadBatch_LoadRunID] UNIQUE,
        [StartedAtUtc] datetime2(3) NOT NULL,
        [CompletedAtUtc] datetime2(3) NULL,
        [LoadStatus] varchar(20) NOT NULL,
        [RequestedBy] nvarchar(256) NULL,
        [ReportID] bigint NOT NULL,
        [ReportName] nvarchar(512) NOT NULL,
        [SourceView] nvarchar(776) NOT NULL,
        [FilterColumn] nvarchar(256) NULL,
        [FilterOperator] varchar(20) NULL,
        [FilterValueFrom] nvarchar(4000) NULL,
        [FilterValueTo] nvarchar(4000) NULL,
        [SchemaVersion] int NULL,
        [SourceColumnCount] int NULL,
        [ExtractedRowCount] int NULL,
        [LoadedRowCount] int NULL,
        [DurationMilliseconds] bigint NULL,
        [ErrorMessage] nvarchar(4000) NULL,
        CONSTRAINT [CK_AdhocReportLoadBatch_LoadStatus]
            CHECK ([LoadStatus] IN ('Running', 'Completed', 'Failed')),
        CONSTRAINT [CK_AdhocReportLoadBatch_FilterOperator]
            CHECK ([FilterOperator] IS NULL OR [FilterOperator] IN
                ('between', 'equals', 'gte', 'lte', 'contains', 'starts_with', 'is_null', 'is_not_null'))
    );

    CREATE INDEX [IX_AdhocReportLoadBatch_Report]
        ON [etl].[AdhocReportLoadBatch]([ReportID], [LoadBatchID] DESC);
END;
GO

IF COL_LENGTH(N'[etl].[AdhocReportLoadBatch]', N'FilterColumn') IS NULL
    ALTER TABLE [etl].[AdhocReportLoadBatch] ADD [FilterColumn] nvarchar(256) NULL;
IF COL_LENGTH(N'[etl].[AdhocReportLoadBatch]', N'FilterOperator') IS NULL
    ALTER TABLE [etl].[AdhocReportLoadBatch] ADD [FilterOperator] varchar(20) NULL;
IF COL_LENGTH(N'[etl].[AdhocReportLoadBatch]', N'FilterValueFrom') IS NULL
    ALTER TABLE [etl].[AdhocReportLoadBatch] ADD [FilterValueFrom] nvarchar(4000) NULL;
IF COL_LENGTH(N'[etl].[AdhocReportLoadBatch]', N'FilterValueTo') IS NULL
    ALTER TABLE [etl].[AdhocReportLoadBatch] ADD [FilterValueTo] nvarchar(4000) NULL;
GO

IF OBJECT_ID(N'[etl].[CK_AdhocReportLoadBatch_FilterOperator]', N'C') IS NULL
    ALTER TABLE [etl].[AdhocReportLoadBatch] WITH CHECK
    ADD CONSTRAINT [CK_AdhocReportLoadBatch_FilterOperator]
        CHECK ([FilterOperator] IS NULL OR [FilterOperator] IN
            ('between', 'equals', 'gte', 'lte', 'contains', 'starts_with', 'is_null', 'is_not_null'));
GO

IF OBJECT_ID(N'[etl].[AdhocReportSchema]', N'U') IS NULL
BEGIN
    CREATE TABLE [etl].[AdhocReportSchema]
    (
        [ReportID] bigint NOT NULL,
        [SchemaVersion] int NOT NULL,
        [SchemaHash] varbinary(32) NOT NULL,
        [SchemaDefinition] nvarchar(max) NOT NULL,
        [FirstSeenAtUtc] datetime2(3) NOT NULL,
        [LastSeenAtUtc] datetime2(3) NOT NULL,
        [IsCurrent] bit NOT NULL,
        CONSTRAINT [PK_AdhocReportSchema]
            PRIMARY KEY ([ReportID], [SchemaVersion]),
        CONSTRAINT [UQ_AdhocReportSchema_Hash]
            UNIQUE ([ReportID], [SchemaHash]),
        CONSTRAINT [CK_AdhocReportSchema_Definition]
            CHECK (ISJSON([SchemaDefinition]) = 1)
    );

    CREATE UNIQUE INDEX [UX_AdhocReportSchema_Current]
        ON [etl].[AdhocReportSchema]([ReportID])
        WHERE [IsCurrent] = 1;
END;
GO

IF OBJECT_ID(N'[dw].[AdhocReport]', N'U') IS NULL
BEGIN
    CREATE TABLE [dw].[AdhocReport]
    (
        [WarehouseRowID] bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT [PK_AdhocReport] PRIMARY KEY,
        [LoadBatchID] bigint NOT NULL,
        [ReportID] bigint NOT NULL,
        [SchemaVersion] int NOT NULL,
        [SourceRowNumber] bigint NOT NULL,
        [BusinessKey] nvarchar(450) NULL,
        [RowPayload] nvarchar(max) NOT NULL,
        [RowHash] varbinary(32) NULL,
        [SnapshotAtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [FK_AdhocReport_LoadBatch]
            FOREIGN KEY ([LoadBatchID]) REFERENCES [etl].[AdhocReportLoadBatch]([LoadBatchID]),
        CONSTRAINT [FK_AdhocReport_Schema]
            FOREIGN KEY ([ReportID], [SchemaVersion])
            REFERENCES [etl].[AdhocReportSchema]([ReportID], [SchemaVersion]),
        CONSTRAINT [CK_AdhocReport_RowPayload]
            CHECK (ISJSON([RowPayload]) = 1)
    );

    CREATE INDEX [IX_AdhocReport_ReportBatch]
        ON [dw].[AdhocReport]([ReportID], [LoadBatchID], [SourceRowNumber]);

    CREATE INDEX [IX_AdhocReport_BusinessKey]
        ON [dw].[AdhocReport]([ReportID], [BusinessKey], [LoadBatchID]);
END;
GO

CREATE OR ALTER VIEW [analytics].[vw_AdhocReportSnapshots]
AS
    SELECT
        snapshot.[WarehouseRowID],
        snapshot.[LoadBatchID],
        snapshot.[ReportID],
        batch.[ReportName],
        batch.[SourceView],
        batch.[FilterColumn],
        batch.[FilterOperator],
        batch.[FilterValueFrom],
        batch.[FilterValueTo],
        snapshot.[SchemaVersion],
        snapshot.[SourceRowNumber],
        snapshot.[BusinessKey],
        snapshot.[RowPayload],
        snapshot.[RowHash],
        snapshot.[SnapshotAtUtc],
        batch.[LoadRunID],
        batch.[StartedAtUtc] AS [LoadStartedAtUtc],
        batch.[CompletedAtUtc] AS [LoadCompletedAtUtc],
        batch.[RequestedBy]
    FROM [dw].[AdhocReport] AS snapshot
    INNER JOIN [etl].[AdhocReportLoadBatch] AS batch
        ON batch.[LoadBatchID] = snapshot.[LoadBatchID]
    WHERE batch.[LoadStatus] = 'Completed';
GO

CREATE OR ALTER VIEW [analytics].[vw_AdhocReportCurrent]
AS
    WITH LatestCompletedBatch AS
    (
        SELECT [ReportID], MAX([LoadBatchID]) AS [LoadBatchID]
        FROM [etl].[AdhocReportLoadBatch]
        WHERE [LoadStatus] = 'Completed'
        GROUP BY [ReportID]
    )
    SELECT snapshots.*
    FROM [analytics].[vw_AdhocReportSnapshots] AS snapshots
    INNER JOIN LatestCompletedBatch AS latest
        ON latest.[ReportID] = snapshots.[ReportID]
       AND latest.[LoadBatchID] = snapshots.[LoadBatchID];
GO

CREATE OR ALTER VIEW [analytics].[vw_ACoAReportSnapshots]
AS
    SELECT
        snapshot.*,
        batch.[LoadRunID],
        batch.[StartedAtUtc] AS [LoadStartedAtUtc],
        batch.[CompletedAtUtc] AS [LoadCompletedAtUtc],
        batch.[RequestedBy],
        batch.[SourceContractVersion],
        batch.[EntitySelection],
        batch.[ReportColumnSelection]
    FROM [dw].[FactACoAReportSnapshot] AS snapshot
    INNER JOIN [etl].[ACoAReportLoadBatch] AS batch
        ON batch.[LoadBatchID] = snapshot.[LoadBatchID]
    WHERE batch.[LoadStatus] = 'Completed'
      AND snapshot.[LineItemValue] IS NOT NULL;
GO

CREATE OR ALTER VIEW [analytics].[vw_ACoAReportCurrent]
AS
    WITH LatestRows AS
    (
        SELECT
            snapshot.[WarehouseRowID],
            ROW_NUMBER() OVER
            (
                PARTITION BY
                    snapshot.[ReportID],
                    snapshot.[Year],
                    snapshot.[Quarter],
                    snapshot.[Entity_GroupID],
                    COALESCE(snapshot.[LicenseNumber], snapshot.[EntityName], N''),
                    snapshot.[Report_LineItemID],
                    snapshot.[ColumnName],
                    snapshot.[Status]
                ORDER BY snapshot.[LoadBatchID] DESC, snapshot.[WarehouseRowID] DESC
            ) AS [CurrentRowNumber]
        FROM [dw].[FactACoAReportSnapshot] AS snapshot
        INNER JOIN [etl].[ACoAReportLoadBatch] AS batch
            ON batch.[LoadBatchID] = snapshot.[LoadBatchID]
        WHERE batch.[LoadStatus] = 'Completed'
    )
    SELECT snapshots.*
    FROM [analytics].[vw_ACoAReportSnapshots] AS snapshots
    INNER JOIN LatestRows
        ON LatestRows.[WarehouseRowID] = snapshots.[WarehouseRowID]
    WHERE LatestRows.[CurrentRowNumber] = 1;
GO

IF DATABASE_PRINCIPAL_ID(N'ReportingWarehouseLoader') IS NULL
    EXEC(N'CREATE ROLE [ReportingWarehouseLoader] AUTHORIZATION [dbo]');
ELSE IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE principal_id = DATABASE_PRINCIPAL_ID(N'ReportingWarehouseLoader')
      AND [type] = N'R'
)
    THROW 51001, 'ReportingWarehouseLoader exists but is not a database role.', 1;

IF DATABASE_PRINCIPAL_ID(N'ReportingWarehouseReader') IS NULL
    EXEC(N'CREATE ROLE [ReportingWarehouseReader] AUTHORIZATION [dbo]');
ELSE IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE principal_id = DATABASE_PRINCIPAL_ID(N'ReportingWarehouseReader')
      AND [type] = N'R'
)
    THROW 51002, 'ReportingWarehouseReader exists but is not a database role.', 1;
GO

GRANT SELECT, INSERT, UPDATE ON [etl].[ACoAReportLoadBatch] TO [ReportingWarehouseLoader];
GRANT INSERT ON [dw].[FactACoAReportSnapshot] TO [ReportingWarehouseLoader];
GRANT SELECT, INSERT, UPDATE ON [etl].[AdhocReportLoadBatch] TO [ReportingWarehouseLoader];
GRANT SELECT, INSERT, UPDATE ON [etl].[AdhocReportSchema] TO [ReportingWarehouseLoader];
-- SqlBulkCopy reads destination metadata before sending rows, so the loader
-- requires SELECT as well as INSERT. CheckConstraints keeps the fact-table
-- checks active without granting ALTER TABLE or bulk-administrator rights.
GRANT SELECT, INSERT ON [dw].[AdhocReport] TO [ReportingWarehouseLoader];
GRANT VIEW DEFINITION ON [analytics].[vw_AdhocReportSnapshots] TO [ReportingWarehouseLoader];
GRANT VIEW DEFINITION ON [analytics].[vw_AdhocReportCurrent] TO [ReportingWarehouseLoader];
GRANT SELECT ON SCHEMA::[analytics] TO [ReportingWarehouseReader];
GO

/*
    After a DBA creates the SQL Server login and database user, add only that
    database user to the loader role. For a remote SQL Server and an IIS
    ApplicationPoolIdentity, the Windows login is normally the web server's
    domain machine account (DOMAIN\WebServer$), not IIS APPPOOL\PoolName.

    Example (replace the placeholder with the principal reported by the page):

        CREATE USER [DOMAIN\WebServer$] FOR LOGIN [DOMAIN\WebServer$];
        ALTER ROLE [ReportingWarehouseLoader] ADD MEMBER [DOMAIN\WebServer$];

    Power BI and other analytical tools should receive only ReportingWarehouseReader.
*/
