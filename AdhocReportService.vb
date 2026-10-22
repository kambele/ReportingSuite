Imports System.Configuration
Imports System.Data
Imports System.Data.Entity.Core.EntityClient
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Web
Imports System.Xml
Imports Newtonsoft.Json

Public NotInheritable Class AdhocReportService
    Private Const CommandTimeoutSeconds As Integer = 120
    Private Const MaximumPageSize As Integer = 200
    Private Const MaximumDistinctValues As Integer = 250
    Private Const MaximumSearchColumns As Integer = 40
    Private Const MaximumFilterColumns As Integer = 50
    Private Const MaximumFilterValuesPerColumn As Integer = 50
    Private Const MaximumExportRows As Integer = 100000
    Private Const AdhocViewSchema As String = "dbo"
    Private Const AdhocViewPrefix As String = "vw_AdhocReport_"

    Private Sub New()
    End Sub

    Public Shared Function GetMetadata(ByVal reportId As Long) As AdhocReportMetadata
        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()
            Dim definition = LoadDefinition(connection, reportId)
            Dim query = AdhocQueryValidator.Normalize(definition.QueryText)
            Dim columns = ReadColumns(connection, query)

            Return New AdhocReportMetadata With {
                .ReportId = definition.ReportId,
                .Title = definition.Title,
                .Description = definition.Description,
                .Columns = columns
            }
        End Using
    End Function

    Public Shared Function GetPage(ByVal request As AdhocReportPageRequest) As AdhocReportPageResult
        If request Is Nothing Then
            Throw New ArgumentNullException("request")
        End If

        request.Start = Math.Max(0, request.Start)
        request.Length = Math.Max(1, Math.Min(MaximumPageSize, request.Length))

        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()
            Dim definition = LoadDefinition(connection, request.ReportId)
            Dim query = AdhocQueryValidator.Normalize(definition.QueryText)
            Dim columns = ReadColumns(connection, query)
            If columns.Count = 0 Then
                Throw New ConfigurationErrorsException("The adhoc query does not return any columns.")
            End If

            Dim totalRows = GetTotalRows(connection, definition.ReportId, query)
            Dim filter = BuildFilter(columns, request.Search, request.Filters)
            Dim filteredRows = If(filter.Sql.Length = 0, totalRows, ExecuteCount(connection, query, filter))
            Dim rows = ExecutePage(connection, query, columns, filter, request)

            Return New AdhocReportPageResult With {
                .Draw = request.Draw,
                .RecordsTotal = totalRows,
                .RecordsFiltered = filteredRows,
                .Data = rows
            }
        End Using
    End Function

    Public Shared Function GetInsights(ByVal request As AdhocReportInsightRequest) As AdhocReportInsightResult
        If request Is Nothing Then
            Throw New ArgumentNullException("request")
        End If

        request.Limit = Math.Max(5, Math.Min(50, request.Limit))

        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()
            Dim definition = LoadDefinition(connection, request.ReportId)
            Dim query = AdhocQueryValidator.Normalize(definition.QueryText)
            Dim columns = ReadColumns(connection, query)
            If request.GroupColumnIndex < 0 OrElse request.GroupColumnIndex >= columns.Count Then
                Throw New ArgumentOutOfRangeException("GroupColumnIndex", "Select a valid group column.")
            End If
            If request.SecondaryGroupColumnIndex < -1 OrElse request.SecondaryGroupColumnIndex >= columns.Count Then
                Throw New ArgumentOutOfRangeException("SecondaryGroupColumnIndex", "Select a valid secondary group column.")
            End If
            If request.SecondaryGroupColumnIndex = request.GroupColumnIndex Then
                Throw New ArgumentException("Primary and secondary group columns must be different.", "SecondaryGroupColumnIndex")
            End If
            If request.MeasureColumnIndex >= columns.Count Then
                Throw New ArgumentOutOfRangeException("MeasureColumnIndex", "Select a valid measure column.")
            End If
            If request.MeasureColumnIndex >= 0 AndAlso Not IsNumericColumn(columns(request.MeasureColumnIndex)) Then
                Throw New ArgumentException("The selected measure must be a numeric report column.", "MeasureColumnIndex")
            End If

            Dim filter = BuildFilter(columns, request.Search, request.Filters)
            Return ExecuteInsights(connection, query, definition.Title, columns, filter, request)
        End Using
    End Function

    Public Shared Function IsCurrentUserAdmin(ByVal context As HttpContext) As Boolean
        If context Is Nothing Then
            Return False
        End If

        Dim userName = GetWindowsUserName(context)
        If String.IsNullOrWhiteSpace(userName) Then
            Return False
        End If

        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()
            Using command = CreateCommand(connection, "SELECT COUNT(1) FROM dbo.Users WHERE Username = @username AND Is_Admin = 1")
                command.Parameters.Add("@username", SqlDbType.NVarChar, 256).Value = userName
                Return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0
            End Using
        End Using
    End Function

    Public Shared Function CreateAdhocReport(ByVal request As AdhocReportCreateRequest) As AdhocReportCreateResult
        If request Is Nothing Then
            Throw New ArgumentNullException("request")
        End If

        request.ReportName = NormalizeRequiredText(request.ReportName, "Report name", 200)
        request.ReportTitle = NormalizeRequiredText(request.ReportTitle, "Report title", 100)
        If request.SectionId <= 0 Then
            Throw New ArgumentException("Select a division.", "SectionId")
        End If

        Dim query = AdhocQueryValidator.Normalize(request.AdhocQuery)
        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()

            ' Compile the saved query and verify that every result column can be addressed safely.
            Dim columns = ReadColumns(connection, query)
            If columns.Count = 0 Then
                Throw New ConfigurationErrorsException("The adhoc query must return at least one named column.")
            End If

            Using transaction = connection.BeginTransaction(IsolationLevel.Serializable)
                Using sectionCommand = CreateCommand(connection, "SELECT COUNT(1) FROM dbo.Sections WHERE SectionID = @sectionId")
                    sectionCommand.Transaction = transaction
                    sectionCommand.Parameters.Add("@sectionId", SqlDbType.BigInt).Value = request.SectionId
                    If Convert.ToInt32(sectionCommand.ExecuteScalar(), CultureInfo.InvariantCulture) = 0 Then
                        Throw New ArgumentException("The selected division is unavailable.", "SectionId")
                    End If
                End Using

                Using typeCommand = CreateCommand(connection, "SELECT COUNT(1) FROM dbo.ReportTypes WHERE ReportTypeID = 3")
                    typeCommand.Transaction = transaction
                    If Convert.ToInt32(typeCommand.ExecuteScalar(), CultureInfo.InvariantCulture) = 0 Then
                        Throw New ConfigurationErrorsException("Custom report type 3 is not configured.")
                    End If
                End Using

                Dim nextSortOrder As Integer
                Using orderCommand = CreateCommand(connection,
                    "SELECT ISNULL(MAX(Sort_Order), 0) + 1 FROM dbo.Reports WITH (UPDLOCK, HOLDLOCK) " &
                    "WHERE Is_Deleted = 0 AND NULLIF(LTRIM(RTRIM(AdhocQuery)), '') IS NOT NULL")
                    orderCommand.Transaction = transaction
                    nextSortOrder = Convert.ToInt32(orderCommand.ExecuteScalar(), CultureInfo.InvariantCulture)
                End Using

                Const insertSql As String =
                    "INSERT INTO dbo.Reports " &
                    "(Report_Name, Report_Title, Sort_Order, SectionID, ReportTypeID, Is_Dynamic, Is_Deleted, AdhocQuery) " &
                    "VALUES (@reportName, @reportTitle, @sortOrder, @sectionId, 3, 0, 0, @adhocQuery); " &
                    "SELECT CAST(SCOPE_IDENTITY() AS bigint);"

                Dim reportId As Long
                Using insertCommand = CreateCommand(connection, insertSql)
                    insertCommand.Transaction = transaction
                    insertCommand.Parameters.Add("@reportName", SqlDbType.VarChar, 200).Value = request.ReportName
                    insertCommand.Parameters.Add("@reportTitle", SqlDbType.VarChar, 100).Value = request.ReportTitle
                    insertCommand.Parameters.Add("@sortOrder", SqlDbType.Int).Value = nextSortOrder
                    insertCommand.Parameters.Add("@sectionId", SqlDbType.BigInt).Value = request.SectionId
                    insertCommand.Parameters.Add("@adhocQuery", SqlDbType.VarChar, -1).Value = query
                    reportId = Convert.ToInt64(insertCommand.ExecuteScalar(), CultureInfo.InvariantCulture)
                End Using

                CreateOrAlterAdhocView(connection, transaction, reportId, query)

                transaction.Commit()
                Return New AdhocReportCreateResult With {
                    .ReportId = reportId,
                    .SortOrder = nextSortOrder,
                    .ViewName = GetPowerBiViewName(reportId),
                    .Url = "AdhocReport.aspx?reportId=" & reportId.ToString(CultureInfo.InvariantCulture)
                }
            End Using
        End Using
    End Function

    Public Shared Function UpdateAdhocReport(ByVal request As AdhocReportUpdateRequest) As AdhocReportUpdateResult
        If request Is Nothing Then
            Throw New ArgumentNullException("request")
        End If
        If request.ReportId <= 0 Then
            Throw New ArgumentOutOfRangeException("ReportId", "Select a valid adhoc report.")
        End If

        request.ReportName = NormalizeRequiredText(request.ReportName, "Report name", 200)
        request.ReportTitle = NormalizeRequiredText(request.ReportTitle, "Report title", 100)
        If request.SectionId <= 0 Then
            Throw New ArgumentException("Select a division.", "SectionId")
        End If
        If request.SortOrder <= 0 Then
            Throw New ArgumentException("Sort order must be greater than zero.", "SortOrder")
        End If

        Dim query = AdhocQueryValidator.Normalize(request.AdhocQuery)
        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()

            ' Compile the replacement query before changing the saved report definition.
            Dim columns = ReadColumns(connection, query)
            If columns.Count = 0 Then
                Throw New ConfigurationErrorsException("The adhoc query must return at least one named column.")
            End If

            Using transaction = connection.BeginTransaction(IsolationLevel.Serializable)
                Using sectionCommand = CreateCommand(connection, "SELECT COUNT(1) FROM dbo.Sections WHERE SectionID = @sectionId")
                    sectionCommand.Transaction = transaction
                    sectionCommand.Parameters.Add("@sectionId", SqlDbType.BigInt).Value = request.SectionId
                    If Convert.ToInt32(sectionCommand.ExecuteScalar(), CultureInfo.InvariantCulture) = 0 Then
                        Throw New ArgumentException("The selected division is unavailable.", "SectionId")
                    End If
                End Using

                Const updateSql As String =
                    "UPDATE dbo.Reports SET Report_Name = @reportName, Report_Title = @reportTitle, " &
                    "Sort_Order = @sortOrder, SectionID = @sectionId, AdhocQuery = @adhocQuery " &
                    "WHERE ReportID = @reportId AND Is_Deleted = 0 " &
                    "AND NULLIF(LTRIM(RTRIM(AdhocQuery)), '') IS NOT NULL"

                Using updateCommand = CreateCommand(connection, updateSql)
                    updateCommand.Transaction = transaction
                    updateCommand.Parameters.Add("@reportName", SqlDbType.VarChar, 200).Value = request.ReportName
                    updateCommand.Parameters.Add("@reportTitle", SqlDbType.VarChar, 100).Value = request.ReportTitle
                    updateCommand.Parameters.Add("@sortOrder", SqlDbType.Int).Value = request.SortOrder
                    updateCommand.Parameters.Add("@sectionId", SqlDbType.BigInt).Value = request.SectionId
                    updateCommand.Parameters.Add("@adhocQuery", SqlDbType.VarChar, -1).Value = query
                    updateCommand.Parameters.Add("@reportId", SqlDbType.BigInt).Value = request.ReportId
                    If updateCommand.ExecuteNonQuery() <> 1 Then
                        Throw New KeyNotFoundException("The selected adhoc report is unavailable.")
                    End If
                End Using

                CreateOrAlterAdhocView(connection, transaction, request.ReportId, query)

                transaction.Commit()
            End Using
        End Using

        Return New AdhocReportUpdateResult With {
            .ReportId = request.ReportId,
            .ViewName = GetPowerBiViewName(request.ReportId),
            .Url = "AdhocReport.aspx?reportId=" & request.ReportId.ToString(CultureInfo.InvariantCulture)
        }
    End Function

    Public Shared Function GetPowerBiViewName(ByVal reportId As Long) As String
        If reportId <= 0 Then
            Throw New ArgumentOutOfRangeException("reportId")
        End If

        Return AdhocViewSchema & "." & AdhocViewPrefix & reportId.ToString(CultureInfo.InvariantCulture)
    End Function

    Public Shared Function Export(ByVal request As AdhocReportPageRequest,
                                  ByVal format As String) As AdhocReportExportResult
        If request Is Nothing Then
            Throw New ArgumentNullException("request")
        End If

        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()
            Dim definition = LoadDefinition(connection, request.ReportId)
            Dim query = AdhocQueryValidator.Normalize(definition.QueryText)
            Dim columns = ReadColumns(connection, query)
            Dim filter = BuildFilter(columns, request.Search, request.Filters)
            Dim rows = ExecuteExportRows(connection, query, columns, filter, request)
            Dim fileBase = MakeSafeFileName(definition.Title) & "_" & DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)

            Select Case Convert.ToString(format).Trim().ToLowerInvariant()
                Case "csv"
                    Return New AdhocReportExportResult With {
                        .FileName = fileBase & ".csv",
                        .ContentType = "text/csv; charset=utf-8",
                        .Content = BuildCsv(columns, rows)
                    }
                Case "xlsx", "excel"
                    Return New AdhocReportExportResult With {
                        .FileName = fileBase & ".xlsx",
                        .ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        .Content = BuildXlsx(definition.Title, columns, rows)
                    }
                Case Else
                    Throw New ArgumentException("Select CSV or Excel export format.", "format")
            End Select
        End Using
    End Function

    Public Shared Function GetDistinctValues(ByVal reportId As Long,
                                             ByVal columnIndex As Integer,
                                             ByVal search As String) As List(Of AdhocDistinctValue)
        Using connection As New SqlConnection(GetConnectionString())
            connection.Open()
            Dim definition = LoadDefinition(connection, reportId)
            Dim query = AdhocQueryValidator.Normalize(definition.QueryText)
            Dim columns = ReadColumns(connection, query)
            If columnIndex < 0 OrElse columnIndex >= columns.Count Then
                Throw New ArgumentOutOfRangeException("columnIndex", "Select a valid report column.")
            End If

            Dim quotedColumn = QuoteIdentifier(columns(columnIndex).Name)
            Dim sql = New StringBuilder()
            sql.Append("SELECT TOP (").Append(MaximumDistinctValues.ToString(CultureInfo.InvariantCulture)).Append(") ")
            sql.Append("CONVERT(nvarchar(4000), ").Append(quotedColumn).Append(") AS FilterValue ")
            sql.Append("FROM (").Append(query).Append(") AS adhoc_source ")
            sql.Append("WHERE ").Append(quotedColumn).Append(" IS NOT NULL ")
            If Not String.IsNullOrWhiteSpace(search) Then
                sql.Append("AND CONVERT(nvarchar(4000), ").Append(quotedColumn).Append(") LIKE @distinctSearch ESCAPE '\' ")
            End If
            sql.Append("GROUP BY CONVERT(nvarchar(4000), ").Append(quotedColumn).Append(") ")
            sql.Append("ORDER BY FilterValue")

            Dim values As New List(Of AdhocDistinctValue)()
            Using command = CreateCommand(connection, sql.ToString())
                If Not String.IsNullOrWhiteSpace(search) Then
                    command.Parameters.Add("@distinctSearch", SqlDbType.NVarChar, 4000).Value = "%" & EscapeLikeValue(search.Trim()) & "%"
                End If

                Using reader = command.ExecuteReader()
                    While reader.Read()
                        Dim value = Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture)
                        values.Add(New AdhocDistinctValue With {.Value = value, .Label = value})
                    End While
                End Using
            End Using
            Return values
        End Using
    End Function

    Private Shared Function GetConnectionString() As String
        Dim setting = ConfigurationManager.ConnectionStrings("ReportingSuiteEntities3")
        If setting Is Nothing OrElse String.IsNullOrWhiteSpace(setting.ConnectionString) Then
            Throw New ConfigurationErrorsException("ReportingSuiteEntities3 is not configured.")
        End If

        Dim builder As New EntityConnectionStringBuilder(setting.ConnectionString)
        Return builder.ProviderConnectionString
    End Function

    Private Shared Function LoadDefinition(ByVal connection As SqlConnection,
                                           ByVal reportId As Long) As AdhocReportDefinition
        If reportId <= 0 Then
            Throw New ArgumentOutOfRangeException("reportId", "Select an adhoc report.")
        End If

        Const sql As String =
            "SELECT ReportID, COALESCE(NULLIF(LTRIM(RTRIM(Report_Title)), ''), NULLIF(LTRIM(RTRIM(Report_Name)), ''), 'Adhoc report') AS ReportTitle, " &
            "COALESCE(Report_Description, '') AS ReportDescription, AdhocQuery " &
            "FROM dbo.Reports WHERE ReportID = @reportId AND Is_Deleted = 0 " &
            "AND NULLIF(LTRIM(RTRIM(AdhocQuery)), '') IS NOT NULL"

        Using command = CreateCommand(connection, sql)
            command.Parameters.Add("@reportId", SqlDbType.BigInt).Value = reportId
            Using reader = command.ExecuteReader(CommandBehavior.SingleRow)
                If Not reader.Read() Then
                    Throw New KeyNotFoundException("The selected adhoc report is unavailable.")
                End If

                Return New AdhocReportDefinition With {
                    .ReportId = reader.GetInt64(0),
                    .Title = reader.GetString(1),
                    .Description = reader.GetString(2),
                    .QueryText = reader.GetString(3)
                }
            End Using
        End Using
    End Function

    Private Shared Function ReadColumns(ByVal connection As SqlConnection,
                                        ByVal query As String) As List(Of AdhocReportColumn)
        Dim columns As New List(Of AdhocReportColumn)()
        Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim sql = "SELECT TOP (0) * FROM (" & query & ") AS adhoc_source"

        Using command = CreateCommand(connection, sql)
            Using reader = command.ExecuteReader(CommandBehavior.SchemaOnly)
                For index = 0 To reader.FieldCount - 1
                    Dim name = reader.GetName(index)
                    If String.IsNullOrWhiteSpace(name) Then
                        Throw New ConfigurationErrorsException("Every adhoc query column must have a name or alias.")
                    End If
                    If Not names.Add(name) Then
                        Throw New ConfigurationErrorsException("Adhoc query column names must be unique. Duplicate: " & name)
                    End If

                    columns.Add(New AdhocReportColumn With {
                        .Index = index,
                        .Name = name,
                        .Title = FormatColumnTitle(name),
                        .DataType = reader.GetFieldType(index).Name
                    })
                Next
            End Using
        End Using
        Return columns
    End Function

    Private Shared Function GetTotalRows(ByVal connection As SqlConnection,
                                         ByVal reportId As Long,
                                         ByVal query As String) As Long
        Dim cacheKey = "ReportingSuite.Adhoc.Total." & reportId.ToString(CultureInfo.InvariantCulture) & "." & query.GetHashCode().ToString(CultureInfo.InvariantCulture)
        Dim cached = HttpRuntime.Cache(cacheKey)
        If cached IsNot Nothing Then
            Return DirectCast(cached, Long)
        End If

        Dim total = ExecuteCount(connection, query, New AdhocSqlFilter())
        HttpRuntime.Cache.Insert(cacheKey, total, Nothing, DateTime.UtcNow.AddMinutes(5), System.Web.Caching.Cache.NoSlidingExpiration)
        Return total
    End Function

    Private Shared Function ExecuteCount(ByVal connection As SqlConnection,
                                         ByVal query As String,
                                         ByVal filter As AdhocSqlFilter) As Long
        Dim sql = "SELECT COUNT_BIG(1) FROM (" & query & ") AS adhoc_source" & filter.Sql
        Using command = CreateCommand(connection, sql)
            AddFilterParameters(command, filter)
            Return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture)
        End Using
    End Function

    Private Shared Function ExecutePage(ByVal connection As SqlConnection,
                                        ByVal query As String,
                                        ByVal columns As List(Of AdhocReportColumn),
                                        ByVal filter As AdhocSqlFilter,
                                        ByVal request As AdhocReportPageRequest) As List(Of Object())
        Dim orderIndex = request.OrderColumnIndex
        If orderIndex < 0 OrElse orderIndex >= columns.Count Then
            orderIndex = 0
        End If
        Dim direction = If(String.Equals(request.OrderDirection, "desc", StringComparison.OrdinalIgnoreCase), "DESC", "ASC")
        Dim sql = "SELECT * FROM (" & query & ") AS adhoc_source" & filter.Sql &
            " ORDER BY " & QuoteIdentifier(columns(orderIndex).Name) & " " & direction &
            " OFFSET @start ROWS FETCH NEXT @length ROWS ONLY"

        Dim rows As New List(Of Object())()
        Using command = CreateCommand(connection, sql)
            AddFilterParameters(command, filter)
            command.Parameters.Add("@start", SqlDbType.Int).Value = request.Start
            command.Parameters.Add("@length", SqlDbType.Int).Value = request.Length

            Using reader = command.ExecuteReader()
                While reader.Read()
                    Dim row(reader.FieldCount - 1) As Object
                    For index = 0 To reader.FieldCount - 1
                        row(index) = ConvertForJson(reader.GetValue(index))
                    Next
                    rows.Add(row)
                End While
            End Using
        End Using
        Return rows
    End Function

    Private Shared Function ExecuteExportRows(ByVal connection As SqlConnection,
                                              ByVal query As String,
                                              ByVal columns As List(Of AdhocReportColumn),
                                              ByVal filter As AdhocSqlFilter,
                                              ByVal request As AdhocReportPageRequest) As List(Of Object())
        If columns.Count = 0 Then
            Return New List(Of Object())()
        End If

        Dim orderIndex = request.OrderColumnIndex
        If orderIndex < 0 OrElse orderIndex >= columns.Count Then
            orderIndex = 0
        End If
        Dim direction = If(String.Equals(request.OrderDirection, "desc", StringComparison.OrdinalIgnoreCase), "DESC", "ASC")
        Dim sql = "SELECT TOP (" & (MaximumExportRows + 1).ToString(CultureInfo.InvariantCulture) & ") * " &
            "FROM (" & query & ") AS adhoc_source" & filter.Sql &
            " ORDER BY " & QuoteIdentifier(columns(orderIndex).Name) & " " & direction

        Dim rows As New List(Of Object())()
        Using command = CreateCommand(connection, sql)
            AddFilterParameters(command, filter)
            Using reader = command.ExecuteReader()
                While reader.Read()
                    If rows.Count >= MaximumExportRows Then
                        Throw New ConfigurationErrorsException(
                            "This export exceeds " & MaximumExportRows.ToString("N0", CultureInfo.InvariantCulture) &
                            " rows. Apply filters and export again.")
                    End If

                    Dim row(reader.FieldCount - 1) As Object
                    For index = 0 To reader.FieldCount - 1
                        row(index) = ConvertForJson(reader.GetValue(index))
                    Next
                    rows.Add(row)
                End While
            End Using
        End Using
        Return rows
    End Function

    Private Shared Function ExecuteInsights(ByVal connection As SqlConnection,
                                            ByVal query As String,
                                            ByVal reportTitle As String,
                                            ByVal columns As List(Of AdhocReportColumn),
                                            ByVal filter As AdhocSqlFilter,
                                            ByVal request As AdhocReportInsightRequest) As AdhocReportInsightResult
        Dim groupColumn = columns(request.GroupColumnIndex)
        Dim groupExpression = "COALESCE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(4000), " &
            QuoteIdentifier(groupColumn.Name) & "))), N''), N'(Blank)')"
        Dim hasSecondaryGroup = request.SecondaryGroupColumnIndex >= 0
        Dim secondaryGroupColumn As AdhocReportColumn = Nothing
        Dim secondaryGroupExpression As String = Nothing
        If hasSecondaryGroup Then
            secondaryGroupColumn = columns(request.SecondaryGroupColumnIndex)
            secondaryGroupExpression = "COALESCE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(4000), " &
                QuoteIdentifier(secondaryGroupColumn.Name) & "))), N''), N'(Blank)')"
        End If
        Dim measureExpression As String
        Dim measureTitle As String

        If request.MeasureColumnIndex < 0 Then
            measureExpression = "CONVERT(decimal(38, 6), COUNT_BIG(1))"
            measureTitle = "Row count"
        Else
            Dim measureColumn = columns(request.MeasureColumnIndex)
            measureExpression = "COALESCE(SUM(TRY_CONVERT(decimal(38, 6), " &
                QuoteIdentifier(measureColumn.Name) & ")), CONVERT(decimal(38, 6), 0))"
            measureTitle = "Sum of " & measureColumn.Title
        End If

        Dim result As New AdhocReportInsightResult With {
            .ReportTitle = reportTitle,
            .GroupColumnTitle = groupColumn.Title,
            .SecondaryGroupColumnTitle = If(hasSecondaryGroup, secondaryGroupColumn.Title, String.Empty),
            .MeasureTitle = measureTitle,
            .Groups = New List(Of AdhocReportInsightGroup)()
        }

        Dim sql = New StringBuilder()
        sql.Append(";WITH filtered_source AS (")
        sql.Append("SELECT * FROM (").Append(query).Append(") AS adhoc_source").Append(filter.Sql)
        sql.Append("), grouped_data AS (")
        sql.Append("SELECT ").Append(groupExpression).Append(" AS [GroupLabel], ")
        If hasSecondaryGroup Then
            sql.Append(secondaryGroupExpression).Append(" AS [SecondaryGroupLabel], ")
        End If
        sql.Append("COUNT_BIG(1) AS [RowCount], ").Append(measureExpression).Append(" AS [MeasureValue] ")
        sql.Append("FROM filtered_source GROUP BY ").Append(groupExpression)
        If hasSecondaryGroup Then
            sql.Append(", ").Append(secondaryGroupExpression)
        End If

        If hasSecondaryGroup Then
            sql.Append("), primary_totals AS (")
            sql.Append("SELECT [GroupLabel], SUM([RowCount]) AS [GroupRowCount], SUM([MeasureValue]) AS [GroupMeasureValue] ")
            sql.Append("FROM grouped_data GROUP BY [GroupLabel]")
            sql.Append("), primary_ranked AS (")
            sql.Append("SELECT [GroupLabel], [GroupRowCount], [GroupMeasureValue], ")
            sql.Append("ROW_NUMBER() OVER (ORDER BY [GroupMeasureValue] DESC, [GroupLabel] ASC) AS [PrimaryRank] FROM primary_totals")
            sql.Append("), secondary_totals AS (")
            sql.Append("SELECT [SecondaryGroupLabel], SUM([MeasureValue]) AS [SecondaryMeasureValue] ")
            sql.Append("FROM grouped_data GROUP BY [SecondaryGroupLabel]")
            sql.Append("), secondary_ranked AS (")
            sql.Append("SELECT [SecondaryGroupLabel], ROW_NUMBER() OVER (ORDER BY [SecondaryMeasureValue] DESC, [SecondaryGroupLabel] ASC) AS [SecondaryRank] ")
            sql.Append("FROM secondary_totals")
            sql.Append("), report_totals AS (")
            sql.Append("SELECT (SELECT COUNT_BIG(1) FROM primary_totals) AS [TotalGroups], ")
            sql.Append("(SELECT COUNT_BIG(1) FROM secondary_totals) AS [TotalSecondaryGroups], ")
            sql.Append("COALESCE(SUM([RowCount]), 0) AS [TotalRows], COALESCE(SUM([MeasureValue]), 0) AS [TotalValue] FROM grouped_data")
            sql.Append(") SELECT g.[GroupLabel], g.[SecondaryGroupLabel], g.[RowCount], g.[MeasureValue], ")
            sql.Append("t.[TotalGroups], t.[TotalRows], t.[TotalValue], t.[TotalSecondaryGroups], p.[GroupRowCount], p.[GroupMeasureValue] ")
            sql.Append("FROM grouped_data g INNER JOIN primary_ranked p ON p.[GroupLabel] = g.[GroupLabel] ")
            sql.Append("INNER JOIN secondary_ranked s ON s.[SecondaryGroupLabel] = g.[SecondaryGroupLabel] CROSS JOIN report_totals t ")
            sql.Append("WHERE p.[PrimaryRank] <= @insightLimit AND s.[SecondaryRank] <= @secondaryLimit ")
            sql.Append("ORDER BY p.[PrimaryRank], s.[SecondaryRank]")
        Else
            sql.Append(") SELECT TOP (@insightLimit) [GroupLabel], [RowCount], [MeasureValue], ")
            sql.Append("COUNT_BIG(1) OVER() AS [TotalGroups], SUM([RowCount]) OVER() AS [TotalRows], ")
            sql.Append("SUM([MeasureValue]) OVER() AS [TotalValue] ")
            sql.Append("FROM grouped_data ORDER BY [MeasureValue] DESC, [GroupLabel] ASC")
        End If

        Using command = CreateCommand(connection, sql.ToString())
            AddFilterParameters(command, filter)
            command.Parameters.Add("@insightLimit", SqlDbType.Int).Value = request.Limit
            If hasSecondaryGroup Then
                command.Parameters.Add("@secondaryLimit", SqlDbType.Int).Value = Math.Min(10, request.Limit)
            End If
            Using reader = command.ExecuteReader()
                While reader.Read()
                    If hasSecondaryGroup Then
                        result.Groups.Add(New AdhocReportInsightGroup With {
                            .Label = Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture),
                            .SecondaryLabel = Convert.ToString(reader.GetValue(1), CultureInfo.CurrentCulture),
                            .RowCount = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                            .Value = Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                            .GroupRowCount = Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture),
                            .GroupValue = Convert.ToDecimal(reader.GetValue(9), CultureInfo.InvariantCulture)
                        })
                        result.GroupCount = Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture)
                        result.TotalRows = Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture)
                        result.TotalValue = Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture)
                        result.SecondaryGroupCount = Convert.ToInt64(reader.GetValue(7), CultureInfo.InvariantCulture)
                    Else
                        Dim rowCount = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture)
                        Dim value = Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture)
                        result.Groups.Add(New AdhocReportInsightGroup With {
                            .Label = Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture),
                            .SecondaryLabel = String.Empty,
                            .RowCount = rowCount,
                            .Value = value,
                            .GroupRowCount = rowCount,
                            .GroupValue = value
                        })
                        result.GroupCount = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture)
                        result.TotalRows = Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture)
                        result.TotalValue = Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture)
                    End If
                End While
            End Using
        End Using

        result.DisplayedGroupCount = result.Groups.Select(Function(item) item.Label).Distinct(StringComparer.Ordinal).Count()
        result.DisplayedSecondaryGroupCount = result.Groups.Where(Function(item) Not String.IsNullOrEmpty(item.SecondaryLabel)).
            Select(Function(item) item.SecondaryLabel).Distinct(StringComparer.Ordinal).Count()
        result.IsTruncated = result.GroupCount > result.DisplayedGroupCount OrElse
            result.SecondaryGroupCount > result.DisplayedSecondaryGroupCount
        Return result
    End Function

    Private Shared Function IsNumericColumn(ByVal column As AdhocReportColumn) As Boolean
        Select Case Convert.ToString(column.DataType).Trim().ToLowerInvariant()
            Case "byte", "sbyte", "int16", "uint16", "int32", "uint32", "int64", "uint64", "single", "double", "decimal"
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function BuildFilter(ByVal columns As List(Of AdhocReportColumn),
                                        ByVal search As String,
                                        ByVal requestedFilters As List(Of AdhocReportColumnFilter)) As AdhocSqlFilter
        Dim conditions As New List(Of String)()
        Dim filter As New AdhocSqlFilter()

        If Not String.IsNullOrWhiteSpace(search) Then
            Dim searchable = columns.Take(MaximumSearchColumns).
                Select(Function(column) "CONVERT(nvarchar(4000), " & QuoteIdentifier(column.Name) & ") LIKE @search ESCAPE '\'").
                ToList()
            If searchable.Count > 0 Then
                conditions.Add("(" & String.Join(" OR ", searchable) & ")")
                filter.Parameters.Add(New AdhocSqlParameter With {
                    .Name = "@search",
                    .Value = "%" & EscapeLikeValue(search.Trim()) & "%"
                })
            End If
        End If

        Dim groupedFilters As New Dictionary(Of Integer, HashSet(Of String))()
        For Each requested In If(requestedFilters, New List(Of AdhocReportColumnFilter)()).Take(MaximumFilterColumns)
            If requested Is Nothing OrElse requested.ColumnIndex < 0 OrElse requested.ColumnIndex >= columns.Count Then
                Continue For
            End If

            Dim values As HashSet(Of String) = Nothing
            If Not groupedFilters.TryGetValue(requested.ColumnIndex, values) Then
                values = New HashSet(Of String)(StringComparer.Ordinal)
                groupedFilters.Add(requested.ColumnIndex, values)
            End If

            For Each value In If(requested.Values, New List(Of String)())
                If value IsNot Nothing AndAlso values.Count < MaximumFilterValuesPerColumn Then
                    values.Add(value)
                End If
            Next
        Next

        Dim filterNumber = 0
        For Each entry In groupedFilters.OrderBy(Function(item) item.Key)
            If entry.Value.Count = 0 Then
                Continue For
            End If

            Dim parameterNames As New List(Of String)()
            Dim valueNumber = 0
            For Each value In entry.Value
                Dim parameterName = "@filter" & filterNumber.ToString(CultureInfo.InvariantCulture) & "_" & valueNumber.ToString(CultureInfo.InvariantCulture)
                parameterNames.Add(parameterName)
                filter.Parameters.Add(New AdhocSqlParameter With {.Name = parameterName, .Value = value})
                valueNumber += 1
            Next

            conditions.Add("CONVERT(nvarchar(4000), " & QuoteIdentifier(columns(entry.Key).Name) & ") IN (" & String.Join(", ", parameterNames) & ")")
            filterNumber += 1
        Next

        If conditions.Count > 0 Then
            filter.Sql = " WHERE " & String.Join(" AND ", conditions)
        End If
        Return filter
    End Function

    Private Shared Sub AddFilterParameters(ByVal command As SqlCommand,
                                           ByVal filter As AdhocSqlFilter)
        For Each item In filter.Parameters
            command.Parameters.Add(item.Name, SqlDbType.NVarChar, 4000).Value = item.Value
        Next
    End Sub

    Private Shared Function CreateCommand(ByVal connection As SqlConnection,
                                          ByVal sql As String) As SqlCommand
        Dim command = connection.CreateCommand()
        command.CommandText = sql
        command.CommandType = CommandType.Text
        command.CommandTimeout = CommandTimeoutSeconds
        Return command
    End Function

    Private Shared Sub CreateOrAlterAdhocView(ByVal connection As SqlConnection,
                                              ByVal transaction As SqlTransaction,
                                              ByVal reportId As Long,
                                              ByVal query As String)
        Dim viewName = GetPowerBiViewName(reportId)
        Dim quotedViewName = QuoteIdentifier(AdhocViewSchema) & "." &
                             QuoteIdentifier(AdhocViewPrefix & reportId.ToString(CultureInfo.InvariantCulture))

        Try
            ' CREATE VIEW must be the first statement in its batch. Set the persisted
            ' view options in a separate command on the same connection first.
            Using settingsCommand = CreateCommand(connection, "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;")
                settingsCommand.Transaction = transaction
                settingsCommand.ExecuteNonQuery()
            End Using

            ' CREATE OR ALTER is unavailable on older SQL Server releases. Determine
            ' the required DDL verb first, then keep CREATE/ALTER VIEW as the first
            ' statement in its own command batch.
            Dim viewExists As Boolean
            Using existenceCommand = CreateCommand(connection,
                "SELECT CASE WHEN OBJECT_ID(@viewName, N'V') IS NULL THEN 0 ELSE 1 END")
                existenceCommand.Transaction = transaction
                existenceCommand.Parameters.Add("@viewName", SqlDbType.NVarChar, 776).Value = viewName
                viewExists = Convert.ToInt32(existenceCommand.ExecuteScalar(), CultureInfo.InvariantCulture) = 1
            End Using

            Dim viewCommandText = If(viewExists, "ALTER VIEW ", "CREATE VIEW ") &
                                  quotedViewName & " AS" & Environment.NewLine & query
            Using viewCommand = CreateCommand(connection,
                viewCommandText)
                viewCommand.Transaction = transaction
                viewCommand.ExecuteNonQuery()
            End Using
        Catch ex As SqlException
            Dim sqlMessage = Regex.Replace(Convert.ToString(ex.Message), "\s+", " ").Trim()
            Dim permissionHint = String.Empty
            If ex.Number = 229 OrElse ex.Number = 262 OrElse ex.Number = 15151 Then
                permissionHint = " Grant CREATE VIEW at database level and ALTER on schema dbo to the Reporting Suite database principal."
            End If
            Throw New ConfigurationErrorsException(
                "The report could not be saved because Power BI view " & viewName &
                " could not be created. SQL Server error " & ex.Number.ToString(CultureInfo.InvariantCulture) &
                ": " & sqlMessage & "." & permissionHint,
                ex)
        End Try
    End Sub

    Private Shared Function QuoteIdentifier(ByVal name As String) As String
        Return "[" & name.Replace("]", "]]") & "]"
    End Function

    Private Shared Function EscapeLikeValue(ByVal value As String) As String
        Return value.Replace("\", "\\").Replace("%", "\%").Replace("_", "\_").Replace("[", "\[")
    End Function

    Private Shared Function ConvertForJson(ByVal value As Object) As Object
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return Nothing
        End If
        If TypeOf value Is DateTime Then
            Return DirectCast(value, DateTime).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        End If
        If TypeOf value Is DateTimeOffset Then
            Return DirectCast(value, DateTimeOffset).ToString("o", CultureInfo.InvariantCulture)
        End If
        If TypeOf value Is Byte() Then
            Return "[binary data]"
        End If
        Return value
    End Function

    Private Shared Function FormatColumnTitle(ByVal name As String) As String
        Dim title = name.Replace("_", " ")
        Return Regex.Replace(title, "(?<=[a-z0-9])(?=[A-Z])", " ")
    End Function

    Private Shared Function NormalizeRequiredText(ByVal value As String,
                                                  ByVal label As String,
                                                  ByVal maximumLength As Integer) As String
        Dim normalized = Convert.ToString(value).Trim()
        If normalized.Length = 0 Then
            Throw New ArgumentException(label & " is required.", label)
        End If
        If normalized.Length > maximumLength Then
            Throw New ArgumentException(label & " cannot exceed " & maximumLength.ToString(CultureInfo.InvariantCulture) & " characters.", label)
        End If
        Return normalized
    End Function

    Private Shared Function GetWindowsUserName(ByVal context As HttpContext) As String
        If context.Request.LogonUserIdentity IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(context.Request.LogonUserIdentity.Name) Then
            Return context.Request.LogonUserIdentity.Name
        End If
        If context.User IsNot Nothing AndAlso context.User.Identity IsNot Nothing Then
            Return Convert.ToString(context.User.Identity.Name)
        End If
        Return String.Empty
    End Function

    Private Shared Function MakeSafeFileName(ByVal value As String) As String
        Dim safe = Regex.Replace(Convert.ToString(value), "[^A-Za-z0-9._-]+", "_").Trim("_"c, "."c)
        If safe.Length = 0 Then
            safe = "AdhocReport"
        End If
        Return safe.Substring(0, Math.Min(80, safe.Length))
    End Function

    Private Shared Function BuildCsv(ByVal columns As List(Of AdhocReportColumn),
                                     ByVal rows As List(Of Object())) As Byte()
        Using stream As New MemoryStream()
            stream.Write(New Byte() {&HEF, &HBB, &HBF}, 0, 3)
            Using writer As New StreamWriter(stream, New UTF8Encoding(False), 4096, True)
                writer.WriteLine(String.Join(",", columns.Select(Function(column) CsvCell(column.Title))))
                For Each row In rows
                    writer.WriteLine(String.Join(",", row.Select(Function(value) CsvCell(FormatExportValue(value)))))
                Next
            End Using
            Return stream.ToArray()
        End Using
    End Function

    Private Shared Function CsvCell(ByVal value As String) As String
        Dim normalized = Convert.ToString(value)
        If normalized.IndexOfAny(New Char() {","c, """"c, ControlChars.Cr, ControlChars.Lf}) >= 0 Then
            Return """" & normalized.Replace("""", """""") & """"
        End If
        Return normalized
    End Function

    Private Shared Function BuildXlsx(ByVal title As String,
                                      ByVal columns As List(Of AdhocReportColumn),
                                      ByVal rows As List(Of Object())) As Byte()
        Using stream As New MemoryStream()
            Using archive As New ZipArchive(stream, ZipArchiveMode.Create, True)
                WriteTextEntry(archive, "[Content_Types].xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" &
                    "<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>" &
                    "<Default Extension=""xml"" ContentType=""application/xml""/>" &
                    "<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>" &
                    "<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>" &
                    "<Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>" &
                    "</Types>")
                WriteTextEntry(archive, "_rels/.rels",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" &
                    "<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>" &
                    "</Relationships>")
                WriteTextEntry(archive, "xl/workbook.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">" &
                    "<sheets><sheet name=""" & XmlEscape(MakeSafeSheetName(title)) & """ sheetId=""1"" r:id=""rId1""/></sheets></workbook>")
                WriteTextEntry(archive, "xl/_rels/workbook.xml.rels",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" &
                    "<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>" &
                    "<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>" &
                    "</Relationships>")
                WriteTextEntry(archive, "xl/styles.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">" &
                    "<fonts count=""2""><font><sz val=""11""/><name val=""Calibri""/></font><font><b/><sz val=""11""/><name val=""Calibri""/></font></fonts>" &
                    "<fills count=""2""><fill><patternFill patternType=""none""/></fill><fill><patternFill patternType=""gray125""/></fill></fills>" &
                    "<borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>" &
                    "<cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>" &
                    "<cellXfs count=""2""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/><xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/></cellXfs>" &
                    "</styleSheet>")
                WriteWorksheetEntry(archive, columns, rows)
            End Using
            Return stream.ToArray()
        End Using
    End Function

    Private Shared Sub WriteTextEntry(ByVal archive As ZipArchive,
                                      ByVal path As String,
                                      ByVal content As String)
        Dim entry = archive.CreateEntry(path, CompressionLevel.Optimal)
        Using writer As New StreamWriter(entry.Open(), New UTF8Encoding(False))
            writer.Write(content)
        End Using
    End Sub

    Private Shared Sub WriteWorksheetEntry(ByVal archive As ZipArchive,
                                           ByVal columns As List(Of AdhocReportColumn),
                                           ByVal rows As List(Of Object()))
        Const spreadsheetNamespace As String = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
        Dim entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal)
        Dim settings As New XmlWriterSettings With {.Encoding = New UTF8Encoding(False), .CloseOutput = False}

        Using entryStream = entry.Open()
            Using writer = XmlWriter.Create(entryStream, settings)
                writer.WriteStartDocument(True)
                writer.WriteStartElement("worksheet", spreadsheetNamespace)
                writer.WriteStartElement("sheetViews", spreadsheetNamespace)
                writer.WriteStartElement("sheetView", spreadsheetNamespace)
                writer.WriteAttributeString("workbookViewId", "0")
                writer.WriteStartElement("pane", spreadsheetNamespace)
                writer.WriteAttributeString("ySplit", "1")
                writer.WriteAttributeString("topLeftCell", "A2")
                writer.WriteAttributeString("activePane", "bottomLeft")
                writer.WriteAttributeString("state", "frozen")
                writer.WriteEndElement()
                writer.WriteEndElement()
                writer.WriteEndElement()
                writer.WriteStartElement("sheetData", spreadsheetNamespace)

                WriteWorksheetRow(writer, columns.Select(Function(column) DirectCast(column.Title, Object)).ToArray(), 1, True)
                Dim rowNumber = 2
                For Each row In rows
                    WriteWorksheetRow(writer, row, rowNumber, False)
                    rowNumber += 1
                Next
                writer.WriteEndElement()

                If columns.Count > 0 Then
                    writer.WriteStartElement("autoFilter", spreadsheetNamespace)
                    writer.WriteAttributeString("ref", "A1:" & GetExcelColumnName(columns.Count - 1) & Math.Max(1, rows.Count + 1).ToString(CultureInfo.InvariantCulture))
                    writer.WriteEndElement()
                End If

                writer.WriteEndElement()
                writer.WriteEndDocument()
            End Using
        End Using
    End Sub

    Private Shared Sub WriteWorksheetRow(ByVal writer As XmlWriter,
                                         ByVal values As Object(),
                                         ByVal rowNumber As Integer,
                                         ByVal header As Boolean)
        writer.WriteStartElement("row")
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture))
        For index = 0 To values.Length - 1
            WriteWorksheetCell(writer, values(index), GetExcelColumnName(index) & rowNumber.ToString(CultureInfo.InvariantCulture), header)
        Next
        writer.WriteEndElement()
    End Sub

    Private Shared Sub WriteWorksheetCell(ByVal writer As XmlWriter,
                                          ByVal value As Object,
                                          ByVal reference As String,
                                          ByVal header As Boolean)
        writer.WriteStartElement("c")
        writer.WriteAttributeString("r", reference)
        If header Then
            writer.WriteAttributeString("s", "1")
        End If

        If value IsNot Nothing AndAlso IsNumericExportValue(value) Then
            writer.WriteAttributeString("t", "n")
            writer.WriteElementString("v", Convert.ToString(value, CultureInfo.InvariantCulture))
        ElseIf TypeOf value Is Boolean Then
            writer.WriteAttributeString("t", "b")
            writer.WriteElementString("v", If(DirectCast(value, Boolean), "1", "0"))
        Else
            writer.WriteAttributeString("t", "inlineStr")
            writer.WriteStartElement("is")
            writer.WriteStartElement("t")
            writer.WriteAttributeString("xml", "space", Nothing, "preserve")
            writer.WriteString(FormatExportValue(value))
            writer.WriteEndElement()
            writer.WriteEndElement()
        End If
        writer.WriteEndElement()
    End Sub

    Private Shared Function IsNumericExportValue(ByVal value As Object) As Boolean
        Return TypeOf value Is Byte OrElse TypeOf value Is SByte OrElse
               TypeOf value Is Short OrElse TypeOf value Is UShort OrElse
               TypeOf value Is Integer OrElse TypeOf value Is UInteger OrElse
               TypeOf value Is Long OrElse TypeOf value Is ULong OrElse
               TypeOf value Is Decimal OrElse TypeOf value Is Single OrElse
               TypeOf value Is Double
    End Function

    Private Shared Function FormatExportValue(ByVal value As Object) As String
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return String.Empty
        End If
        If TypeOf value Is IFormattable Then
            Return DirectCast(value, IFormattable).ToString(Nothing, CultureInfo.InvariantCulture)
        End If
        Return Convert.ToString(value, CultureInfo.InvariantCulture)
    End Function

    Private Shared Function GetExcelColumnName(ByVal zeroBasedIndex As Integer) As String
        Dim number = zeroBasedIndex + 1
        Dim name = String.Empty
        While number > 0
            number -= 1
            name = ChrW(AscW("A"c) + (number Mod 26)) & name
            number \= 26
        End While
        Return name
    End Function

    Private Shared Function MakeSafeSheetName(ByVal value As String) As String
        Dim name = Regex.Replace(Convert.ToString(value), "[\\/:?*\[\]]", " ").Trim()
        If name.Length = 0 Then
            name = "Adhoc Report"
        End If
        Return name.Substring(0, Math.Min(31, name.Length))
    End Function

    Private Shared Function XmlEscape(ByVal value As String) As String
        Return Convert.ToString(value).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;").Replace("'", "&apos;")
    End Function
End Class

Public NotInheritable Class AdhocQueryValidator
    Private Shared ReadOnly ForbiddenTokens As New HashSet(Of String)(
        New String() {"ALTER", "BACKUP", "BULK", "CREATE", "DBCC", "DECLARE", "DELETE", "DENY", "DROP", "EXEC", "EXECUTE", "GRANT", "INSERT", "INTO", "MERGE", "OPENQUERY", "OPENROWSET", "OPENDATASOURCE", "RECONFIGURE", "RESTORE", "REVOKE", "SET", "TRUNCATE", "UPDATE", "USE", "WAITFOR"},
        StringComparer.OrdinalIgnoreCase)

    Private Sub New()
    End Sub

    Public Shared Function Normalize(ByVal queryText As String) As String
        If String.IsNullOrWhiteSpace(queryText) Then
            Throw New ConfigurationErrorsException("This report does not contain an adhoc query.")
        End If

        Dim query = queryText.Trim()
        If query.EndsWith(";", StringComparison.Ordinal) Then
            query = query.Substring(0, query.Length - 1).TrimEnd()
        End If

        Dim sanitized = Sanitize(query)
        If Not Regex.IsMatch(sanitized, "^\s*SELECT\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant) Then
            Throw New ConfigurationErrorsException("AdhocQuery must be a single SELECT statement.")
        End If
        If ContainsTopLevelOrderBy(sanitized) Then
            Throw New ConfigurationErrorsException("Top-level ORDER BY is not supported. ORDER BY inside a valid subquery or window expression is allowed.")
        End If
        If Regex.IsMatch(sanitized, "\bFOR\s+(XML|JSON)\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant) Then
            Throw New ConfigurationErrorsException("FOR XML and FOR JSON are not supported in AdhocQuery.")
        End If

        For Each token As Match In Regex.Matches(sanitized, "\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant)
            If ForbiddenTokens.Contains(token.Value) Then
                Throw New ConfigurationErrorsException("AdhocQuery contains a statement or keyword that is not allowed: " & token.Value.ToUpperInvariant())
            End If
        Next
        Return query
    End Function

    Private Shared Function ContainsTopLevelOrderBy(ByVal sanitized As String) As Boolean
        Dim depth = 0
        Dim index = 0

        While index < sanitized.Length
            Dim current = sanitized(index)
            If current = "("c Then
                depth += 1
                index += 1
                Continue While
            End If
            If current = ")"c Then
                depth = Math.Max(0, depth - 1)
                index += 1
                Continue While
            End If

            If depth = 0 AndAlso IsKeywordAt(sanitized, index, "ORDER") Then
                Dim byIndex = index + "ORDER".Length
                While byIndex < sanitized.Length AndAlso Char.IsWhiteSpace(sanitized(byIndex))
                    byIndex += 1
                End While
                If IsKeywordAt(sanitized, byIndex, "BY") Then
                    Return True
                End If
            End If

            index += 1
        End While

        Return False
    End Function

    Private Shared Function IsKeywordAt(ByVal value As String,
                                        ByVal index As Integer,
                                        ByVal keyword As String) As Boolean
        If index < 0 OrElse index + keyword.Length > value.Length Then
            Return False
        End If
        If index > 0 AndAlso IsIdentifierCharacter(value(index - 1)) Then
            Return False
        End If
        If index + keyword.Length < value.Length AndAlso IsIdentifierCharacter(value(index + keyword.Length)) Then
            Return False
        End If

        Return String.Compare(value, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) = 0
    End Function

    Private Shared Function IsIdentifierCharacter(ByVal value As Char) As Boolean
        Return Char.IsLetterOrDigit(value) OrElse value = "_"c
    End Function

    Private Shared Function Sanitize(ByVal query As String) As String
        Dim result As New StringBuilder(query.Length)
        Dim index = 0
        Dim state As Integer = 0 ' 0 normal, 1 string, 2 bracket, 3 line comment, 4 block comment

        While index < query.Length
            Dim current = query(index)
            Dim nextCharacter = If(index + 1 < query.Length, query(index + 1), ChrW(0))

            Select Case state
                Case 0
                    If current = "'"c Then
                        state = 1
                        result.Append(" "c)
                    ElseIf current = "["c Then
                        state = 2
                        result.Append(" "c)
                    ElseIf current = "-"c AndAlso nextCharacter = "-"c Then
                        state = 3
                        result.Append("  ")
                        index += 1
                    ElseIf current = "/"c AndAlso nextCharacter = "*"c Then
                        state = 4
                        result.Append("  ")
                        index += 1
                    ElseIf current = ";"c Then
                        Throw New ConfigurationErrorsException("AdhocQuery must contain one statement only.")
                    Else
                        result.Append(current)
                    End If
                Case 1
                    result.Append(" "c)
                    If current = "'"c Then
                        If nextCharacter = "'"c Then
                            result.Append(" "c)
                            index += 1
                        Else
                            state = 0
                        End If
                    End If
                Case 2
                    result.Append(" "c)
                    If current = "]"c Then
                        If nextCharacter = "]"c Then
                            result.Append(" "c)
                            index += 1
                        Else
                            state = 0
                        End If
                    End If
                Case 3
                    If current = ControlChars.Cr OrElse current = ControlChars.Lf Then
                        state = 0
                        result.Append(current)
                    Else
                        result.Append(" "c)
                    End If
                Case 4
                    result.Append(" "c)
                    If current = "*"c AndAlso nextCharacter = "/"c Then
                        result.Append(" "c)
                        index += 1
                        state = 0
                    End If
            End Select
            index += 1
        End While

        If state = 1 OrElse state = 2 OrElse state = 4 Then
            Throw New ConfigurationErrorsException("AdhocQuery contains an unterminated string, identifier, or comment.")
        End If
        Return result.ToString()
    End Function
End Class

Friend Class AdhocReportDefinition
    Public Property ReportId As Long
    Public Property Title As String
    Public Property Description As String
    Public Property QueryText As String
End Class

Friend Class AdhocSqlFilter
    Public Property Sql As String = String.Empty
    Public Property Parameters As New List(Of AdhocSqlParameter)()
End Class

Friend Class AdhocSqlParameter
    Public Property Name As String
    Public Property Value As String
End Class

Public Class AdhocReportMetadata
    <JsonProperty("reportId")>
    Public Property ReportId As Long
    <JsonProperty("title")>
    Public Property Title As String
    <JsonProperty("description")>
    Public Property Description As String
    <JsonProperty("columns")>
    Public Property Columns As List(Of AdhocReportColumn)
End Class

Public Class AdhocReportColumn
    <JsonProperty("index")>
    Public Property Index As Integer
    <JsonProperty("name")>
    Public Property Name As String
    <JsonProperty("title")>
    Public Property Title As String
    <JsonProperty("dataType")>
    Public Property DataType As String
End Class

Public Class AdhocDistinctValue
    <JsonProperty("value")>
    Public Property Value As String
    <JsonProperty("label")>
    Public Property Label As String
End Class

Public Class AdhocReportPageRequest
    Public Property ReportId As Long
    Public Property Draw As Integer
    Public Property Start As Integer
    Public Property Length As Integer
    Public Property Search As String
    Public Property OrderColumnIndex As Integer
    Public Property OrderDirection As String
    Public Property Filters As New List(Of AdhocReportColumnFilter)()
End Class

Public Class AdhocReportInsightRequest
    Public Property ReportId As Long
    Public Property GroupColumnIndex As Integer
    Public Property SecondaryGroupColumnIndex As Integer = -1
    Public Property MeasureColumnIndex As Integer = -1
    Public Property Limit As Integer = 12
    Public Property Search As String
    Public Property Filters As New List(Of AdhocReportColumnFilter)()
End Class

Public Class AdhocReportInsightResult
    <JsonProperty("reportTitle")>
    Public Property ReportTitle As String
    <JsonProperty("groupColumnTitle")>
    Public Property GroupColumnTitle As String
    <JsonProperty("secondaryGroupColumnTitle")>
    Public Property SecondaryGroupColumnTitle As String
    <JsonProperty("measureTitle")>
    Public Property MeasureTitle As String
    <JsonProperty("totalRows")>
    Public Property TotalRows As Long
    <JsonProperty("groupCount")>
    Public Property GroupCount As Long
    <JsonProperty("displayedGroupCount")>
    Public Property DisplayedGroupCount As Integer
    <JsonProperty("secondaryGroupCount")>
    Public Property SecondaryGroupCount As Long
    <JsonProperty("displayedSecondaryGroupCount")>
    Public Property DisplayedSecondaryGroupCount As Integer
    <JsonProperty("totalValue")>
    Public Property TotalValue As Decimal
    <JsonProperty("isTruncated")>
    Public Property IsTruncated As Boolean
    <JsonProperty("groups")>
    Public Property Groups As New List(Of AdhocReportInsightGroup)()
End Class

Public Class AdhocReportInsightGroup
    <JsonProperty("label")>
    Public Property Label As String
    <JsonProperty("secondaryLabel")>
    Public Property SecondaryLabel As String
    <JsonProperty("rowCount")>
    Public Property RowCount As Long
    <JsonProperty("value")>
    Public Property Value As Decimal
    <JsonProperty("groupRowCount")>
    Public Property GroupRowCount As Long
    <JsonProperty("groupValue")>
    Public Property GroupValue As Decimal
End Class

Public Class AdhocReportColumnFilter
    <JsonProperty("columnIndex")>
    Public Property ColumnIndex As Integer
    <JsonProperty("values")>
    Public Property Values As New List(Of String)()
End Class

Public Class AdhocReportCreateRequest
    Public Property ReportName As String
    Public Property ReportTitle As String
    Public Property SectionId As Long
    Public Property AdhocQuery As String
End Class

Public Class AdhocReportCreateResult
    <JsonProperty("reportId")>
    Public Property ReportId As Long
    <JsonProperty("sortOrder")>
    Public Property SortOrder As Integer
    <JsonProperty("viewName")>
    Public Property ViewName As String
    <JsonProperty("url")>
    Public Property Url As String
End Class

Public Class AdhocReportUpdateRequest
    Public Property ReportId As Long
    Public Property ReportName As String
    Public Property ReportTitle As String
    Public Property SectionId As Long
    Public Property SortOrder As Integer
    Public Property AdhocQuery As String
End Class

Public Class AdhocReportUpdateResult
    <JsonProperty("reportId")>
    Public Property ReportId As Long
    <JsonProperty("viewName")>
    Public Property ViewName As String
    <JsonProperty("url")>
    Public Property Url As String
End Class

Public Class AdhocReportExportResult
    Public Property FileName As String
    Public Property ContentType As String
    Public Property Content As Byte()
End Class

Public Class AdhocReportPageResult
    <JsonProperty("draw")>
    Public Property Draw As Integer
    <JsonProperty("recordsTotal")>
    Public Property RecordsTotal As Long
    <JsonProperty("recordsFiltered")>
    Public Property RecordsFiltered As Long
    <JsonProperty("data")>
    Public Property Data As List(Of Object())
End Class
