Imports System.Configuration
Imports System.Collections.Generic
Imports System.Data
Imports System.Data.Common
Imports System.Data.Entity.Core.EntityClient
Imports System.Globalization
Imports System.Text
Imports System.Web
Imports System.Web.UI.WebControls

Public Class ReportRenderPage
    Inherits System.Web.UI.Page

    Private Const DefaultRowLimit As Integer = 1000
    Private Const DefaultAggregateCellLimit As Integer = 5000
    Private Const DefaultCommandTimeoutSeconds As Integer = 120
    Private Const AdvancedReportProcedure As String = "[dbo].[get_ACoA_Reports_Advance_v2]"
    Private Const ReportColumnsProcedure As String = "[dbo].[GetReportColumns]"
    Private Const ReportEntitiesProcedure As String = "[dbo].[GetAllCompaniesByGroupWithSubmission]"
    Private Const ReportsProcedure As String = "[dbo].[ReportsList]"
    Private Const EntityGroupsProcedure As String = "[dbo].[GetEntityGroups]"
    Private Const YearsProcedure As String = "[dbo].[getYears]"
    Private Const QuartersProcedure As String = "[dbo].[getQuarters]"

    Private ReadOnly _db As New ReportingSuiteEntities3()

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        accessMessagePanel.Visible = False
        resultsPanel.Visible = False
        emptyStatePanel.Visible = True

        Dim authorizationMessage As String = Nothing
        Dim authorizationStatus As Integer = 403
        If Not TryAuthorizeAuthenticatedUser(authorizationMessage, authorizationStatus) Then
            ShowPageMessage(authorizationMessage, authorizationStatus)
            Return
        End If

        Try
            Dim procedureSource As New DatabaseViewInfo With {
                .SchemaName = "dbo",
                .ViewName = "get_ACoA_Reports_Advance_v2"
            }
            RenderSelectedView(procedureSource)
        Catch ex As UnauthorizedAccessException
            Trace.Warn("ReportRender", "Report access was denied.", ex)
            ShowPageMessage(ex.Message, 403)
        Catch ex As Exception
            Trace.Warn("ReportRender", "Unable to render the advanced ACoA report.", ex)
            ShowPageMessage("The advanced ACoA report could not be loaded. Verify the selected parameters and database availability.", 500)
        End Try
    End Sub

    Private Function TryAuthorizeAuthenticatedUser(ByRef message As String, ByRef statusCode As Integer) As Boolean
        Dim accountName As String = GetWindowsUserName()
        If String.IsNullOrWhiteSpace(accountName) Then
            message = "Your Windows account could not be identified."
            statusCode = 401
            Return False
        End If

        Return True
    End Function

    Private Sub RenderSelectedView(ByVal selectedView As DatabaseViewInfo)
        Try
            Dim wasTruncated As Boolean = False
            Dim aggregateDefinition As AggregateDefinition = CreateAdvancedReportDefinition()
            PopulateProcedureParameterControls(selectedView)
            If String.IsNullOrWhiteSpace(reportParameterDropDown.SelectedValue) Then
                Throw New UnauthorizedAccessException("No ACoA reports are available to your Windows account.")
            End If
            PopulateViewModeOptions(selectedView, True)

            Dim isDashboardMode As Boolean = viewModeDropDown.SelectedValue.Equals("dashboard", StringComparison.OrdinalIgnoreCase)
            Dim isAggregatedMode As Boolean = viewModeDropDown.SelectedValue.Equals("aggregate", StringComparison.OrdinalIgnoreCase)
            Dim usesAggregatedData As Boolean = isDashboardMode OrElse isAggregatedMode
            Dim rawData As DataTable = LoadAdvancedProcedureRows(selectedView)
            Dim data As DataTable = rawData
            Dim resultLimit As Integer

            If usesAggregatedData Then
                resultLimit = GetConfiguredAggregateCellLimit()
                data = AggregateAdvancedReportData(rawData, aggregateDefinition)
                TrimResult(data, resultLimit, wasTruncated)
            Else
                resultLimit = GetConfiguredRowLimit()
                TrimResult(data, resultLimit, wasTruncated)
            End If

            Dim reportTitle As String = If(reportParameterDropDown.SelectedItem Is Nothing,
                                           "Advanced ACoA report",
                                           reportParameterDropDown.SelectedItem.Text)
            selectedViewTitleLiteral.Text = HttpUtility.HtmlEncode(reportTitle)
            groupByPanel.Visible = Not usesAggregatedData
            parametersPanel.Visible = True
            statusParameterPanel.Visible = True
            reportColumnsParameterPanel.Visible = True
            parameterSummaryLiteral.Text = BuildParameterSummary()
            clearParametersLink.NavigateUrl = ResolveUrl("~/ReportRender.aspx") &
                                              "?mode=" & HttpUtility.UrlEncode(viewModeDropDown.SelectedValue)

            If isDashboardMode Then
                viewMetaLiteral.Text = BuildViewMetadata(selectedView, data.Rows.Count, data.Columns.Count,
                                                         "aggregated cells", "get_ACoA_Reports_Advance_v2")
                viewContentLiteral.Text = BuildReportDashboard(data, aggregateDefinition, reportTitle)
            ElseIf isAggregatedMode Then
                viewMetaLiteral.Text = BuildViewMetadata(selectedView, data.Rows.Count, data.Columns.Count,
                                                         "aggregated cells", "get_ACoA_Reports_Advance_v2")
                viewContentLiteral.Text = BuildAggregatedReport(data, aggregateDefinition, reportTitle)
            Else
                PopulateGroupingOptions(selectedView, data)

                Dim selectedGroupColumn As String = groupByDropDown.SelectedValue
                If Not String.IsNullOrWhiteSpace(selectedGroupColumn) AndAlso Not data.Columns.Contains(selectedGroupColumn) Then
                    selectedGroupColumn = String.Empty
                    groupByDropDown.SelectedIndex = 0
                End If

                viewMetaLiteral.Text = BuildViewMetadata(selectedView, data.Rows.Count, data.Columns.Count,
                                                         "procedure rows", "get_ACoA_Reports_Advance_v2")
                viewContentLiteral.Text = BuildRenderedOutput(data, selectedGroupColumn)
            End If

            renderNoticePanel.Visible = wasTruncated
            If wasTruncated Then
                Dim itemLabel As String = If(usesAggregatedData, "aggregated cells", "procedure rows")
                renderNoticeLiteral.Text = "This result contains more than " & resultLimit.ToString("N0", CultureInfo.CurrentCulture) &
                                           " " & itemLabel & ". The first " & resultLimit.ToString("N0", CultureInfo.CurrentCulture) &
                                           " are shown to protect application performance."
            End If

            resultsPanel.Visible = True
            emptyStatePanel.Visible = False
        Catch ex As Exception
            Trace.Warn("ReportRender", "Unable to execute " & AdvancedReportProcedure & ".", ex)
            ShowPageMessage("The advanced ACoA report could not be rendered for the selected parameters.", 500)
        End Try
    End Sub

    Private Sub PopulateViewModeOptions(ByVal selectedView As DatabaseViewInfo, ByVal supportsAggregation As Boolean)
        Dim viewChanged As Boolean = Not selectedView.Key.Equals(
            Convert.ToString(ViewState("ViewModeViewKey"), CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase)

        If IsPostBack AndAlso Not viewChanged AndAlso viewModeDropDown.Items.Count > 0 Then
            Return
        End If

        viewModeDropDown.Items.Clear()
        If supportsAggregation Then
            viewModeDropDown.Items.Add(New ListItem("Dashboard", "dashboard"))
            viewModeDropDown.Items.Add(New ListItem("Aggregated report", "aggregate"))
        End If
        viewModeDropDown.Items.Add(New ListItem("Detailed records", "detail"))

        Dim requestedMode As String = Convert.ToString(Request.QueryString("mode"), CultureInfo.InvariantCulture)
        If Not String.IsNullOrWhiteSpace(requestedMode) AndAlso viewModeDropDown.Items.FindByValue(requestedMode) IsNot Nothing Then
            viewModeDropDown.SelectedValue = requestedMode
        End If

        ViewState("ViewModeViewKey") = selectedView.Key
    End Sub

    Private Function LoadViewSchema(ByVal selectedView As DatabaseViewInfo) As DataTable
        Dim sql As String = "SELECT TOP (0) * FROM " & QuoteIdentifier(selectedView.SchemaName) & "." &
                            QuoteIdentifier(selectedView.ViewName)
        Return ExecuteViewQuery(selectedView, sql)
    End Function

    Private Function LoadViewData(ByVal selectedView As DatabaseViewInfo,
                                  ByVal reportFilter As ReportFilter,
                                  ByVal rowLimit As Integer,
                                  ByRef wasTruncated As Boolean) As DataTable
        Dim sql As String = "SELECT TOP (" & (rowLimit + 1).ToString(CultureInfo.InvariantCulture) & ") * FROM " &
                            QuoteIdentifier(selectedView.SchemaName) & "." & QuoteIdentifier(selectedView.ViewName)
        sql &= BuildWhereClause(reportFilter)
        Dim data As DataTable = ExecuteViewQuery(selectedView, sql, reportFilter.Parameters)

        TrimResult(data, rowLimit, wasTruncated)
        Return data
    End Function

    Private Function LoadAggregatedViewData(ByVal selectedView As DatabaseViewInfo,
                                            ByVal schema As DataTable,
                                            ByVal definition As AggregateDefinition,
                                            ByVal reportFilter As ReportFilter,
                                            ByVal cellLimit As Integer,
                                            ByRef wasTruncated As Boolean) As DataTable
        Dim sql As String = BuildAggregateSql(selectedView, schema, definition, reportFilter, cellLimit + 1)
        Dim data As DataTable = ExecuteViewQuery(selectedView, sql, reportFilter.Parameters)

        TrimResult(data, cellLimit, wasTruncated)
        Return data
    End Function

    Private Function LoadAdvancedReportData(ByVal selectedView As DatabaseViewInfo,
                                            ByVal definition As AggregateDefinition,
                                            ByVal cellLimit As Integer,
                                            ByRef wasTruncated As Boolean) As DataTable
        Dim rawData As DataTable = LoadAdvancedProcedureRows(selectedView)
        Dim aggregatedData As DataTable = AggregateAdvancedReportData(rawData, definition)
        TrimResult(aggregatedData, cellLimit, wasTruncated)
        Return aggregatedData
    End Function

    Private Function LoadAdvancedProcedureRows(ByVal selectedView As DatabaseViewInfo) As DataTable
        Return ExecuteViewQuery(selectedView, AdvancedReportProcedure, BuildAdvancedProcedureParameters(),
                                CommandType.StoredProcedure)
    End Function

    Private Shared Function AggregateAdvancedReportData(ByVal rawData As DataTable,
                                                        ByVal definition As AggregateDefinition) As DataTable
        If rawData.Columns.Count = 0 OrElse
           String.IsNullOrWhiteSpace(definition.ValueColumn) OrElse
           Not rawData.Columns.Contains(definition.ValueColumn) Then
            Return rawData
        End If

        Dim groupingColumns As New List(Of String)()
        AddUniqueColumn(groupingColumns, definition.ReportIdColumn)
        AddUniqueColumn(groupingColumns, definition.ReportNameColumn)
        AddUniqueColumn(groupingColumns, definition.ReportTitleColumn)
        AddUniqueColumn(groupingColumns, definition.ReportDescriptionColumn)
        AddUniqueColumn(groupingColumns, definition.ReportSubtitleColumn)
        AddUniqueColumn(groupingColumns, definition.EntityGroupIdColumn)
        AddUniqueColumn(groupingColumns, definition.EntityGroupNameColumn)
        AddUniqueColumn(groupingColumns, definition.LineItemTypeIdColumn)
        AddUniqueColumn(groupingColumns, definition.LineItemIdColumn)
        AddUniqueColumn(groupingColumns, definition.LineItemNameColumn)
        AddUniqueColumn(groupingColumns, definition.SortOrderColumn)
        AddUniqueColumn(groupingColumns, definition.IsMainColumn)
        AddUniqueColumn(groupingColumns, definition.IsTitleColumn)
        AddUniqueColumn(groupingColumns, definition.YearColumn)
        AddUniqueColumn(groupingColumns, definition.PeriodIdColumn)
        AddUniqueColumn(groupingColumns, definition.QuarterColumn)
        AddUniqueColumn(groupingColumns, definition.ColumnSortOrderColumn)
        AddUniqueColumn(groupingColumns, definition.ColumnNameColumn)

        groupingColumns = groupingColumns.Where(Function(columnName) rawData.Columns.Contains(columnName)).ToList()

        Dim aggregatedData As New DataTable(rawData.TableName) With {
            .Locale = rawData.Locale,
            .CaseSensitive = False
        }
        For Each columnName As String In groupingColumns
            aggregatedData.Columns.Add(columnName, rawData.Columns(columnName).DataType)
        Next
        aggregatedData.Columns.Add(definition.ValueColumn, GetType(Decimal))

        Dim rowLookup As New Dictionary(Of String, DataRow)(StringComparer.Ordinal)
        For Each sourceRow As DataRow In rawData.Rows
            Dim keyBuilder As New StringBuilder()
            For Each columnName As String In groupingColumns
                Dim keyPart As String = GetStableKeyPart(sourceRow, columnName)
                keyBuilder.Append(keyPart.Length.ToString(CultureInfo.InvariantCulture)).Append(":"c)
                keyBuilder.Append(keyPart).Append("|"c)
            Next

            Dim rowKey As String = keyBuilder.ToString()
            Dim targetRow As DataRow = Nothing
            If Not rowLookup.TryGetValue(rowKey, targetRow) Then
                targetRow = aggregatedData.NewRow()
                For Each columnName As String In groupingColumns
                    targetRow(columnName) = sourceRow(columnName)
                Next
                targetRow(definition.ValueColumn) = 0D
                aggregatedData.Rows.Add(targetRow)
                rowLookup.Add(rowKey, targetRow)
            End If

            Dim amount As Decimal
            If TryGetDecimalValue(sourceRow, definition.ValueColumn, amount) Then
                targetRow(definition.ValueColumn) = Convert.ToDecimal(targetRow(definition.ValueColumn), CultureInfo.InvariantCulture) + amount
            End If
        Next

        Return aggregatedData
    End Function

    Private Function BuildAdvancedProcedureParameters() As List(Of QueryParameterValue)
        Dim reportId As Long
        If Not Long.TryParse(reportParameterDropDown.SelectedValue, NumberStyles.Integer,
                             CultureInfo.InvariantCulture, reportId) Then
            Throw New InvalidOperationException("Select a report before rendering the advanced ACoA report.")
        End If
        ReportAccessService.DemandReportAccess(Context, reportId)

        Dim entityGroupId As Long
        If Not Long.TryParse(entityGroupDropDown.SelectedValue, NumberStyles.Integer,
                             CultureInfo.InvariantCulture, entityGroupId) Then
            Throw New InvalidOperationException("Select an entity group before rendering the advanced ACoA report.")
        End If

        Dim statusValue As Integer = 1
        Integer.TryParse(statusDropDown.SelectedValue, NumberStyles.Integer,
                         CultureInfo.InvariantCulture, statusValue)

        Dim years As String = BuildInclusiveRangeCsv(yearFromDropDown, yearToDropDown)
        Dim quarters As String = BuildInclusiveRangeCsv(quarterFromDropDown, quarterToDropDown)
        Dim entities As String = BuildSelectedValuesCsv(entitiesListBox, False)
        Dim reportColumns As String = BuildSelectedValuesCsv(reportColumnsListBox, True)

        If String.IsNullOrWhiteSpace(years) OrElse String.IsNullOrWhiteSpace(quarters) Then
            Throw New InvalidOperationException("The stored procedure requires at least one year and quarter.")
        End If
        If String.IsNullOrWhiteSpace(reportColumns) Then
            Throw New InvalidOperationException("The selected report does not expose any report columns.")
        End If

        Return New List(Of QueryParameterValue) From {
            New QueryParameterValue With {.Name = "@Report_id", .Value = reportId},
            New QueryParameterValue With {.Name = "@quarter", .Value = quarters},
            New QueryParameterValue With {.Name = "@year", .Value = years},
            New QueryParameterValue With {.Name = "@entity_type", .Value = entityGroupId.ToString(CultureInfo.InvariantCulture)},
            New QueryParameterValue With {.Name = "@entities", .Value = entities},
            New QueryParameterValue With {.Name = "@status", .Value = statusValue},
            New QueryParameterValue With {.Name = "@ReportColumnID", .Value = reportColumns}
        }
    End Function

    Private Shared Function BuildInclusiveRangeCsv(ByVal fromControl As DropDownList,
                                                   ByVal toControl As DropDownList) As String
        Dim fromValue As String = fromControl.SelectedValue
        Dim toValue As String = toControl.SelectedValue
        NormalizeNumericRange(fromValue, toValue)

        Dim fromNumber As Decimal
        Dim toNumber As Decimal
        Dim hasFrom As Boolean = Decimal.TryParse(fromValue, NumberStyles.Number,
                                                  CultureInfo.InvariantCulture, fromNumber)
        Dim hasTo As Boolean = Decimal.TryParse(toValue, NumberStyles.Number,
                                                CultureInfo.InvariantCulture, toNumber)
        Dim values As New List(Of String)()

        For Each item As ListItem In fromControl.Items
            Dim itemNumber As Decimal
            If Not String.IsNullOrWhiteSpace(item.Value) AndAlso
               Decimal.TryParse(item.Value, NumberStyles.Number, CultureInfo.InvariantCulture, itemNumber) AndAlso
               (Not hasFrom OrElse itemNumber >= fromNumber) AndAlso
               (Not hasTo OrElse itemNumber <= toNumber) Then
                values.Add(item.Value)
            End If
        Next

        Return String.Join(",", values)
    End Function

    Private Shared Function BuildSelectedValuesCsv(ByVal control As ListControl,
                                                   ByVal useAllWhenNoneSelected As Boolean) As String
        Dim selectedValues As List(Of String) = control.Items.Cast(Of ListItem)().
            Where(Function(item) item.Selected AndAlso Not String.IsNullOrWhiteSpace(item.Value)).
            Select(Function(item) item.Value).ToList()

        If selectedValues.Count = 0 AndAlso useAllWhenNoneSelected Then
            selectedValues = control.Items.Cast(Of ListItem)().
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Value)).
                Select(Function(item) item.Value).ToList()
        End If

        Return String.Join(",", selectedValues)
    End Function

    Private Function ExecuteViewQuery(ByVal selectedView As DatabaseViewInfo,
                                      ByVal commandText As String,
                                      Optional ByVal queryParameters As IList(Of QueryParameterValue) = Nothing,
                                      Optional ByVal commandType As CommandType = CommandType.Text) As DataTable
        Dim contextConnection As DbConnection = _db.Database.Connection
        Dim commandConnection As DbConnection = contextConnection
        Dim entityConnection As EntityConnection = TryCast(contextConnection, EntityConnection)

        If entityConnection IsNot Nothing Then
            commandConnection = entityConnection.StoreConnection
        End If

        Dim closeWhenFinished As Boolean = contextConnection.State <> ConnectionState.Open
        Dim data As New DataTable(selectedView.ViewName) With {
            .Locale = CultureInfo.CurrentCulture,
            .CaseSensitive = False
        }

        Try
            If closeWhenFinished Then
                contextConnection.Open()
            End If

            Using command As DbCommand = commandConnection.CreateCommand()
                command.CommandText = commandText
                command.CommandType = commandType
                command.CommandTimeout = GetConfiguredCommandTimeout()

                If queryParameters IsNot Nothing Then
                    For Each queryParameter As QueryParameterValue In queryParameters
                        Dim parameter As DbParameter = command.CreateParameter()
                        parameter.ParameterName = queryParameter.Name
                        parameter.Value = If(queryParameter.Value, DBNull.Value)
                        command.Parameters.Add(parameter)
                    Next
                End If

                Using reader As DbDataReader = command.ExecuteReader(CommandBehavior.SequentialAccess)
                    data.Load(reader)
                End Using
            End Using
        Finally
            If closeWhenFinished AndAlso contextConnection.State <> ConnectionState.Closed Then
                contextConnection.Close()
            End If
        End Try

        Return data
    End Function

    Private Shared Sub TrimResult(ByVal data As DataTable, ByVal resultLimit As Integer, ByRef wasTruncated As Boolean)
        wasTruncated = data.Rows.Count > resultLimit
        If wasTruncated Then
            While data.Rows.Count > resultLimit
                data.Rows.RemoveAt(data.Rows.Count - 1)
            End While
        End If
    End Sub

    Private Shared Function CreateFilterDefinition(ByVal schema As DataTable) As FilterDefinition
        Return New FilterDefinition With {
            .ReportIdColumn = FindColumn(schema, "ReportID", "Report_ID"),
            .ReportNameColumn = FindColumn(schema, "Report_Name", "ReportName"),
            .YearColumn = FindColumn(schema, "Year", "ReportYear"),
            .QuarterIdColumn = FindColumn(schema, "QuarterID", "Quarter_ID"),
            .QuarterColumn = FindColumn(schema, "Quarter", "ReportQuarter"),
            .EntityGroupIdColumn = FindColumn(schema, "Entity_GroupID", "EntityGroupID"),
            .EntityGroupNameColumn = FindColumn(schema, "EntityGroupName", "Entity_Group_Name"),
            .EntityNameColumn = FindColumn(schema, "Entity_Name", "Entity Name", "EntityName")
        }
    End Function

    Private Sub PopulateParameterControls(ByVal selectedView As DatabaseViewInfo,
                                          ByVal schema As DataTable,
                                          ByVal definition As FilterDefinition)
        Dim viewChanged As Boolean = Not selectedView.Key.Equals(
            Convert.ToString(ViewState("ParameterViewKey"), CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase)

        If IsPostBack AndAlso Not viewChanged AndAlso reportParameterDropDown.Items.Count > 0 Then
            Return
        End If

        ResetParameterControls(definition)

        Dim reportValueColumn As String = If(Not String.IsNullOrWhiteSpace(definition.ReportIdColumn),
                                             definition.ReportIdColumn,
                                             definition.ReportNameColumn)
        If Not String.IsNullOrWhiteSpace(reportValueColumn) Then
            Dim reportOptions As DataTable = LoadDistinctOptions(selectedView, schema, reportValueColumn,
                                                                 definition.ReportNameColumn, 500)
            AddDropDownOptions(reportParameterDropDown, reportOptions)
        End If

        If Not String.IsNullOrWhiteSpace(definition.YearColumn) Then
            Dim yearOptions As DataTable = LoadDistinctOptions(selectedView, schema, definition.YearColumn,
                                                               definition.YearColumn, 250)
            AddDropDownOptions(yearFromDropDown, yearOptions)
            AddDropDownOptions(yearToDropDown, yearOptions)
        End If

        Dim quarterValueColumn As String = If(Not String.IsNullOrWhiteSpace(definition.QuarterIdColumn),
                                              definition.QuarterIdColumn,
                                              definition.QuarterColumn)
        If Not String.IsNullOrWhiteSpace(quarterValueColumn) Then
            Dim quarterOptions As DataTable = LoadDistinctOptions(selectedView, schema, quarterValueColumn,
                                                                  definition.QuarterColumn, 20)
            AddQuarterOptions(quarterFromDropDown, quarterToDropDown, quarterOptions,
                              Not String.IsNullOrWhiteSpace(definition.QuarterIdColumn))
        End If

        Dim groupValueColumn As String = If(Not String.IsNullOrWhiteSpace(definition.EntityGroupIdColumn),
                                            definition.EntityGroupIdColumn,
                                            definition.EntityGroupNameColumn)
        If Not String.IsNullOrWhiteSpace(groupValueColumn) Then
            Dim groupOptions As DataTable = LoadDistinctOptions(selectedView, schema, groupValueColumn,
                                                                definition.EntityGroupNameColumn, 500)
            AddDropDownOptions(entityGroupDropDown, groupOptions)
        End If

        If Not String.IsNullOrWhiteSpace(definition.EntityNameColumn) Then
            Dim entityOptions As DataTable = LoadDistinctOptions(selectedView, schema, definition.EntityNameColumn,
                                                                 definition.EntityNameColumn, 1000)
            For Each optionRow As DataRow In entityOptions.Rows
                Dim value As String = Convert.ToString(optionRow("OptionValue"), CultureInfo.CurrentCulture)
                Dim label As String = Convert.ToString(optionRow("OptionLabel"), CultureInfo.CurrentCulture)
                If Not String.IsNullOrWhiteSpace(value) Then
                    entitiesListBox.Items.Add(New ListItem(label, value))
                End If
            Next
        End If

        ApplyInitialParameterValues()
        ViewState("ParameterViewKey") = selectedView.Key
    End Sub

    Private Function FilterReportOptionsForCurrentUser(ByVal reportOptions As DataTable) As DataTable
        If reportOptions Is Nothing Then
            Return New DataTable()
        End If

        Dim filtered As DataTable = reportOptions.Clone()
        Dim reportIdColumn As String = FindColumn(reportOptions, "ReportID", "Report_ID")
        If String.IsNullOrWhiteSpace(reportIdColumn) Then
            Return filtered
        End If

        Dim reportIds As New List(Of Long)()
        For Each row As DataRow In reportOptions.Rows
            Dim reportId As Long
            If Long.TryParse(Convert.ToString(row(reportIdColumn), CultureInfo.InvariantCulture),
                             NumberStyles.Integer, CultureInfo.InvariantCulture, reportId) Then
                reportIds.Add(reportId)
            End If
        Next

        Dim accessibleReportIds As HashSet(Of Long) =
            ReportAccessService.GetAccessibleReportIds(Context, reportIds)
        For Each row As DataRow In reportOptions.Rows
            Dim reportId As Long
            If Long.TryParse(Convert.ToString(row(reportIdColumn), CultureInfo.InvariantCulture),
                             NumberStyles.Integer, CultureInfo.InvariantCulture, reportId) AndAlso
               accessibleReportIds.Contains(reportId) Then
                filtered.ImportRow(row)
            End If
        Next
        Return filtered
    End Function

    Private Sub PopulateProcedureParameterControls(ByVal selectedView As DatabaseViewInfo)
        Dim selectedGroup As String = entityGroupDropDown.SelectedValue
        Dim selectedReport As String = reportParameterDropDown.SelectedValue
        Dim selectedYearFrom As String = yearFromDropDown.SelectedValue
        Dim selectedYearTo As String = yearToDropDown.SelectedValue
        Dim selectedQuarterFrom As String = quarterFromDropDown.SelectedValue
        Dim selectedQuarterTo As String = quarterToDropDown.SelectedValue
        Dim selectedStatus As String = statusDropDown.SelectedValue

        If Not IsPostBack Then
            selectedGroup = FirstQueryStringValue("entity_type", "EntityGroup")
            selectedReport = FirstQueryStringValue("Report_id", "report_id")
            selectedYearFrom = FirstQueryStringValue("year_from", "year")
            selectedYearTo = FirstQueryStringValue("year_to", "year")
            selectedQuarterFrom = FirstQueryStringValue("quarter_from", "quarter")
            selectedQuarterTo = FirstQueryStringValue("quarter_to", "quarter")
            selectedStatus = FirstQueryStringValue("status")
        End If

        Dim groupOptions As DataTable = ExecuteViewQuery(selectedView, EntityGroupsProcedure, Nothing,
                                                         CommandType.StoredProcedure)
        entityGroupDropDown.Items.Clear()
        entityGroupDropDown.Items.Add(New ListItem("Select an entity group", String.Empty))
        AddProcedureDropDownOptions(entityGroupDropDown, groupOptions,
                                    New String() {"EntityGroupID"},
                                    New String() {"EntityGroupName"})
        SelectDropDownValue(entityGroupDropDown, selectedGroup)
        EnsureFirstNonEmptySelection(entityGroupDropDown, "Select an entity group")
        entityGroupDropDown.Enabled = entityGroupDropDown.Items.Count > 1

        Dim reportOptions As New DataTable()
        Dim entityGroupId As Long
        If Long.TryParse(entityGroupDropDown.SelectedValue, NumberStyles.Integer,
                         CultureInfo.InvariantCulture, entityGroupId) Then
            reportOptions = ExecuteViewQuery(
                selectedView,
                ReportsProcedure,
                New List(Of QueryParameterValue) From {
                    New QueryParameterValue With {.Name = "@EntityGroup", .Value = entityGroupId}
                },
                CommandType.StoredProcedure)
        End If
        reportOptions = FilterReportOptionsForCurrentUser(reportOptions)
        reportParameterDropDown.Items.Clear()
        reportParameterDropDown.Items.Add(New ListItem("Select a report", String.Empty))
        AddProcedureDropDownOptions(reportParameterDropDown, reportOptions,
                                    New String() {"ReportID", "Report_ID"},
                                    New String() {"Report_Name", "ReportName", "Report_Title"})
        SelectDropDownValue(reportParameterDropDown, selectedReport)
        EnsureFirstNonEmptySelection(reportParameterDropDown, "Select a report")
        reportParameterDropDown.Enabled = reportParameterDropDown.Items.Count > 1

        Dim yearOptions As DataTable = ExecuteViewQuery(selectedView, YearsProcedure, Nothing,
                                                        CommandType.StoredProcedure)
        yearFromDropDown.Items.Clear()
        yearFromDropDown.Items.Add(New ListItem("Earliest", String.Empty))
        yearToDropDown.Items.Clear()
        yearToDropDown.Items.Add(New ListItem("Latest", String.Empty))
        AddProcedureDropDownOptions(yearFromDropDown, yearOptions,
                                    New String() {"Year"}, New String() {"Year"})
        AddProcedureDropDownOptions(yearToDropDown, yearOptions,
                                    New String() {"Year"}, New String() {"Year"})
        If Not IsPostBack AndAlso
           String.IsNullOrWhiteSpace(selectedYearFrom) AndAlso
           String.IsNullOrWhiteSpace(selectedYearTo) Then
            Dim latestYear As String = FindHighestNumericListValue(yearFromDropDown)
            selectedYearFrom = latestYear
            selectedYearTo = latestYear
        End If
        SelectDropDownValue(yearFromDropDown, selectedYearFrom)
        SelectDropDownValue(yearToDropDown, selectedYearTo)
        yearFromDropDown.Enabled = yearFromDropDown.Items.Count > 1
        yearToDropDown.Enabled = yearFromDropDown.Enabled

        Dim quarterOptions As DataTable = ExecuteViewQuery(selectedView, QuartersProcedure, Nothing,
                                                           CommandType.StoredProcedure)
        quarterFromDropDown.Items.Clear()
        quarterFromDropDown.Items.Add(New ListItem("First quarter", String.Empty))
        quarterToDropDown.Items.Clear()
        quarterToDropDown.Items.Add(New ListItem("Last quarter", String.Empty))
        AddProcedureDropDownOptions(quarterFromDropDown, quarterOptions,
                                    New String() {"QuarterID", "PeriodID"},
                                    New String() {"Quarter"})
        AddProcedureDropDownOptions(quarterToDropDown, quarterOptions,
                                    New String() {"QuarterID", "PeriodID"},
                                    New String() {"Quarter"})
        SelectDropDownValue(quarterFromDropDown, selectedQuarterFrom)
        SelectDropDownValue(quarterToDropDown, selectedQuarterTo)
        quarterFromDropDown.Enabled = quarterFromDropDown.Items.Count > 1
        quarterToDropDown.Enabled = quarterFromDropDown.Enabled

        statusDropDown.Items.Clear()
        statusDropDown.Items.Add(New ListItem("Approved only", "1"))
        statusDropDown.Items.Add(New ListItem("Approved and pending approval", "3"))
        SelectDropDownValue(statusDropDown, selectedStatus)

        PopulateReportColumnOptions(selectedView)
        PopulateProcedureEntityOptions(selectedView)
    End Sub

    Private Function FirstQueryStringValue(ParamArray names() As String) As String
        For Each name As String In names
            Dim value As String = Convert.ToString(Request.QueryString(name), CultureInfo.InvariantCulture)
            If Not String.IsNullOrWhiteSpace(value) Then
                Return value
            End If
        Next
        Return String.Empty
    End Function

    Private Shared Sub AddProcedureDropDownOptions(ByVal control As DropDownList,
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

    Private Shared Sub EnsureFirstNonEmptySelection(ByVal control As DropDownList, ByVal emptyLabel As String)
        If control.Items.Count > 0 Then
            control.Items(0).Text = emptyLabel
        End If
        If Not String.IsNullOrWhiteSpace(control.SelectedValue) Then
            Return
        End If

        For index As Integer = 0 To control.Items.Count - 1
            If Not String.IsNullOrWhiteSpace(control.Items(index).Value) Then
                control.SelectedIndex = index
                Return
            End If
        Next
    End Sub

    Private Shared Function FindHighestNumericListValue(ByVal control As ListControl) As String
        Dim highestNumber As Decimal
        Dim highestValue As String = String.Empty
        Dim hasValue As Boolean = False

        For Each item As ListItem In control.Items
            Dim itemNumber As Decimal
            If Decimal.TryParse(item.Value, NumberStyles.Number, CultureInfo.InvariantCulture, itemNumber) AndAlso
               (Not hasValue OrElse itemNumber > highestNumber) Then
                highestNumber = itemNumber
                highestValue = item.Value
                hasValue = True
            End If
        Next

        Return highestValue
    End Function

    Private Sub PopulateStatusOptions()
        If statusDropDown.Items.Count = 0 Then
            statusDropDown.Items.Add(New ListItem("Approved only", "1"))
            statusDropDown.Items.Add(New ListItem("Approved and pending approval", "3"))
        End If

        If Not IsPostBack Then
            SelectDropDownValue(statusDropDown, Request.QueryString("status"))
        End If
    End Sub

    Private Sub PopulateReportColumnOptions(ByVal selectedView As DatabaseViewInfo)
        Dim reportId As Long
        If Not Long.TryParse(reportParameterDropDown.SelectedValue, NumberStyles.Integer,
                             CultureInfo.InvariantCulture, reportId) Then
            reportColumnsListBox.Items.Clear()
            reportColumnsListBox.Enabled = False
            ViewState("ProcedureReportColumnKey") = Nothing
            Return
        End If

        Dim contextKey As String = selectedView.Key & "|" & reportId.ToString(CultureInfo.InvariantCulture)
        Dim previousContextKey As String = Convert.ToString(ViewState("ProcedureReportColumnKey"), CultureInfo.InvariantCulture)
        If contextKey.Equals(previousContextKey, StringComparison.Ordinal) AndAlso reportColumnsListBox.Items.Count > 0 Then
            Return
        End If

        Dim options As DataTable = ExecuteViewQuery(
            selectedView,
            ReportColumnsProcedure,
            New List(Of QueryParameterValue) From {
                New QueryParameterValue With {.Name = "@ReportID", .Value = reportId}
            },
            CommandType.StoredProcedure)

        reportColumnsListBox.Items.Clear()
        Dim valueColumn As String = FindColumn(options, "Report_ColumnID", "ReportColumnID")
        Dim labelColumn As String = FindColumn(options, "ColumnName", "Column_Name")
        If Not String.IsNullOrWhiteSpace(valueColumn) Then
            For Each optionRow As DataRow In options.Rows
                Dim value As String = GetTextValue(optionRow, valueColumn)
                Dim label As String = GetTextValue(optionRow, labelColumn)
                If Not String.IsNullOrWhiteSpace(value) AndAlso reportColumnsListBox.Items.FindByValue(value) Is Nothing Then
                    reportColumnsListBox.Items.Add(New ListItem(If(String.IsNullOrWhiteSpace(label), value, label), value))
                End If
            Next
        End If

        reportColumnsListBox.Enabled = reportColumnsListBox.Items.Count > 0
        If Not IsPostBack Then
            Dim requestedColumns As String = Convert.ToString(Request.QueryString("ReportColumnID"), CultureInfo.InvariantCulture)
            If String.IsNullOrWhiteSpace(requestedColumns) Then
                requestedColumns = Convert.ToString(Request.QueryString("ReportColumns"), CultureInfo.InvariantCulture)
            End If
            ApplyCsvSelection(reportColumnsListBox, requestedColumns)
        End If
        ViewState("ProcedureReportColumnKey") = contextKey
    End Sub

    Private Sub PopulateProcedureEntityOptions(ByVal selectedView As DatabaseViewInfo)
        Dim entityGroupId As Integer
        Dim years As String = BuildInclusiveRangeCsv(yearFromDropDown, yearToDropDown)
        Dim quarters As String = BuildInclusiveRangeCsv(quarterFromDropDown, quarterToDropDown)
        If Not Integer.TryParse(entityGroupDropDown.SelectedValue, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, entityGroupId) OrElse
           String.IsNullOrWhiteSpace(years) OrElse String.IsNullOrWhiteSpace(quarters) Then
            entitiesListBox.Items.Clear()
            entitiesListBox.Enabled = False
            ViewState("ProcedureEntityContextKey") = Nothing
            Return
        End If

        Dim contextKey As String = selectedView.Key & "|" & entityGroupId.ToString(CultureInfo.InvariantCulture) &
                                   "|" & years & "|" & quarters
        Dim previousContextKey As String = Convert.ToString(ViewState("ProcedureEntityContextKey"), CultureInfo.InvariantCulture)
        If contextKey.Equals(previousContextKey, StringComparison.Ordinal) AndAlso entitiesListBox.Items.Count > 0 Then
            Return
        End If

        Dim options As DataTable = ExecuteViewQuery(
            selectedView,
            ReportEntitiesProcedure,
            New List(Of QueryParameterValue) From {
                New QueryParameterValue With {.Name = "@group", .Value = entityGroupId},
                New QueryParameterValue With {.Name = "@year", .Value = years},
                New QueryParameterValue With {.Name = "@q", .Value = quarters}
            },
            CommandType.StoredProcedure)

        entitiesListBox.Items.Clear()
        Dim valueColumn As String = FindColumn(options, "entity_instance_id", "EntityInstanceID")
        Dim nameColumn As String = FindColumn(options, "Company Name", "Company_Name", "Entity_Name", "EntityName")
        Dim licenseColumn As String = FindColumn(options, "License Number", "License_Number")
        If Not String.IsNullOrWhiteSpace(valueColumn) Then
            For Each optionRow As DataRow In options.Rows
                Dim value As String = GetTextValue(optionRow, valueColumn)
                Dim entityName As String = GetTextValue(optionRow, nameColumn)
                Dim label As String = entityName
                Dim licenseNumber As String = GetTextValue(optionRow, licenseColumn)
                If Not String.IsNullOrWhiteSpace(licenseNumber) Then
                    label = If(String.IsNullOrWhiteSpace(label), licenseNumber, label & " (" & licenseNumber & ")")
                End If
                If Not String.IsNullOrWhiteSpace(value) AndAlso entitiesListBox.Items.FindByValue(value) Is Nothing Then
                    Dim entityItem As New ListItem(If(String.IsNullOrWhiteSpace(label), value, label), value)
                    entityItem.Attributes("data-entity-name") = entityName
                    entitiesListBox.Items.Add(entityItem)
                End If
            Next
        End If

        entitiesListBox.Enabled = entitiesListBox.Items.Count > 0
        If Not IsPostBack Then
            ApplyCsvSelection(entitiesListBox, Request.QueryString("entities"))
        End If
        ViewState("ProcedureEntityContextKey") = contextKey
    End Sub

    Private Shared Sub ApplyCsvSelection(ByVal control As ListControl, ByVal csvValues As String)
        If String.IsNullOrWhiteSpace(csvValues) Then
            Return
        End If

        For Each requestedValue As String In csvValues.Split(","c)
            Dim matchingItem As ListItem = control.Items.FindByValue(requestedValue.Trim())
            If matchingItem IsNot Nothing Then
                matchingItem.Selected = True
            End If
        Next
    End Sub

    Private Sub ResetParameterControls(ByVal definition As FilterDefinition)
        reportParameterDropDown.Items.Clear()
        reportParameterDropDown.Items.Add(New ListItem("All reports", String.Empty))
        reportParameterDropDown.Enabled = Not String.IsNullOrWhiteSpace(definition.ReportIdColumn) OrElse
                                              Not String.IsNullOrWhiteSpace(definition.ReportNameColumn)

        entityGroupDropDown.Items.Clear()
        entityGroupDropDown.Items.Add(New ListItem("All entity groups", String.Empty))
        entityGroupDropDown.Enabled = Not String.IsNullOrWhiteSpace(definition.EntityGroupIdColumn) OrElse
                                      Not String.IsNullOrWhiteSpace(definition.EntityGroupNameColumn)

        yearFromDropDown.Items.Clear()
        yearFromDropDown.Items.Add(New ListItem("Earliest", String.Empty))
        yearToDropDown.Items.Clear()
        yearToDropDown.Items.Add(New ListItem("Latest", String.Empty))
        yearFromDropDown.Enabled = Not String.IsNullOrWhiteSpace(definition.YearColumn)
        yearToDropDown.Enabled = yearFromDropDown.Enabled

        quarterFromDropDown.Items.Clear()
        quarterFromDropDown.Items.Add(New ListItem("First quarter", String.Empty))
        quarterToDropDown.Items.Clear()
        quarterToDropDown.Items.Add(New ListItem("Last quarter", String.Empty))
        quarterFromDropDown.Enabled = Not String.IsNullOrWhiteSpace(definition.QuarterIdColumn) OrElse
                                      Not String.IsNullOrWhiteSpace(definition.QuarterColumn)
        quarterToDropDown.Enabled = quarterFromDropDown.Enabled

        entitiesListBox.Items.Clear()
        entitiesListBox.Enabled = Not String.IsNullOrWhiteSpace(definition.EntityNameColumn)
    End Sub

    Private Function LoadDistinctOptions(ByVal selectedView As DatabaseViewInfo,
                                         ByVal schema As DataTable,
                                         ByVal valueColumn As String,
                                         ByVal labelColumn As String,
                                         ByVal optionLimit As Integer) As DataTable
        If String.IsNullOrWhiteSpace(labelColumn) Then
            labelColumn = valueColumn
        End If

        Dim valueExpression As String = GetGroupExpression(schema, valueColumn)
        Dim labelExpression As String = "CONVERT(nvarchar(4000), " & QuoteIdentifier(labelColumn) & ")"
        Dim sql As New StringBuilder()
        sql.Append("SELECT TOP (").Append(optionLimit.ToString(CultureInfo.InvariantCulture)).Append(") ")
        sql.Append(valueExpression).Append(" AS [OptionValue], MAX(").Append(labelExpression).Append(") AS [OptionLabel]")
        sql.Append(" FROM ").Append(QuoteIdentifier(selectedView.SchemaName)).Append(".").Append(QuoteIdentifier(selectedView.ViewName))
        sql.Append(" WHERE ").Append(QuoteIdentifier(valueColumn)).Append(" IS NOT NULL")
        sql.Append(" GROUP BY ").Append(valueExpression).Append(" ORDER BY ").Append(valueExpression)
        Return ExecuteViewQuery(selectedView, sql.ToString())
    End Function

    Private Shared Sub AddDropDownOptions(ByVal control As DropDownList, ByVal options As DataTable)
        For Each optionRow As DataRow In options.Rows
            Dim value As String = Convert.ToString(optionRow("OptionValue"), CultureInfo.CurrentCulture)
            Dim label As String = Convert.ToString(optionRow("OptionLabel"), CultureInfo.CurrentCulture)
            If Not String.IsNullOrWhiteSpace(value) AndAlso control.Items.FindByValue(value) Is Nothing Then
                control.Items.Add(New ListItem(If(String.IsNullOrWhiteSpace(label), value, label), value))
            End If
        Next
    End Sub

    Private Shared Sub AddQuarterOptions(ByVal fromControl As DropDownList,
                                         ByVal toControl As DropDownList,
                                         ByVal options As DataTable,
                                         ByVal valuesAreQuarterIds As Boolean)
        Dim quarterLabels As New SortedDictionary(Of Integer, String)()
        For Each optionRow As DataRow In options.Rows
            Dim rawValue As String = Convert.ToString(optionRow("OptionValue"), CultureInfo.CurrentCulture)
            Dim label As String = Convert.ToString(optionRow("OptionLabel"), CultureInfo.CurrentCulture)
            Dim quarterNumber As Integer

            If valuesAreQuarterIds Then
                Integer.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, quarterNumber)
            Else
                quarterNumber = ExtractQuarterNumber(If(String.IsNullOrWhiteSpace(label), rawValue, label))
            End If

            If quarterNumber >= 1 AndAlso quarterNumber <= 4 AndAlso Not quarterLabels.ContainsKey(quarterNumber) Then
                quarterLabels.Add(quarterNumber, If(String.IsNullOrWhiteSpace(label), "Quarter " & quarterNumber, label))
            End If
        Next

        For Each quarterOption As KeyValuePair(Of Integer, String) In quarterLabels
            Dim value As String = quarterOption.Key.ToString(CultureInfo.InvariantCulture)
            fromControl.Items.Add(New ListItem(quarterOption.Value, value))
            toControl.Items.Add(New ListItem(quarterOption.Value, value))
        Next
    End Sub

    Private Shared Function ExtractQuarterNumber(ByVal quarterLabel As String) As Integer
        Dim numberMatch As System.Text.RegularExpressions.Match =
            System.Text.RegularExpressions.Regex.Match(Convert.ToString(quarterLabel), "[1-4]")
        If numberMatch.Success Then
            Return Convert.ToInt32(numberMatch.Value, CultureInfo.InvariantCulture)
        End If

        Return 0
    End Function

    Private Sub ApplyInitialParameterValues()
        If IsPostBack Then
            Return
        End If

        SelectDropDownValue(reportParameterDropDown, Request.QueryString("Report_id"))
        SelectDropDownValue(yearFromDropDown, Request.QueryString("year_from"))
        SelectDropDownValue(yearToDropDown, Request.QueryString("year_to"))
        SelectDropDownValue(quarterFromDropDown, Request.QueryString("quarter_from"))
        SelectDropDownValue(quarterToDropDown, Request.QueryString("quarter_to"))
        Dim requestedEntityGroup As String = Convert.ToString(Request.QueryString("EntityGroup"), CultureInfo.InvariantCulture)
        If String.IsNullOrWhiteSpace(requestedEntityGroup) Then
            requestedEntityGroup = Convert.ToString(Request.QueryString("entity_type"), CultureInfo.InvariantCulture)
        End If
        SelectDropDownValue(entityGroupDropDown, requestedEntityGroup)

        Dim requestedEntities As String = Convert.ToString(Request.QueryString("entities"), CultureInfo.InvariantCulture)
        If Not String.IsNullOrWhiteSpace(requestedEntities) Then
            For Each entityValue As String In requestedEntities.Split(","c)
                Dim matchingItem As ListItem = entitiesListBox.Items.FindByValue(entityValue.Trim())
                If matchingItem IsNot Nothing Then
                    matchingItem.Selected = True
                End If
            Next
        End If
    End Sub

    Private Shared Sub SelectDropDownValue(ByVal control As DropDownList, ByVal requestedValue As String)
        If String.IsNullOrWhiteSpace(requestedValue) Then
            Return
        End If

        Dim matchingItem As ListItem = control.Items.FindByValue(requestedValue)
        If matchingItem IsNot Nothing Then
            control.ClearSelection()
            matchingItem.Selected = True
        End If
    End Sub

    Private Function BuildReportFilter(ByVal schema As DataTable, ByVal definition As FilterDefinition) As ReportFilter
        Dim filter As New ReportFilter()

        Dim reportColumn As String = If(Not String.IsNullOrWhiteSpace(definition.ReportIdColumn),
                                        definition.ReportIdColumn,
                                        definition.ReportNameColumn)
        AddEqualityFilter(filter, schema, reportColumn, reportParameterDropDown.SelectedValue, "@reportId")

        AddRangeFilter(filter, schema, definition.YearColumn, yearFromDropDown.SelectedValue,
                       yearToDropDown.SelectedValue, "@yearFrom", "@yearTo")

        Dim quarterExpression As String = BuildQuarterFilterExpression(schema, definition)
        AddIntegerRangeFilter(filter, quarterExpression, quarterFromDropDown.SelectedValue,
                              quarterToDropDown.SelectedValue, "@quarterFrom", "@quarterTo")

        Dim entityGroupColumn As String = If(Not String.IsNullOrWhiteSpace(definition.EntityGroupIdColumn),
                                             definition.EntityGroupIdColumn,
                                             definition.EntityGroupNameColumn)
        AddEqualityFilter(filter, schema, entityGroupColumn, entityGroupDropDown.SelectedValue, "@entityGroup")

        If Not String.IsNullOrWhiteSpace(definition.EntityNameColumn) Then
            Dim entityParameters As New List(Of String)()
            Dim entityIndex As Integer = 0
            For Each entityItem As ListItem In entitiesListBox.Items
                If entityItem.Selected AndAlso entityIndex < 1000 Then
                    Dim parameterName As String = "@entity" & entityIndex.ToString(CultureInfo.InvariantCulture)
                    Dim entityFilterValue As String = entityItem.Value
                    If Not String.IsNullOrWhiteSpace(entityItem.Attributes("data-entity-name")) Then
                        entityFilterValue = entityItem.Attributes("data-entity-name")
                    End If
                    entityParameters.Add(parameterName)
                    filter.Parameters.Add(New QueryParameterValue With {
                        .Name = parameterName,
                        .Value = ConvertParameterValue(schema, definition.EntityNameColumn, entityFilterValue)
                    })
                    entityIndex += 1
                End If
            Next

            If entityParameters.Count > 0 Then
                filter.Conditions.Add(GetGroupExpression(schema, definition.EntityNameColumn) & " IN (" &
                                      String.Join(", ", entityParameters) & ")")
            End If
        End If

        Return filter
    End Function

    Private Shared Sub AddEqualityFilter(ByVal filter As ReportFilter,
                                         ByVal schema As DataTable,
                                         ByVal columnName As String,
                                         ByVal selectedValue As String,
                                         ByVal parameterName As String)
        If String.IsNullOrWhiteSpace(columnName) OrElse String.IsNullOrWhiteSpace(selectedValue) Then
            Return
        End If

        filter.Conditions.Add(GetGroupExpression(schema, columnName) & " = " & parameterName)
        filter.Parameters.Add(New QueryParameterValue With {
            .Name = parameterName,
            .Value = ConvertParameterValue(schema, columnName, selectedValue)
        })
    End Sub

    Private Shared Sub AddRangeFilter(ByVal filter As ReportFilter,
                                      ByVal schema As DataTable,
                                      ByVal columnName As String,
                                      ByVal fromValue As String,
                                      ByVal toValue As String,
                                      ByVal fromParameterName As String,
                                      ByVal toParameterName As String)
        If String.IsNullOrWhiteSpace(columnName) Then
            Return
        End If

        Dim normalizedFrom As String = fromValue
        Dim normalizedTo As String = toValue
        NormalizeNumericRange(normalizedFrom, normalizedTo)

        If Not String.IsNullOrWhiteSpace(normalizedFrom) Then
            filter.Conditions.Add(QuoteIdentifier(columnName) & " >= " & fromParameterName)
            filter.Parameters.Add(New QueryParameterValue With {
                .Name = fromParameterName,
                .Value = ConvertParameterValue(schema, columnName, normalizedFrom)
            })
        End If

        If Not String.IsNullOrWhiteSpace(normalizedTo) Then
            filter.Conditions.Add(QuoteIdentifier(columnName) & " <= " & toParameterName)
            filter.Parameters.Add(New QueryParameterValue With {
                .Name = toParameterName,
                .Value = ConvertParameterValue(schema, columnName, normalizedTo)
            })
        End If
    End Sub

    Private Shared Sub AddIntegerRangeFilter(ByVal filter As ReportFilter,
                                             ByVal expression As String,
                                             ByVal fromValue As String,
                                             ByVal toValue As String,
                                             ByVal fromParameterName As String,
                                             ByVal toParameterName As String)
        If String.IsNullOrWhiteSpace(expression) Then
            Return
        End If

        Dim normalizedFrom As String = fromValue
        Dim normalizedTo As String = toValue
        NormalizeNumericRange(normalizedFrom, normalizedTo)

        Dim numericValue As Integer
        If Integer.TryParse(normalizedFrom, NumberStyles.Integer, CultureInfo.InvariantCulture, numericValue) Then
            filter.Conditions.Add(expression & " >= " & fromParameterName)
            filter.Parameters.Add(New QueryParameterValue With {.Name = fromParameterName, .Value = numericValue})
        End If

        If Integer.TryParse(normalizedTo, NumberStyles.Integer, CultureInfo.InvariantCulture, numericValue) Then
            filter.Conditions.Add(expression & " <= " & toParameterName)
            filter.Parameters.Add(New QueryParameterValue With {.Name = toParameterName, .Value = numericValue})
        End If
    End Sub

    Private Shared Sub NormalizeNumericRange(ByRef fromValue As String, ByRef toValue As String)
        Dim fromNumber As Decimal
        Dim toNumber As Decimal
        If Decimal.TryParse(fromValue, NumberStyles.Number, CultureInfo.InvariantCulture, fromNumber) AndAlso
           Decimal.TryParse(toValue, NumberStyles.Number, CultureInfo.InvariantCulture, toNumber) AndAlso
           fromNumber > toNumber Then
            Dim temporaryValue As String = fromValue
            fromValue = toValue
            toValue = temporaryValue
        End If
    End Sub

    Private Shared Function BuildQuarterFilterExpression(ByVal schema As DataTable,
                                                         ByVal definition As FilterDefinition) As String
        If Not String.IsNullOrWhiteSpace(definition.QuarterIdColumn) Then
            Return QuoteIdentifier(definition.QuarterIdColumn)
        End If

        If String.IsNullOrWhiteSpace(definition.QuarterColumn) Then
            Return String.Empty
        End If

        If IsNumericType(schema.Columns(definition.QuarterColumn).DataType) Then
            Return QuoteIdentifier(definition.QuarterColumn)
        End If

        Dim quarterText As String = "LOWER(CONVERT(nvarchar(100), " & QuoteIdentifier(definition.QuarterColumn) & "))"
        Return "CASE WHEN " & quarterText & " LIKE '%1%' THEN 1 " &
               "WHEN " & quarterText & " LIKE '%2%' THEN 2 " &
               "WHEN " & quarterText & " LIKE '%3%' THEN 3 " &
               "WHEN " & quarterText & " LIKE '%4%' THEN 4 END"
    End Function

    Private Shared Function ConvertParameterValue(ByVal schema As DataTable,
                                                  ByVal columnName As String,
                                                  ByVal selectedValue As String) As Object
        Dim targetType As Type = schema.Columns(columnName).DataType
        If targetType Is GetType(String) Then
            Return selectedValue
        End If

        Return Convert.ChangeType(selectedValue, targetType, CultureInfo.InvariantCulture)
    End Function

    Private Shared Function BuildWhereClause(ByVal filter As ReportFilter) As String
        If filter Is Nothing OrElse filter.Conditions.Count = 0 Then
            Return String.Empty
        End If

        Return " WHERE " & String.Join(" AND ", filter.Conditions)
    End Function

    Private Function BuildParameterSummary() As String
        Dim summaries As New List(Of String)()
        AddSelectedSummary(summaries, "Report", reportParameterDropDown)
        AddSelectedSummary(summaries, "Entity group", entityGroupDropDown)
        AddRangeSummary(summaries, "Year", yearFromDropDown, yearToDropDown)
        AddRangeSummary(summaries, "Quarter", quarterFromDropDown, quarterToDropDown)

        If statusDropDown.Items.Count > 0 AndAlso statusDropDown.SelectedItem IsNot Nothing Then
            summaries.Add("Status: " & statusDropDown.SelectedItem.Text)
        End If

        Dim selectedEntityCount As Integer = entitiesListBox.Items.Cast(Of ListItem)().Count(Function(item) item.Selected)
        If selectedEntityCount > 0 Then
            summaries.Add(selectedEntityCount.ToString("N0", CultureInfo.CurrentCulture) & " entities")
        End If

        If reportColumnsListBox.Items.Count > 0 Then
            Dim selectedColumnCount As Integer = reportColumnsListBox.Items.Cast(Of ListItem)().Count(Function(item) item.Selected)
            If selectedColumnCount > 0 Then
                summaries.Add(selectedColumnCount.ToString("N0", CultureInfo.CurrentCulture) & " report columns")
            Else
                summaries.Add("All report columns")
            End If
        End If

        If summaries.Count = 0 Then
            Return "<span class=""badge badge-light"">All available data</span>"
        End If

        Dim html As New StringBuilder()
        For Each summary As String In summaries
            html.Append("<span class=""badge badge-light"">").Append(HttpUtility.HtmlEncode(summary)).Append("</span>")
        Next
        Return html.ToString()
    End Function

    Private Shared Sub AddSelectedSummary(ByVal summaries As List(Of String),
                                          ByVal label As String,
                                          ByVal control As DropDownList)
        If Not String.IsNullOrWhiteSpace(control.SelectedValue) AndAlso control.SelectedItem IsNot Nothing Then
            summaries.Add(label & ": " & control.SelectedItem.Text)
        End If
    End Sub

    Private Shared Sub AddRangeSummary(ByVal summaries As List(Of String),
                                       ByVal label As String,
                                       ByVal fromControl As DropDownList,
                                       ByVal toControl As DropDownList)
        Dim hasFrom As Boolean = Not String.IsNullOrWhiteSpace(fromControl.SelectedValue)
        Dim hasTo As Boolean = Not String.IsNullOrWhiteSpace(toControl.SelectedValue)
        If hasFrom AndAlso hasTo Then
            summaries.Add(label & ": " & fromControl.SelectedItem.Text & "–" & toControl.SelectedItem.Text)
        ElseIf hasFrom Then
            summaries.Add(label & " from " & fromControl.SelectedItem.Text)
        ElseIf hasTo Then
            summaries.Add(label & " to " & toControl.SelectedItem.Text)
        End If
    End Sub

    Private Shared Function CreateAdvancedReportDefinition() As AggregateDefinition
        Return New AggregateDefinition With {
            .YearColumn = "Year",
            .QuarterColumn = "Quarter",
            .PeriodIdColumn = "PeriodID",
            .LineItemIdColumn = "Report_LineItemID",
            .LineItemNameColumn = "Line_itemName",
            .ValueColumn = "LineItemValue",
            .SortOrderColumn = "Sort_Order",
            .ColumnNameColumn = "ColumnName",
            .ColumnSortOrderColumn = "Sort_Order3",
            .IsMainColumn = "Is_Main",
            .IsTitleColumn = "Is_Title",
            .LineItemTypeIdColumn = "LineItemTypeID",
            .ReportIdColumn = "ReportID",
            .ReportNameColumn = "Report_Name",
            .ReportTitleColumn = "Report_Title",
            .ReportDescriptionColumn = "Report_Description",
            .ReportSubtitleColumn = "Report_SubTitle",
            .EntityGroupIdColumn = "Entity_GroupID",
            .EntityGroupNameColumn = "EntityGroupName"
        }
    End Function

    Private Shared Function CreateAggregateDefinition(ByVal schema As DataTable) As AggregateDefinition
        Dim definition As New AggregateDefinition With {
            .YearColumn = FindColumn(schema, "Year"),
            .QuarterColumn = FindColumn(schema, "Quarter"),
            .PeriodIdColumn = FindColumn(schema, "PeriodID"),
            .LineItemIdColumn = FindColumn(schema, "Report_LineItemID", "ReportLineItemID"),
            .LineItemNameColumn = FindColumn(schema, "Line_itemName", "LineItemName"),
            .ValueColumn = FindColumn(schema, "LineItemValue", "Line_Item_Value"),
            .SortOrderColumn = FindColumn(schema, "Sort_Order", "SortOrder"),
            .ColumnNameColumn = FindColumn(schema, "ColumnName", "Column_Name"),
            .ColumnSortOrderColumn = FindColumn(schema, "Sort_Order3", "Column_Sort_Order"),
            .IsMainColumn = FindColumn(schema, "Is_Main", "IsMain"),
            .IsTitleColumn = FindColumn(schema, "Is_Title", "IsTitle"),
            .LineItemTypeIdColumn = FindColumn(schema, "LineItemTypeID", "Line_Item_Type_ID"),
            .ReportIdColumn = FindColumn(schema, "ReportID", "Report_ID"),
            .ReportNameColumn = FindColumn(schema, "Report_Name", "ReportName"),
            .ReportTitleColumn = FindColumn(schema, "Report_Title", "ReportTitle"),
            .ReportDescriptionColumn = FindColumn(schema, "Report_Description", "ReportDescription"),
            .ReportSubtitleColumn = FindColumn(schema, "Report_SubTitle", "ReportSubtitle"),
            .EntityGroupIdColumn = FindColumn(schema, "Entity_GroupID", "EntityGroupID"),
            .EntityGroupNameColumn = FindColumn(schema, "EntityGroupName", "Entity_Group_Name")
        }

        If String.IsNullOrWhiteSpace(definition.YearColumn) OrElse
           String.IsNullOrWhiteSpace(definition.QuarterColumn) OrElse
           String.IsNullOrWhiteSpace(definition.LineItemIdColumn) OrElse
           String.IsNullOrWhiteSpace(definition.LineItemNameColumn) OrElse
           String.IsNullOrWhiteSpace(definition.ValueColumn) OrElse
           String.IsNullOrWhiteSpace(definition.SortOrderColumn) Then
            Return Nothing
        End If

        Dim valueType As Type = schema.Columns(definition.ValueColumn).DataType
        If Not IsNumericType(valueType) Then
            Return Nothing
        End If

        Return definition
    End Function

    Private Shared Function FindColumn(ByVal schema As DataTable, ParamArray candidateNames() As String) As String
        For Each candidateName As String In candidateNames
            For Each column As DataColumn In schema.Columns
                If column.ColumnName.Equals(candidateName, StringComparison.OrdinalIgnoreCase) Then
                    Return column.ColumnName
                End If
            Next
        Next

        Return Nothing
    End Function

    Private Shared Function IsNumericType(ByVal valueType As Type) As Boolean
        Return valueType Is GetType(Byte) OrElse
               valueType Is GetType(Short) OrElse
               valueType Is GetType(Integer) OrElse
               valueType Is GetType(Long) OrElse
               valueType Is GetType(Single) OrElse
               valueType Is GetType(Double) OrElse
               valueType Is GetType(Decimal)
    End Function

    Private Shared Function BuildAggregateSql(ByVal selectedView As DatabaseViewInfo,
                                              ByVal schema As DataTable,
                                              ByVal definition As AggregateDefinition,
                                              ByVal reportFilter As ReportFilter,
                                              ByVal resultLimit As Integer) As String
        Dim groupColumns As New List(Of String)()
        AddUniqueColumn(groupColumns, definition.ReportIdColumn)
        If String.IsNullOrWhiteSpace(definition.ReportIdColumn) Then
            AddUniqueColumn(groupColumns, definition.ReportNameColumn)
        End If

        AddUniqueColumn(groupColumns, definition.EntityGroupIdColumn)
        If String.IsNullOrWhiteSpace(definition.EntityGroupIdColumn) Then
            AddUniqueColumn(groupColumns, definition.EntityGroupNameColumn)
        End If

        AddUniqueColumn(groupColumns, definition.LineItemTypeIdColumn)
        AddUniqueColumn(groupColumns, definition.LineItemIdColumn)
        AddUniqueColumn(groupColumns, definition.LineItemNameColumn)
        AddUniqueColumn(groupColumns, definition.SortOrderColumn)
        AddUniqueColumn(groupColumns, definition.IsMainColumn)
        AddUniqueColumn(groupColumns, definition.IsTitleColumn)
        AddUniqueColumn(groupColumns, definition.YearColumn)
        AddUniqueColumn(groupColumns, definition.PeriodIdColumn)
        AddUniqueColumn(groupColumns, definition.QuarterColumn)
        AddUniqueColumn(groupColumns, definition.ColumnSortOrderColumn)
        AddUniqueColumn(groupColumns, definition.ColumnNameColumn)

        Dim selectParts As New List(Of String)()
        Dim groupByParts As New List(Of String)()
        For Each columnName As String In groupColumns
            Dim expression As String = GetGroupExpression(schema, columnName)
            selectParts.Add(expression & " AS " & QuoteIdentifier(columnName))
            groupByParts.Add(expression)
        Next

        Dim metadataColumns As New List(Of String) From {
            definition.ReportNameColumn,
            definition.ReportTitleColumn,
            definition.ReportDescriptionColumn,
            definition.ReportSubtitleColumn,
            definition.EntityGroupNameColumn
        }

        For Each metadataColumn As String In metadataColumns
            If Not String.IsNullOrWhiteSpace(metadataColumn) AndAlso
               Not groupColumns.Any(Function(item) item.Equals(metadataColumn, StringComparison.OrdinalIgnoreCase)) Then
                selectParts.Add("MAX(CONVERT(nvarchar(4000), " & QuoteIdentifier(metadataColumn) & ")) AS " &
                                QuoteIdentifier(metadataColumn))
            End If
        Next

        selectParts.Add("SUM(CONVERT(decimal(38, 6), " & QuoteIdentifier(definition.ValueColumn) & ")) AS " &
                        QuoteIdentifier(definition.ValueColumn))

        Dim orderColumns As New List(Of String)()
        AddUniqueColumn(orderColumns, definition.ReportIdColumn)
        If String.IsNullOrWhiteSpace(definition.ReportIdColumn) Then
            AddUniqueColumn(orderColumns, definition.ReportNameColumn)
        End If
        AddUniqueColumn(orderColumns, definition.EntityGroupIdColumn)
        If String.IsNullOrWhiteSpace(definition.EntityGroupIdColumn) Then
            AddUniqueColumn(orderColumns, definition.EntityGroupNameColumn)
        End If
        AddUniqueColumn(orderColumns, definition.LineItemTypeIdColumn)
        AddUniqueColumn(orderColumns, definition.SortOrderColumn)
        AddUniqueColumn(orderColumns, definition.LineItemIdColumn)
        AddUniqueColumn(orderColumns, definition.YearColumn)
        AddUniqueColumn(orderColumns, definition.PeriodIdColumn)
        AddUniqueColumn(orderColumns, definition.QuarterColumn)
        AddUniqueColumn(orderColumns, definition.ColumnSortOrderColumn)
        AddUniqueColumn(orderColumns, definition.ColumnNameColumn)

        Dim sql As New StringBuilder()
        sql.Append("SELECT TOP (").Append(resultLimit.ToString(CultureInfo.InvariantCulture)).Append(") ")
        sql.Append(String.Join(", ", selectParts))
        sql.Append(" FROM ").Append(QuoteIdentifier(selectedView.SchemaName)).Append(".").Append(QuoteIdentifier(selectedView.ViewName))
        sql.Append(BuildWhereClause(reportFilter))
        sql.Append(" GROUP BY ").Append(String.Join(", ", groupByParts))
        If orderColumns.Count > 0 Then
            sql.Append(" ORDER BY ").Append(String.Join(", ", orderColumns.Select(Function(item) QuoteIdentifier(item))))
        End If
        Return sql.ToString()
    End Function

    Private Shared Function GetGroupExpression(ByVal schema As DataTable, ByVal columnName As String) As String
        If schema.Columns(columnName).DataType Is GetType(String) Then
            Return "CONVERT(nvarchar(4000), " & QuoteIdentifier(columnName) & ")"
        End If

        Return QuoteIdentifier(columnName)
    End Function

    Private Shared Sub AddUniqueColumn(ByVal columns As List(Of String), ByVal columnName As String)
        If String.IsNullOrWhiteSpace(columnName) Then
            Return
        End If

        If Not columns.Any(Function(item) item.Equals(columnName, StringComparison.OrdinalIgnoreCase)) Then
            columns.Add(columnName)
        End If
    End Sub

    Private Sub PopulateGroupingOptions(ByVal selectedView As DatabaseViewInfo, ByVal data As DataTable)
        Dim viewChanged As Boolean = Not selectedView.Key.Equals(
            Convert.ToString(ViewState("GroupingViewKey"), CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase)

        If IsPostBack AndAlso Not viewChanged AndAlso groupByDropDown.Items.Count > 0 Then
            Return
        End If

        groupByDropDown.Items.Clear()
        groupByDropDown.Items.Add(New ListItem("No grouping", String.Empty))

        For Each column As DataColumn In data.Columns
            groupByDropDown.Items.Add(New ListItem(ToDisplayName(column.ColumnName), column.ColumnName))
        Next

        Dim requestedGroup As String = Convert.ToString(Request.QueryString("groupBy"), CultureInfo.InvariantCulture)
        If Not String.IsNullOrWhiteSpace(requestedGroup) AndAlso groupByDropDown.Items.FindByValue(requestedGroup) IsNot Nothing Then
            groupByDropDown.SelectedValue = requestedGroup
        End If

        ViewState("GroupingViewKey") = selectedView.Key
    End Sub

    Private Shared Function BuildViewMetadata(ByVal selectedView As DatabaseViewInfo,
                                              ByVal rowCount As Integer,
                                              ByVal columnCount As Integer,
                                              ByVal rowLabel As String,
                                              Optional ByVal dataSourceLabel As String = Nothing) As String
        Dim html As New StringBuilder()
        html.Append("<span><i class=""icon-database mr-1"" aria-hidden=""true""></i>")
        html.Append(HttpUtility.HtmlEncode(selectedView.Key)).Append("</span>")
        html.Append("<span><i class=""icon-list-numbered mr-1"" aria-hidden=""true""></i>")
        html.Append(rowCount.ToString("N0", CultureInfo.CurrentCulture)).Append(" ")
        html.Append(HttpUtility.HtmlEncode(rowLabel)).Append("</span>")
        html.Append("<span><i class=""icon-grid7 mr-1"" aria-hidden=""true""></i>")
        html.Append(columnCount.ToString("N0", CultureInfo.CurrentCulture)).Append(" columns</span>")
        If Not String.IsNullOrWhiteSpace(dataSourceLabel) Then
            html.Append("<span><i class=""icon-database-menu mr-1"" aria-hidden=""true""></i>")
            html.Append(HttpUtility.HtmlEncode(dataSourceLabel)).Append("</span>")
        End If
        Return html.ToString()
    End Function

    Private Shared Function BuildReportDashboard(ByVal data As DataTable,
                                                 ByVal definition As AggregateDefinition,
                                                 ByVal fallbackTitle As String) As String
        If data.Rows.Count = 0 Then
            Return "<div class=""alert alert-info mb-0""><i class=""icon-info22 mr-2"" aria-hidden=""true""></i>The stored procedure returned no dashboard values for the selected parameters.</div>"
        End If

        Dim sections As New List(Of AggregateSection)()
        Dim sectionLookup As New Dictionary(Of String, AggregateSection)(StringComparer.Ordinal)

        For Each row As DataRow In data.Rows
            Dim sectionKey As String = BuildSectionKey(row, definition)
            Dim section As AggregateSection = Nothing
            If Not sectionLookup.TryGetValue(sectionKey, section) Then
                section = New AggregateSection()
                sectionLookup.Add(sectionKey, section)
                sections.Add(section)
            End If
            section.Rows.Add(row)
        Next

        Dim html As New StringBuilder()
        For index As Integer = 0 To sections.Count - 1
            html.Append(BuildDashboardSection(sections(index).Rows, definition, fallbackTitle, index))
        Next
        Return html.ToString()
    End Function

    Private Shared Function BuildDashboardSection(ByVal rows As List(Of DataRow),
                                                  ByVal definition As AggregateDefinition,
                                                  ByVal fallbackTitle As String,
                                                  ByVal sectionIndex As Integer) As String
        Dim periods As New List(Of DashboardPeriod)()
        Dim periodLookup As New Dictionary(Of String, DashboardPeriod)(StringComparer.Ordinal)
        Dim lineItems As New List(Of DashboardLineItem)()
        Dim lineItemLookup As New Dictionary(Of String, DashboardLineItem)(StringComparer.Ordinal)

        For Each row As DataRow In rows
            Dim periodKey As String = GetStableKeyPart(row, definition.YearColumn) & "|" &
                                      GetStableKeyPart(row, definition.PeriodIdColumn) & "|" &
                                      GetStableKeyPart(row, definition.QuarterColumn)
            Dim period As DashboardPeriod = Nothing
            If Not periodLookup.TryGetValue(periodKey, period) Then
                Dim yearLabel As String = GetTextValue(row, definition.YearColumn)
                Dim quarterLabel As String = GetTextValue(row, definition.QuarterColumn)
                period = New DashboardPeriod With {
                    .Key = periodKey,
                    .Label = BuildDashboardPeriodLabel(yearLabel, quarterLabel),
                    .YearLabel = yearLabel,
                    .QuarterLabel = quarterLabel,
                    .YearSort = GetNumericSortValue(row, definition.YearColumn),
                    .PeriodSort = GetNumericSortValue(row, definition.PeriodIdColumn),
                    .QuarterSort = GetQuarterSortValue(row, definition.QuarterColumn)
                }
                periodLookup.Add(periodKey, period)
                periods.Add(period)
            End If

            Dim lineItemKey As String = GetStableKeyPart(row, definition.LineItemIdColumn) & "|" &
                                        GetStableKeyPart(row, definition.LineItemNameColumn)
            Dim lineItem As DashboardLineItem = Nothing
            If Not lineItemLookup.TryGetValue(lineItemKey, lineItem) Then
                lineItem = New DashboardLineItem With {
                    .Key = lineItemKey,
                    .Label = NormalizeLineItemLabel(GetTextValue(row, definition.LineItemNameColumn)),
                    .SortOrder = GetNumericSortValue(row, definition.SortOrderColumn),
                    .IdSort = GetNumericSortValue(row, definition.LineItemIdColumn),
                    .IsMain = GetBooleanValue(row, definition.IsMainColumn),
                    .IsTitle = GetBooleanValue(row, definition.IsTitleColumn)
                }
                lineItemLookup.Add(lineItemKey, lineItem)
                lineItems.Add(lineItem)
            Else
                lineItem.IsMain = lineItem.IsMain OrElse GetBooleanValue(row, definition.IsMainColumn)
                lineItem.IsTitle = lineItem.IsTitle OrElse GetBooleanValue(row, definition.IsTitleColumn)
            End If

            Dim amount As Decimal
            If TryGetDecimalValue(row, definition.ValueColumn, amount) Then
                If lineItem.ValuesByPeriod.ContainsKey(periodKey) Then
                    lineItem.ValuesByPeriod(periodKey) += amount
                Else
                    lineItem.ValuesByPeriod.Add(periodKey, amount)
                End If
            End If
        Next

        periods.Sort(Function(left, right) CompareDashboardPeriods(left, right))
        lineItems.Sort(Function(left, right) CompareDashboardLineItems(left, right))

        Dim reportTitle As String = FirstNonBlankValue(rows, definition.ReportTitleColumn, definition.ReportNameColumn)
        If String.IsNullOrWhiteSpace(reportTitle) Then
            reportTitle = fallbackTitle
        End If
        Dim reportSubtitle As String = FirstNonBlankValue(rows, definition.ReportSubtitleColumn)
        Dim reportDescription As String = FirstNonBlankValue(rows, definition.ReportDescriptionColumn)
        Dim entityGroupName As String = FirstNonBlankValue(rows, definition.EntityGroupNameColumn)
        Dim lineItemType As String = FirstNonBlankValue(rows, definition.LineItemTypeIdColumn)

        Dim eligibleLineItems As List(Of DashboardLineItem) = lineItems.Where(
            Function(item) Not String.IsNullOrWhiteSpace(item.Label) AndAlso Not item.IsTitle).ToList()
        Dim mainLineItems As List(Of DashboardLineItem) = eligibleLineItems.Where(Function(item) item.IsMain).ToList()
        Dim trendLineItems As List(Of DashboardLineItem) = If(mainLineItems.Count > 0, mainLineItems, eligibleLineItems).Take(5).ToList()

        Dim latestPeriod As DashboardPeriod = If(periods.Count > 0, periods(periods.Count - 1), Nothing)
        Dim previousPeriod As DashboardPeriod = If(periods.Count > 1, periods(periods.Count - 2), Nothing)
        Dim latestLineItems As New List(Of DashboardLineItem)()
        If latestPeriod IsNot Nothing Then
            latestLineItems = eligibleLineItems.Where(Function(item) item.ValuesByPeriod.ContainsKey(latestPeriod.Key)).ToList()
            latestLineItems.Sort(
                Function(left, right)
                    Dim comparison As Integer = Math.Abs(right.ValuesByPeriod(latestPeriod.Key)).CompareTo(
                        Math.Abs(left.ValuesByPeriod(latestPeriod.Key)))
                    If comparison = 0 Then
                        comparison = CompareDashboardLineItems(left, right)
                    End If
                    Return comparison
                End Function)
        End If

        Dim html As New StringBuilder()
        html.Append("<section class=""report-dashboard-section"">")
        html.Append("<header class=""report-dashboard-heading""><div>")
        html.Append("<p class=""report-aggregate-eyebrow"">Quarterly return dashboard</p>")
        html.Append("<h3>").Append(HttpUtility.HtmlEncode(reportTitle)).Append("</h3>")
        If Not String.IsNullOrWhiteSpace(reportSubtitle) Then
            html.Append("<p class=""report-aggregate-subtitle"">").Append(HttpUtility.HtmlEncode(reportSubtitle)).Append("</p>")
        End If
        If Not String.IsNullOrWhiteSpace(reportDescription) Then
            html.Append("<p class=""report-aggregate-description"">").Append(HttpUtility.HtmlEncode(reportDescription)).Append("</p>")
        End If
        html.Append("</div><div class=""report-aggregate-badges"">")
        If Not String.IsNullOrWhiteSpace(entityGroupName) Then
            html.Append("<span class=""badge badge-light""><i class=""icon-office mr-1"" aria-hidden=""true""></i>")
            html.Append(HttpUtility.HtmlEncode(entityGroupName)).Append("</span>")
        End If
        If Not String.IsNullOrWhiteSpace(lineItemType) Then
            html.Append("<span class=""badge badge-light"">Line item type ").Append(HttpUtility.HtmlEncode(lineItemType)).Append("</span>")
        End If
        html.Append("</div></header>")

        If periods.Count = 0 OrElse lineItems.Count = 0 Then
            html.Append("<div class=""alert alert-info mb-0"">This report section contains no dashboard values.</div></section>")
            Return html.ToString()
        End If

        html.Append("<div class=""report-dashboard-kpis"">")
        AppendDashboardKpi(html, "icon-calendar3", periods.Count.ToString("N0", CultureInfo.CurrentCulture), "Reporting periods", "Distinct year and quarter combinations")
        AppendDashboardKpi(html, "icon-list-numbered", eligibleLineItems.Count.ToString("N0", CultureInfo.CurrentCulture), "Line items", "Non-heading items in this report section")
        AppendDashboardKpi(html, "icon-stats-bars2", rows.Count.ToString("N0", CultureInfo.CurrentCulture), "Aggregated values", "Cells returned after applying parameters")
        AppendDashboardKpi(html, "icon-calendar52", If(latestPeriod Is Nothing, "—", latestPeriod.Label), "Latest period", "Most recent period in the result")
        html.Append("</div>")

        html.Append("<div class=""report-dashboard-grid""><article class=""report-dashboard-panel""><header><div><h4>Line-item trends</h4>")
        html.Append("<p>Up to five primary line items across the selected reporting periods.</p></div></header>")
        If trendLineItems.Count > 0 Then
            html.Append("<div class=""report-dashboard-chart"" role=""img"" aria-label=""Line-item trends across reporting periods"" data-report-dashboard-chart data-chart-option=""")
            html.Append(HttpUtility.HtmlAttributeEncode(BuildTrendChartOption(periods, trendLineItems))).Append("""></div>")
        Else
            html.Append("<div class=""alert alert-info mb-0"">No line items are available for the trend chart.</div>")
        End If
        html.Append("</article>")

        html.Append("<article class=""report-dashboard-panel""><header><div><h4>Latest-period comparison</h4><p>")
        html.Append(HttpUtility.HtmlEncode(If(latestPeriod Is Nothing, "No reporting period is available.",
                                               "Largest line items by absolute value for " & latestPeriod.Label & "."))).Append("</p></div></header>")
        If latestLineItems.Count > 0 Then
            html.Append("<div class=""report-dashboard-chart"" role=""img"" aria-label=""Latest-period line-item comparison"" data-report-dashboard-chart data-chart-option=""")
            html.Append(HttpUtility.HtmlAttributeEncode(BuildLatestComparisonChartOption(latestPeriod, latestLineItems.Take(8).ToList()))).Append("""></div>")
        Else
            html.Append("<div class=""alert alert-info mb-0"">No values are available for the latest-period comparison.</div>")
        End If
        html.Append("</article></div>")

        html.Append(BuildDashboardSnapshot(latestLineItems.Take(10).ToList(), previousPeriod, latestPeriod, sectionIndex))
        html.Append("</section>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendDashboardKpi(ByVal html As StringBuilder,
                                          ByVal iconClass As String,
                                          ByVal value As String,
                                          ByVal label As String,
                                          ByVal description As String)
        html.Append("<article class=""report-dashboard-kpi""><span class=""report-dashboard-kpi-icon""><i class=""")
        html.Append(HttpUtility.HtmlAttributeEncode(iconClass)).Append(""" aria-hidden=""true""></i></span><div><strong>")
        html.Append(HttpUtility.HtmlEncode(value)).Append("</strong><span>").Append(HttpUtility.HtmlEncode(label))
        html.Append("</span><small>").Append(HttpUtility.HtmlEncode(description)).Append("</small></div></article>")
    End Sub

    Private Shared Function BuildDashboardSnapshot(ByVal lineItems As List(Of DashboardLineItem),
                                                   ByVal previousPeriod As DashboardPeriod,
                                                   ByVal latestPeriod As DashboardPeriod,
                                                   ByVal sectionIndex As Integer) As String
        Dim html As New StringBuilder()
        html.Append("<article class=""report-dashboard-panel report-dashboard-snapshot"" aria-labelledby=""report-dashboard-snapshot-")
        html.Append(sectionIndex.ToString(CultureInfo.InvariantCulture)).Append("""><header><div><h4 id=""report-dashboard-snapshot-")
        html.Append(sectionIndex.ToString(CultureInfo.InvariantCulture)).Append(""">Latest-period snapshot</h4>")
        html.Append("<p>Previous and latest values for the largest line items. Direction shows movement only, not regulatory performance.</p></div></header>")

        If lineItems.Count = 0 OrElse latestPeriod Is Nothing Then
            html.Append("<div class=""alert alert-info mb-0"">No values are available for the period snapshot.</div></article>")
            Return html.ToString()
        End If

        Dim previousLabel As String = If(previousPeriod Is Nothing, "Previous period", previousPeriod.Label)
        html.Append("<div class=""table-responsive""><table class=""table table-hover report-dashboard-table""><thead><tr><th scope=""col"">Line item</th><th scope=""col"">")
        html.Append(HttpUtility.HtmlEncode(previousLabel)).Append("</th><th scope=""col"">")
        html.Append(HttpUtility.HtmlEncode(latestPeriod.Label)).Append("</th><th scope=""col"">Change</th></tr></thead><tbody>")

        For Each lineItem As DashboardLineItem In lineItems
            Dim latestValue As Decimal = lineItem.ValuesByPeriod(latestPeriod.Key)
            Dim previousValue As Decimal? = Nothing
            Dim amount As Decimal
            If previousPeriod IsNot Nothing AndAlso lineItem.ValuesByPeriod.TryGetValue(previousPeriod.Key, amount) Then
                previousValue = amount
            End If

            html.Append("<tr><th scope=""row"">").Append(HttpUtility.HtmlEncode(lineItem.Label)).Append("</th><td>")
            html.Append(If(previousValue.HasValue, HttpUtility.HtmlEncode(FormatAggregateValue(previousValue.Value)), "—"))
            html.Append("</td><td>").Append(HttpUtility.HtmlEncode(FormatAggregateValue(latestValue))).Append("</td><td>")

            If Not previousValue.HasValue OrElse previousValue.Value = 0D Then
                html.Append("<span class=""report-dashboard-change report-dashboard-change-neutral"">—</span>")
            Else
                Dim percentageChange As Decimal = (latestValue - previousValue.Value) / Math.Abs(previousValue.Value)
                Dim changeClass As String = "report-dashboard-change-neutral"
                Dim changeIcon As String = ""
                If percentageChange > 0D Then
                    changeClass = "report-dashboard-change-up"
                    changeIcon = "↑ "
                ElseIf percentageChange < 0D Then
                    changeClass = "report-dashboard-change-down"
                    changeIcon = "↓ "
                End If
                html.Append("<span class=""report-dashboard-change ").Append(changeClass).Append(""">")
                html.Append(changeIcon).Append(HttpUtility.HtmlEncode(percentageChange.ToString("+0.0%;-0.0%;0.0%", CultureInfo.CurrentCulture))).Append("</span>")
            End If
            html.Append("</td></tr>")
        Next

        html.Append("</tbody></table></div></article>")
        Return html.ToString()
    End Function

    Private Shared Function BuildTrendChartOption(ByVal periods As List(Of DashboardPeriod),
                                                  ByVal lineItems As List(Of DashboardLineItem)) As String
        Dim optionJson As New StringBuilder()
        optionJson.Append("{""animationDuration"":500,""color"":[""#2457d6"",""#00a17d"",""#f59e0b"",""#8b5cf6"",""#e85d75""],")
        optionJson.Append("""tooltip"":{""trigger"":""axis""},""legend"":{""type"":""scroll"",""bottom"":0},")
        optionJson.Append("""grid"":{""left"":16,""right"":18,""top"":28,""bottom"":64,""containLabel"":true},")
        optionJson.Append("""xAxis"":{""type"":""category"",""boundaryGap"":false,""data"":[")
        For periodIndex As Integer = 0 To periods.Count - 1
            If periodIndex > 0 Then optionJson.Append(",")
            optionJson.Append(JsonString(periods(periodIndex).Label))
        Next
        optionJson.Append("]},""yAxis"":{""type"":""value"",""scale"":true},""series"":[")

        For lineIndex As Integer = 0 To lineItems.Count - 1
            If lineIndex > 0 Then optionJson.Append(",")
            Dim lineItem As DashboardLineItem = lineItems(lineIndex)
            optionJson.Append("{""name"":").Append(JsonString(lineItem.Label))
            optionJson.Append(",""type"":""line"",""smooth"":true,""symbolSize"":7,""connectNulls"":false,""data"":[")
            For periodIndex As Integer = 0 To periods.Count - 1
                If periodIndex > 0 Then optionJson.Append(",")
                Dim amount As Decimal
                If lineItem.ValuesByPeriod.TryGetValue(periods(periodIndex).Key, amount) Then
                    optionJson.Append(JsonNumber(amount))
                Else
                    optionJson.Append("null")
                End If
            Next
            optionJson.Append("]}")
        Next
        optionJson.Append("]}")
        Return optionJson.ToString()
    End Function

    Private Shared Function BuildLatestComparisonChartOption(ByVal latestPeriod As DashboardPeriod,
                                                             ByVal lineItems As List(Of DashboardLineItem)) As String
        Dim optionJson As New StringBuilder()
        optionJson.Append("{""animationDuration"":500,""color"":[""#2457d6""],""tooltip"":{""trigger"":""axis"",""axisPointer"":{""type"":""shadow""}},")
        optionJson.Append("""grid"":{""left"":18,""right"":24,""top"":18,""bottom"":24,""containLabel"":true},")
        optionJson.Append("""xAxis"":{""type"":""value""},""yAxis"":{""type"":""category"",""inverse"":true,""data"":[")
        For index As Integer = 0 To lineItems.Count - 1
            If index > 0 Then optionJson.Append(",")
            optionJson.Append(JsonString(lineItems(index).Label))
        Next
        optionJson.Append("]},""series"":[{""name"":").Append(JsonString(latestPeriod.Label))
        optionJson.Append(",""type"":""bar"",""barMaxWidth"":24,""data"":[")
        For index As Integer = 0 To lineItems.Count - 1
            If index > 0 Then optionJson.Append(",")
            optionJson.Append(JsonNumber(lineItems(index).ValuesByPeriod(latestPeriod.Key)))
        Next
        optionJson.Append("]}]}")
        Return optionJson.ToString()
    End Function

    Private Shared Function JsonString(ByVal value As String) As String
        Dim safeValue As String = If(value Is Nothing, String.Empty, value)
        Return """" & HttpUtility.JavaScriptStringEncode(safeValue) & """"
    End Function

    Private Shared Function JsonNumber(ByVal value As Decimal) As String
        Return value.ToString("0.############################", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function BuildDashboardPeriodLabel(ByVal yearLabel As String, ByVal quarterLabel As String) As String
        If String.IsNullOrWhiteSpace(yearLabel) Then
            Return If(String.IsNullOrWhiteSpace(quarterLabel), "Period", quarterLabel)
        End If
        If String.IsNullOrWhiteSpace(quarterLabel) Then
            Return yearLabel
        End If
        Return yearLabel & " · " & quarterLabel
    End Function

    Private Shared Function CompareDashboardPeriods(ByVal left As DashboardPeriod, ByVal right As DashboardPeriod) As Integer
        Dim comparison As Integer = left.YearSort.CompareTo(right.YearSort)
        If comparison = 0 Then comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.YearLabel, right.YearLabel)
        If comparison = 0 Then comparison = left.PeriodSort.CompareTo(right.PeriodSort)
        If comparison = 0 Then comparison = left.QuarterSort.CompareTo(right.QuarterSort)
        If comparison = 0 Then comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.QuarterLabel, right.QuarterLabel)
        Return comparison
    End Function

    Private Shared Function CompareDashboardLineItems(ByVal left As DashboardLineItem, ByVal right As DashboardLineItem) As Integer
        Dim comparison As Integer = left.SortOrder.CompareTo(right.SortOrder)
        If comparison = 0 Then comparison = left.IdSort.CompareTo(right.IdSort)
        If comparison = 0 Then comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.Label, right.Label)
        Return comparison
    End Function

    Private Shared Function BuildAggregatedReport(ByVal data As DataTable,
                                                  ByVal definition As AggregateDefinition,
                                                  ByVal fallbackTitle As String) As String
        If data.Rows.Count = 0 Then
            Return "<div class=""alert alert-info mb-0""><i class=""icon-info22 mr-2"" aria-hidden=""true""></i>The stored procedure returned no values to aggregate for the selected parameters.</div>"
        End If

        Dim sections As New List(Of AggregateSection)()
        Dim sectionLookup As New Dictionary(Of String, AggregateSection)(StringComparer.Ordinal)

        For Each row As DataRow In data.Rows
            Dim sectionKey As String = BuildSectionKey(row, definition)
            Dim section As AggregateSection = Nothing
            If Not sectionLookup.TryGetValue(sectionKey, section) Then
                section = New AggregateSection()
                sectionLookup.Add(sectionKey, section)
                sections.Add(section)
            End If
            section.Rows.Add(row)
        Next

        Dim html As New StringBuilder()
        For Each section As AggregateSection In sections
            html.Append(BuildAggregateSection(section.Rows, definition, fallbackTitle))
        Next
        Return html.ToString()
    End Function

    Private Shared Function BuildAggregateSection(ByVal rows As List(Of DataRow),
                                                  ByVal definition As AggregateDefinition,
                                                  ByVal fallbackTitle As String) As String
        Dim aggregateColumns As New List(Of AggregateColumn)()
        Dim columnLookup As New Dictionary(Of String, AggregateColumn)(StringComparer.Ordinal)
        Dim lineItems As New List(Of AggregateLineItem)()
        Dim lineItemLookup As New Dictionary(Of String, AggregateLineItem)(StringComparer.Ordinal)

        For Each row As DataRow In rows
            Dim columnKey As String = BuildAggregateColumnKey(row, definition)
            Dim aggregateColumn As AggregateColumn = Nothing
            If Not columnLookup.TryGetValue(columnKey, aggregateColumn) Then
                aggregateColumn = New AggregateColumn With {
                    .Key = columnKey,
                    .YearLabel = GetTextValue(row, definition.YearColumn),
                    .QuarterLabel = GetTextValue(row, definition.QuarterColumn),
                    .DetailLabel = GetTextValue(row, definition.ColumnNameColumn),
                    .YearSort = GetNumericSortValue(row, definition.YearColumn),
                    .PeriodSort = GetNumericSortValue(row, definition.PeriodIdColumn),
                    .QuarterSort = GetQuarterSortValue(row, definition.QuarterColumn),
                    .DetailSort = GetNumericSortValue(row, definition.ColumnSortOrderColumn)
                }
                columnLookup.Add(columnKey, aggregateColumn)
                aggregateColumns.Add(aggregateColumn)
            End If

            Dim lineItemKey As String = GetStableKeyPart(row, definition.LineItemIdColumn) & "|" &
                                        GetStableKeyPart(row, definition.LineItemNameColumn)
            Dim lineItem As AggregateLineItem = Nothing
            If Not lineItemLookup.TryGetValue(lineItemKey, lineItem) Then
                lineItem = New AggregateLineItem With {
                    .Key = lineItemKey,
                    .Label = NormalizeLineItemLabel(GetTextValue(row, definition.LineItemNameColumn)),
                    .SortOrder = GetNumericSortValue(row, definition.SortOrderColumn),
                    .IdSort = GetNumericSortValue(row, definition.LineItemIdColumn),
                    .IsMain = GetBooleanValue(row, definition.IsMainColumn),
                    .IsTitle = GetBooleanValue(row, definition.IsTitleColumn)
                }
                lineItemLookup.Add(lineItemKey, lineItem)
                lineItems.Add(lineItem)
            Else
                lineItem.IsMain = lineItem.IsMain OrElse GetBooleanValue(row, definition.IsMainColumn)
                lineItem.IsTitle = lineItem.IsTitle OrElse GetBooleanValue(row, definition.IsTitleColumn)
            End If

            Dim amount As Decimal
            If TryGetDecimalValue(row, definition.ValueColumn, amount) Then
                If lineItem.Values.ContainsKey(columnKey) Then
                    lineItem.Values(columnKey) += amount
                Else
                    lineItem.Values.Add(columnKey, amount)
                End If
            End If
        Next

        aggregateColumns.Sort(Function(left, right) CompareAggregateColumns(left, right))
        lineItems.Sort(Function(left, right) CompareAggregateLineItems(left, right))

        Dim reportTitle As String = FirstNonBlankValue(rows, definition.ReportTitleColumn, definition.ReportNameColumn)
        If String.IsNullOrWhiteSpace(reportTitle) Then
            reportTitle = fallbackTitle
        End If
        Dim reportSubtitle As String = FirstNonBlankValue(rows, definition.ReportSubtitleColumn)
        Dim reportDescription As String = FirstNonBlankValue(rows, definition.ReportDescriptionColumn)
        Dim entityGroupName As String = FirstNonBlankValue(rows, definition.EntityGroupNameColumn)
        Dim lineItemType As String = FirstNonBlankValue(rows, definition.LineItemTypeIdColumn)
        Dim includesDetailColumns As Boolean = aggregateColumns.Any(
            Function(item) Not String.IsNullOrWhiteSpace(item.DetailLabel))

        Dim html As New StringBuilder()
        html.Append("<section class=""report-aggregate-section"">")
        html.Append("<header class=""report-aggregate-heading""><div>")
        html.Append("<p class=""report-aggregate-eyebrow"">Aggregated quarterly return</p>")
        html.Append("<h3>").Append(HttpUtility.HtmlEncode(reportTitle)).Append("</h3>")
        If Not String.IsNullOrWhiteSpace(reportSubtitle) Then
            html.Append("<p class=""report-aggregate-subtitle"">").Append(HttpUtility.HtmlEncode(reportSubtitle)).Append("</p>")
        End If
        If Not String.IsNullOrWhiteSpace(reportDescription) Then
            html.Append("<p class=""report-aggregate-description"">").Append(HttpUtility.HtmlEncode(reportDescription)).Append("</p>")
        End If
        html.Append("</div><div class=""report-aggregate-badges"">")
        If Not String.IsNullOrWhiteSpace(entityGroupName) Then
            html.Append("<span class=""badge badge-light""><i class=""icon-office mr-1"" aria-hidden=""true""></i>")
            html.Append(HttpUtility.HtmlEncode(entityGroupName)).Append("</span>")
        End If
        If Not String.IsNullOrWhiteSpace(lineItemType) Then
            html.Append("<span class=""badge badge-light"">Line item type ").Append(HttpUtility.HtmlEncode(lineItemType)).Append("</span>")
        End If
        html.Append("</div></header>")

        If aggregateColumns.Count = 0 OrElse lineItems.Count = 0 Then
            html.Append("<div class=""alert alert-info mb-0"">This report section contains no matrix values.</div></section>")
            Return html.ToString()
        End If

        html.Append("<div class=""table-responsive report-aggregate-table-wrap""><table class=""table table-bordered report-aggregate-table""><thead>")
        AppendAggregateHeaders(html, aggregateColumns, includesDetailColumns)
        html.Append("</thead><tbody>")

        For Each lineItem As AggregateLineItem In lineItems
            Dim rowClass As String = String.Empty
            If lineItem.IsMain Then
                rowClass = " class=""report-aggregate-main"""
            ElseIf lineItem.IsTitle Then
                rowClass = " class=""report-aggregate-title"""
            ElseIf String.IsNullOrWhiteSpace(lineItem.Label) Then
                rowClass = " class=""report-aggregate-spacer"""
            End If

            html.Append("<tr").Append(rowClass).Append("><th scope=""row"">")
            html.Append(If(String.IsNullOrWhiteSpace(lineItem.Label), "&nbsp;", HttpUtility.HtmlEncode(lineItem.Label))).Append("</th>")

            For Each aggregateColumn As AggregateColumn In aggregateColumns
                Dim amount As Decimal
                If lineItem.Values.TryGetValue(aggregateColumn.Key, amount) Then
                    html.Append("<td>").Append(HttpUtility.HtmlEncode(FormatAggregateValue(amount))).Append("</td>")
                Else
                    html.Append("<td class=""report-aggregate-empty"">—</td>")
                End If
            Next
            html.Append("</tr>")
        Next

        html.Append("</tbody></table></div></section>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendAggregateHeaders(ByVal html As StringBuilder,
                                              ByVal columns As List(Of AggregateColumn),
                                              ByVal includesDetailColumns As Boolean)
        Dim headerLevels As Integer = If(includesDetailColumns, 3, 2)
        html.Append("<tr><th class=""report-aggregate-line-item-header"" rowspan=""")
        html.Append(headerLevels.ToString(CultureInfo.InvariantCulture)).Append(""">Line item</th>")

        Dim index As Integer = 0
        While index < columns.Count
            Dim yearLabel As String = columns(index).YearLabel
            Dim yearSpan As Integer = 1
            While index + yearSpan < columns.Count AndAlso
                  columns(index + yearSpan).YearLabel.Equals(yearLabel, StringComparison.OrdinalIgnoreCase)
                yearSpan += 1
            End While
            html.Append("<th class=""report-aggregate-year"" colspan=""").Append(yearSpan).Append(""">")
            html.Append(HttpUtility.HtmlEncode(If(String.IsNullOrWhiteSpace(yearLabel), "Year", yearLabel))).Append("</th>")
            index += yearSpan
        End While
        html.Append("</tr><tr>")

        index = 0
        While index < columns.Count
            Dim yearLabel As String = columns(index).YearLabel
            Dim quarterLabel As String = columns(index).QuarterLabel
            Dim quarterSpan As Integer = 1
            While index + quarterSpan < columns.Count AndAlso
                  columns(index + quarterSpan).YearLabel.Equals(yearLabel, StringComparison.OrdinalIgnoreCase) AndAlso
                  columns(index + quarterSpan).QuarterLabel.Equals(quarterLabel, StringComparison.OrdinalIgnoreCase)
                quarterSpan += 1
            End While
            html.Append("<th class=""report-aggregate-quarter"" colspan=""").Append(quarterSpan).Append(""">")
            html.Append(HttpUtility.HtmlEncode(If(String.IsNullOrWhiteSpace(quarterLabel), "Period", quarterLabel))).Append("</th>")
            index += quarterSpan
        End While
        html.Append("</tr>")

        If includesDetailColumns Then
            html.Append("<tr>")
            For Each aggregateColumn As AggregateColumn In columns
                Dim detailLabel As String = If(String.IsNullOrWhiteSpace(aggregateColumn.DetailLabel), "Value", aggregateColumn.DetailLabel)
                html.Append("<th class=""report-aggregate-column"">").Append(HttpUtility.HtmlEncode(detailLabel)).Append("</th>")
            Next
            html.Append("</tr>")
        End If
    End Sub

    Private Shared Function BuildSectionKey(ByVal row As DataRow, ByVal definition As AggregateDefinition) As String
        Dim reportPart As String = GetStableKeyPart(row, definition.ReportIdColumn)
        If String.IsNullOrWhiteSpace(reportPart) Then
            reportPart = GetStableKeyPart(row, definition.ReportNameColumn)
        End If

        Dim entityGroupPart As String = GetStableKeyPart(row, definition.EntityGroupIdColumn)
        If String.IsNullOrWhiteSpace(entityGroupPart) Then
            entityGroupPart = GetStableKeyPart(row, definition.EntityGroupNameColumn)
        End If

        Return reportPart & "|" & entityGroupPart & "|" & GetStableKeyPart(row, definition.LineItemTypeIdColumn)
    End Function

    Private Shared Function BuildAggregateColumnKey(ByVal row As DataRow, ByVal definition As AggregateDefinition) As String
        Return GetStableKeyPart(row, definition.YearColumn) & "|" &
               GetStableKeyPart(row, definition.PeriodIdColumn) & "|" &
               GetStableKeyPart(row, definition.QuarterColumn) & "|" &
               GetStableKeyPart(row, definition.ColumnNameColumn)
    End Function

    Private Shared Function GetStableKeyPart(ByVal row As DataRow, ByVal columnName As String) As String
        If String.IsNullOrWhiteSpace(columnName) OrElse Not row.Table.Columns.Contains(columnName) Then
            Return String.Empty
        End If

        Dim value As Object = row(columnName)
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return "__NULL__"
        End If

        Return value.GetType().FullName & ":" & Convert.ToString(value, CultureInfo.InvariantCulture)
    End Function

    Private Shared Function GetTextValue(ByVal row As DataRow, ByVal columnName As String) As String
        If String.IsNullOrWhiteSpace(columnName) OrElse Not row.Table.Columns.Contains(columnName) Then
            Return String.Empty
        End If

        Dim value As Object = row(columnName)
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return String.Empty
        End If

        Return Convert.ToString(value, CultureInfo.CurrentCulture).Trim()
    End Function

    Private Shared Function FirstNonBlankValue(ByVal rows As List(Of DataRow), ParamArray columnNames() As String) As String
        For Each columnName As String In columnNames
            If String.IsNullOrWhiteSpace(columnName) Then
                Continue For
            End If

            For Each row As DataRow In rows
                Dim value As String = GetTextValue(row, columnName)
                If Not String.IsNullOrWhiteSpace(value) Then
                    Return value
                End If
            Next
        Next

        Return String.Empty
    End Function

    Private Shared Function NormalizeLineItemLabel(ByVal value As String) As String
        If String.IsNullOrWhiteSpace(value) OrElse value.Equals("<Blank>", StringComparison.OrdinalIgnoreCase) Then
            Return String.Empty
        End If

        Return value
    End Function

    Private Shared Function GetBooleanValue(ByVal row As DataRow, ByVal columnName As String) As Boolean
        If String.IsNullOrWhiteSpace(columnName) OrElse Not row.Table.Columns.Contains(columnName) OrElse
           row(columnName) Is DBNull.Value Then
            Return False
        End If

        Try
            Return Convert.ToBoolean(row(columnName), CultureInfo.InvariantCulture)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function TryGetDecimalValue(ByVal row As DataRow,
                                               ByVal columnName As String,
                                               ByRef amount As Decimal) As Boolean
        amount = 0D
        If String.IsNullOrWhiteSpace(columnName) OrElse Not row.Table.Columns.Contains(columnName) OrElse
           row(columnName) Is DBNull.Value Then
            Return False
        End If

        Try
            amount = Convert.ToDecimal(row(columnName), CultureInfo.InvariantCulture)
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Shared Function GetNumericSortValue(ByVal row As DataRow, ByVal columnName As String) As Decimal
        Dim value As Decimal
        If TryGetDecimalValue(row, columnName, value) Then
            Return value
        End If

        Return Decimal.MaxValue
    End Function

    Private Shared Function GetQuarterSortValue(ByVal row As DataRow, ByVal columnName As String) As Decimal
        Dim directValue As Decimal
        If TryGetDecimalValue(row, columnName, directValue) Then
            Return directValue
        End If

        Dim quarterText As String = GetTextValue(row, columnName)
        Dim numberMatch As System.Text.RegularExpressions.Match =
            System.Text.RegularExpressions.Regex.Match(quarterText, "\d+")
        If numberMatch.Success AndAlso Decimal.TryParse(numberMatch.Value, NumberStyles.Number,
                                                        CultureInfo.InvariantCulture, directValue) Then
            Return directValue
        End If

        Return Decimal.MaxValue
    End Function

    Private Shared Function CompareAggregateColumns(ByVal left As AggregateColumn, ByVal right As AggregateColumn) As Integer
        Dim comparison As Integer = left.YearSort.CompareTo(right.YearSort)
        If comparison = 0 Then
            comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.YearLabel, right.YearLabel)
        End If
        If comparison = 0 Then
            comparison = left.PeriodSort.CompareTo(right.PeriodSort)
        End If
        If comparison = 0 Then
            comparison = left.QuarterSort.CompareTo(right.QuarterSort)
        End If
        If comparison = 0 Then
            comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.QuarterLabel, right.QuarterLabel)
        End If
        If comparison = 0 Then
            comparison = left.DetailSort.CompareTo(right.DetailSort)
        End If
        If comparison = 0 Then
            comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.DetailLabel, right.DetailLabel)
        End If
        Return comparison
    End Function

    Private Shared Function CompareAggregateLineItems(ByVal left As AggregateLineItem, ByVal right As AggregateLineItem) As Integer
        Dim comparison As Integer = left.SortOrder.CompareTo(right.SortOrder)
        If comparison = 0 Then
            comparison = left.IdSort.CompareTo(right.IdSort)
        End If
        If comparison = 0 Then
            comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left.Label, right.Label)
        End If
        Return comparison
    End Function

    Private Shared Function FormatAggregateValue(ByVal amount As Decimal) As String
        Return amount.ToString("#,##0.00;(#,##0.00);0.00", CultureInfo.CurrentCulture)
    End Function

    Private Shared Function BuildRenderedOutput(ByVal data As DataTable, ByVal groupColumnName As String) As String
        If data.Columns.Count = 0 Then
            Return "<div class=""alert alert-info mb-0"">The stored procedure did not return any columns.</div>"
        End If

        If data.Rows.Count = 0 Then
            Return "<div class=""alert alert-info mb-0""><i class=""icon-info22 mr-2"" aria-hidden=""true""></i>The stored procedure returned no records for the selected parameters.</div>"
        End If

        If String.IsNullOrWhiteSpace(groupColumnName) Then
            Dim allRows As New List(Of DataRow)()
            For Each row As DataRow In data.Rows
                allRows.Add(row)
            Next
            Return BuildTable(data, allRows)
        End If

        Dim groupedRows As New List(Of RowGroup)()
        Dim groupLookup As New Dictionary(Of String, RowGroup)(StringComparer.Ordinal)

        For Each row As DataRow In data.Rows
            Dim rawValue As Object = row(groupColumnName)
            Dim label As String = If(rawValue Is DBNull.Value, "Not specified", FormatValue(rawValue))
            If String.IsNullOrWhiteSpace(label) Then
                label = "Not specified"
            End If

            Dim groupKey As String = If(rawValue Is DBNull.Value,
                                        "__NULL__",
                                        rawValue.GetType().FullName & "|" & Convert.ToString(rawValue, CultureInfo.InvariantCulture))
            Dim rowGroup As RowGroup = Nothing
            If Not groupLookup.TryGetValue(groupKey, rowGroup) Then
                rowGroup = New RowGroup With {.Label = label}
                groupLookup.Add(groupKey, rowGroup)
                groupedRows.Add(rowGroup)
            End If

            rowGroup.Rows.Add(row)
        Next

        Dim html As New StringBuilder()
        For Each rowGroup As RowGroup In groupedRows
            html.Append("<section class=""report-view-row-group"">")
            html.Append("<header><h3>").Append(HttpUtility.HtmlEncode(rowGroup.Label)).Append("</h3>")
            html.Append("<span class=""badge badge-light badge-pill"">")
            html.Append(rowGroup.Rows.Count.ToString("N0", CultureInfo.CurrentCulture)).Append(" records</span></header>")
            html.Append(BuildTable(data, rowGroup.Rows)).Append("</section>")
        Next

        Return html.ToString()
    End Function

    Private Shared Function BuildTable(ByVal data As DataTable, ByVal rows As List(Of DataRow)) As String
        Dim html As New StringBuilder()
        html.Append("<div class=""table-responsive report-view-table-wrap""><table class=""table table-hover table-striped table-bordered report-view-table""><thead><tr>")

        For Each column As DataColumn In data.Columns
            html.Append("<th scope=""col"">").Append(HttpUtility.HtmlEncode(ToDisplayName(column.ColumnName))).Append("</th>")
        Next

        html.Append("</tr></thead><tbody>")
        For Each row As DataRow In rows
            html.Append("<tr>")
            For Each column As DataColumn In data.Columns
                html.Append("<td data-label=""").Append(HttpUtility.HtmlAttributeEncode(ToDisplayName(column.ColumnName))).Append(""">")
                html.Append(HttpUtility.HtmlEncode(FormatValue(row(column)))).Append("</td>")
            Next
            html.Append("</tr>")
        Next

        html.Append("</tbody></table></div>")
        Return html.ToString()
    End Function

    Private Shared Function FormatValue(ByVal value As Object) As String
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return "—"
        End If

        If TypeOf value Is DateTime Then
            Return DirectCast(value, DateTime).ToString("dd MMM yyyy HH:mm:ss", CultureInfo.CurrentCulture)
        End If

        If TypeOf value Is DateTimeOffset Then
            Return DirectCast(value, DateTimeOffset).ToString("dd MMM yyyy HH:mm:ss zzz", CultureInfo.CurrentCulture)
        End If

        If TypeOf value Is Boolean Then
            Return If(DirectCast(value, Boolean), "Yes", "No")
        End If

        If TypeOf value Is Byte() Then
            Return "Binary data (" & DirectCast(value, Byte()).Length.ToString("N0", CultureInfo.CurrentCulture) & " bytes)"
        End If

        Return Convert.ToString(value, CultureInfo.CurrentCulture)
    End Function

    Private Shared Function ToDisplayName(ByVal databaseName As String) As String
        If String.IsNullOrWhiteSpace(databaseName) Then
            Return databaseName
        End If

        Dim displayName As String = databaseName
        If displayName.StartsWith("VIEW_", StringComparison.OrdinalIgnoreCase) Then
            displayName = displayName.Substring(5)
        End If

        Return displayName.Replace("_", " ").Trim()
    End Function

    Private Shared Function QuoteIdentifier(ByVal identifier As String) As String
        Return "[" & identifier.Replace("]", "]]") & "]"
    End Function

    Private Shared Function GetConfiguredRowLimit() As Integer
        Dim configuredValue As Integer
        If Integer.TryParse(ConfigurationManager.AppSettings("ReportRenderMaxRows"), configuredValue) Then
            Return Math.Max(100, Math.Min(5000, configuredValue))
        End If

        Return DefaultRowLimit
    End Function

    Private Shared Function GetConfiguredAggregateCellLimit() As Integer
        Dim configuredValue As Integer
        If Integer.TryParse(ConfigurationManager.AppSettings("ReportRenderMaxAggregateCells"), configuredValue) Then
            Return Math.Max(500, Math.Min(25000, configuredValue))
        End If

        Return DefaultAggregateCellLimit
    End Function

    Private Shared Function GetConfiguredCommandTimeout() As Integer
        Dim configuredValue As Integer
        If Integer.TryParse(ConfigurationManager.AppSettings("ReportRenderCommandTimeoutSeconds"), configuredValue) Then
            Return Math.Max(30, Math.Min(600, configuredValue))
        End If

        Return DefaultCommandTimeoutSeconds
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
        resultsPanel.Visible = False
        emptyStatePanel.Visible = statusCode < 400
    End Sub

    Protected Overrides Sub OnUnload(ByVal e As EventArgs)
        _db.Dispose()
        MyBase.OnUnload(e)
    End Sub

    Public NotInheritable Class DatabaseViewInfo
        Public Property SchemaName As String
        Public Property ViewName As String

        Public ReadOnly Property Key As String
            Get
                Return SchemaName & "." & ViewName
            End Get
        End Property

        Public ReadOnly Property DisplayName As String
            Get
                Return ReportRenderPage.ToDisplayName(ViewName)
            End Get
        End Property
    End Class

    Private NotInheritable Class RowGroup
        Public Property Label As String
        Public Property Rows As List(Of DataRow) = New List(Of DataRow)()
    End Class

    Private NotInheritable Class FilterDefinition
        Public Property ReportIdColumn As String
        Public Property ReportNameColumn As String
        Public Property YearColumn As String
        Public Property QuarterIdColumn As String
        Public Property QuarterColumn As String
        Public Property EntityGroupIdColumn As String
        Public Property EntityGroupNameColumn As String
        Public Property EntityNameColumn As String

        Public ReadOnly Property HasAnyFilter As Boolean
            Get
                Return Not String.IsNullOrWhiteSpace(ReportIdColumn) OrElse
                       Not String.IsNullOrWhiteSpace(ReportNameColumn) OrElse
                       Not String.IsNullOrWhiteSpace(YearColumn) OrElse
                       Not String.IsNullOrWhiteSpace(QuarterIdColumn) OrElse
                       Not String.IsNullOrWhiteSpace(QuarterColumn) OrElse
                       Not String.IsNullOrWhiteSpace(EntityGroupIdColumn) OrElse
                       Not String.IsNullOrWhiteSpace(EntityGroupNameColumn) OrElse
                       Not String.IsNullOrWhiteSpace(EntityNameColumn)
            End Get
        End Property
    End Class

    Private NotInheritable Class ReportFilter
        Public Property Conditions As List(Of String) = New List(Of String)()
        Public Property Parameters As List(Of QueryParameterValue) = New List(Of QueryParameterValue)()
    End Class

    Private NotInheritable Class QueryParameterValue
        Public Property Name As String
        Public Property Value As Object
    End Class

    Private NotInheritable Class AggregateDefinition
        Public Property YearColumn As String
        Public Property QuarterColumn As String
        Public Property PeriodIdColumn As String
        Public Property LineItemIdColumn As String
        Public Property LineItemNameColumn As String
        Public Property ValueColumn As String
        Public Property SortOrderColumn As String
        Public Property ColumnNameColumn As String
        Public Property ColumnSortOrderColumn As String
        Public Property IsMainColumn As String
        Public Property IsTitleColumn As String
        Public Property LineItemTypeIdColumn As String
        Public Property ReportIdColumn As String
        Public Property ReportNameColumn As String
        Public Property ReportTitleColumn As String
        Public Property ReportDescriptionColumn As String
        Public Property ReportSubtitleColumn As String
        Public Property EntityGroupIdColumn As String
        Public Property EntityGroupNameColumn As String
    End Class

    Private NotInheritable Class AggregateSection
        Public Property Rows As List(Of DataRow) = New List(Of DataRow)()
    End Class

    Private NotInheritable Class AggregateColumn
        Public Property Key As String
        Public Property YearLabel As String
        Public Property QuarterLabel As String
        Public Property DetailLabel As String
        Public Property YearSort As Decimal
        Public Property PeriodSort As Decimal
        Public Property QuarterSort As Decimal
        Public Property DetailSort As Decimal
    End Class

    Private NotInheritable Class AggregateLineItem
        Public Property Key As String
        Public Property Label As String
        Public Property SortOrder As Decimal
        Public Property IdSort As Decimal
        Public Property IsMain As Boolean
        Public Property IsTitle As Boolean
        Public Property Values As Dictionary(Of String, Decimal) = New Dictionary(Of String, Decimal)(StringComparer.Ordinal)
    End Class

    Private NotInheritable Class DashboardPeriod
        Public Property Key As String
        Public Property Label As String
        Public Property YearLabel As String
        Public Property QuarterLabel As String
        Public Property YearSort As Decimal
        Public Property PeriodSort As Decimal
        Public Property QuarterSort As Decimal
    End Class

    Private NotInheritable Class DashboardLineItem
        Public Property Key As String
        Public Property Label As String
        Public Property SortOrder As Decimal
        Public Property IdSort As Decimal
        Public Property IsMain As Boolean
        Public Property IsTitle As Boolean
        Public Property ValuesByPeriod As Dictionary(Of String, Decimal) = New Dictionary(Of String, Decimal)(StringComparer.Ordinal)
    End Class
End Class
