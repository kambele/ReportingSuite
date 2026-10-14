Imports System.Configuration
Imports System.Collections.Generic
Imports System.Data
Imports System.Data.Entity.Core.EntityClient
Imports System.Data.SqlClient
Imports System.Globalization

Public NotInheritable Class ACoAReportWarehouseService
    Public Const SourceProcedure As String = "[dbo].[get_ACoA_Reports_Advance_v2]"
    Public Const WarehouseConnectionName As String = "ReportingWarehouse"
    Public Const ContractVersion As String = "ACOA_ADVANCE_V2_38_20260902"

    Private Const DefaultCommandTimeoutSeconds As Integer = 300
    Private Const DefaultMaximumExtractRows As Integer = 1000000

    Private Shared ReadOnly RequiredResultColumns As String() = {
        "Year", "Quarter", "PeriodID", "ReportID", "Report_Name", "Report_Title",
        "Entity_GroupID", "Report_Description", "Report_SubTitle", "Sort_Order",
        "SectionID", "ReportTypeID", "ReportURL", "Is_Dynamic", "LineItemTypeID",
        "Is_Deleted", "Is_MulitpleEntities", "YearFrom", "YearTo", "QuarterFrom",
        "QuarterTo", "ReportCode", "Updated", "ShowMappedItems", "Report_LineItemID",
        "Line_itemName", "Is_Main", "Is_Title", "LineItemValue", "Sort_Order2",
        "Sort_Order3", "Sort_Order4", "Sort_Order5", "EntityGroupName", "ColumnName",
        "Entity Name", "License Number", "Status"
    }

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
        Dim setting As ConnectionStringSettings = ConfigurationManager.ConnectionStrings("ReportingSuiteEntities3")
        If setting Is Nothing OrElse String.IsNullOrWhiteSpace(setting.ConnectionString) Then
            Throw New ConfigurationErrorsException("ReportingSuiteEntities3 is not configured.")
        End If

        Dim entityBuilder As New EntityConnectionStringBuilder(setting.ConnectionString)
        Return entityBuilder.ProviderConnectionString
    End Function

    Public Shared Function GetConfiguredWarehouseConnectionString() As String
        Dim setting As ConnectionStringSettings = ConfigurationManager.ConnectionStrings(WarehouseConnectionName)
        If setting Is Nothing Then
            Return String.Empty
        End If
        Return setting.ConnectionString
    End Function

    Public Shared Function ExecuteSourceProcedure(ByVal procedureName As String,
                                                  Optional ByVal parameters As IDictionary(Of String, Object) = Nothing) As DataTable
        Return ExecuteProcedure(GetConfiguredSourceConnectionString(), procedureName, parameters)
    End Function

    Public Shared Function ExtractFromConfiguredSource(ByVal request As ACoAReportWarehouseRequest) As DataTable
        Return ExtractFromConnection(GetConfiguredSourceConnectionString(), request)
    End Function

    Public Function Extract(ByVal request As ACoAReportWarehouseRequest) As DataTable
        Return ExtractFromConnection(_sourceConnectionString, request)
    End Function

    Private Shared Function ExtractFromConnection(ByVal sourceConnectionString As String,
                                                  ByVal request As ACoAReportWarehouseRequest) As DataTable
        ValidateRequest(request)

        Dim parameters As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase) From {
            {"@Report_id", request.ReportId},
            {"@quarter", request.QuarterCsv},
            {"@year", request.YearCsv},
            {"@entity_type", request.EntityType},
            {"@entities", request.EntitiesCsv},
            {"@status", request.Status},
            {"@ReportColumnID", request.ReportColumnIdsCsv}
        }

        Dim result As DataTable = ExecuteProcedure(sourceConnectionString, SourceProcedure, parameters)
        ValidateResultContract(result)

        Dim maximumRows As Integer = GetPositiveSetting("ReportWarehouseMaximumExtractRows", DefaultMaximumExtractRows)
        If result.Rows.Count > maximumRows Then
            Throw New InvalidOperationException(
                "The extract returned " & result.Rows.Count.ToString("N0", CultureInfo.InvariantCulture) &
                " rows, which exceeds the configured warehouse safety limit of " &
                maximumRows.ToString("N0", CultureInfo.InvariantCulture) & ".")
        End If

        Return result
    End Function

    Public Function LoadSnapshot(ByVal request As ACoAReportWarehouseRequest) As ACoAReportWarehouseResult
        ValidateRequest(request)
        Dim startedAtUtc As DateTime = DateTime.UtcNow
        Dim loadBatchId As Long = 0

        Try
            Using warehouseConnection As New SqlConnection(_warehouseConnectionString)
                warehouseConnection.Open()
                EnsureWarehouseSchema(warehouseConnection)
                loadBatchId = CreateLoadBatch(warehouseConnection, request, startedAtUtc)
            End Using

            Dim extract As DataTable = Me.Extract(request)
            Dim warehouseRows As DataTable = FilterRowsWithLineItemValue(extract)
            Dim extractedRowCount As Integer = extract.Rows.Count
            Dim loadedRowCount As Integer = warehouseRows.Rows.Count
            Dim skippedNullValueRowCount As Integer = extractedRowCount - loadedRowCount

            warehouseRows.Columns.Add("LoadBatchID", GetType(Long))
            For Each row As DataRow In warehouseRows.Rows
                row("LoadBatchID") = loadBatchId
            Next

            Dim completedAtUtc As DateTime
            Using warehouseConnection As New SqlConnection(_warehouseConnectionString)
                warehouseConnection.Open()
                Using transaction As SqlTransaction = warehouseConnection.BeginTransaction(IsolationLevel.ReadCommitted)
                    BulkInsertSnapshot(warehouseConnection, transaction, warehouseRows)
                    completedAtUtc = DateTime.UtcNow
                    MarkLoadCompleted(warehouseConnection, transaction, loadBatchId,
                                      extractedRowCount, loadedRowCount, skippedNullValueRowCount,
                                      completedAtUtc, CLng((completedAtUtc - startedAtUtc).TotalMilliseconds))
                    transaction.Commit()
                End Using
            End Using

            Return New ACoAReportWarehouseResult With {
                .LoadBatchId = loadBatchId,
                .ExtractedRowCount = extractedRowCount,
                .LoadedRowCount = loadedRowCount,
                .SkippedNullValueRowCount = skippedNullValueRowCount,
                .StartedAtUtc = startedAtUtc,
                .CompletedAtUtc = completedAtUtc
            }
        Catch ex As Exception
            If loadBatchId > 0 Then
                TryMarkLoadFailed(loadBatchId, ex.Message, startedAtUtc)
            End If
            Throw
        End Try
    End Function

    Public Shared Function FilterRowsWithLineItemValue(ByVal extract As DataTable) As DataTable
        If extract Is Nothing Then
            Throw New ArgumentNullException("extract")
        End If
        If Not extract.Columns.Contains("LineItemValue") Then
            Throw New InvalidOperationException("The extract does not contain the required LineItemValue column.")
        End If

        Dim warehouseRows As DataTable = extract.Clone()
        warehouseRows.TableName = extract.TableName
        For Each row As DataRow In extract.Rows
            If Not row.IsNull("LineItemValue") Then
                warehouseRows.ImportRow(row)
            End If
        Next
        Return warehouseRows
    End Function

    Public Function CheckWarehouse() As ACoAReportWarehouseHealth
        Dim health As New ACoAReportWarehouseHealth With {
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

                Dim missingPermissions As New List(Of String)()
                Using permissionCommand As New SqlCommand(
                    "SELECT " &
                    "HAS_PERMS_BY_NAME(N'etl.ACoAReportLoadBatch', N'OBJECT', N'SELECT') AS BatchSelect, " &
                    "HAS_PERMS_BY_NAME(N'etl.ACoAReportLoadBatch', N'OBJECT', N'INSERT') AS BatchInsert, " &
                    "HAS_PERMS_BY_NAME(N'etl.ACoAReportLoadBatch', N'OBJECT', N'UPDATE') AS BatchUpdate, " &
                    "HAS_PERMS_BY_NAME(N'dw.FactACoAReportSnapshot', N'OBJECT', N'INSERT') AS FactInsert", connection)
                    Using reader As SqlDataReader = permissionCommand.ExecuteReader()
                        If reader.Read() Then
                            AddMissingPermission(reader("BatchSelect"), "SELECT on etl.ACoAReportLoadBatch", missingPermissions)
                            AddMissingPermission(reader("BatchInsert"), "INSERT on etl.ACoAReportLoadBatch", missingPermissions)
                            AddMissingPermission(reader("BatchUpdate"), "UPDATE on etl.ACoAReportLoadBatch", missingPermissions)
                            AddMissingPermission(reader("FactInsert"), "INSERT on dw.FactACoAReportSnapshot", missingPermissions)
                        End If
                    End Using
                End Using
                health.MissingPermissions = String.Join(", ", missingPermissions)
                health.HasRequiredPermissions = missingPermissions.Count = 0

                Using command As New SqlCommand(
                    "SELECT CASE WHEN OBJECT_ID(N'[etl].[ACoAReportLoadBatch]', N'U') IS NOT NULL " &
                    "AND OBJECT_ID(N'[dw].[FactACoAReportSnapshot]', N'U') IS NOT NULL " &
                    "AND COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'ExtractedRowCount') IS NOT NULL " &
                    "AND COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'LoadedRowCount') IS NOT NULL " &
                    "AND COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'SkippedNullValueRowCount') IS NOT NULL " &
                    "THEN 1 ELSE 0 END", connection)
                    health.IsSchemaReady = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) = 1
                End Using
            End Using

            If Not health.HasRequiredPermissions Then
                health.Message =
                    "The warehouse is reachable, but its schema is not visible or the connected database principal " &
                    "is missing loader permissions: " & health.MissingPermissions & "."
            ElseIf Not health.IsSchemaReady Then
                health.Message = "The warehouse database is reachable, but its deployment script has not been applied."
            Else
                health.Message = "The separate warehouse database is ready for loads."
            End If
        Catch ex As Exception
            Dim sqlException As SqlException = TryCast(ex, SqlException)
            If sqlException IsNot Nothing AndAlso
                sqlException.Message.IndexOf("SSPI", StringComparison.OrdinalIgnoreCase) >= 0 Then
                health.Message =
                    "Windows authentication could not establish the SQL Server identity (SSPI). " &
                    "Verify the IIS service account, domain connectivity, and SQL Server SPN configuration."
            ElseIf sqlException IsNot Nothing Then
                health.Message =
                    "The warehouse connection failed with SQL error " &
                    sqlException.Number.ToString(CultureInfo.InvariantCulture) & "."
            Else
                health.Message = "The separate warehouse database is not reachable with the configured application identity."
            End If
        End Try

        Return health
    End Function

    Private Shared Sub AddMissingPermission(ByVal permissionValue As Object,
                                            ByVal permissionLabel As String,
                                            ByVal missingPermissions As IList(Of String))
        If permissionValue Is Nothing OrElse
            Convert.IsDBNull(permissionValue) OrElse
            Convert.ToInt32(permissionValue, CultureInfo.InvariantCulture) <> 1 Then
            missingPermissions.Add(permissionLabel)
        End If
    End Sub

    Public Function GetRecentLoads(ByVal maximumRows As Integer) As DataTable
        Dim result As New DataTable("RecentLoads") With {.Locale = CultureInfo.CurrentCulture}
        Using connection As New SqlConnection(_warehouseConnectionString)
            connection.Open()
            EnsureWarehouseSchema(connection)
            Using command As New SqlCommand(
                "SELECT TOP (@MaximumRows) LoadBatchID, StartedAtUtc, CompletedAtUtc, LoadStatus, " &
                "ReportID, YearFrom, YearTo, QuarterFrom, QuarterTo, [Status], " &
                "ExtractedRowCount, LoadedRowCount, SkippedNullValueRowCount, RequestedBy " &
                "FROM [etl].[ACoAReportLoadBatch] ORDER BY LoadBatchID DESC", connection)
                command.Parameters.Add("@MaximumRows", SqlDbType.Int).Value = Math.Max(1, Math.Min(maximumRows, 100))
                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(result)
                End Using
            End Using
        End Using
        Return result
    End Function

    Private Shared Function ExecuteProcedure(ByVal connectionString As String,
                                             ByVal procedureName As String,
                                             ByVal parameters As IDictionary(Of String, Object)) As DataTable
        Dim result As New DataTable(procedureName) With {.Locale = CultureInfo.CurrentCulture}
        Using connection As New SqlConnection(connectionString)
            connection.Open()
            Using command As New SqlCommand(procedureName, connection)
                command.CommandType = CommandType.StoredProcedure
                command.CommandTimeout = GetPositiveSetting("ReportWarehouseCommandTimeoutSeconds",
                                                            DefaultCommandTimeoutSeconds)

                If parameters IsNot Nothing Then
                    For Each item As KeyValuePair(Of String, Object) In parameters
                        command.Parameters.AddWithValue(item.Key, If(item.Value, DBNull.Value))
                    Next
                End If

                Using adapter As New SqlDataAdapter(command)
                    adapter.Fill(result)
                End Using
            End Using
        End Using
        Return result
    End Function

    Private Sub ValidateSeparateDatabases()
        Dim sourceBuilder As New SqlConnectionStringBuilder(_sourceConnectionString)
        Dim warehouseBuilder As New SqlConnectionStringBuilder(_warehouseConnectionString)
        If sourceBuilder.DataSource.Equals(warehouseBuilder.DataSource, StringComparison.OrdinalIgnoreCase) AndAlso
           sourceBuilder.InitialCatalog.Equals(warehouseBuilder.InitialCatalog, StringComparison.OrdinalIgnoreCase) Then
            Throw New ConfigurationErrorsException(
                "ReportingWarehouse must point to a database separate from the ReportingSuite operational database.")
        End If
    End Sub

    Private Shared Sub ValidateRequest(ByVal request As ACoAReportWarehouseRequest)
        If request Is Nothing Then
            Throw New ArgumentNullException("request")
        End If
        If request.ReportId <= 0 Then
            Throw New InvalidOperationException("Select a report.")
        End If
        If request.YearFrom <= 0 OrElse request.YearTo < request.YearFrom OrElse
           String.IsNullOrWhiteSpace(request.YearCsv) Then
            Throw New InvalidOperationException("Select a valid year range.")
        End If
        If request.QuarterFrom <= 0 OrElse request.QuarterTo < request.QuarterFrom OrElse
           String.IsNullOrWhiteSpace(request.QuarterCsv) Then
            Throw New InvalidOperationException("Select a valid quarter range.")
        End If
        If String.IsNullOrWhiteSpace(request.EntityType) Then
            Throw New InvalidOperationException("Select an entity type.")
        End If
        If String.IsNullOrWhiteSpace(request.ReportColumnIdsCsv) Then
            Throw New InvalidOperationException("The selected report does not expose warehouseable report columns.")
        End If
        If request.Status <> 1 AndAlso request.Status <> 3 Then
            Throw New InvalidOperationException("Select a supported submission status.")
        End If
    End Sub

    Private Shared Sub ValidateResultContract(ByVal result As DataTable)
        Dim missingColumns As New List(Of String)()
        For Each requiredColumn As String In RequiredResultColumns
            If Not result.Columns.Contains(requiredColumn) Then
                missingColumns.Add(requiredColumn)
            End If
        Next

        If missingColumns.Count > 0 Then
            Throw New InvalidOperationException(
                SourceProcedure & " no longer matches warehouse contract " & ContractVersion &
                ". Missing columns: " & String.Join(", ", missingColumns) & ".")
        End If
    End Sub

    Private Shared Sub EnsureWarehouseSchema(ByVal connection As SqlConnection)
        Using command As New SqlCommand(
            "SELECT CASE WHEN OBJECT_ID(N'[etl].[ACoAReportLoadBatch]', N'U') IS NOT NULL " &
            "AND OBJECT_ID(N'[dw].[FactACoAReportSnapshot]', N'U') IS NOT NULL " &
            "AND COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'ExtractedRowCount') IS NOT NULL " &
            "AND COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'LoadedRowCount') IS NOT NULL " &
            "AND COL_LENGTH(N'[etl].[ACoAReportLoadBatch]', N'SkippedNullValueRowCount') IS NOT NULL " &
            "THEN 1 ELSE 0 END", connection)
            If Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) <> 1 Then
                Throw New InvalidOperationException(
                    "The ReportingWarehouse schema is missing. Apply Database\ReportingWarehouse.sql to the separate warehouse database.")
            End If
        End Using
    End Sub

    Private Shared Function CreateLoadBatch(ByVal connection As SqlConnection,
                                            ByVal request As ACoAReportWarehouseRequest,
                                            ByVal startedAtUtc As DateTime) As Long
        Const sql As String =
            "INSERT INTO [etl].[ACoAReportLoadBatch] " &
            "(LoadRunID, StartedAtUtc, LoadStatus, RequestedBy, SourceProcedure, SourceContractVersion, " &
            "ReportID, QuarterFrom, QuarterTo, QuarterSelection, YearFrom, YearTo, YearSelection, " &
            "EntityType, EntitySelection, [Status], ReportColumnSelection) " &
            "VALUES (@LoadRunID, @StartedAtUtc, 'Running', @RequestedBy, @SourceProcedure, @ContractVersion, " &
            "@ReportID, @QuarterFrom, @QuarterTo, @QuarterSelection, @YearFrom, @YearTo, @YearSelection, " &
            "@EntityType, @EntitySelection, @Status, @ReportColumnSelection); SELECT CAST(SCOPE_IDENTITY() AS bigint);"

        Using command As New SqlCommand(sql, connection)
            command.Parameters.Add("@LoadRunID", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid()
            command.Parameters.Add("@StartedAtUtc", SqlDbType.DateTime2).Value = startedAtUtc
            AddNVarChar(command, "@RequestedBy", 256, request.RequestedBy)
            AddNVarChar(command, "@SourceProcedure", 256, SourceProcedure)
            AddNVarChar(command, "@ContractVersion", 128, ContractVersion)
            command.Parameters.Add("@ReportID", SqlDbType.BigInt).Value = request.ReportId
            command.Parameters.Add("@QuarterFrom", SqlDbType.BigInt).Value = request.QuarterFrom
            command.Parameters.Add("@QuarterTo", SqlDbType.BigInt).Value = request.QuarterTo
            AddNVarChar(command, "@QuarterSelection", -1, request.QuarterCsv)
            command.Parameters.Add("@YearFrom", SqlDbType.Int).Value = request.YearFrom
            command.Parameters.Add("@YearTo", SqlDbType.Int).Value = request.YearTo
            AddNVarChar(command, "@YearSelection", -1, request.YearCsv)
            AddNVarChar(command, "@EntityType", 4000, request.EntityType)
            AddNVarChar(command, "@EntitySelection", -1, request.EntitiesCsv)
            command.Parameters.Add("@Status", SqlDbType.Int).Value = request.Status
            AddNVarChar(command, "@ReportColumnSelection", -1, request.ReportColumnIdsCsv)
            Return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture)
        End Using
    End Function

    Private Shared Sub BulkInsertSnapshot(ByVal connection As SqlConnection,
                                          ByVal transaction As SqlTransaction,
                                          ByVal extract As DataTable)
        Using bulkCopy As New SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock Or SqlBulkCopyOptions.CheckConstraints,
                                          transaction)
            bulkCopy.DestinationTableName = "[dw].[FactACoAReportSnapshot]"
            bulkCopy.BatchSize = 5000
            bulkCopy.BulkCopyTimeout = GetPositiveSetting("ReportWarehouseCommandTimeoutSeconds",
                                                         DefaultCommandTimeoutSeconds)

            bulkCopy.ColumnMappings.Add("LoadBatchID", "LoadBatchID")
            For Each requiredColumn As String In RequiredResultColumns
                Dim destinationColumn As String = requiredColumn
                If requiredColumn.Equals("Entity Name", StringComparison.Ordinal) Then
                    destinationColumn = "EntityName"
                ElseIf requiredColumn.Equals("License Number", StringComparison.Ordinal) Then
                    destinationColumn = "LicenseNumber"
                End If
                bulkCopy.ColumnMappings.Add(requiredColumn, destinationColumn)
            Next

            bulkCopy.WriteToServer(extract)
        End Using
    End Sub

    Private Shared Sub MarkLoadCompleted(ByVal connection As SqlConnection,
                                         ByVal transaction As SqlTransaction,
                                         ByVal loadBatchId As Long,
                                         ByVal extractedRowCount As Integer,
                                         ByVal loadedRowCount As Integer,
                                         ByVal skippedNullValueRowCount As Integer,
                                         ByVal completedAtUtc As DateTime,
                                         ByVal durationMilliseconds As Long)
        Using command As New SqlCommand(
            "UPDATE [etl].[ACoAReportLoadBatch] SET LoadStatus = 'Completed', CompletedAtUtc = @CompletedAtUtc, " &
            "ExtractedRowCount = @ExtractedRowCount, LoadedRowCount = @LoadedRowCount, " &
            "SkippedNullValueRowCount = @SkippedNullValueRowCount, RowCount = @LoadedRowCount, " &
            "DurationMilliseconds = @DurationMilliseconds, ErrorMessage = NULL " &
            "WHERE LoadBatchID = @LoadBatchID", connection, transaction)
            command.Parameters.Add("@CompletedAtUtc", SqlDbType.DateTime2).Value = completedAtUtc
            command.Parameters.Add("@ExtractedRowCount", SqlDbType.Int).Value = extractedRowCount
            command.Parameters.Add("@LoadedRowCount", SqlDbType.Int).Value = loadedRowCount
            command.Parameters.Add("@SkippedNullValueRowCount", SqlDbType.Int).Value = skippedNullValueRowCount
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
                    "UPDATE [etl].[ACoAReportLoadBatch] SET LoadStatus = 'Failed', CompletedAtUtc = @CompletedAtUtc, " &
                    "DurationMilliseconds = @DurationMilliseconds, ErrorMessage = @ErrorMessage " &
                    "WHERE LoadBatchID = @LoadBatchID", connection)
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
            ' Do not replace the original extraction/load failure with a logging failure.
        End Try
    End Sub

    Private Shared Sub AddNVarChar(ByVal command As SqlCommand,
                                   ByVal parameterName As String,
                                   ByVal size As Integer,
                                   ByVal value As String)
        Dim parameter As SqlParameter = command.Parameters.Add(parameterName, SqlDbType.NVarChar, size)
        parameter.Value = If(String.IsNullOrWhiteSpace(value), CType(DBNull.Value, Object), value)
    End Sub

    Private Shared Function GetPositiveSetting(ByVal settingName As String, ByVal fallback As Integer) As Integer
        Dim configuredValue As Integer
        If Integer.TryParse(ConfigurationManager.AppSettings(settingName), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, configuredValue) AndAlso configuredValue > 0 Then
            Return configuredValue
        End If
        Return fallback
    End Function
End Class

Public NotInheritable Class ACoAReportWarehouseRequest
    Public Property ReportId As Long
    Public Property QuarterFrom As Long
    Public Property QuarterTo As Long
    Public Property QuarterCsv As String
    Public Property YearFrom As Integer
    Public Property YearTo As Integer
    Public Property YearCsv As String
    Public Property EntityType As String
    Public Property EntitiesCsv As String
    Public Property Status As Integer
    Public Property ReportColumnIdsCsv As String
    Public Property RequestedBy As String
End Class

Public NotInheritable Class ACoAReportWarehouseResult
    Public Property LoadBatchId As Long
    Public Property ExtractedRowCount As Integer
    Public Property LoadedRowCount As Integer
    Public Property SkippedNullValueRowCount As Integer
    Public Property StartedAtUtc As DateTime
    Public Property CompletedAtUtc As DateTime
End Class

Public NotInheritable Class ACoAReportWarehouseHealth
    Public Property IsConfigured As Boolean
    Public Property IsReachable As Boolean
    Public Property IsSchemaReady As Boolean
    Public Property HasRequiredPermissions As Boolean
    Public Property DatabaseName As String
    Public Property DatabasePrincipal As String
    Public Property MissingPermissions As String
    Public Property Message As String
End Class
