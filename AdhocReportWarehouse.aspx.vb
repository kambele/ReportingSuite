Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Web
Imports System.Web.UI.WebControls

Public Class AdhocReportWarehousePage
    Inherits System.Web.UI.Page

    Private _warehouseReady As Boolean
    Private _isAuthorized As Boolean

    Protected Overrides Sub OnInit(ByVal e As EventArgs)
        MyBase.OnInit(e)
        Dim accountName As String = GetWindowsUserName()
        If Not String.IsNullOrWhiteSpace(accountName) Then
            ViewStateUserKey = accountName
        End If
    End Sub

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        accessMessagePanel.Visible = False
        operationMessagePanel.Visible = False
        _isAuthorized = Not String.IsNullOrWhiteSpace(GetWindowsUserName())
        If Not _isAuthorized Then
            ShowPageMessage("Your Windows account could not be identified.", 401)
            Return
        End If

        Try
            If Not IsPostBack Then
                BindReports()
                BindFilterColumns()
            End If
            UpdateParameterSummary()
            UpdateWarehouseHealth()
            BindRecentLoads()
        Catch ex As Exception
            Trace.Warn("AdhocReportWarehouse", "Unable to initialize the Adhoc warehouse page.", ex)
            ShowPageMessage("The Adhoc warehouse page could not be initialized. Review the application trace.", 500)
        End Try
    End Sub

    Private Sub reportDropDown_SelectedIndexChanged(ByVal sender As Object, ByVal e As EventArgs) Handles reportDropDown.SelectedIndexChanged
        BindFilterColumns()
        UpdateParameterSummary()
    End Sub

    Private Sub previewButton_Click(ByVal sender As Object, ByVal e As EventArgs) Handles previewButton.Click
        If Not _isAuthorized Then Return
        Try
            Dim request As AdhocReportWarehouseRequest = BuildRequest()
            Dim preview As AdhocReportWarehousePreview = CreateWarehouseService().ValidateExtraction(request)
            ShowOperationMessage(
                "<strong>Extraction validated.</strong> " & HttpUtility.HtmlEncode(preview.ReportName) &
                " returned " & preview.RowCount.ToString("N0", CultureInfo.CurrentCulture) & " rows and " &
                preview.ColumnCount.ToString("N0", CultureInfo.CurrentCulture) & " columns from <code>" &
                HttpUtility.HtmlEncode(preview.SourceView) & "</code>. Schema fingerprint: <code>" &
                HttpUtility.HtmlEncode(preview.SchemaHash.Substring(0, 16)) & "</code>. Loaded range: <strong>" &
                HttpUtility.HtmlEncode(preview.FilterSummary) & "</strong>. No warehouse data was written.", False)
        Catch ex As Exception
            Trace.Warn("AdhocReportWarehouse", "Adhoc extraction validation failed.", ex)
            ShowOperationMessage(HttpUtility.HtmlEncode(GetSafeOperationMessage(ex)), True)
        End Try
    End Sub

    Private Sub loadWarehouseButton_Click(ByVal sender As Object, ByVal e As EventArgs) Handles loadWarehouseButton.Click
        If Not _isAuthorized Then Return
        Try
            If Not _warehouseReady Then
                Throw New InvalidOperationException(
                    "The Adhoc warehouse is not ready. Apply the updated ReportingWarehouse deployment script and verify loader permissions.")
            End If
            Dim request As AdhocReportWarehouseRequest = BuildRequest()
            Dim result As AdhocReportWarehouseResult = CreateWarehouseService().LoadSnapshot(request)
            Dim elapsed As TimeSpan = result.CompletedAtUtc - result.StartedAtUtc
            ShowOperationMessage(
                "<strong>Adhoc warehouse load completed.</strong> Batch " &
                result.LoadBatchId.ToString(CultureInfo.CurrentCulture) & " stored " &
                result.LoadedRowCount.ToString("N0", CultureInfo.CurrentCulture) & " JSON rows across " &
                result.ColumnCount.ToString("N0", CultureInfo.CurrentCulture) & " source columns using schema version " &
                result.SchemaVersion.ToString(CultureInfo.CurrentCulture) & " in " &
                elapsed.TotalSeconds.ToString("N1", CultureInfo.CurrentCulture) & " seconds. Loaded range: <strong>" &
                HttpUtility.HtmlEncode(result.FilterSummary) & "</strong>.", False)
            BindRecentLoads()
        Catch ex As Exception
            Trace.Warn("AdhocReportWarehouse", "Adhoc warehouse load failed.", ex)
            ShowOperationMessage(HttpUtility.HtmlEncode(GetSafeOperationMessage(ex)), True)
        End Try
    End Sub

    Private Sub BindReports()
        Dim reports As DataTable = CreateWarehouseService().GetAvailableReports()
        Dim requestedIds As New List(Of Long)()
        For Each reportRow As DataRow In reports.Rows
            requestedIds.Add(Convert.ToInt64(reportRow("ReportID"), CultureInfo.InvariantCulture))
        Next
        Dim accessibleIds As HashSet(Of Long) = ReportAccessService.GetAccessibleReportIds(Context, requestedIds)
        Dim requestedReportId As Long
        Long.TryParse(Convert.ToString(Request.QueryString("reportId"), CultureInfo.InvariantCulture),
                      NumberStyles.Integer, CultureInfo.InvariantCulture, requestedReportId)

        reportDropDown.Items.Clear()
        reportDropDown.Items.Add(New ListItem("Select an Adhoc report", String.Empty))
        For Each row As DataRow In reports.Rows
            Dim reportId As Long = Convert.ToInt64(row("ReportID"), CultureInfo.InvariantCulture)
            If Not accessibleIds.Contains(reportId) Then Continue For
            Dim label As String = Convert.ToString(row("DisplayName"), CultureInfo.CurrentCulture) &
                                  " (R" & reportId.ToString(CultureInfo.InvariantCulture) & ")"
            reportDropDown.Items.Add(New ListItem(label, reportId.ToString(CultureInfo.InvariantCulture)))
        Next
        If requestedReportId > 0 AndAlso reportDropDown.Items.FindByValue(
            requestedReportId.ToString(CultureInfo.InvariantCulture)) IsNot Nothing Then
            reportDropDown.SelectedValue = requestedReportId.ToString(CultureInfo.InvariantCulture)
        End If
        Dim hasReports As Boolean = reportDropDown.Items.Count > 1
        previewButton.Enabled = hasReports
        loadWarehouseButton.Enabled = hasReports
    End Sub

    Private Sub BindFilterColumns()
        filterColumnDropDown.Items.Clear()
        filterColumnDropDown.Items.Add(New ListItem("Full report (no filter)", String.Empty))
        filterOperatorDropDown.SelectedValue = String.Empty
        filterValueFromTextBox.Text = String.Empty
        filterValueToTextBox.Text = String.Empty

        Dim reportId As Long
        If Not Long.TryParse(reportDropDown.SelectedValue, NumberStyles.Integer,
                             CultureInfo.InvariantCulture, reportId) OrElse reportId <= 0 Then
            filterColumnDropDown.Enabled = False
            filterOperatorDropDown.Enabled = False
            filterValueFromTextBox.Enabled = False
            filterValueToTextBox.Enabled = False
            Return
        End If

        ReportAccessService.DemandReportAccess(Context, reportId)
        Dim columns As DataTable = CreateWarehouseService().GetFilterColumns(reportId)
        For Each row As DataRow In columns.Rows
            Dim columnName As String = Convert.ToString(row("ColumnName"), CultureInfo.InvariantCulture)
            Dim dataTypeName As String = Convert.ToString(row("DataTypeName"), CultureInfo.InvariantCulture)
            filterColumnDropDown.Items.Add(New ListItem(columnName & " (" & dataTypeName & ")", columnName))
        Next
        filterColumnDropDown.Enabled = filterColumnDropDown.Items.Count > 1
        filterOperatorDropDown.Enabled = filterColumnDropDown.Enabled
        filterValueFromTextBox.Enabled = filterColumnDropDown.Enabled
        filterValueToTextBox.Enabled = filterColumnDropDown.Enabled
    End Sub

    Private Function BuildRequest() As AdhocReportWarehouseRequest
        Dim reportId As Long
        If Not Long.TryParse(reportDropDown.SelectedValue, NumberStyles.Integer,
                             CultureInfo.InvariantCulture, reportId) OrElse reportId <= 0 Then
            Throw New InvalidOperationException("Select an Adhoc report.")
        End If
        ReportAccessService.DemandReportAccess(Context, reportId)
        Return New AdhocReportWarehouseRequest With {
            .ReportId = reportId,
            .RequestedBy = GetWindowsUserName(),
            .FilterColumn = filterColumnDropDown.SelectedValue,
            .FilterOperator = filterOperatorDropDown.SelectedValue,
            .FilterValueFrom = filterValueFromTextBox.Text,
            .FilterValueTo = filterValueToTextBox.Text
        }
    End Function

    Private Sub UpdateParameterSummary()
        If reportDropDown.SelectedItem Is Nothing OrElse String.IsNullOrWhiteSpace(reportDropDown.SelectedValue) Then
            parameterSummaryLiteral.Text = "<span class=""badge badge-light"">No report selected</span>"
            Return
        End If
        Dim rangeLabel As String = "Full snapshot"
        If filterColumnDropDown.SelectedItem IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(filterColumnDropDown.SelectedValue) Then
            rangeLabel = filterColumnDropDown.SelectedValue & " · " &
                         GetOperatorDisplayName(filterOperatorDropDown.SelectedValue)
        End If
        parameterSummaryLiteral.Text = "<span class=""badge badge-light"">" &
            HttpUtility.HtmlEncode(reportDropDown.SelectedItem.Text) & "</span> " &
            "<span class=""badge badge-light"">" & HttpUtility.HtmlEncode(rangeLabel) & "</span>"
    End Sub

    Private Shared Function GetOperatorDisplayName(ByVal value As String) As String
        Select Case If(value, String.Empty).Trim().ToLowerInvariant()
            Case "between"
                Return "Between"
            Case "equals"
                Return "Equal to"
            Case "gte"
                Return "On or after"
            Case "lte"
                Return "On or before"
            Case "contains"
                Return "Contains"
            Case "starts_with"
                Return "Starts with"
            Case "is_null"
                Return "Is blank"
            Case "is_not_null"
                Return "Is not blank"
            Case Else
                Return "Select a condition"
        End Select
    End Function

    Private Sub UpdateWarehouseHealth()
        _warehouseReady = False
        Try
            Dim health As AdhocReportWarehouseHealth = CreateWarehouseService().CheckWarehouse()
            _warehouseReady = health.IsReachable AndAlso health.IsSchemaReady AndAlso health.HasRequiredPermissions
            loadWarehouseButton.Enabled = reportDropDown.Items.Count > 1 AndAlso _warehouseReady
            warehouseHealthPanel.CssClass = If(_warehouseReady,
                                               "alert alert-success m-3 mb-0",
                                               "alert alert-warning m-3 mb-0")
            Dim databaseLabel As String = If(String.IsNullOrWhiteSpace(health.DatabaseName), String.Empty,
                " <strong>Database:</strong> " & HttpUtility.HtmlEncode(health.DatabaseName) & ".")
            Dim principalLabel As String = If(String.IsNullOrWhiteSpace(health.DatabasePrincipal), String.Empty,
                " <strong>Connected as:</strong> " & HttpUtility.HtmlEncode(health.DatabasePrincipal) & ".")
            warehouseHealthLiteral.Text = HttpUtility.HtmlEncode(health.Message) & databaseLabel & principalLabel
        Catch ex As Exception
            Trace.Warn("AdhocReportWarehouse", "Adhoc warehouse health check failed.", ex)
            warehouseHealthPanel.CssClass = "alert alert-warning m-3 mb-0"
            warehouseHealthLiteral.Text = "The Adhoc warehouse connection is unavailable or incorrectly configured."
            loadWarehouseButton.Enabled = False
        End Try
    End Sub

    Private Sub BindRecentLoads()
        recentLoadsGrid.DataSource = Nothing
        If _warehouseReady Then
            Try
                recentLoadsGrid.DataSource = CreateWarehouseService().GetRecentLoads(10)
            Catch ex As Exception
                Trace.Warn("AdhocReportWarehouse", "Recent Adhoc warehouse loads could not be read.", ex)
            End Try
        End If
        recentLoadsGrid.DataBind()
    End Sub

    Private Function CreateWarehouseService() As AdhocReportWarehouseService
        Return New AdhocReportWarehouseService(
            AdhocReportWarehouseService.GetConfiguredSourceConnectionString(),
            AdhocReportWarehouseService.GetConfiguredWarehouseConnectionString())
    End Function

    Private Function GetWindowsUserName() As String
        If Request IsNot Nothing AndAlso Request.LogonUserIdentity IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(Request.LogonUserIdentity.Name) Then
            Return Request.LogonUserIdentity.Name
        End If
        If Context IsNot Nothing AndAlso Context.User IsNot Nothing AndAlso Context.User.Identity IsNot Nothing Then
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
        If TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ConfigurationErrorsException OrElse
           TypeOf ex Is UnauthorizedAccessException Then
            Return ex.Message
        End If
        Dim current As Exception = ex
        While current IsNot Nothing
            Dim sqlException As SqlException = TryCast(current, SqlException)
            If sqlException IsNot Nothing Then
                Select Case sqlException.Number
                    Case 229
                        Return "SQL Server denied a warehouse permission. Grant ReportingWarehouseLoader to the connected principal."
                    Case -2
                        Return "The Adhoc extraction timed out. Review the source query or configured warehouse timeout."
                    Case Else
                        Return "The database operation failed with SQL error " &
                            sqlException.Number.ToString(CultureInfo.InvariantCulture) & ": " & sqlException.Message
                End Select
            End If
            current = current.InnerException
        End While
        Return "The Adhoc warehouse operation failed. Review the application trace for details."
    End Function
End Class
