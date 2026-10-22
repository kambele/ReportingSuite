Imports System.Collections.Generic
Imports System.Collections.Specialized
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Security.Cryptography
Imports System.Text
Imports Newtonsoft.Json

Public NotInheritable Class AdhocReportWarehouseService
    Private Const DefaultCommandTimeoutSeconds As Integer = 300
    Private Const DefaultMaximumExtractRows As Integer = 1000000
    Private Const SourceViewPrefix As String = "vw_AdhocReport_"

    Private ReadOnly _sourceConnectionString As String
    Private ReadOnly _warehouseConnectionString As String

    Public Sub New(ByVal sourceConnectionString As String, ByVal warehouseConnectionString As String)
        If String.IsNullOrWhiteSpace(sourceConnectionString) Then
            Throw New ConfigurationErrorsException("The ReportingSuite source connection string is not configured.")
        End If
        If String.IsNullOrWhiteSpace(warehouseConnectionString) Then
            Throw New ConfigurationErrorsException("The ReportingWarehouse connection string is not configured.")
        End If

        _sourceConnectionString = sourceConnectionString
        _warehouseConnectionString = warehouseConnectionString
        ValidateSeparateDatabases()
    End Sub

    Public Shared Function GetConfiguredSourceConnectionString() As String
        Return ACoAReportWarehouseService.GetConfiguredSourceConnectionString()
    End Function

    Public Shared Function GetConfiguredWarehouseConnectionString() As String
        Return ACoAReportWarehouseService.GetConfiguredWarehouseConnectionString()
    End Function

    Public Function GetAvailableReports() As DataTable
        Dim result As New DataTable("AdhocReports") With {.Locale = CultureInfo.CurrentCulture}
        Using connection As New SqlConnection(_sourceConnectionString)
            connection.Open()
            Using command As New SqlCommand(
                "SELECT ReportID, Report_Name, Report_Title, SectionID, " &
                "COALESCE(NULLIF(LTRIM(RTRIM(Report_Title)), ''), Report_Name) AS DisplayName " &
                "FROM dbo.Reports WHERE Is_Deleted = 0 " &
                "AND NULLIF(LTRIM(RTRIM(AdhocQuery)), '') IS NOT NULL " &
                "ORDER BY COALESCE(NULLIF(LTRIM(RTRIM(Report_Title)), ''), Report_Name), ReportID", connection)
                command.CommandTimeout = GetCommandTimeout()
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(result)
                End Using
            End Using
        End Using
        Return result
    End Function

    Public Function ValidateExtraction(ByVal request As AdhocReportWarehouseRequest) As AdhocReportWarehousePreview
        ValidateRequest(request)
        Dim definition As AdhocWarehouseReportDefinition = LoadDefinition(request.ReportId)
        Dim extract As DataTable = ExtractReport(definition)
        Dim schemaDefinition As String = BuildSchemaDefinition(extract)
        Return New AdhocReportWarehousePreview With {
            .ReportId = definition.ReportId,
            .ReportName = definition.ReportName,
            .SourceView = definition.SourceView,
            .RowCount = extract.Rows.Count,
            .ColumnCount = extract.Columns.Count,
            .SchemaHash = ToHex(ComputeHash(schemaDefinition))
        }
    End Function

    Public Function LoadSnapshot(ByVal request As AdhocReportWarehouseRequest) As AdhocReportWarehouseResult
        ValidateRequest(request)
        Dim startedAtUtc As DateTime = DateTime.UtcNow
        Dim loadBatchId As Long = 0
        Dim definition As AdhocWarehouseReportDefinition = LoadDefinition(request.ReportId)

        Try
            Using warehouseConnection As New SqlConnection(_warehouseConnectionString)
                warehouseConnection.Open()
                EnsureWarehouseSchema(warehouseConnection)
                loadBatchId = CreateLoadBatch(warehouseConnection, request, definition, startedAtUtc)
            End Using

            ' Extraction is intentionally completed outside the warehouse transaction.
            ' Adhoc views may reference linked servers that cannot participate in MSDTC.
            Dim extract As DataTable = ExtractReport(definition)
            Dim schemaDefinition As String = BuildSchemaDefinition(extract)
            Dim schemaHash As Byte() = ComputeHash(schemaDefinition)
            Dim snapshotAtUtc As DateTime = DateTime.UtcNow
            Dim warehouseRows As DataTable = BuildWarehouseRows(extract, loadBatchId, request.ReportId, snapshotAtUtc)
            Dim schemaVersion As Integer

            Using warehouseConnection As New SqlConnection(_warehouseConnectionString)
                warehouseConnection.Open()
                Using transaction As SqlTransaction = warehouseConnection.BeginTransaction(IsolationLevel.ReadCommitted)
                    schemaVersion = RegisterSchema(warehouseConnection, transaction, request.ReportId,
                                                   schemaHash, schemaDefinition, snapshotAtUtc)
                    For Each row As DataRow In warehouseRows.Rows
                        row("SchemaVersion") = schemaVersion
                    Next
                    BulkInsertSnapshot(warehouseConnection, transaction, warehouseRows)

                    Dim completedAtUtc As DateTime = DateTime.UtcNow
                    MarkLoadCompleted(warehouseConnection, transaction, loadBatchId, schemaVersion,
                                      extract.Columns.Count, extract.Rows.Count, completedAtUtc,
                                      CLng((completedAtUtc - startedAtUtc).TotalMilliseconds))
                    transaction.Commit()

                    Return New AdhocReportWarehouseResult With {
                        .LoadBatchId = loadBatchId,
                        .ReportId = request.ReportId,
                        .ReportName = definition.ReportName,
                        .SchemaVersion = schemaVersion,
                        .ColumnCount = extract.Columns.Count,
                        .ExtractedRowCount = extract.Rows.Count,
                        .LoadedRowCount = warehouseRows.Rows.Count,
                        .StartedAtUtc = startedAtUtc,
                        .CompletedAtUtc = completedAtUtc
                    }
                End Using
            End Using
        Catch ex As Exception
            If loadBatchId > 0 Then
                TryMarkLoadFailed(loadBatchId, ex.Message, startedAtUtc)
            End If
            Throw
        End Try
    End Function

    Public Function CheckWarehouse() As AdhocReportWarehouseHealth
        Dim health As New AdhocReportWarehouseHealth With {
            .IsConfigured = Not String.IsNullOrWhiteSpace(_warehouseConnectionString)
        }

        Try
            Using connection As New SqlConnection(_warehouseConnectionString)
                connection.Open()
                health.IsReachable = True
                Using identityCommand As New SqlCommand(
                    "SELECT DB_NAME() AS DatabaseName, ORIGINAL_LOGIN() AS DatabasePrincipal", connection)
                    Using reader As SqlDataReader = identityCommand.ExecuteReader()
                        If reader.Read() Then
                            health.DatabaseName = Convert.ToString(reader("DatabaseName"), CultureInfo.InvariantCulture)
                            health.DatabasePrincipal = Convert.ToString(reader("DatabasePrincipal"), CultureInfo.InvariantCulture)
                        End If
                    End Using
                End Using

                Using schemaCommand As New SqlCommand(
                    "SELECT CASE WHEN OBJECT_ID(N'[etl].[AdhocReportLoadBatch]', N'U') IS NOT NULL " &
                    "AND OBJECT_ID(N'[etl].[AdhocReportSchema]', N'U') IS NOT NULL " &
                    "AND OBJECT_ID(N'[dw].[AdhocReport]', N'U') IS NOT NULL " &
                    "AND OBJECT_ID(N'[analytics].[vw_AdhocReportSnapshots]', N'V') IS NOT NULL " &
                    "AND OBJECT_ID(N'[analytics].[vw_AdhocReportCurrent]', N'V') IS NOT NULL " &
                    "THEN 1 ELSE 0 END", connection)
                    health.IsSchemaReady = Convert.ToInt32(schemaCommand.ExecuteScalar(), CultureInfo.InvariantCulture) = 1
                End Using

                Dim missingPermissions As New List(Of String)()
                Using permissionCommand As New SqlCommand(
                    "SELECT " &
                    "HAS_PERMS_BY_NAME(N'etl.AdhocReportLoadBatch', N'OBJECT', N'SELECT') AS BatchSelect, " &
                    "HAS_PERMS_BY_NAME(N'etl.AdhocReportLoadBatch', N'OBJECT', N'INSERT') AS BatchInsert, " &
                    "HAS_PERMS_BY_NAME(N'etl.AdhocReportLoadBatch', N'OBJECT', N'UPDATE') AS BatchUpdate, " &
                    "HAS_PERMS_BY_NAME(N'etl.AdhocReportSchema', N'OBJECT', N'SELECT') AS SchemaSelect, " &
                    "HAS_PERMS_BY_NAME(N'etl.AdhocReportSchema', N'OBJECT', N'INSERT') AS SchemaInsert, " &
                    "HAS_PERMS_BY_NAME(N'etl.AdhocReportSchema', N'OBJECT', N'UPDATE') AS SchemaUpdate, " &
                    "HAS_PERMS_BY_NAME(N'dw.AdhocReport', N'OBJECT', N'INSERT') AS FactInsert", connection)
                    Using reader As SqlDataReader = permissionCommand.ExecuteReader()
                        If reader.Read() Then
                            AddMissingPermission(reader("BatchSelect"), "SELECT on etl.AdhocReportLoadBatch", missingPermissions)
                            AddMissingPermission(reader("BatchInsert"), "INSERT on etl.AdhocReportLoadBatch", missingPermissions)
                            AddMissingPermission(reader("BatchUpdate"), "UPDATE on etl.AdhocReportLoadBatch", missingPermissions)
                            AddMissingPermission(reader("SchemaSelect"), "SELECT on etl.AdhocReportSchema", missingPermissions)
                            AddMissingPermission(reader("SchemaInsert"), "INSERT on etl.AdhocReportSchema", missingPermissions)
                            AddMissingPermission(reader("SchemaUpdate"), "UPDATE on etl.AdhocReportSchema", missingPermissions)
                            AddMissingPermission(reader("FactInsert"), "INSERT on dw.AdhocReport", missingPermissions)
                        End If
                    End Using
                End Using
                health.MissingPermissions = String.Join(", ", missingPermissions)
                health.HasRequiredPermissions = missingPermissions.Count = 0
            End Using

            If Not health.IsSchemaReady Then
                health.Message = "The warehouse is reachable, but the Adhoc warehouse deployment has not been applied."
            ElseIf Not health.HasRequiredPermissions Then
                health.Message = "The warehouse is reachable, but the connected principal is missing loader permissions: " &
                                 health.MissingPermissions & "."
            Else
                health.Message = "The shared Adhoc warehouse is ready for JSON snapshot loads."
            End If
        Catch ex As SqlException
            health.Message = "The Adhoc warehouse connection failed with SQL error " &
                             ex.Number.ToString(CultureInfo.InvariantCulture) & "."
        Catch
            health.Message = "The Adhoc warehouse is not reachable with the configured application identity."
        End Try
        Return health
    End Function

    Public Function GetRecentLoads(ByVal maximumRows As Integer) As DataTable
        Dim result As New DataTable("RecentAdhocLoads") With {.Locale = CultureInfo.CurrentCulture}
        Using connection As New SqlConnection(_warehouseConnectionString)
            connection.Open()
            EnsureWarehouseSchema(connection)
            Using command As New SqlCommand(
                "SELECT TOP (@MaximumRows) LoadBatchID, StartedAtUtc, CompletedAtUtc, LoadStatus, " &
                "ReportID, ReportName, SchemaVersion, SourceColumnCount, ExtractedRowCount, LoadedRowCount, " &
                "DurationMilliseconds, RequestedBy FROM [etl].[AdhocReportLoadBatch] " &
                "ORDER BY LoadBatchID DESC", connection)
                command.Parameters.Add("@MaximumRows", SqlDbType.Int).Value = Math.Max(1, Math.Min(maximumRows, 100))
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(result)
                End Using
            End Using
        End Using
        Return result
    End Function

    Private Function LoadDefinition(ByVal reportId As Long) As AdhocWarehouseReportDefinition
        Using connection As New SqlConnection(_sourceConnectionString)
            connection.Open()
            Using command As New SqlCommand(
                "SELECT ReportID, Report_Name, Report_Title FROM dbo.Reports " &
                "WHERE ReportID = @ReportID AND Is_Deleted = 0 " &
                "AND NULLIF(LTRIM(RTRIM(AdhocQuery)), '') IS NOT NULL", connection)
                command.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
                Using reader As SqlDataReader = command.ExecuteReader(CommandBehavior.SingleRow)
                    If Not reader.Read() Then
                        Throw New InvalidOperationException("The selected Adhoc report is unavailable.")
                    End If
                    Dim reportName As String = Convert.ToString(reader("Report_Title"), CultureInfo.InvariantCulture).Trim()
                    If reportName.Length = 0 Then
                        reportName = Convert.ToString(reader("Report_Name"), CultureInfo.InvariantCulture).Trim()
                    End If
                    Return New AdhocWarehouseReportDefinition With {
                        .ReportId = reportId,
                        .ReportName = reportName,
                        .SourceView = "dbo." & SourceViewPrefix & reportId.ToString(CultureInfo.InvariantCulture)
                    }
                End Using
            End Using
        End Using
    End Function

    Private Function ExtractReport(ByVal definition As AdhocWarehouseReportDefinition) As DataTable
        Dim result As New DataTable("AdhocExtract") With {.Locale = CultureInfo.InvariantCulture}
        Dim quotedView As String = "[dbo].[" & SourceViewPrefix &
                                   definition.ReportId.ToString(CultureInfo.InvariantCulture) & "]"
        Using connection As New SqlConnection(_sourceConnectionString)
            connection.Open()
            Using existenceCommand As New SqlCommand(
                "SELECT CASE WHEN OBJECT_ID(@ViewName, N'V') IS NULL THEN 0 ELSE 1 END", connection)
                existenceCommand.Parameters.Add("@ViewName", SqlDbType.NVarChar, 776).Value = definition.SourceView
                If Convert.ToInt32(existenceCommand.ExecuteScalar(), CultureInfo.InvariantCulture) <> 1 Then
                    Throw New InvalidOperationException(
                        "The source view " & definition.SourceView & " does not exist. Save the Adhoc report before loading its warehouse snapshot.")
                End If
            End Using
            Using command As New SqlCommand("SELECT * FROM " & quotedView, connection)
                command.CommandTimeout = GetCommandTimeout()
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(result)
                End Using
            End Using
        End Using

        Dim maximumRows As Integer = GetMaximumExtractRows()
        If result.Rows.Count > maximumRows Then
            Throw New InvalidOperationException(
                "The Adhoc report returned " & result.Rows.Count.ToString("N0", CultureInfo.InvariantCulture) &
                " rows, exceeding the configured warehouse limit of " &
                maximumRows.ToString("N0", CultureInfo.InvariantCulture) & ".")
        End If
        If result.Columns.Count = 0 Then
            Throw New InvalidOperationException("The selected Adhoc report does not expose any columns.")
        End If
        Return result
    End Function

    Private Shared Function BuildSchemaDefinition(ByVal extract As DataTable) As String
        Dim columns As New List(Of AdhocWarehouseColumnDefinition)()
        For index As Integer = 0 To extract.Columns.Count - 1
            Dim column As DataColumn = extract.Columns(index)
            columns.Add(New AdhocWarehouseColumnDefinition With {
                .Ordinal = index,
                .Name = column.ColumnName,
                .DataType = If(column.DataType.FullName, column.DataType.Name),
                .AllowNull = column.AllowDBNull
            })
        Next
        Return JsonConvert.SerializeObject(columns, Formatting.None)
    End Function

    Private Shared Function BuildWarehouseRows(ByVal extract As DataTable,
                                                ByVal loadBatchId As Long,
                                                ByVal reportId As Long,
                                                ByVal snapshotAtUtc As DateTime) As DataTable
        Dim result As New DataTable("AdhocWarehouseRows") With {.Locale = CultureInfo.InvariantCulture}
        result.Columns.Add("LoadBatchID", GetType(Long))
        result.Columns.Add("ReportID", GetType(Long))
        result.Columns.Add("SchemaVersion", GetType(Integer))
        result.Columns.Add("SourceRowNumber", GetType(Long))
        result.Columns.Add("BusinessKey", GetType(String))
        result.Columns.Add("RowPayload", GetType(String))
        result.Columns.Add("RowHash", GetType(Byte()))
        result.Columns.Add("SnapshotAtUtc", GetType(DateTime))

        Dim rowNumber As Long = 0
        For Each sourceRow As DataRow In extract.Rows
            rowNumber += 1
            ' OrderedDictionary keeps JSON property order aligned with the captured
            ' schema, producing stable row hashes across repeated snapshots.
            Dim payloadValues As New OrderedDictionary()
            For Each column As DataColumn In extract.Columns
                payloadValues(column.ColumnName) = NormalizeJsonValue(sourceRow(column))
            Next
            Dim payload As String = JsonConvert.SerializeObject(payloadValues, Formatting.None)
            Dim targetRow As DataRow = result.NewRow()
            targetRow("LoadBatchID") = loadBatchId
            targetRow("ReportID") = reportId
            targetRow("SchemaVersion") = 0
            targetRow("SourceRowNumber") = rowNumber
            targetRow("BusinessKey") = DBNull.Value
            targetRow("RowPayload") = payload
            targetRow("RowHash") = ComputeHash(payload)
            targetRow("SnapshotAtUtc") = snapshotAtUtc
            result.Rows.Add(targetRow)
        Next
        Return result
    End Function

    Private Shared Function NormalizeJsonValue(ByVal value As Object) As Object
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return Nothing
        End If
        If TypeOf value Is DateTime Then
            Return DirectCast(value, DateTime).ToString("o", CultureInfo.InvariantCulture)
        End If
        If TypeOf value Is DateTimeOffset Then
            Return DirectCast(value, DateTimeOffset).ToString("o", CultureInfo.InvariantCulture)
        End If
        If TypeOf value Is TimeSpan Then
            Return DirectCast(value, TimeSpan).ToString("c", CultureInfo.InvariantCulture)
        End If
        If TypeOf value Is Byte() Then
            Return Convert.ToBase64String(DirectCast(value, Byte()))
        End If
        If TypeOf value Is Guid Then
            Return DirectCast(value, Guid).ToString("D")
        End If
        If value.GetType().IsPrimitive OrElse TypeOf value Is Decimal OrElse TypeOf value Is String Then
            Return value
        End If
        Return Convert.ToString(value, CultureInfo.InvariantCulture)
    End Function

    Private Shared Function RegisterSchema(ByVal connection As SqlConnection,
                                           ByVal transaction As SqlTransaction,
                                           ByVal reportId As Long,
                                           ByVal schemaHash As Byte(),
                                           ByVal schemaDefinition As String,
                                           ByVal observedAtUtc As DateTime) As Integer
        Using existingCommand As New SqlCommand(
            "SELECT TOP (1) SchemaVersion FROM [etl].[AdhocReportSchema] WITH (UPDLOCK, HOLDLOCK) " &
            "WHERE ReportID = @ReportID AND SchemaHash = @SchemaHash ORDER BY SchemaVersion DESC",
            connection, transaction)
            existingCommand.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
            existingCommand.Parameters.Add("@SchemaHash", SqlDbType.VarBinary, 32).Value = schemaHash
            Dim existing As Object = existingCommand.ExecuteScalar()
            If existing IsNot Nothing AndAlso existing IsNot DBNull.Value Then
                Dim version As Integer = Convert.ToInt32(existing, CultureInfo.InvariantCulture)
                Using touchCommand As New SqlCommand(
                    "UPDATE [etl].[AdhocReportSchema] SET IsCurrent = 0 " &
                    "WHERE ReportID = @ReportID AND SchemaVersion <> @SchemaVersion; " &
                    "UPDATE [etl].[AdhocReportSchema] SET LastSeenAtUtc = @ObservedAtUtc, IsCurrent = 1 " &
                    "WHERE ReportID = @ReportID AND SchemaVersion = @SchemaVersion;",
                    connection, transaction)
                    touchCommand.Parameters.Add("@ObservedAtUtc", SqlDbType.DateTime2).Value = observedAtUtc
                    touchCommand.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
                    touchCommand.Parameters.Add("@SchemaVersion", SqlDbType.Int).Value = version
                    touchCommand.ExecuteNonQuery()
                End Using
                Return version
            End If
        End Using

        Dim nextVersion As Integer
        Using versionCommand As New SqlCommand(
            "SELECT ISNULL(MAX(SchemaVersion), 0) + 1 FROM [etl].[AdhocReportSchema] WITH (UPDLOCK, HOLDLOCK) WHERE ReportID = @ReportID",
            connection, transaction)
            versionCommand.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
            nextVersion = Convert.ToInt32(versionCommand.ExecuteScalar(), CultureInfo.InvariantCulture)
        End Using
        Using clearCommand As New SqlCommand(
            "UPDATE [etl].[AdhocReportSchema] SET IsCurrent = 0 WHERE ReportID = @ReportID", connection, transaction)
            clearCommand.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
            clearCommand.ExecuteNonQuery()
        End Using
        Using insertCommand As New SqlCommand(
            "INSERT INTO [etl].[AdhocReportSchema] " &
            "(ReportID, SchemaVersion, SchemaHash, SchemaDefinition, FirstSeenAtUtc, LastSeenAtUtc, IsCurrent) " &
            "VALUES (@ReportID, @SchemaVersion, @SchemaHash, @SchemaDefinition, @ObservedAtUtc, @ObservedAtUtc, 1)",
            connection, transaction)
            insertCommand.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = reportId
            insertCommand.Parameters.Add("@SchemaVersion", SqlDbType.Int).Value = nextVersion
            insertCommand.Parameters.Add("@SchemaHash", SqlDbType.VarBinary, 32).Value = schemaHash
            insertCommand.Parameters.Add("@SchemaDefinition", SqlDbType.NVarChar, -1).Value = schemaDefinition
            insertCommand.Parameters.Add("@ObservedAtUtc", SqlDbType.DateTime2).Value = observedAtUtc
            insertCommand.ExecuteNonQuery()
        End Using
        Return nextVersion
    End Function

    Private Shared Sub BulkInsertSnapshot(ByVal connection As SqlConnection,
                                          ByVal transaction As SqlTransaction,
                                          ByVal rows As DataTable)
        If rows.Rows.Count = 0 Then
            Return
        End If
        Using bulkCopy As New SqlBulkCopy(connection,
            SqlBulkCopyOptions.TableLock Or SqlBulkCopyOptions.CheckConstraints, transaction)
            bulkCopy.DestinationTableName = "[dw].[AdhocReport]"
            bulkCopy.BatchSize = 5000
            bulkCopy.BulkCopyTimeout = GetCommandTimeout()
            For Each column As DataColumn In rows.Columns
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName)
            Next
            bulkCopy.WriteToServer(rows)
        End Using
    End Sub

    Private Shared Function CreateLoadBatch(ByVal connection As SqlConnection,
                                            ByVal request As AdhocReportWarehouseRequest,
                                            ByVal definition As AdhocWarehouseReportDefinition,
                                            ByVal startedAtUtc As DateTime) As Long
        Using command As New SqlCommand(
            "INSERT INTO [etl].[AdhocReportLoadBatch] " &
            "(LoadRunID, StartedAtUtc, LoadStatus, RequestedBy, ReportID, ReportName, SourceView) " &
            "VALUES (@LoadRunID, @StartedAtUtc, 'Running', @RequestedBy, @ReportID, @ReportName, @SourceView); " &
            "SELECT CAST(SCOPE_IDENTITY() AS bigint);", connection)
            command.Parameters.Add("@LoadRunID", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid()
            command.Parameters.Add("@StartedAtUtc", SqlDbType.DateTime2).Value = startedAtUtc
            AddNVarChar(command, "@RequestedBy", 256, request.RequestedBy)
            command.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = request.ReportId
            AddNVarChar(command, "@ReportName", 512, definition.ReportName)
            AddNVarChar(command, "@SourceView", 776, definition.SourceView)
            Return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture)
        End Using
    End Function

    Private Shared Sub MarkLoadCompleted(ByVal connection As SqlConnection,
                                         ByVal transaction As SqlTransaction,
                                         ByVal loadBatchId As Long,
                                         ByVal schemaVersion As Integer,
                                         ByVal columnCount As Integer,
                                         ByVal rowCount As Integer,
                                         ByVal completedAtUtc As DateTime,
                                         ByVal durationMilliseconds As Long)
        Using command As New SqlCommand(
            "UPDATE [etl].[AdhocReportLoadBatch] SET LoadStatus = 'Completed', CompletedAtUtc = @CompletedAtUtc, " &
            "SchemaVersion = @SchemaVersion, SourceColumnCount = @SourceColumnCount, " &
            "ExtractedRowCount = @RowCount, LoadedRowCount = @RowCount, DurationMilliseconds = @DurationMilliseconds, " &
            "ErrorMessage = NULL WHERE LoadBatchID = @LoadBatchID", connection, transaction)
            command.Parameters.Add("@CompletedAtUtc", SqlDbType.DateTime2).Value = completedAtUtc
            command.Parameters.Add("@SchemaVersion", SqlDbType.Int).Value = schemaVersion
            command.Parameters.Add("@SourceColumnCount", SqlDbType.Int).Value = columnCount
            command.Parameters.Add("@RowCount", SqlDbType.Int).Value = rowCount
            command.Parameters.Add("@DurationMilliseconds", SqlDbType.BigInt).Value = durationMilliseconds
            command.Parameters.Add("@LoadBatchID", SqlDbType.BigInt).Value = loadBatchId
            command.ExecuteNonQuery()
        End Using
    End Sub

    Private Sub TryMarkLoadFailed(ByVal loadBatchId As Long,
                                  ByVal errorMessage As String,
                                  ByVal startedAtUtc As DateTime)
        Try
            Using connection As New SqlConnection(_warehouseConnectionString)
                connection.Open()
                Using command As New SqlCommand(
                    "UPDATE [etl].[AdhocReportLoadBatch] SET LoadStatus = 'Failed', CompletedAtUtc = @CompletedAtUtc, " &
                    "DurationMilliseconds = @DurationMilliseconds, ErrorMessage = @ErrorMessage WHERE LoadBatchID = @LoadBatchID",
                    connection)
                    Dim completedAtUtc As DateTime = DateTime.UtcNow
                    command.Parameters.Add("@CompletedAtUtc", SqlDbType.DateTime2).Value = completedAtUtc
                    command.Parameters.Add("@DurationMilliseconds", SqlDbType.BigInt).Value =
                        CLng((completedAtUtc - startedAtUtc).TotalMilliseconds)
                    AddNVarChar(command, "@ErrorMessage", 4000, Left(If(errorMessage, String.Empty), 4000))
                    command.Parameters.Add("@LoadBatchID", SqlDbType.BigInt).Value = loadBatchId
                    command.ExecuteNonQuery()
                End Using
            End Using
        Catch
            ' Preserve the original extraction/load exception.
        End Try
    End Sub

    Private Shared Sub EnsureWarehouseSchema(ByVal connection As SqlConnection)
        Using command As New SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(N'[etl].[AdhocReportLoadBatch]', N'U') IS NOT NULL " &
            "AND OBJECT_ID(N'[etl].[AdhocReportSchema]', N'U') IS NOT NULL " &
            "AND OBJECT_ID(N'[dw].[AdhocReport]', N'U') IS NOT NULL THEN 1 ELSE 0 END", connection)
            If Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) <> 1 Then
                Throw New InvalidOperationException(
                    "The Adhoc warehouse schema is missing. Apply Database\ReportingWarehouse.sql to the separate warehouse database.")
            End If
        End Using
    End Sub

    Private Shared Sub ValidateRequest(ByVal request As AdhocReportWarehouseRequest)
        If request Is Nothing Then
            Throw New ArgumentNullException("request")
        End If
        If request.ReportId <= 0 Then
            Throw New InvalidOperationException("Select an Adhoc report.")
        End If
    End Sub

    Private Sub ValidateSeparateDatabases()
        Dim sourceBuilder As New SqlConnectionStringBuilder(_sourceConnectionString)
        Dim warehouseBuilder As New SqlConnectionStringBuilder(_warehouseConnectionString)
        If sourceBuilder.DataSource.Equals(warehouseBuilder.DataSource, StringComparison.OrdinalIgnoreCase) AndAlso
           sourceBuilder.InitialCatalog.Equals(warehouseBuilder.InitialCatalog, StringComparison.OrdinalIgnoreCase) Then
            Throw New ConfigurationErrorsException(
                "ReportingWarehouse must point to a database separate from the ReportingSuite operational database.")
        End If
    End Sub

    Private Shared Sub AddMissingPermission(ByVal permissionValue As Object,
                                            ByVal permissionLabel As String,
                                            ByVal missingPermissions As IList(Of String))
        If permissionValue Is Nothing OrElse Convert.IsDBNull(permissionValue) OrElse
           Convert.ToInt32(permissionValue, CultureInfo.InvariantCulture) <> 1 Then
            missingPermissions.Add(permissionLabel)
        End If
    End Sub

    Private Shared Sub AddNVarChar(ByVal command As SqlCommand,
                                   ByVal name As String,
                                   ByVal size As Integer,
                                   ByVal value As String)
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
            If(String.IsNullOrWhiteSpace(value), CType(DBNull.Value, Object), value)
    End Sub

    Private Shared Function ComputeHash(ByVal value As String) As Byte()
        Using algorithm As SHA256 = SHA256.Create()
            Return algorithm.ComputeHash(Encoding.UTF8.GetBytes(If(value, String.Empty)))
        End Using
    End Function

    Private Shared Function ToHex(ByVal value As Byte()) As String
        Dim builder As New StringBuilder(value.Length * 2)
        For Each item As Byte In value
            builder.Append(item.ToString("x2", CultureInfo.InvariantCulture))
        Next
        Return builder.ToString()
    End Function

    Private Shared Function GetCommandTimeout() As Integer
        Return GetPositiveSetting("ReportWarehouseCommandTimeoutSeconds", DefaultCommandTimeoutSeconds)
    End Function

    Private Shared Function GetMaximumExtractRows() As Integer
        Return GetPositiveSetting("ReportWarehouseMaximumExtractRows", DefaultMaximumExtractRows)
    End Function

    Private Shared Function GetPositiveSetting(ByVal key As String, ByVal defaultValue As Integer) As Integer
        Dim configured As Integer
        If Integer.TryParse(ConfigurationManager.AppSettings(key), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, configured) AndAlso configured > 0 Then
            Return configured
        End If
        Return defaultValue
    End Function
