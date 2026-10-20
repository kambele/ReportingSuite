USE [ReportingSuite];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @SectionID bigint;

    SELECT TOP (1)
        @SectionID = SectionID
    FROM dbo.Sections WITH (UPDLOCK, HOLDLOCK)
    WHERE LOWER(LTRIM(RTRIM(COALESCE(SectionName, '')))) = 'chart of accounts'
    ORDER BY SectionID;

    IF @SectionID IS NULL
    BEGIN
        INSERT dbo.Sections (SectionName, Description)
        VALUES
        (
            'Chart of Accounts',
            'SSRS Chart of Accounts reports registered for catalogue navigation and report security.'
        );

        SET @SectionID = CONVERT(bigint, SCOPE_IDENTITY());
    END;

    DECLARE @Reports TABLE
    (
        Report_Name varchar(200) NOT NULL,
        Report_Title varchar(100) NOT NULL,
        Report_Description varchar(max) NOT NULL,
        Sort_Order int NOT NULL,
        ReportURL varchar(200) NOT NULL
    );

    INSERT @Reports
    (
        Report_Name,
        Report_Title,
        Report_Description,
        Sort_Order,
        ReportURL
    )
    VALUES
        ('Aggregated report', 'Quarterly aggregated report', 'Quarterly Chart of Accounts aggregated report.', 10, 'ERS-Reports/OCoA_Quarterly_Returns'),
        ('Entity level (vertical)', 'Quarterly entity level vertical', 'Quarterly Chart of Accounts report by entity in vertical format.', 20, 'ERS-Reports/OCoA_Quarterly_Returns_Per_Entity'),
        ('Entity level (horizontal)', 'Quarterly entity level horizontal', 'Quarterly Chart of Accounts report by entity in horizontal format.', 30, 'ERS-Reports/OCoA_Quarterly_Returns_Extra'),
        ('Additional information', 'Quarterly additional information', 'Quarterly Chart of Accounts additional information report.', 40, 'ERS-Reports/OCoA_Quarterly_Returns_ExtraAdditional'),
        ('Single entity', 'Quarterly single entity report', 'Quarterly Chart of Accounts report for one entity.', 50, 'ERS-Reports/OCoA_Quarterly_Returns_By_Entity'),
        ('Cross-entity aggregated', 'Quarterly cross-entity aggregated', 'Quarterly Chart of Accounts aggregated report for multiple entities.', 60, 'ERS-Reports/OCoA_Quarterly_Returns_Multipe_Entities'),
        ('Cross-entity detail', 'Quarterly cross-entity detail', 'Quarterly Chart of Accounts detailed report for multiple entities.', 70, 'ERS-Reports/OCoA_Quarterly_Returns_Multipe_Entities_Per_Entity'),
        ('Monthly return by entity group', 'Monthly return by entity group', 'Monthly Chart of Accounts return by entity group.', 80, 'ERS-Reports/OCoA_Monthly_Returns_Per_Entity');

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Reports AS existing
        INNER JOIN @Reports AS source
            ON LOWER(LTRIM(RTRIM(REPLACE(existing.ReportURL, '\', '/')))) =
               LOWER(LTRIM(RTRIM(source.ReportURL)))
        GROUP BY LOWER(LTRIM(RTRIM(REPLACE(existing.ReportURL, '\', '/'))))
        HAVING COUNT_BIG(*) > 1
    )
    BEGIN
        THROW 50001, 'A Chart of Accounts report path is registered more than once. Resolve the duplicate before continuing.', 1;
    END;

    UPDATE target
       SET target.Report_Name = source.Report_Name,
           target.Report_Title = source.Report_Title,
           target.Report_Description = source.Report_Description,
           target.Sort_Order = source.Sort_Order,
           target.SectionID = @SectionID,
           target.ReportTypeID = 1,
           target.Is_Dynamic = 0,
           target.Is_Deleted = 0
    FROM dbo.Reports AS target
    INNER JOIN @Reports AS source
        ON LOWER(LTRIM(RTRIM(REPLACE(target.ReportURL, '\', '/')))) =
           LOWER(LTRIM(RTRIM(source.ReportURL)));

    INSERT dbo.Reports
    (
        Report_Name,
        Report_Title,
        Report_Description,
        Sort_Order,
        SectionID,
        ReportTypeID,
        ReportURL,
        Is_Dynamic,
        Is_Deleted
    )
    SELECT
        source.Report_Name,
        source.Report_Title,
        source.Report_Description,
        source.Sort_Order,
        @SectionID,
        1,
        source.ReportURL,
        0,
        0
    FROM @Reports AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Reports AS target WITH (UPDLOCK, HOLDLOCK)
        WHERE LOWER(LTRIM(RTRIM(REPLACE(target.ReportURL, '\', '/')))) =
              LOWER(LTRIM(RTRIM(source.ReportURL)))
    );

    COMMIT TRANSACTION;

    SELECT
        report.ReportID,
        report.Report_Name,
        report.ReportURL,
        report.SectionID,
        report.ReportTypeID,
        report.Is_Deleted
    FROM dbo.Reports AS report
    INNER JOIN @Reports AS source
        ON LOWER(LTRIM(RTRIM(REPLACE(report.ReportURL, '\', '/')))) =
           LOWER(LTRIM(RTRIM(source.ReportURL)))
    ORDER BY report.Sort_Order, report.ReportID;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
