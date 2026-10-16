USE [ReportingSuite]
GO
/****** Object:  StoredProcedure [dbo].[GetAllCompanies]    Script Date: 16/10/2026 10:51:42 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
ALTER PROCEDURE [dbo].[GetAllCompanies] 
	-- Add the parameters for the stored procedure here
	@status varchar(100),
	@entity varchar(100),
	@fin date,
	@hasFin int,
	@registered_from date,
	@registered_to date
	
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;


   IF @registered_from IS NOT NULL AND @registered_to IS NULL
   BEGIN

   
	IF @fin IS NOT NULL
		BEGIN
			 -- Insert statements for procedure here
	SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       vpd.schema_instance_id,
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id



  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	   LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id

	   LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id
	   
       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c	   
where ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and MONTH(convert(date, gen.item_financialyear_1)) = MONTH(convert(date, @fin))
AND gen.item_registrationdate >= @registered_from AND ei.entity_deleted = 0
order by eit.name
		END
	ELSE
		BEGIN

		if @hasFin = 1
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date'
		  ,

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id
		  
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id


       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) 
AND gen.item_registrationdate >= @registered_from AND ei.entity_deleted = 0
order by eit.name

			END
		ELSE IF @hasFin  = 2
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
       LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id

       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and gen.item_financialyear_1 is not null
AND gen.item_registrationdate >= @registered_from AND ei.entity_deleted = 0
order by eit.name
			END
		ELSE IF @hasFin = 3
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id

       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and gen.item_financialyear_1 iS NULL
AND gen.item_registrationdate >= @registered_from AND ei.entity_deleted = 0
order by eit.name
			END
			
		END
   END
   ELSE IF @registered_from IS NOT NULL AND @registered_to IS NOT NULL
   BEGIN
   	IF @fin IS NOT NULL
		BEGIN
	SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id



  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	   LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id

	   LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id
	   
       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c	   
where ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and MONTH(convert(date, gen.item_financialyear_1)) = MONTH(convert(date, @fin))
AND gen.item_registrationdate BETWEEN @registered_from AND @registered_to AND ei.entity_deleted = 0
order by eit.name
		END
	ELSE
		BEGIN

		if @hasFin = 1
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date'
		  ,

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id
		  
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id


       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) 
AND gen.item_registrationdate BETWEEN @registered_from AND @registered_to AND ei.entity_deleted = 0
order by eit.name

			END
		ELSE IF @hasFin  = 2
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		 -- ins.item_actuaryname  ,
		  eg.group_id
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
       LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id

       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and gen.item_financialyear_1 is not null
AND gen.item_registrationdate BETWEEN @registered_from AND @registered_to AND ei.entity_deleted = 0
order by eit.name
			END
		ELSE IF @hasFin = 3
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
      et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id

       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and gen.item_financialyear_1 iS NULL
AND gen.item_registrationdate BETWEEN @registered_from AND @registered_to AND ei.entity_deleted = 0
order by eit.name
			END
			
		END

   END
   ELSE
   BEGIN
   	IF @fin IS NOT NULL
		BEGIN
	SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		  eg.group_id



  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	   LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id

	   LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id
	   
       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and MONTH(convert(date, gen.item_financialyear_1)) = MONTH(convert(date, @fin)) AND ei.entity_deleted = 0

order by eit.name
		END
	ELSE
		BEGIN

		if @hasFin = 1
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date'
		  ,

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		--  ins.item_actuaryname  ,
		  eg.group_id
		  
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       --LEFT JOIN VSABUSER.group_bankdetails	as bd
       --   ON vpd.schema_instance_id = bd.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id


       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) AND ei.entity_deleted = 0
order by eit.name

			END
		ELSE IF @hasFin  = 2
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
		ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

		--  ins.item_actuaryname  ,
		  eg.group_id
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       --LEFT JOIN VSABUSER.group_bankdetails	as bd
       --   ON vpd.schema_instance_id = bd.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
       LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id

       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and gen.item_financialyear_1 is not null AND ei.entity_deleted = 0
order by eit.name
			END
		ELSE IF @hasFin = 3
			BEGIN
				SELECT eit.name AS 'Company Name',
       ei.reference AS 'License Number',
	   [CUSTNMBR] as 'GP Account Number',
       gen.owner_schema_instance_id ,
       et.name AS 'Type of Company',
       et.name AS 'Type of Company',
       CASE ei.state_id
          WHEN 1 THEN 'Registering'
          WHEN 2 THEN 'Active'
          WHEN 3 THEN 'Cancelled'
          WHEN 4 THEN 'Deregistered'
          WHEN 5 THEN 'Suspended' 
          WHEN 6 THEN 'Declined'
          WHEN 7 THEN 'Online Enrolling'
          WHEN 8 THEN 'NON-FIMA Active'
          END  AS 'Company Status',
		  convert(datetime, gen.item_registrationdate, 101)
        AS 'Registration Date',
		convert(datetime, gen.item_deregistrationdate, 101)
        AS 'De-Registration Date',
         DATENAME (day, gen.item_financialyear_1)
       + ' '
       + DATENAME (month, gen.item_financialyear_1)
          AS 'Financial Year End',
         ISNULL (gen.item_physicaladdressline1, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline2, '')
       + ' '
       + ISNULL (gen.item_physicaladdressline3, '')
       + ' '
       + ISNULL (gen.item_physicaladdresstowncity, '')
       + ' '
       + CASE gen.item_physicaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 1',
          ISNULL (gen.item_postaladdressline1, '')
       + ' '
       + ISNULL (gen.item_postaladdressline2, '')
       + ' '
       + ISNULL (gen.item_postaladdressline3, '')
       + ' '
       + ISNULL (gen.item_postaladdresstowncity, '')
       + ' '
       + CASE gen.item_postaladdresscountry
            WHEN 1 THEN 'Namibia'
            ELSE ''
         END
          AS 'Physical Address 2',
         po.item_poname AS 'Principal Officer Name',
        ISNULL (po.item_email, '') AS 'Principal Officer Email',
        ISNULL (po.item_telephone, '') AS 'Principal Officer Contact Info',
        ISNULL (gen.item_auditorname,'') AS 'AUDITOR NAME',
        ISNULL (gen.item_telephone,'') AS 'Telephone Number',
		ISNULL (gen.item_email,'') AS 'Email'
		,ei.entity_instance_id,
		CASE ei.entity_deleted
            WHEN 1 THEN 'Deleted'
            ELSE ''
         END
          AS 'Deletion',
		  CASE eeg.start_date
            WHEN '1900-01-01 00:00:00.000' THEN ''
            ELSE eeg.start_date
         END
          AS 'Group Assigned Date',
		  eeg.end_date as 'Group De-assigned Date',

		  v.item_valuatorname,
		  v.item_email as 'Valuator Email',
		  v.item_telephone as 'Valuator Telephone',

		  a.item_administratorname,
		  a.item_email as 'Administrator Email',
		  a.item_telephone as 'Administrator Telephone',

	--	  ins.item_actuaryname  ,
		  eg.group_id
  FROM VSAB.VSABUSER.entity_instance ei
       LEFT JOIN VSAB.VSABUSER.view_profile_data vpd
          ON ei.entity_instance_id = vpd.entity_instance_id
       LEFT JOIN VSAB.VSABUSER.entity_instance_translation eit
          ON     ei.entity_instance_id = eit.entity_instance_id
             AND eit.language = 'en-GB'
       LEFT JOIN VSAB.VSABUSER.group_generaldetails AS gen
          ON vpd.schema_instance_id = gen.owner_schema_instance_id
       LEFT JOIN VSAB.VSABUSER.group_principalofficer AS po
          ON vpd.schema_instance_id = po.owner_schema_instance_id
       --LEFT JOIN VSABUSER.group_bankdetails	as bd
       --   ON vpd.schema_instance_id = bd.owner_schema_instance_id
       LEFT JOIN VSAB.vsabuser.entity_entity_groups as eeg
       ON eeg.entity_instance_id = ei.entity_instance_id and eeg.end_date IS NULL
       LEFT JOIN VSAB.vsabuser.entity_groups eg
       on eg.group_id = eeg.group_id
	   LEFT JOIN VSAB.vsabuser.entity_group_translation AS et
       on et.group_id = eg.group_id
	   LEFT JOIN VSAB.vsabuser.group_valuator v
	   ON v.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_administrator a
	   ON a.owner_schema_instance_id = gen.owner_schema_instance_id
	    LEFT JOIN VSAB.vsabuser.group_insurance_details ins
	   ON ins.owner_schema_instance_id = gen.owner_schema_instance_id

       OUTER APPLY
       (
           SELECT TOP (1)
               gp.[CUSTNMBR],
               gp.[CUSTNAME],
               gp.[COMMENT2]
           FROM [NMFS-GP-DYNADB].[NAMFI].[dbo].[RM00101] AS gp
           WHERE
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))), N'') IS NOT NULL
                   AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                       = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
               )
               OR
               (
                   NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name]))), N'') IS NOT NULL
                   AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME]))), N'') IS NOT NULL
                   AND UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), eit.[name])))) COLLATE DATABASE_DEFAULT
                       = UPPER(LTRIM(RTRIM(CONVERT(nvarchar(255), gp.[CUSTNAME])))) COLLATE DATABASE_DEFAULT
               )
           ORDER BY
               CASE
                   WHEN NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))), N'') IS NOT NULL
                        AND LTRIM(RTRIM(CONVERT(nvarchar(100), ei.[reference]))) COLLATE DATABASE_DEFAULT
                            = LTRIM(RTRIM(CONVERT(nvarchar(100), gp.[COMMENT2]))) COLLATE DATABASE_DEFAULT
                   THEN 0
                   ELSE 1
               END,
               gp.[CUSTNMBR]
       ) AS c
WHERE ei.state_id in(select * from VSAB.vsabuser.fn_split(@status,',')) and eg.group_id in (select * from VSAB.vsabuser.fn_split(@entity,',')) and gen.item_financialyear_1 iS NULL AND ei.entity_deleted = 0
order by eit.name
			END
			
		END

   END


END