End Class

Public NotInheritable Class AdhocReportWarehouseRequest
    Public Property ReportId As Long
    Public Property RequestedBy As String
End Class

Public NotInheritable Class AdhocReportWarehousePreview
    Public Property ReportId As Long
    Public Property ReportName As String
    Public Property SourceView As String
    Public Property RowCount As Integer
    Public Property ColumnCount As Integer
    Public Property SchemaHash As String
End Class

Public NotInheritable Class AdhocReportWarehouseResult
    Public Property LoadBatchId As Long
    Public Property ReportId As Long
    Public Property ReportName As String
    Public Property SchemaVersion As Integer
    Public Property ColumnCount As Integer
    Public Property ExtractedRowCount As Integer
    Public Property LoadedRowCount As Integer
    Public Property StartedAtUtc As DateTime
    Public Property CompletedAtUtc As DateTime
End Class

Public NotInheritable Class AdhocReportWarehouseHealth
    Public Property IsConfigured As Boolean
    Public Property IsReachable As Boolean
    Public Property IsSchemaReady As Boolean
    Public Property HasRequiredPermissions As Boolean
    Public Property DatabaseName As String
    Public Property DatabasePrincipal As String
    Public Property MissingPermissions As String
    Public Property Message As String
End Class

Friend NotInheritable Class AdhocWarehouseReportDefinition
    Public Property ReportId As Long
    Public Property ReportName As String
    Public Property SourceView As String
End Class

Friend NotInheritable Class AdhocWarehouseColumnDefinition
    Public Property Ordinal As Integer
    Public Property Name As String
    Public Property DataType As String
    Public Property AllowNull As Boolean
End Class
