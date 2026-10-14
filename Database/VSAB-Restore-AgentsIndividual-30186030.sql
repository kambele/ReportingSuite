USE [VSAB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/*
    Restores one archived agents-individual profile row to its original table.

    Target SQL Server: NMFS-ERSDB-TEST
    Archive row: group_instance_id = 30186030

    The archive record is intentionally retained as an audit/recovery record.
    This script refuses to continue unless exactly one matching archive row
    exists and no conflicting target row exists.
*/
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @lock_result int;
    EXEC @lock_result = sys.sp_getapplock
        @Resource = N'VSAB.RestoreArchive.AgentsIndividual.30186030',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;

    IF @lock_result < 0
        THROW 51300, 'Could not acquire the archive-row restoration lock.', 1;

    IF OBJECT_ID(N'vsabuser.z_archieve_group_agentsindividual_repeat_group', N'U') IS NULL
        THROW 51301, 'The agents-individual archive table does not exist.', 1;

    IF OBJECT_ID(N'vsabuser.group_agentsindividual_repeat_group', N'U') IS NULL
        THROW 51302, 'The agents-individual profile table does not exist.', 1;

    DECLARE @archive_row_count int;

    SELECT @archive_row_count = COUNT(*)
    FROM [vsabuser].[z_archieve_group_agentsindividual_repeat_group] WITH (UPDLOCK, HOLDLOCK)
    WHERE [group_instance_id] = 30186030
      AND [owner_schema_instance_id] = 453881
      AND [unique_item_guid] = '47DB49B2-8A44-44B5-B363-3C4DDA488F7D'
      AND [item_name] = N'Sharon Judith Hubsch'
      AND [z_submission_pack_instance_id] = 191307
      AND [z_source_entity_instance_id] = 1001
      AND [z_source_formset_id] = '2BE1CA8F-36E0-4DBF-86F1-722A2A8623FA'
      AND [z_archived_at_utc] = CONVERT(datetime2(3), '2026-09-17T12:55:29.072', 126);

    IF @archive_row_count = 0
        THROW 51303, 'The specified archive row was not found. Nothing was restored.', 1;

    IF @archive_row_count > 1
        THROW 51304, 'More than one archive row matched. Nothing was restored.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM [vsabuser].[group_agentsindividual_repeat_group] WITH (UPDLOCK, HOLDLOCK)
        WHERE [group_instance_id] = 30186030
           OR
           (
               [owner_schema_instance_id] = 453881
               AND
               (
                   [unique_item_guid] = '47DB49B2-8A44-44B5-B363-3C4DDA488F7D'
                   OR [item_name] = N'Sharon Judith Hubsch'
               )
           )
    )
        THROW 51305, 'A conflicting profile row already exists. Nothing was restored.', 1;

    INSERT INTO [vsabuser].[group_agentsindividual_repeat_group]
    (
        [group_instance_id],
        [owner_schema_instance_id],
        [unique_item_guid],
        [item_name],
        [item_agentstatus],
        [item_licensenumber],
        [item_registrationdate],
        [item_seqnumber],
        [item_registrationtype],
        [item_existingagentlicensenumber],
        [item_agentisactive],
        [item_registrationdateforreport],
        [item_registrationyearforreport],
        [item_newlyregisteredwiththisentity],
        [item_namibianid_deprecated],
        [item_passportnumber],
        [item_otheridpassport],
        [item_deregistrationdate],
        [item_originalregistrationdate],
        [item_namibianid_1],
        [item_financialservices],
        [item_listedtype],
        [item_entity_company],
        [item_renewaldate],
        [item_expirydate],
        [item_comment],
        [item_renewalflag],
        [item_statusatrenewal],
        [item_pendingrenewal],
        [item_telephone],
        [item_email]
    )
    SELECT
        archive_row.[group_instance_id],
        archive_row.[owner_schema_instance_id],
        archive_row.[unique_item_guid],
        archive_row.[item_name],
        archive_row.[item_agentstatus],
        archive_row.[item_licensenumber],
        archive_row.[item_registrationdate],
        archive_row.[item_seqnumber],
        archive_row.[item_registrationtype],
        archive_row.[item_existingagentlicensenumber],
        archive_row.[item_agentisactive],
        archive_row.[item_registrationdateforreport],
        archive_row.[item_registrationyearforreport],
        archive_row.[item_newlyregisteredwiththisentity],
        archive_row.[item_namibianid_deprecated],
        archive_row.[item_passportnumber],
        archive_row.[item_otheridpassport],
        archive_row.[item_deregistrationdate],
        archive_row.[item_originalregistrationdate],
        archive_row.[item_namibianid_1],
        archive_row.[item_financialservices],
        archive_row.[item_listedtype],
        archive_row.[item_entity_company],
        archive_row.[item_renewaldate],
        archive_row.[item_expirydate],
        archive_row.[item_comment],
        archive_row.[item_renewalflag],
        archive_row.[item_statusatrenewal],
        archive_row.[item_pendingrenewal],
        archive_row.[item_telephone],
        archive_row.[item_email]
    FROM [vsabuser].[z_archieve_group_agentsindividual_repeat_group] AS archive_row
    WHERE archive_row.[group_instance_id] = 30186030
      AND archive_row.[owner_schema_instance_id] = 453881
      AND archive_row.[unique_item_guid] = '47DB49B2-8A44-44B5-B363-3C4DDA488F7D'
      AND archive_row.[item_name] = N'Sharon Judith Hubsch'
      AND archive_row.[z_submission_pack_instance_id] = 191307
      AND archive_row.[z_source_entity_instance_id] = 1001
      AND archive_row.[z_source_formset_id] = '2BE1CA8F-36E0-4DBF-86F1-722A2A8623FA'
      AND archive_row.[z_archived_at_utc] = CONVERT(datetime2(3), '2026-09-17T12:55:29.072', 126);

    IF @@ROWCOUNT <> 1
        THROW 51306, 'The restore did not insert exactly one row. The transaction was rolled back.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT
    [group_instance_id],
    [owner_schema_instance_id],
    [unique_item_guid],
    [item_name],
    [item_licensenumber],
    [item_originalregistrationdate]
FROM [vsabuser].[group_agentsindividual_repeat_group]
WHERE [group_instance_id] = 30186030;
GO
