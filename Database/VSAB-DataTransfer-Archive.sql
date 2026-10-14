USE [VSAB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*
    Archives profile rows that conflict with form rows in a submission before the
    workflow's DataTransfer task runs. Existing profile rows remain in place; the
    DataTransfer task can skip those duplicates while continuing with other rows.

    The archive tables are pre-provisioned replicas of their profile tables plus
    correlation fields. Run VSAB-DataTransfer-Archive-Tables.sql before deploying
    these procedures. Correlation is required because Pre, DataTransfer, and Post
    are separate workflow steps and cannot safely share one database transaction.
*/
CREATE OR ALTER PROCEDURE [vsabuser].[zPreDataTransfer]
    @submission_pack_instance_id bigint
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;
    /*
        Do not let a statement error automatically roll back a transaction
        owned by the workflow's ambient TransactionScope.  The CATCH block
        below rolls back only when this procedure started the transaction.
    */
    SET XACT_ABORT OFF;

    DECLARE @entity_instance_id bigint;
    DECLARE @submission_pack_version_id bigint;

    SELECT @entity_instance_id = spi.entity_instance_id
    FROM [vsabuser].[submission_pack_instances] AS spi
    WHERE spi.submission_pack_instance_id = @submission_pack_instance_id
      AND spi.is_deleted = 0;

    IF @entity_instance_id IS NULL
    BEGIN
        SELECT *
        FROM [vsabuser].[submission_pack_instances]
        WHERE submission_pack_instance_id = @submission_pack_instance_id;
        RETURN;
    END;

    SELECT TOP (1)
           @submission_pack_version_id = spv.submission_pack_version_id
    FROM [vsabuser].[submission_pack_versions] AS spv
    WHERE spv.submission_pack_instance_id = @submission_pack_instance_id
    ORDER BY spv.submission_pack_version_id DESC;

    CREATE TABLE #SubmissionForms
    (
        FormsetId uniqueidentifier NOT NULL,
        FormSchemaInstanceId bigint NOT NULL,
        EntityInstanceId bigint NOT NULL,
        PRIMARY KEY (FormsetId, FormSchemaInstanceId)
    );

    IF @submission_pack_version_id IS NOT NULL
    BEGIN
        INSERT INTO #SubmissionForms
        (
            FormsetId,
            FormSchemaInstanceId,
            EntityInstanceId
        )
        SELECT DISTINCT
               CONVERT(uniqueidentifier, fi.formset_guid),
               fi.schema_instance_id,
               @entity_instance_id
        FROM [vsabuser].[submission_pack_version_formset_instances] AS spfi
        INNER JOIN [vsabuser].[formset_instances] AS fi
            ON fi.formset_instance_id = spfi.formset_instance_id
        WHERE spfi.submission_pack_version_id = @submission_pack_version_id
          AND fi.formset_guid IN
          (
              '2BE1CA8F-36E0-4DBF-86F1-722A2A8623FA',
              '5D49BFDA-8BB8-4B55-A0A9-A9303F97E2B9',
              '4723AA41-6281-4A4D-93E6-F252456B2119'
          );
    END;

    DECLARE @Mappings TABLE
    (
        MappingId int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        FormsetId uniqueidentifier NOT NULL,
        ProfileTable sysname NOT NULL,
        FormTable sysname NOT NULL,
        ArchiveTable sysname NOT NULL
    );

    INSERT INTO @Mappings (FormsetId, ProfileTable, FormTable, ArchiveTable)
    VALUES
        ('2BE1CA8F-36E0-4DBF-86F1-722A2A8623FA', N'group_agentsindividual_repeat_group', N'group_addlistedindividual_repeat_group', N'z_archieve_group_agentsindividual_repeat_group'),
        ('2BE1CA8F-36E0-4DBF-86F1-722A2A8623FA', N'group_agentsindividual_repeat_group', N'group_addexistinglistedindividual_repeat_group', N'z_archieve_group_agentsindividual_repeat_group'),
        ('5D49BFDA-8BB8-4B55-A0A9-A9303F97E2B9', N'group_agentscompany_repeat_group', N'group_addlistedcompany_entitysectiona_repeat_group', N'z_archieve_group_agentscompany_repeat_group'),
        ('5D49BFDA-8BB8-4B55-A0A9-A9303F97E2B9', N'group_agentscompany_repeat_group', N'group_addlistedcompanyentitylloyds_repeat_group', N'z_archieve_group_agentscompany_repeat_group'),
        ('5D49BFDA-8BB8-4B55-A0A9-A9303F97E2B9', N'group_agentscompany_repeat_group', N'group_addexistinglistedcompany_entity_repeat_group', N'z_archieve_group_agentscompany_repeat_group'),
        ('4723AA41-6281-4A4D-93E6-F252456B2119', N'group_brokersindividual_repeat_group', N'group_individualdetails_1', N'z_archieve_group_brokersindividual_repeat_group');

    /*
        The workflow service may already have enlisted this connection in an
        ambient TransactionScope.  Only commit or roll back a transaction that
        this procedure started; otherwise the caller remains its owner.
    */
    DECLARE @started_transaction bit = 0;

    BEGIN TRY
        /* XACT_STATE also detects delegated/ambient transactions whose
           ownership is not reliably represented by @@TRANCOUNT. */
        IF XACT_STATE() = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @started_transaction = 1;
        END;

        DECLARE @lock_result int;
        DECLARE @lock_resource nvarchar(255) = N'VSAB.DataTransfer.' + CONVERT(nvarchar(30), @submission_pack_instance_id);
        EXEC @lock_result = sys.sp_getapplock
            @Resource = @lock_resource,
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 60000;

        IF @lock_result < 0
            THROW 51000, 'Could not acquire the submission-pack data-transfer lock.', 1;

        DECLARE @profile_table sysname;
        DECLARE @archive_table sysname;
        DECLARE @profile_object_name nvarchar(517);
        DECLARE @archive_object_name nvarchar(517);
        DECLARE @error_message nvarchar(2048);

        DECLARE @mapping_id int = 1;
        DECLARE @mapping_count int = (SELECT COUNT(*) FROM @Mappings);
        DECLARE @formset_id uniqueidentifier;
        DECLARE @form_table sysname;
        DECLARE @form_object_name nvarchar(517);
        DECLARE @profile_object_id int;
        DECLARE @archive_object_id int;
        DECLARE @column_list nvarchar(max);
        DECLARE @column_select_list nvarchar(max);
        DECLARE @has_identity bit;
        DECLARE @insert_sql nvarchar(max);
        DECLARE @insert_core nvarchar(max);

        WHILE @mapping_id <= @mapping_count
        BEGIN
            SELECT
                @formset_id = FormsetId,
                @profile_table = ProfileTable,
                @form_table = FormTable,
                @archive_table = ArchiveTable
            FROM @Mappings
            WHERE MappingId = @mapping_id;

            IF EXISTS (SELECT 1 FROM #SubmissionForms WHERE FormsetId = @formset_id)
            BEGIN
                SET @profile_object_name = QUOTENAME(N'vsabuser') + N'.' + QUOTENAME(@profile_table);
                SET @archive_object_name = QUOTENAME(N'vsabuser') + N'.' + QUOTENAME(@archive_table);
                SET @form_object_name = QUOTENAME(N'vsabuser') + N'.' + QUOTENAME(@form_table);

                IF OBJECT_ID(@profile_object_name, N'U') IS NULL
                BEGIN
                    SET @error_message = N'Required profile table does not exist: ' + @profile_object_name;
                    THROW 51001, @error_message, 1;
                END;

                IF OBJECT_ID(@form_object_name, N'U') IS NULL
                BEGIN
                    SET @error_message = N'Required form table does not exist: ' + @form_object_name;
                    THROW 51002, @error_message, 1;
                END;

                IF OBJECT_ID(@archive_object_name, N'U') IS NULL
                BEGIN
                    SET @error_message = N'Required archive table does not exist: ' + @archive_object_name + N'. Run VSAB-DataTransfer-Archive-Tables.sql first.';
                    THROW 51005, @error_message, 1;
                END;

                IF COL_LENGTH(N'vsabuser.' + @profile_table, N'owner_schema_instance_id') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @profile_table, N'item_name') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @profile_table, N'item_originalregistrationdate') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @form_table, N'owner_schema_instance_id') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @form_table, N'item_name') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @archive_table, N'z_submission_pack_instance_id') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @archive_table, N'z_source_entity_instance_id') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @archive_table, N'z_source_formset_id') IS NULL OR
                   COL_LENGTH(N'vsabuser.' + @archive_table, N'z_archived_at_utc') IS NULL
                BEGIN
                    SET @error_message = N'A required transfer or archive column is missing for ' + @profile_object_name + N'. Run VSAB-DataTransfer-Archive-Tables.sql first.';
                    THROW 51003, @error_message, 1;
                END;

                SET @profile_object_id = OBJECT_ID(@profile_object_name, N'U');
                SET @archive_object_id = OBJECT_ID(@archive_object_name, N'U');
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
                    SET @error_message = N'Archive table schema is not compatible with ' + @profile_object_name + N'. Re-run VSAB-DataTransfer-Archive-Tables.sql after reconciling the archive data.';
                    THROW 51006, @error_message, 1;
                END;

                SET @column_list = NULL;
                SET @column_select_list = NULL;

                SELECT @column_list = STUFF
                (
                    (
                        SELECT N',' + QUOTENAME(source_column.name)
                        FROM sys.columns AS source_column
                        INNER JOIN sys.columns AS archive_column
                            ON archive_column.object_id = @archive_object_id
                           AND archive_column.name = source_column.name
                        WHERE source_column.object_id = @profile_object_id
                          AND archive_column.is_computed = 0
                          AND archive_column.system_type_id <> 189
                          AND archive_column.generated_always_type = 0
                        ORDER BY source_column.column_id
                        FOR XML PATH(N''), TYPE
                    ).value(N'.', N'nvarchar(max)'),
                    1,
                    1,
                    N''
                );

                SELECT @column_select_list = STUFF
                (
                    (
                        SELECT N',profile_row.' + QUOTENAME(source_column.name)
                        FROM sys.columns AS source_column
                        INNER JOIN sys.columns AS archive_column
                            ON archive_column.object_id = @archive_object_id
                           AND archive_column.name = source_column.name
                        WHERE source_column.object_id = @profile_object_id
                          AND archive_column.is_computed = 0
                          AND archive_column.system_type_id <> 189
                          AND archive_column.generated_always_type = 0
                        ORDER BY source_column.column_id
                        FOR XML PATH(N''), TYPE
                    ).value(N'.', N'nvarchar(max)'),
                    1,
                    1,
                    N''
                );

                IF NULLIF(@column_list, N'') IS NULL
                BEGIN
                    SET @error_message = N'No compatible archive columns were found for ' + @profile_object_name + N'.';
                    THROW 51004, @error_message, 1;
                END;

                SET @has_identity = CASE WHEN EXISTS
                (
                    SELECT 1
                    FROM sys.identity_columns AS identity_column
                    WHERE identity_column.object_id = @archive_object_id
                      AND CHARINDEX(N',' + QUOTENAME(identity_column.name) + N',', N',' + @column_list + N',') > 0
                ) THEN 1 ELSE 0 END;

                SET @insert_core =
                    N'INSERT INTO ' + @archive_object_name + N'
                      (' + @column_list + N', [z_submission_pack_instance_id], [z_source_entity_instance_id], [z_source_formset_id], [z_archived_at_utc])
                      SELECT ' + @column_select_list + N', @PackId, @EntityId, @FormsetId, SYSUTCDATETIME()
                      FROM ' + @profile_object_name + N' AS profile_row
                      WHERE EXISTS
                      (
                          SELECT 1
                          FROM [vsabuser].[view_profile_data] AS profile_view
                          WHERE profile_view.entity_instance_id = @EntityId
                            AND profile_view.schema_instance_id = profile_row.owner_schema_instance_id
                      )
                        AND EXISTS
                      (
                          SELECT 1
                          FROM #SubmissionForms AS submission_form
                          INNER JOIN ' + @form_object_name + N' AS form_row
                              ON form_row.owner_schema_instance_id = submission_form.FormSchemaInstanceId
                          WHERE submission_form.FormsetId = @FormsetId
                            AND submission_form.EntityInstanceId = @EntityId
                            AND form_row.item_name = profile_row.item_name
                      )
                        AND NOT EXISTS
                      (
                          SELECT 1
                          FROM ' + @archive_object_name + N' AS archived_row
                          WHERE archived_row.z_submission_pack_instance_id = @PackId
                            AND archived_row.z_source_entity_instance_id = @EntityId
                            AND archived_row.owner_schema_instance_id = profile_row.owner_schema_instance_id
                            AND archived_row.item_name = profile_row.item_name
                      );';

                IF @has_identity = 1
                BEGIN
                    SET @insert_sql =
                        N'BEGIN TRY
                              SET IDENTITY_INSERT ' + @archive_object_name + N' ON;
                              ' + @insert_core + N'
                              SET IDENTITY_INSERT ' + @archive_object_name + N' OFF;
                          END TRY
                          BEGIN CATCH
                              BEGIN TRY
                                  SET IDENTITY_INSERT ' + @archive_object_name + N' OFF;
                              END TRY
                              BEGIN CATCH
                              END CATCH;
                              THROW;
                          END CATCH;';
                END
                ELSE
                BEGIN
                    SET @insert_sql = @insert_core;
                END;

                EXEC sys.sp_executesql
                    @insert_sql,
                    N'@PackId bigint, @EntityId bigint, @FormsetId uniqueidentifier',
                    @PackId = @submission_pack_instance_id,
                    @EntityId = @entity_instance_id,
                    @FormsetId = @formset_id;

            END;

            SET @mapping_id += 1;
        END;

        IF @started_transaction = 1
            COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @started_transaction = 1 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT *
    FROM [vsabuser].[submission_pack_instances]
    WHERE submission_pack_instance_id = @submission_pack_instance_id;
