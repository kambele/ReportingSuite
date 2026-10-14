USE [VSAB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    Run this deployment script once, before zPreDataTransfer and
    zPostDataTransfer are used by the workflow.

    Archive tables are deliberately provisioned outside the workflow stored
    procedures so that workflow execution performs data operations only.
    Existing archive tables and data are never dropped by this script.
*/
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ArchiveMappings TABLE
    (
        MappingId int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        ProfileTable sysname NOT NULL,
        ArchiveTable sysname NOT NULL,
        IndexName sysname NOT NULL
    );

    INSERT INTO @ArchiveMappings (ProfileTable, ArchiveTable, IndexName)
    VALUES
        (N'group_agentsindividual_repeat_group', N'z_archieve_group_agentsindividual_repeat_group', N'IX_z_archieve_agentsindividual_correlation'),
        (N'group_agentscompany_repeat_group', N'z_archieve_group_agentscompany_repeat_group', N'IX_z_archieve_agentscompany_correlation'),
        (N'group_brokersindividual_repeat_group', N'z_archieve_group_brokersindividual_repeat_group', N'IX_z_archieve_brokersindividual_correlation');

    DECLARE @mapping_id int = 1;
    DECLARE @mapping_count int = (SELECT COUNT(*) FROM @ArchiveMappings);
    DECLARE @profile_table sysname;
    DECLARE @archive_table sysname;
    DECLARE @index_name sysname;
    DECLARE @profile_object_name nvarchar(517);
    DECLARE @archive_object_name nvarchar(517);
    DECLARE @profile_object_id int;
    DECLARE @archive_object_id int;
    DECLARE @sql nvarchar(max);
    DECLARE @error_message nvarchar(2048);

    WHILE @mapping_id <= @mapping_count
    BEGIN
        SELECT
            @profile_table = ProfileTable,
            @archive_table = ArchiveTable,
            @index_name = IndexName
        FROM @ArchiveMappings
        WHERE MappingId = @mapping_id;

        SET @profile_object_name = QUOTENAME(N'vsabuser') + N'.' + QUOTENAME(@profile_table);
        SET @archive_object_name = QUOTENAME(N'vsabuser') + N'.' + QUOTENAME(@archive_table);
        SET @profile_object_id = OBJECT_ID(@profile_object_name, N'U');

        IF @profile_object_id IS NULL
        BEGIN
            SET @error_message = N'Required profile table does not exist: ' + @profile_object_name;
            THROW 51200, @error_message, 1;
        END;

        IF OBJECT_ID(@archive_object_name) IS NOT NULL
           AND OBJECT_ID(@archive_object_name, N'U') IS NULL
        BEGIN
            SET @error_message = N'An object already uses the required archive name: ' + @archive_object_name;
            THROW 51201, @error_message, 1;
        END;

        IF OBJECT_ID(@archive_object_name, N'U') IS NULL
        BEGIN
            SET @sql =
                N'SELECT TOP (0) profile_row.*
                  INTO ' + @archive_object_name + N'
                  FROM ' + @profile_object_name + N' AS profile_row;';
            EXEC sys.sp_executesql @sql;
        END;

        SET @archive_object_id = OBJECT_ID(@archive_object_name, N'U');

        /* Never silently archive a partial profile row. */
        IF EXISTS
        (
            SELECT 1
            FROM sys.columns AS profile_column
            WHERE profile_column.object_id = @profile_object_id
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM sys.columns AS archive_column
                  WHERE archive_column.object_id = @archive_object_id
                    AND archive_column.name = profile_column.name
                    AND archive_column.system_type_id = profile_column.system_type_id
                    AND archive_column.max_length = profile_column.max_length
                    AND archive_column.precision = profile_column.precision
                    AND archive_column.scale = profile_column.scale
              )
        )
        BEGIN
            SET @error_message = N'Existing archive table is not a compatible replica of ' + @profile_object_name + N': ' + @archive_object_name;
            THROW 51202, @error_message, 1;
        END;

        IF COL_LENGTH(N'vsabuser.' + @archive_table, N'z_submission_pack_instance_id') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE ' + @archive_object_name + N' ADD [z_submission_pack_instance_id] bigint NULL;';
            EXEC sys.sp_executesql @sql;
        END;

        IF COL_LENGTH(N'vsabuser.' + @archive_table, N'z_source_entity_instance_id') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE ' + @archive_object_name + N' ADD [z_source_entity_instance_id] bigint NULL;';
            EXEC sys.sp_executesql @sql;
        END;

        IF COL_LENGTH(N'vsabuser.' + @archive_table, N'z_source_formset_id') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE ' + @archive_object_name + N' ADD [z_source_formset_id] uniqueidentifier NULL;';
            EXEC sys.sp_executesql @sql;
        END;

        IF COL_LENGTH(N'vsabuser.' + @archive_table, N'z_archived_at_utc') IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE ' + @archive_object_name + N' ADD [z_archived_at_utc] datetime2(3) NULL;';
            EXEC sys.sp_executesql @sql;
        END;

        SET @archive_object_id = OBJECT_ID(@archive_object_name, N'U');

        IF EXISTS
        (
            SELECT expected.ColumnName
            FROM
            (
                VALUES
                    (N'z_submission_pack_instance_id', N'bigint', 0),
                    (N'z_source_entity_instance_id', N'bigint', 0),
                    (N'z_source_formset_id', N'uniqueidentifier', 0),
                    (N'z_archived_at_utc', N'datetime2', 3)
            ) AS expected (ColumnName, TypeName, ScaleValue)
            LEFT JOIN sys.columns AS archive_column
                ON archive_column.object_id = @archive_object_id
               AND archive_column.name = expected.ColumnName
            LEFT JOIN sys.types AS archive_type
                ON archive_type.user_type_id = archive_column.user_type_id
            WHERE archive_column.column_id IS NULL
               OR archive_type.name <> expected.TypeName
               OR (expected.TypeName = N'datetime2' AND archive_column.scale <> expected.ScaleValue)
        )
        BEGIN
            SET @error_message = N'Archive correlation columns have incompatible definitions on ' + @archive_object_name;
            THROW 51203, @error_message, 1;
        END;

        IF NOT EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = @archive_object_id
              AND name = @index_name
        )
        BEGIN
            SET @sql =
                N'CREATE INDEX ' + QUOTENAME(@index_name) + N'
                  ON ' + @archive_object_name + N'
                  (
                      [z_submission_pack_instance_id],
                      [z_source_entity_instance_id],
                      [z_source_formset_id],
                      [z_archived_at_utc]
                  );';
            EXEC sys.sp_executesql @sql;
        END;

        SET @mapping_id += 1;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT
    s.name AS schema_name,
    t.name AS archive_table,
    SUM(CASE WHEN p.index_id IN (0, 1) THEN p.rows ELSE 0 END) AS archived_rows
FROM sys.tables AS t
INNER JOIN sys.schemas AS s
    ON s.schema_id = t.schema_id
LEFT JOIN sys.partitions AS p
    ON p.object_id = t.object_id
WHERE s.name = N'vsabuser'
  AND t.name IN
  (
      N'z_archieve_group_agentsindividual_repeat_group',
      N'z_archieve_group_agentscompany_repeat_group',
      N'z_archieve_group_brokersindividual_repeat_group'
  )
GROUP BY s.name, t.name
ORDER BY t.name;
GO
