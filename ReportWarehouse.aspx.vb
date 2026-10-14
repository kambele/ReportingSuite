Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Web
Imports System.Web.UI.WebControls

Public Class ReportWarehousePage
    Inherits System.Web.UI.Page

    Private Const ReportColumnsProcedure As String = "[dbo].[GetReportColumns]"
    Private Const ReportEntitiesProcedure As String = "[dbo].[GetAllCompaniesByGroupWithSubmission]"
    Private Const ReportsProcedure As String = "[dbo].[ReportsList]"
    Private Const EntityGroupsProcedure As String = "[dbo].[GetEntityGroups]"
    Private Const YearsProcedure As String = "[dbo].[getYears]"
    Private Const QuartersProcedure As String = "[dbo].[getQuarters]"

    Private ReadOnly _db As New ReportingSuiteEntities3()
    Private _warehouseReady As Boolean

    Protected Overrides Sub OnInit(ByVal e As EventArgs)
        MyBase.OnInit(e)
        ' Bind ViewState to the authenticated Windows identity rather than the
        ' session ID. A session can expire between rendering and postback,
        ' which changes the key and causes a valid postback to fail MAC checks.
        If Context IsNot Nothing AndAlso
            Context.User IsNot Nothing AndAlso
            Context.User.Identity IsNot Nothing AndAlso
            Context.User.Identity.IsAuthenticated AndAlso
            Not String.IsNullOrWhiteSpace(Context.User.Identity.Name) Then
            ViewStateUserKey = Context.User.Identity.Name
        End If
    End Sub

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        accessMessagePanel.Visible = False
        operationMessagePanel.Visible = False

        Dim authorizationMessage As String = Nothing
        Dim authorizationStatus As Integer = 403
        If Not TryAuthorizeAdministrator(authorizationMessage, authorizationStatus) Then
            ShowPageMessage(authorizationMessage, authorizationStatus)
            Return
        End If

        Try
            PopulateParameterControls()
            parameterSummaryLiteral.Text = BuildParameterSummary()
            UpdateWarehouseHealth()
            BindRecentLoads()
        Catch ex As Exception
            Trace.Warn("ReportWarehouse", "Unable to initialize the warehouse page.", ex)
            ShowPageMessage("The warehouse parameters could not be loaded from ReportingSuite.", 500)
        End Try
    End Sub

    Private Sub previewButton_Click(ByVal sender As Object, ByVal e As EventArgs) Handles previewButton.Click
        Try
            Dim request As ACoAReportWarehouseRequest = BuildRequest()
            Dim extract As DataTable = ACoAReportWarehouseService.ExtractFromConfiguredSource(request)
            Dim warehouseRows As DataTable = ACoAReportWarehouseService.FilterRowsWithLineItemValue(extract)
            Dim skippedRows As Integer = extract.Rows.Count - warehouseRows.Rows.Count
            ShowOperationMessage(
                "<strong>Extraction validated.</strong> The procedure returned " &
                extract.Rows.Count.ToString("N0", CultureInfo.CurrentCulture) & " rows; " &
                warehouseRows.Rows.Count.ToString("N0", CultureInfo.CurrentCulture) &
                " have a LineItemValue and are eligible to load, while " &
                skippedRows.ToString("N0", CultureInfo.CurrentCulture) & " null-value rows will be skipped. The result has " &
                extract.Columns.Count.ToString("N0", CultureInfo.CurrentCulture) &
                " columns. No warehouse data was written.", False)
        Catch ex As Exception
            Trace.Warn("ReportWarehouse", "Warehouse extraction validation failed.", ex)
            ShowOperationMessage(HttpUtility.HtmlEncode(GetSafeOperationMessage(ex)), True)
        End Try
    End Sub

    Private Sub loadWarehouseButton_Click(ByVal sender As Object, ByVal e As EventArgs) Handles loadWarehouseButton.Click
        Try
            If Not _warehouseReady Then
                Throw New InvalidOperationException(
                    "The separate warehouse database is not ready. Apply the deployment script and verify its connection first.")
            End If

            Dim request As ACoAReportWarehouseRequest = BuildRequest()
            Dim service As ACoAReportWarehouseService = CreateWarehouseService()
            Dim result As ACoAReportWarehouseResult = service.LoadSnapshot(request)
            Dim elapsed As TimeSpan = result.CompletedAtUtc - result.StartedAtUtc

            ShowOperationMessage(
                "<strong>Warehouse load completed.</strong> Batch " &
                result.LoadBatchId.ToString(CultureInfo.CurrentCulture) & " extracted " &
                result.ExtractedRowCount.ToString("N0", CultureInfo.CurrentCulture) & " rows, stored " &
                result.LoadedRowCount.ToString("N0", CultureInfo.CurrentCulture) &
                " non-null values, and skipped " &
                result.SkippedNullValueRowCount.ToString("N0", CultureInfo.CurrentCulture) & " null values in " &
                elapsed.TotalSeconds.ToString("N1", CultureInfo.CurrentCulture) & " seconds.", False)
            BindRecentLoads()
        Catch ex As Exception
            Trace.Warn("ReportWarehouse", "Warehouse load failed.", ex)
            ShowOperationMessage(HttpUtility.HtmlEncode(GetSafeOperationMessage(ex)), True)
        End Try
    End Sub

    Private Sub PopulateParameterControls()
        Dim selectedGroup As String = entityTypeDropDown.SelectedValue
        Dim selectedReport As String = reportDropDown.SelectedValue
        Dim selectedYearFrom As String = yearFromDropDown.SelectedValue
        Dim selectedYearTo As String = yearToDropDown.SelectedValue
        Dim selectedQuarterFrom As String = quarterFromDropDown.SelectedValue
        Dim selectedQuarterTo As String = quarterToDropDown.SelectedValue
        Dim selectedStatus As String = statusDropDown.SelectedValue
        Dim selectedEntities As List(Of String) = CaptureSelectedValues(entitiesListBox)
        Dim selectedColumns As List(Of String) = CaptureSelectedValues(reportColumnsListBox)

        If Not IsPostBack Then
            selectedGroup = FirstQueryStringValue("entity_type")
            selectedReport = FirstQueryStringValue("Report_id", "report_id")
            selectedYearFrom = FirstQueryStringValue("year_from")
            selectedYearTo = FirstQueryStringValue("year_to")
            selectedQuarterFrom = FirstQueryStringValue("quarter_from")
            selectedQuarterTo = FirstQueryStringValue("quarter_to")
            selectedStatus = FirstQueryStringValue("status")
            selectedEntities = SplitCsv(FirstQueryStringValue("entities"))
            selectedColumns = SplitCsv(FirstQueryStringValue("ReportColumnID", "ReportColumns"))
        End If

        Dim groupOptions As DataTable = ACoAReportWarehouseService.ExecuteSourceProcedure(EntityGroupsProcedure)
        entityTypeDropDown.Items.Clear()
        entityTypeDropDown.Items.Add(New ListItem("Select an entity type", String.Empty))
        AddOptions(entityTypeDropDown, groupOptions,
                   New String() {"EntityGroupID"}, New String() {"EntityGroupName"})
        SelectValue(entityTypeDropDown, selectedGroup)
        EnsureFirstNonEmptySelection(entityTypeDropDown)

        Dim reportOptions As New DataTable()
        Dim entityGroupId As Long
        If Long.TryParse(entityTypeDropDown.SelectedValue, NumberStyles.Integer,
                         CultureInfo.InvariantCulture, entityGroupId) Then
            reportOptions = ACoAReportWarehouseService.ExecuteSourceProcedure(
                ReportsProcedure,
                New Dictionary(Of String, Object) From {{"@EntityGroup", entityGroupId}})
        End If
        reportDropDown.Items.Clear()
        reportDropDown.Items.Add(New ListItem("Select a report", String.Empty))
        AddOptions(reportDropDown, reportOptions,
                   New String() {"ReportID", "Report_ID"},
                   New String() {"Report_Name", "ReportName", "Report_Title"})
        SelectValue(reportDropDown, selectedReport)
        EnsureFirstNonEmptySelection(reportDropDown)

        Dim yearOptions As DataTable = ACoAReportWarehouseService.ExecuteSourceProcedure(YearsProcedure)
        PopulateRangeDropDown(yearFromDropDown, yearOptions, "Year", "Year")
        PopulateRangeDropDown(yearToDropDown, yearOptions, "Year", "Year")
        If Not IsPostBack AndAlso String.IsNullOrWhiteSpace(selectedYearFrom) AndAlso
           String.IsNullOrWhiteSpace(selectedYearTo) Then
            selectedYearFrom = FindExtremeNumericValue(yearFromDropDown, True)
            selectedYearTo = selectedYearFrom
        End If
        SelectValue(yearFromDropDown, selectedYearFrom)
        SelectValue(yearToDropDown, selectedYearTo)
        EnsureFirstNonEmptySelection(yearFromDropDown)
        EnsureFirstNonEmptySelection(yearToDropDown)

        Dim quarterOptions As DataTable = ACoAReportWarehouseService.ExecuteSourceProcedure(QuartersProcedure)
        PopulateRangeDropDown(quarterFromDropDown, quarterOptions, "QuarterID", "Quarter")
        PopulateRangeDropDown(quarterToDropDown, quarterOptions, "QuarterID", "Quarter")
        If Not IsPostBack AndAlso String.IsNullOrWhiteSpace(selectedQuarterFrom) Then
            selectedQuarterFrom = FindExtremeNumericValue(quarterFromDropDown, False)
        End If
        If Not IsPostBack AndAlso String.IsNullOrWhiteSpace(selectedQuarterTo) Then
            selectedQuarterTo = FindExtremeNumericValue(quarterToDropDown, True)
        End If
        SelectValue(quarterFromDropDown, selectedQuarterFrom)
        SelectValue(quarterToDropDown, selectedQuarterTo)
        EnsureFirstNonEmptySelection(quarterFromDropDown)
        EnsureFirstNonEmptySelection(quarterToDropDown)

        SelectValue(statusDropDown, selectedStatus)
        PopulateReportColumns(selectedColumns)
        PopulateEntities(selectedEntities)
    End Sub

    Private Sub PopulateReportColumns(ByVal selectedValues As IEnumerable(Of String))
        reportColumnsListBox.Items.Clear()
        Dim reportId As Long
        If Not Long.TryParse(reportDropDown.SelectedValue, NumberStyles.Integer,
                             CultureInfo.InvariantCulture, reportId) Then
            reportColumnsListBox.Enabled = False
            Return
        End If

        Dim options As DataTable = ACoAReportWarehouseService.ExecuteSourceProcedure(
            ReportColumnsProcedure,
            New Dictionary(Of String, Object) From {{"@ReportID", reportId}})
        AddOptions(reportColumnsListBox, options,
                   New String() {"Report_ColumnID", "ReportColumnID"},
                   New String() {"ColumnName", "Column_Name"})
        ApplySelections(reportColumnsListBox, selectedValues)
        reportColumnsListBox.Enabled = reportColumnsListBox.Items.Count > 0
    End Sub

    Private Sub PopulateEntities(ByVal selectedValues As IEnumerable(Of String))
        entitiesListBox.Items.Clear()
        Dim entityGroupId As Integer
        If Not Integer.TryParse(entityTypeDropDown.SelectedValue, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, entityGroupId) Then
            entitiesListBox.Enabled = False
            Return
        End If

        Dim years As String = BuildInclusiveRangeCsv(yearFromDropDown, yearToDropDown)
        Dim quarters As String = BuildInclusiveRangeCsv(quarterFromDropDown, quarterToDropDown)
        If String.IsNullOrWhiteSpace(years) OrElse String.IsNullOrWhiteSpace(quarters) Then
            entitiesListBox.Enabled = False
            Return
        End If

        Dim options As DataTable = ACoAReportWarehouseService.ExecuteSourceProcedure(
            ReportEntitiesProcedure,
            New Dictionary(Of String, Object) From {
                {"@group", entityGroupId}, {"@year", years}, {"@q", quarters}
            })

        Dim valueColumn As String = FindColumn(options, "entity_instance_id", "EntityInstanceID")
        Dim nameColumn As String = FindColumn(options, "Company Name", "Company_Name", "Entity_Name", "EntityName")
        Dim licenseColumn As String = FindColumn(options, "License Number", "License_Number")
        For Each optionRow As DataRow In options.Rows
            Dim value As String = GetTextValue(optionRow, valueColumn)
            Dim label As String = GetTextValue(optionRow, nameColumn)
            Dim licenseNumber As String = GetTextValue(optionRow, licenseColumn)
            If Not String.IsNullOrWhiteSpace(licenseNumber) Then
                label = If(String.IsNullOrWhiteSpace(label), licenseNumber, label & " (" & licenseNumber & ")")
            End If
            If Not String.IsNullOrWhiteSpace(value) AndAlso entitiesListBox.Items.FindByValue(value) Is Nothing Then
                entitiesListBox.Items.Add(New ListItem(If(String.IsNullOrWhiteSpace(label), value, label), value))
            End If
        Next

        ApplySelections(entitiesListBox, selectedValues)
        entitiesListBox.Enabled = entitiesListBox.Items.Count > 0
    End Sub

    Private Function BuildRequest() As ACoAReportWarehouseRequest
        Dim reportId As Long = ParseRequiredLong(reportDropDown, "report")
        Dim yearFrom As Integer = CInt(ParseRequiredLong(yearFromDropDown, "year from"))
        Dim yearTo As Integer = CInt(ParseRequiredLong(yearToDropDown, "year to"))
        Dim quarterFrom As Long = ParseRequiredLong(quarterFromDropDown, "quarter from")
        Dim quarterTo As Long = ParseRequiredLong(quarterToDropDown, "quarter to")
        If yearTo < yearFrom Then
            Throw New InvalidOperationException("Year to must be greater than or equal to year from.")
        End If
        If quarterTo < quarterFrom Then
            Throw New InvalidOperationException("Quarter to must be greater than or equal to quarter from.")
        End If
        If String.IsNullOrWhiteSpace(entityTypeDropDown.SelectedValue) Then
            Throw New InvalidOperationException("Select an entity type.")
        End If

        Dim status As Integer
        If Not Integer.TryParse(statusDropDown.SelectedValue, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, status) Then
            Throw New InvalidOperationException("Select a submission status.")
        End If

        Return New ACoAReportWarehouseRequest With {
            .ReportId = reportId,
            .YearFrom = yearFrom,
            .YearTo = yearTo,
            .YearCsv = BuildInclusiveRangeCsv(yearFromDropDown, yearToDropDown),
            .QuarterFrom = quarterFrom,
            .QuarterTo = quarterTo,
            .QuarterCsv = BuildInclusiveRangeCsv(quarterFromDropDown, quarterToDropDown),
            .EntityType = entityTypeDropDown.SelectedValue,
            .EntitiesCsv = BuildSelectedValuesCsv(entitiesListBox, False),
            .Status = status,
            .ReportColumnIdsCsv = BuildSelectedValuesCsv(reportColumnsListBox, True),
            .RequestedBy = GetWindowsUserName()
        }
    End Function

    Private Sub UpdateWarehouseHealth()
        _warehouseReady = False
        Dim warehouseConnectionString As String = ACoAReportWarehouseService.GetConfiguredWarehouseConnectionString()
        If String.IsNullOrWhiteSpace(warehouseConnectionString) Then
            warehouseHealthPanel.CssClass = "alert alert-warning m-3 mb-0"
            warehouseHealthLiteral.Text = "The <code>ReportingWarehouse</code> connection is not configured."
            loadWarehouseButton.Enabled = False
            Return
        End If

        Try
            Dim health As ACoAReportWarehouseHealth = CreateWarehouseService().CheckWarehouse()
            _warehouseReady = health.IsReachable AndAlso
                              health.IsSchemaReady AndAlso
                              health.HasRequiredPermissions
            loadWarehouseButton.Enabled = _warehouseReady
            warehouseHealthPanel.CssClass = If(_warehouseReady,
                                               "alert alert-success m-3 mb-0",
                                               "alert alert-warning m-3 mb-0")
            Dim databaseLabel As String = If(String.IsNullOrWhiteSpace(health.DatabaseName),
                                              String.Empty,
                                              " <strong>Database:</strong> " & HttpUtility.HtmlEncode(health.DatabaseName) & ".")
            Dim principalLabel As String = If(String.IsNullOrWhiteSpace(health.DatabasePrincipal),
                                               String.Empty,
                                               " <strong>Connected as:</strong> " &
                                               HttpUtility.HtmlEncode(health.DatabasePrincipal) & ".")
            warehouseHealthLiteral.Text = HttpUtility.HtmlEncode(health.Message) & databaseLabel & principalLabel
        Catch ex As Exception
            Trace.Warn("ReportWarehouse", "Warehouse health check failed.", ex)
            warehouseHealthPanel.CssClass = "alert alert-warning m-3 mb-0"
            warehouseHealthLiteral.Text = "The separate warehouse connection is invalid or points to the operational database."
            loadWarehouseButton.Enabled = False
        End Try
    End Sub

    Private Sub BindRecentLoads()
        recentLoadsGrid.DataSource = Nothing
        If _warehouseReady Then
            Try
                recentLoadsGrid.DataSource = CreateWarehouseService().GetRecentLoads(10)
            Catch ex As Exception
                Trace.Warn("ReportWarehouse", "Recent warehouse loads could not be read.", ex)
            End Try
        End If
        recentLoadsGrid.DataBind()
    End Sub

    Private Function CreateWarehouseService() As ACoAReportWarehouseService
        Return New ACoAReportWarehouseService(
            ACoAReportWarehouseService.GetConfiguredSourceConnectionString(),
            ACoAReportWarehouseService.GetConfiguredWarehouseConnectionString())
    End Function

    Private Function BuildParameterSummary() As String
        Dim entityCount As Integer = entitiesListBox.Items.Cast(Of ListItem)().Count(Function(item) item.Selected)
        Dim columnCount As Integer = reportColumnsListBox.Items.Cast(Of ListItem)().Count(Function(item) item.Selected)
        Dim reportLabel As String = If(reportDropDown.SelectedItem Is Nothing,
                                       "No report", reportDropDown.SelectedItem.Text)
        Return "<span class=""badge badge-light"">" & HttpUtility.HtmlEncode(reportLabel) & "</span> " &
               "<span class=""badge badge-light"">" &
               HttpUtility.HtmlEncode(yearFromDropDown.SelectedValue & "–" & yearToDropDown.SelectedValue) & "</span> " &
               "<span class=""badge badge-light"">" &
               If(entityCount = 0, "All entities", entityCount.ToString(CultureInfo.CurrentCulture) & " entities") & "</span> " &
               "<span class=""badge badge-light"">" &
               If(columnCount = 0, "All columns", columnCount.ToString(CultureInfo.CurrentCulture) & " columns") & "</span>"
    End Function

    Private Shared Sub PopulateRangeDropDown(ByVal control As DropDownList,
                                             ByVal options As DataTable,
                                             ByVal valueColumnName As String,
                                             ByVal labelColumnName As String)
        control.Items.Clear()
        AddOptions(control, options, New String() {valueColumnName}, New String() {labelColumnName})
    End Sub

    Private Shared Sub AddOptions(ByVal control As ListControl,
                                  ByVal options As DataTable,
                                  ByVal valueCandidates As String(),
                                  ByVal labelCandidates As String())
        Dim valueColumn As String = FindColumn(options, valueCandidates)
        Dim labelColumn As String = FindColumn(options, labelCandidates)
        If String.IsNullOrWhiteSpace(valueColumn) Then
            Return
        End If

        For Each optionRow As DataRow In options.Rows
            Dim value As String = GetTextValue(optionRow, valueColumn)
            Dim label As String = GetTextValue(optionRow, labelColumn)
            If Not String.IsNullOrWhiteSpace(value) AndAlso control.Items.FindByValue(value) Is Nothing Then
                control.Items.Add(New ListItem(If(String.IsNullOrWhiteSpace(label), value, label), value))
            End If
        Next
    End Sub

    Private Shared Function FindColumn(ByVal table As DataTable, ParamArray candidates() As String) As String
        For Each candidate As String In candidates
            For Each column As DataColumn In table.Columns
                If column.ColumnName.Equals(candidate, StringComparison.OrdinalIgnoreCase) Then
                    Return column.ColumnName
                End If
            Next
        Next
        Return String.Empty
    End Function

    Private Shared Function GetTextValue(ByVal row As DataRow, ByVal columnName As String) As String
        If String.IsNullOrWhiteSpace(columnName) OrElse row.IsNull(columnName) Then
            Return String.Empty
        End If
        Return Convert.ToString(row(columnName), CultureInfo.CurrentCulture).Trim()
    End Function

    Private Shared Sub SelectValue(ByVal control As ListControl, ByVal value As String)
        If String.IsNullOrWhiteSpace(value) Then
            Return
        End If
        Dim item As ListItem = control.Items.FindByValue(value.Trim())
        If item IsNot Nothing Then
            control.ClearSelection()
            item.Selected = True
        End If
    End Sub

    Private Shared Sub EnsureFirstNonEmptySelection(ByVal control As ListControl)
        If Not String.IsNullOrWhiteSpace(control.SelectedValue) Then
            Return
        End If
        For Each item As ListItem In control.Items
            If Not String.IsNullOrWhiteSpace(item.Value) Then
                control.ClearSelection()
                item.Selected = True
                Return
            End If
        Next
    End Sub

    Private Shared Function FindExtremeNumericValue(ByVal control As ListControl, ByVal findHighest As Boolean) As String
        Dim extreme As Decimal
        Dim extremeValue As String = String.Empty
        Dim hasValue As Boolean
        For Each item As ListItem In control.Items
            Dim number As Decimal
            If Decimal.TryParse(item.Value, NumberStyles.Number, CultureInfo.InvariantCulture, number) AndAlso
               (Not hasValue OrElse (findHighest AndAlso number > extreme) OrElse (Not findHighest AndAlso number < extreme)) Then
                extreme = number
                extremeValue = item.Value
                hasValue = True
            End If
        Next
        Return extremeValue
    End Function

    Private Shared Function CaptureSelectedValues(ByVal control As ListControl) As List(Of String)
        Return control.Items.Cast(Of ListItem)().
            Where(Function(item) item.Selected AndAlso Not String.IsNullOrWhiteSpace(item.Value)).
            Select(Function(item) item.Value).ToList()
    End Function

    Private Shared Sub ApplySelections(ByVal control As ListControl, ByVal values As IEnumerable(Of String))
        If values Is Nothing Then
            Return
        End If
        For Each value As String In values
            Dim item As ListItem = control.Items.FindByValue(value)
            If item IsNot Nothing Then
                item.Selected = True
            End If
        Next
    End Sub

    Private Shared Function SplitCsv(ByVal csv As String) As List(Of String)
        If String.IsNullOrWhiteSpace(csv) Then
            Return New List(Of String)()
        End If
        Return csv.Split(","c).Select(Function(value) value.Trim()).
            Where(Function(value) Not String.IsNullOrWhiteSpace(value)).Distinct().ToList()
    End Function

    Private Shared Function BuildSelectedValuesCsv(ByVal control As ListControl,
                                                   ByVal useAllWhenNoneSelected As Boolean) As String
        Dim values As List(Of String) = CaptureSelectedValues(control)
        If values.Count = 0 AndAlso useAllWhenNoneSelected Then
            values = control.Items.Cast(Of ListItem)().
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Value)).
                Select(Function(item) item.Value).ToList()
        End If
        Return String.Join(",", values)
    End Function

    Private Shared Function BuildInclusiveRangeCsv(ByVal fromControl As ListControl,
                                                   ByVal toControl As ListControl) As String
        Dim fromNumber As Decimal
        Dim toNumber As Decimal
        If Not Decimal.TryParse(fromControl.SelectedValue, NumberStyles.Number,
                                CultureInfo.InvariantCulture, fromNumber) OrElse
           Not Decimal.TryParse(toControl.SelectedValue, NumberStyles.Number,
                                CultureInfo.InvariantCulture, toNumber) Then
            Return String.Empty
        End If

        Return String.Join(",", fromControl.Items.Cast(Of ListItem)().
            Where(Function(item)
                      Dim number As Decimal
                      Return Decimal.TryParse(item.Value, NumberStyles.Number,
                                              CultureInfo.InvariantCulture, number) AndAlso
                             number >= fromNumber AndAlso number <= toNumber
                  End Function).
            Select(Function(item) item.Value))
    End Function

    Private Shared Function ParseRequiredLong(ByVal control As ListControl, ByVal label As String) As Long
        Dim value As Long
        If Not Long.TryParse(control.SelectedValue, NumberStyles.Integer,
                             CultureInfo.InvariantCulture, value) Then
            Throw New InvalidOperationException("Select " & label & ".")
        End If
        Return value
    End Function

    Private Function FirstQueryStringValue(ParamArray names() As String) As String
        For Each name As String In names
            Dim value As String = Convert.ToString(Request.QueryString(name), CultureInfo.InvariantCulture)
            If Not String.IsNullOrWhiteSpace(value) Then
                Return value
            End If
        Next
        Return String.Empty
    End Function

    Private Function TryAuthorizeAdministrator(ByRef message As String, ByRef statusCode As Integer) As Boolean
        Dim accountName As String = GetWindowsUserName()
        If String.IsNullOrWhiteSpace(accountName) Then
            message = "Your Windows account could not be identified."
            statusCode = 401
            Return False
        End If

        Try
            Dim currentUser As User = _db.Users.SingleOrDefault(Function(item) item.Username = accountName)
            If currentUser Is Nothing OrElse Not currentUser.Is_Admin Then
                message = "ACoA warehouse loading is available to ReportingSuite administrators only."
                statusCode = 403
                Return False
            End If
        Catch ex As Exception
            Trace.Warn("ReportWarehouse", "Unable to validate the current administrator.", ex)
            message = "Your access could not be validated because ReportingSuite is unavailable."
            statusCode = 503
            Return False
        End Try
        Return True
    End Function

    Private Function GetWindowsUserName() As String
        If Request.LogonUserIdentity IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(Request.LogonUserIdentity.Name) Then
            Return Request.LogonUserIdentity.Name
        End If
        If Context.User IsNot Nothing AndAlso Context.User.Identity IsNot Nothing Then
            Return Convert.ToString(Context.User.Identity.Name, CultureInfo.InvariantCulture)
        End If
        Return String.Empty
    End Function

    Private Sub ShowPageMessage(ByVal message As String, ByVal statusCode As Integer)
        Response.StatusCode = statusCode
        Response.TrySkipIisCustomErrors = True
        accessMessagePanel.Visible = True
        accessMessageLiteral.Text = HttpUtility.HtmlEncode(message)
    End Sub

    Private Sub ShowOperationMessage(ByVal messageHtml As String, ByVal isError As Boolean)
        operationMessagePanel.Visible = True
        operationMessagePanel.CssClass = If(isError,
                                            "alert alert-danger m-3 mb-0",
                                            "alert alert-success m-3 mb-0")
        operationMessageLiteral.Text = messageHtml
    End Sub

    Private Shared Function GetSafeOperationMessage(ByVal ex As Exception) As String
        If TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ConfigurationErrorsException Then
            Return ex.Message
        End If

        Dim currentException As Exception = ex
        While currentException IsNot Nothing
            Dim sqlException As SqlException = TryCast(currentException, SqlException)
            If sqlException IsNot Nothing Then
                If sqlException.Message.IndexOf("SSPI", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    Return "Windows authentication could not establish the SQL Server identity (SSPI). " &
                           "Verify the IIS service account, domain connectivity, and SQL Server SPN configuration."
                End If

                Select Case sqlException.Number
                    Case 18456
                        Return "SQL Server rejected the configured application identity (error 18456)."
                    Case 4060
                        Return "The configured application identity cannot open the warehouse database (error 4060)."
                    Case 229
                        Return "SQL Server denied a permission required by this operation (error 229). " &
                               "Grant the warehouse loader role to the database principal shown in Warehouse status."
                    Case -2
                        Return "The database operation timed out. Narrow the selected parameters or review SQL Server performance."
                    Case 2601, 2627
                        Return "The warehouse rejected a duplicate load key (SQL error " &
                               sqlException.Number.ToString(CultureInfo.InvariantCulture) & ")."
                    Case 515, 547, 8114, 8152, 245, 2628
                        Return "The extracted result does not satisfy the warehouse schema (SQL error " &
                               sqlException.Number.ToString(CultureInfo.InvariantCulture) & "): " & sqlException.Message
                    Case Else
                        Return "The database operation failed with SQL error " &
                               sqlException.Number.ToString(CultureInfo.InvariantCulture) & ": " & sqlException.Message
                End Select
            End If
            currentException = currentException.InnerException
        End While

        Return "The operation failed. No warehouse data was committed. Review the server log for the captured exception."
    End Function

    Protected Overrides Sub OnUnload(ByVal e As EventArgs)
        _db.Dispose()
        MyBase.OnUnload(e)
    End Sub
End Class