END;
GO

/*
    Renews profile rows that zPreDataTransfer archived as duplicate conflicts.
    DataTransfer skips those existing rows while continuing with non-conflicting
    rows, so the existing profile rows are updated here after DataTransfer.
*/
CREATE OR ALTER PROCEDURE [vsabuser].[zPostDataTransfer]
    @submission_pack_instance_id bigint
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;
    /*
        Do not let a statement error automatically roll back a transaction
        owned by the workflow's ambient TransactionScope.  The CATCH block
        below rolls back only when this procedure started the transaction.
    */
    SET XACT_ABORT OFF;

    DECLARE @entity_instance_id bigint;

    SELECT @entity_instance_id = spi.entity_instance_id
    FROM [vsabuser].[submission_pack_instances] AS spi
    WHERE spi.submission_pack_instance_id = @submission_pack_instance_id
      AND spi.is_deleted = 0;

    IF @entity_instance_id IS NULL
    BEGIN
        SELECT *
        FROM [vsabuser].[submission_pack_instances]
        WHERE submission_pack_instance_id = @submission_pack_instance_id;
        RETURN;
    END;

    DECLARE @Profiles TABLE
    (
        MappingId int IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        ProfileTable sysname NOT NULL,
        ArchiveTable sysname NOT NULL
    );

    INSERT INTO @Profiles (ProfileTable, ArchiveTable)
    VALUES
        (N'group_agentsindividual_repeat_group', N'z_archieve_group_agentsindividual_repeat_group'),
        (N'group_agentscompany_repeat_group', N'z_archieve_group_agentscompany_repeat_group'),
        (N'group_brokersindividual_repeat_group', N'z_archieve_group_brokersindividual_repeat_group');

    DECLARE @today date = CONVERT(date, GETDATE());
    DECLARE @this_year_expiry date = DATEFROMPARTS(YEAR(@today), 3, 31);
    DECLARE @next_expiry date = DATEFROMPARTS
    (
        YEAR(@today) + CASE WHEN @today >= @this_year_expiry THEN 1 ELSE 0 END,
        3,
        31
    );
    DECLARE @today_text nvarchar(23) = CONVERT(nvarchar(23), CONVERT(datetime2(3), @today), 121);
    DECLARE @next_expiry_text nvarchar(23) = CONVERT(nvarchar(23), CONVERT(datetime2(3), @next_expiry), 121);

    /*
        The workflow service may already have enlisted this connection in an
        ambient TransactionScope.  Only commit or roll back a transaction that
        this procedure started; otherwise the caller remains its owner.
    */
    DECLARE @started_transaction bit = 0;

    BEGIN TRY
        /* XACT_STATE also detects delegated/ambient transactions whose
           ownership is not reliably represented by @@TRANCOUNT. */
        IF XACT_STATE() = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @started_transaction = 1;
        END;

        DECLARE @lock_result int;
        DECLARE @lock_resource nvarchar(255) = N'VSAB.DataTransfer.' + CONVERT(nvarchar(30), @submission_pack_instance_id);
        EXEC @lock_result = sys.sp_getapplock
            @Resource = @lock_resource,
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 60000;

        IF @lock_result < 0
            THROW 51100, 'Could not acquire the submission-pack data-transfer lock.', 1;

        DECLARE @mapping_id int = 1;
        DECLARE @mapping_count int = (SELECT COUNT(*) FROM @Profiles);
        DECLARE @profile_table sysname;
        DECLARE @archive_table sysname;
        DECLARE @profile_object_name nvarchar(517);
        DECLARE @archive_object_name nvarchar(517);
        DECLARE @sql nvarchar(max);
        DECLARE @error_message nvarchar(2048);

        WHILE @mapping_id <= @mapping_count
        BEGIN
            SELECT
                @profile_table = ProfileTable,
                @archive_table = ArchiveTable
            FROM @Profiles
            WHERE MappingId = @mapping_id;

            SET @profile_object_name = QUOTENAME(N'vsabuser') + N'.' + QUOTENAME(@profile_table);
            SET @archive_object_name = QUOTENAME(N'vsabuser') + N'.' + QUOTENAME(@archive_table);

            IF OBJECT_ID(@profile_object_name, N'U') IS NULL OR
               OBJECT_ID(@archive_object_name, N'U') IS NULL
            BEGIN
                SET @error_message = N'Required profile or archive table does not exist for ' + @profile_object_name + N'. Run VSAB-DataTransfer-Archive-Tables.sql first.';
                THROW 51101, @error_message, 1;
            END;

            IF COL_LENGTH(N'vsabuser.' + @profile_table, N'owner_schema_instance_id') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @profile_table, N'item_name') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @profile_table, N'item_registrationdate') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @profile_table, N'item_agentstatus') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @profile_table, N'item_agentisactive') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @profile_table, N'item_expirydate') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @profile_table, N'item_originalregistrationdate') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @archive_table, N'item_originalregistrationdate') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @archive_table, N'z_submission_pack_instance_id') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @archive_table, N'z_source_entity_instance_id') IS NULL OR
               COL_LENGTH(N'vsabuser.' + @archive_table, N'z_archived_at_utc') IS NULL
            BEGIN
                SET @error_message = N'Required post-transfer columns are missing for ' + @profile_object_name + N'.';
                THROW 51102, @error_message, 1;
            END;

            SET @sql =
                N'UPDATE profile_row
                  SET item_registrationdate = @TodayValue,
                      item_agentstatus = N''1'',
                      item_agentisactive = 1,
                      item_expirydate = @ExpiryValue,
                      item_originalregistrationdate = COALESCE
                      (
                          NULLIF(LTRIM(RTRIM(archived_row.item_originalregistrationdate)), N''''),
                          @TodayValue
                      )
                  FROM ' + @profile_object_name + N' AS profile_row
                  CROSS APPLY
                  (
                      SELECT TOP (1)
                             archive_source.item_originalregistrationdate
                      FROM ' + @archive_object_name + N' AS archive_source
                      WHERE archive_source.z_submission_pack_instance_id = @PackId
                        AND archive_source.z_source_entity_instance_id = @EntityId
                        AND archive_source.owner_schema_instance_id = profile_row.owner_schema_instance_id
                        AND archive_source.item_name = profile_row.item_name
                      ORDER BY archive_source.z_archived_at_utc DESC
                  ) AS archived_row
                  WHERE EXISTS
                  (
                      SELECT 1
                      FROM [vsabuser].[view_profile_data] AS profile_view
                      WHERE profile_view.entity_instance_id = @EntityId
                        AND profile_view.schema_instance_id = profile_row.owner_schema_instance_id
                  );';

            EXEC sys.sp_executesql
                @sql,
                N'@PackId bigint, @EntityId bigint, @TodayValue nvarchar(23), @ExpiryValue nvarchar(23)',
                @PackId = @submission_pack_instance_id,
                @EntityId = @entity_instance_id,
                @TodayValue = @today_text,
                @ExpiryValue = @next_expiry_text;

            SET @mapping_id += 1;
        END;

        IF @started_transaction = 1
            COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @started_transaction = 1 AND XACT_STATE() <> 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT *
    FROM [vsabuser].[submission_pack_instances]
    WHERE submission_pack_instance_id = @submission_pack_instance_id;
END;
GO

/* Grant only procedure execution to the workflow identity/role, for example:
   GRANT EXECUTE ON [vsabuser].[zPreDataTransfer] TO [YourWorkflowDatabaseUserOrRole];
   GRANT EXECUTE ON [vsabuser].[zPostDataTransfer] TO [YourWorkflowDatabaseUserOrRole];
*/
