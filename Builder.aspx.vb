Imports System.IO
Imports System.Security.Cryptography
Imports System.Data.Entity
Imports System.Globalization
Imports System.Linq
Imports System.Web
Imports Antlr.Runtime.Misc
Imports System.Web.Services.Description
Imports ReportingSuite.ReportingSuiteEntities3

Public Class Builder
    Inherits System.Web.UI.Page

    Public db As New ReportingSuiteEntities3
    Public RequestedReportId As String
    Public Report As Report
    Public ReportHealth As ReportHealthResult
    Public BuilderReports As List(Of Report) = New List(Of Report)()
    Public ReportSecurityRules As List(Of ReportAccessRuleInfo) = New List(Of ReportAccessRuleInfo)()
    Public ReportSecurityUsersText As String = String.Empty
    Public ReportSecurityGroupsText As String = String.Empty
    Public ReportSecurityRestricted As Boolean
    Public WorkspaceInsights As New ReportBuilderWorkspaceInsights()
    Public BuilderReportCount As Integer
    Public BuilderConfiguredCount As Integer
    Public BuilderSecuredCount As Integer
    Public BuilderAdhocCount As Integer
    Public BuilderErrorMessage As String = String.Empty
    Public BuilderSuccessMessage As String = String.Empty
    Public CurrentWindowsAccount As String = String.Empty
    Public Sort As Integer

    Protected Overrides Sub OnInit(ByVal e As EventArgs)
        MyBase.OnInit(e)
        CurrentWindowsAccount = ReportAccessService.GetCurrentWindowsAccount(Context)
        If Not String.IsNullOrWhiteSpace(CurrentWindowsAccount) Then
            ViewStateUserKey = CurrentWindowsAccount
        End If

        Try
            If Not ReportAccessService.IsCurrentUserAdministrator(Context) Then
                Throw New HttpException(403, "The Report Builder Workspace is available to Reporting Suite administrators only.")
            End If
        Catch ex As HttpException
            Throw
        Catch ex As Exception
            Trace.Warn("ReportBuilder", "Unable to validate the current administrator.", ex)
            Throw New HttpException(503, "Report Builder access could not be validated because the Reporting Suite database is unavailable.", ex)
        End Try
    End Sub

    Sub Page_Load(ByVal Sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        RequestedReportId = Request.QueryString("report")
        Session("formset") = Request.Form("formset")

        If Not String.IsNullOrWhiteSpace(RequestedReportId) Then
            Dim reportId As Long
            If Not Long.TryParse(RequestedReportId, NumberStyles.Integer, CultureInfo.InvariantCulture, reportId) OrElse reportId <= 0 Then
                BuilderErrorMessage = "Select a valid report to open the workspace."
            Else
                Report = db.Reports.
                    Include(Function(item) item.EntityGroup).
                    Include(Function(item) item.LineItemType).
                    Include(Function(item) item.ReportType).
                    Include(Function(item) item.ReportSections).
                    Include(Function(item) item.Report_Columns).
                    Include(Function(item) item.Report_LineItems).
                    SingleOrDefault(Function(item) item.ReportID = reportId)

                If Report Is Nothing Then
                    BuilderErrorMessage = "The selected report could not be found."
                Else
                    If Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(Request.Form("builderAction"), "saveSecurity", StringComparison.Ordinal) Then
                        Try
                            ReportAccessService.SaveReportAccess(
                                Report.ReportID,
                                String.Equals(Request.Form("ReportSecurityRestricted"), "1", StringComparison.Ordinal),
                                Request.Form("ReportSecurityUsers"),
                                Request.Form("ReportSecurityGroups"))
                            Response.Redirect("Builder.aspx?report=" & Report.ReportID.ToString(CultureInfo.InvariantCulture) &
                                              "&securitySaved=1", False)
                            Context.ApplicationInstance.CompleteRequest()
                            Return
                        Catch ex As ArgumentException
                            BuilderErrorMessage = ex.Message
                        Catch ex As Exception
                            Trace.Warn("ReportBuilderSecurity", "Unable to save report access rules.", ex)
                            BuilderErrorMessage = "The report security settings could not be saved. Review the application trace and database permissions."
                        End Try
                    End If

                    If String.Equals(Request.QueryString("securitySaved"), "1", StringComparison.Ordinal) Then
                        BuilderSuccessMessage = "Report security settings were saved."
                    End If

                    LoadReportWorkspace(reportId)
                End If
            End If
        End If

        If Report Is Nothing Then
            LoadReportCatalogue()
        End If
    End Sub

    Private Sub LoadReportWorkspace(ByVal reportId As Long)
        Try
            ReportHealth = ReportHealthService.CheckConfiguredReport(reportId)
        Catch healthException As Exception
            Trace.Warn("ReportBuilderHealth", "Unable to evaluate report formula dependencies.", healthException)
            ReportHealth = ReportHealthResult.Failed(
                reportId,
                "The report health check could not read the formula dependency metadata.")
        End Try

        Try
            ReportSecurityRules = ReportAccessService.GetRules(reportId)
            ReportSecurityRestricted = ReportSecurityRules.Count > 0
            ReportSecurityUsersText = String.Join(Environment.NewLine,
                ReportSecurityRules.Where(Function(rule) Not rule.IsGroup).Select(Function(rule) rule.Grantee))
            ReportSecurityGroupsText = String.Join(Environment.NewLine,
                ReportSecurityRules.Where(Function(rule) rule.IsGroup).Select(Function(rule) rule.Grantee))
        Catch ex As Exception
            Trace.Warn("ReportBuilderSecurity", "Unable to load report access rules.", ex)
            If String.IsNullOrWhiteSpace(BuilderErrorMessage) Then
                BuilderErrorMessage = "Report security settings could not be loaded."
            End If
        End Try

        WorkspaceInsights = BuildWorkspaceInsights(reportId)
    End Sub

    Private Sub LoadReportCatalogue()
        BuilderReports = db.Reports _
            .Include("EntityGroup") _
            .Include("LineItemType") _
            .Include("ReportType") _
            .Where(Function(item) item.Is_Deleted = False) _
            .OrderBy(Function(item) item.Report_Name) _
            .ToList()

        BuilderReportCount = BuilderReports.Count
        BuilderConfiguredCount = BuilderReports.Where(
            Function(item) Not String.IsNullOrWhiteSpace(item.Report_Name) AndAlso
                           item.ReportTypeID.HasValue AndAlso
                           (item.Is_MulitpleEntities.GetValueOrDefault(False) OrElse item.Entity_GroupID.HasValue)).Count()
        BuilderAdhocCount = BuilderReports.Where(Function(item) Not String.IsNullOrWhiteSpace(item.AdhocQuery)).Count()

        Dim activeReportIds As List(Of Long) = BuilderReports.Select(Function(item) item.ReportID).ToList()
        If activeReportIds.Count > 0 Then
            Try
                BuilderSecuredCount = db.ReportRoles.
                    Where(Function(rule) rule.ReportID.HasValue AndAlso activeReportIds.Contains(rule.ReportID.Value)).
                    Select(Function(rule) rule.ReportID.Value).
                    Distinct().
                    Count()
            Catch ex As Exception
                Trace.Warn("ReportBuilderSecurity", "Unable to calculate secured report totals.", ex)
                BuilderSecuredCount = 0
            End Try
        End If
    End Sub

    Private Function BuildWorkspaceInsights(ByVal reportId As Long) As ReportBuilderWorkspaceInsights
        Dim reportColumns As List(Of Report_Columns) = db.Report_Columns.
            Where(Function(item) item.ReportID.HasValue AndAlso item.ReportID.Value = reportId).
            ToList()
        Dim reportLineItems As List(Of Report_LineItems) = db.Report_LineItems.
            Where(Function(item) item.ReportID.HasValue AndAlso item.ReportID.Value = reportId).
            ToList()
        Dim lineItemIds As List(Of Long) = reportLineItems.Select(Function(item) item.Report_LineItemID).ToList()
        Dim mappings As List(Of LineItem_Columns) = New List(Of LineItem_Columns)()
        If lineItemIds.Count > 0 Then
            mappings = db.LineItem_Columns.
                Where(Function(item) item.Report_LineItemID.HasValue AndAlso lineItemIds.Contains(item.Report_LineItemID.Value)).
                ToList()
        End If

        Dim configuredMappings As Integer = mappings.Where(
            Function(item) item.Is_CalculatedValue OrElse item.Is_Static_Value OrElse item.Is_Inherited OrElse
                           Not String.IsNullOrWhiteSpace(item.StoredProc) OrElse
                           (Not String.IsNullOrWhiteSpace(item.TableName) AndAlso
                            Not String.IsNullOrWhiteSpace(item.ColumnName))).Count()
        Dim sectionCount As Integer = db.ReportSections.Count(
            Function(item) item.ReportID.HasValue AndAlso item.ReportID.Value = reportId)
        Dim completedChecks As Integer = 0
        Dim totalChecks As Integer = 7
        If Not String.IsNullOrWhiteSpace(Report.Report_Name) Then completedChecks += 1
        If Report.ReportTypeID.HasValue Then completedChecks += 1
        If Report.Is_MulitpleEntities.GetValueOrDefault(False) OrElse Report.Entity_GroupID.HasValue Then completedChecks += 1
        If reportColumns.Count > 0 Then completedChecks += 1
        If reportLineItems.Count > 0 Then completedChecks += 1
        If mappings.Count > 0 AndAlso configuredMappings = mappings.Count Then completedChecks += 1
        If ReportHealth IsNot Nothing AndAlso Not ReportHealth.HasCheckError AndAlso ReportHealth.IsHealthy Then completedChecks += 1

        Return New ReportBuilderWorkspaceInsights With {
            .ColumnCount = reportColumns.Count,
            .VisibleColumnCount = reportColumns.Where(Function(item) item.Show).Count(),
            .LineItemCount = reportLineItems.Count,
            .VisibleLineItemCount = reportLineItems.Where(Function(item) item.Show.GetValueOrDefault(False)).Count(),
            .MappingCount = mappings.Count,
            .ConfiguredMappingCount = configuredMappings,
            .SectionCount = sectionCount,
            .SecurityRuleCount = ReportSecurityRules.Count,
            .VisibleColumnPercent = If(reportColumns.Count = 0, 0,
                                       CInt(Math.Round((reportColumns.Where(Function(item) item.Show).Count() / CDbl(reportColumns.Count)) * 100.0R))),
            .VisibleLineItemPercent = If(reportLineItems.Count = 0, 0,
                                         CInt(Math.Round((reportLineItems.Where(Function(item) item.Show.GetValueOrDefault(False)).Count() / CDbl(reportLineItems.Count)) * 100.0R))),
            .ConfigurationPercent = CInt(Math.Round((completedChecks / CDbl(totalChecks)) * 100.0R)),
            .MappingPercent = If(mappings.Count = 0, 0,
                                 CInt(Math.Round((configuredMappings / CDbl(mappings.Count)) * 100.0R)))
        }
    End Function

    Public Function GetPreviewUrl() As String
        If Report Is Nothing Then
            Return "#"
        End If
        Return SiteMaster.GetReportUrl(Report)
    End Function

    Public Function GetReportGroupName(ByVal reportItem As Report) As String
        If reportItem Is Nothing Then
            Return "Not configured"
        End If

        If reportItem.EntityGroup IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(reportItem.EntityGroup.EntityGroupName) Then
            Return reportItem.EntityGroup.EntityGroupName
        End If

        If reportItem.Is_MulitpleEntities.GetValueOrDefault(False) Then
            Return "Multiple entity groups"
        End If

        Return "Not configured"
    End Function

    Public Function GetReportTypeName(ByVal reportItem As Report) As String
        If reportItem Is Nothing Then
            Return "Not configured"
        End If

        If reportItem.LineItemType IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(reportItem.LineItemType.LineItemTypeName) Then
            Return reportItem.LineItemType.LineItemTypeName
        End If

        If reportItem.ReportType IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(reportItem.ReportType.ReportTypeName) Then
            Return reportItem.ReportType.ReportTypeName
        End If

        Return "Not configured"
    End Function

    Protected Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        btnSaveColumns_Click(sender, e)
    End Sub
    Protected Async Sub btnSaveColumns_Click(sender As Object, e As EventArgs) Handles btnSaveColumns.Click
        'Report = db.Reports.Single(Function(f) f.ReportID = i)




        Report.Report_Name = Request.Form("Report_Name")
        Report.Sort_Order = CInt(Request.Form("Sort_Order"))
        If Request.Form("Is_MultipleEntities") = "1" Then
            Report.Is_MulitpleEntities = True
            Report.Entity_GroupID = Nothing
        Else
            Report.Is_MulitpleEntities = False
            Report.Entity_GroupID = CInt(Request.Form("EntityGroupID"))
        End If
        If Request.Form("Is_Deleted") = "1" Then
            Report.Is_Deleted = True
        Else
            Report.Is_Deleted = False
        End If
        If Request.Form("ShowMappedItems") = "1" Then
            Report.ShowMappedItems = True
        Else
            Report.ShowMappedItems = False
        End If
        If Report.Is_MulitpleEntities Then
            Dim list As New List(Of Integer)

            For Each ee In Report.ReportEntityGroups
                If CInt(Request.Form("Multipe_EntityGroupID_" & ee.ReportEntityGroupID.ToString)) > 0 Then

                    ee.EntityGroupID = CInt(Request.Form("Multipe_EntityGroupID_" & ee.ReportEntityGroupID))
                Else
                    list.Add(ee.ReportEntityGroupID)
                End If
            Next
            If list.Count > 0 Then
                For Each l In list
                    Dim el = db.ReportEntityGroups.Single(Function(f) f.ReportEntityGroupID = l)
                    db.ReportEntityGroups.Remove(el)
                Next
            End If
        End If

        If CInt(Request.Form("AddMultipleEntities")) > 0 Then
            For i As Integer = 1 To CInt(Request.Form("AddMultipleEntities"))
                Dim re As New ReportEntityGroup
                re.ReportID = Report.ReportID
                re.EntityGroupID = CInt(Request.Form("AddMultiple_EntityGroupID_" & i))
                db.ReportEntityGroups.Add(re)
            Next
        End If
        Await db.SaveChangesAsync()

        If Request.Form("Add_ReportSections_ReportSectionName") <> "" And Request.Form("Add_ReportSections_Sort_Order") <> "" Then
            Dim section As New ReportSection
            section.ReportID = Report.ReportID


            section.ReportSectionName = Request.Form("Add_ReportSections_ReportSectionName")
            section.SortOrder = CInt(Request.Form("Add_ReportSections_Sort_Order"))
            If Request.Form("Add_ReportSections_Show") = "1" Then
                section.Show = True
            Else
                section.Show = False
            End If
            db.ReportSections.Add(section)

            Await db.SaveChangesAsync()


        End If

        'Update Report Columns
        For Each c In Report.Report_Columns
            c.ColumnName = Request.Form("RC_ColumnName_" & c.Report_ColumnID.ToString)

            c.DataType = Request.Form("RC_DateType_" & c.Report_ColumnID.ToString)
            c.Sort_Order = CInt(Request.Form("RC_Sort_Order_" & c.Report_ColumnID.ToString))
            If c.Is_Calculated Then
                ''c.CalculatedByID = CInt(Request.Form("RC_CalculatedByID_" & c.Report_ColumnID.ToString))
                ''c.CalculationValue = CInt(Request.Form("RC_CalculationValue_" & c.Report_ColumnID.ToString))
                ''c.CalculatedFromReportColumnID = CInt(Request.Form("RC_CalculatedFromReportColumnID_" & c.Report_ColumnID.ToString))
            End If
            If Not Request.Form("RC_Show_" & c.Report_ColumnID) Is Nothing Then
                If Request.Form("RC_Show_" & c.Report_ColumnID) = "1" Then
                    c.Show = True

                    For Each item In c.LineItem_Columns
                        item.Show = True


                    Next


                Else
                    c.Show = False
                    For Each item In c.LineItem_Columns
                        item.Show = False


                    Next

                End If
            End If

            'If Not Request.Form("RC_Is_Calculated_" & c.Report_ColumnID) Is Nothing Then
            '    If Request.Form("RC_Is_Calculated_" & c.Report_ColumnID) = "1" Then
            '        c.Is_Calculated = True
            '    Else
            '        c.Is_Calculated = False
            '    End If
            'End If



        Next
        Await db.SaveChangesAsync()

        'Update Report Columns
        For Each c In Report.Report_Columns
            c.DataType = Request.Form("RC_DateType_" & c.Report_ColumnID.ToString)
        Next
        Await db.SaveChangesAsync()




        'Response.Redirect("Builder?report=" & Report.ReportID.ToString)
        'Update Report Line Items
        For Each l In Report.Report_LineItems
            'l.Line_itemName = Request.Form("Line_itemName_" & l.Report_LineItemID.ToString)
            'l.Sort_Order = CInt(Request.Form("Sort_Order_" & l.Report_LineItemID.ToString))
            ''l.ReportSectionID = CInt(Request.Form("ReportSectionID_" & l.Report_LineItemID.ToString))

            'If Not Request.Form("Is_Main_" & l.Report_LineItemID.ToString) Is Nothing Then
            '    If Request.Form("Is_Main_" & l.Report_LineItemID.ToString) = "1" Then
            '        l.Is_Main = True
            '    Else
            '        l.Is_Main = False
            '    End If
            'End If

            'If Not Request.Form("Is_Title_" & l.Report_LineItemID.ToString) Is Nothing Then
            '    If Request.Form("Is_Title_" & l.Report_LineItemID.ToString) = "1" Then
            '        l.Is_Title = True
            '    Else
            '        l.Is_Title = False
            '    End If
            'End If

            'If Not Request.Form("Show_" & l.Report_LineItemID.ToString) Is Nothing Then
            '    If Request.Form("Show_" & l.Report_LineItemID.ToString) = "1" Then
            '        l.Show = True
            '    Else
            '        l.Show = False
            '    End If
            'End If

            'Await db.SaveChangesAsync()
            'Update Line Item Columns
            For Each lc In l.LineItem_Columns

                If lc.Report_Columns.Show Then
                    If Not Request.Form("EntityGroupBasedMapping_" & lc.LineItemColumnID.ToString) Is Nothing Then



                        lc.AggregationTypeID = Long.Parse(Request.Form("AggregationTypeID_" & lc.LineItemColumnID.ToString))

                        If lc.Report_Columns.DataType <> "decimal" Then
                            lc.AggregationTypeID = 3
                        End If
                        lc.CountDataType = Request.Form("CountDataType_" & lc.LineItemColumnID.ToString)
                        lc.CountParameter = Request.Form("CountParameter_" & lc.LineItemColumnID.ToString)
                        lc.TableName = Request.Form("TableName_" & lc.LineItemColumnID.ToString)
                        lc.ColumnName = Request.Form("ColumnName_" & lc.LineItemColumnID.ToString)
                        lc.StoredProc = Request.Form("StoredProc_" & lc.LineItemColumnID.ToString)
                        lc.ProcessTypeID = 1

                        If lc.Is_Static_Value Then
                            lc.Static_Value = Request.Form("Static_Value_" & lc.LineItemColumnID.ToString)
                        End If

                        If Not Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) Is Nothing Then
                            lc.ProcessTypeID = 2
                            If Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) = "Yes" Then
                                lc.Is_CalculatedValue = True

                            Else
                                lc.Is_CalculatedValue = False
                            End If
                        Else
                            lc.Is_CalculatedValue = False
                        End If

                        If Not Request.Form("Is_Static_Value_" & lc.LineItemColumnID.ToString) Is Nothing Then

                            If Request.Form("Is_Static_Value_" & lc.LineItemColumnID.ToString) = "Yes" Then
                                lc.Is_Static_Value = True

                            Else
                                lc.Is_Static_Value = False
                            End If
                        Else
                            lc.Is_Static_Value = False
                        End If

                        If Not Request.Form("Is_Inherited_" & lc.LineItemColumnID.ToString) Is Nothing Then
                            If Request.Form("Is_Inherited_" & lc.LineItemColumnID.ToString) = "1" Then
                                lc.Is_Inherited = True
                                lc.ProcessTypeID = 3
                                If Not Request.Form("Inherited_Lineitem_ColumnID_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                    lc.Inherited_Lineitem_ColumnID = CInt(Request.Form("Inherited_Lineitem_ColumnID_" & lc.LineItemColumnID.ToString))

                                End If

                            Else
                                lc.Is_Inherited = False
                                lc.Inherited_Lineitem_ColumnID = Nothing
                            End If
                        Else
                            lc.Is_Inherited = False
                            lc.Inherited_Lineitem_ColumnID = Nothing
                        End If


                        Try
                            lc.AddMonthToReportingPeriod = CInt(Request.Form("AddMonthToReportingPeriod_" & lc.LineItemColumnID.ToString))
                        Catch ex As Exception
                            lc.AddMonthToReportingPeriod = 0
                        End Try

                        lc.Updated = False

                        If Not Request.Form("EntityGroupBasedMapping_" & lc.LineItemColumnID.ToString) Is Nothing Then
                            If Request.Form("EntityGroupBasedMapping_" & lc.LineItemColumnID.ToString) = "1" Then
                                lc.EntityGroupBasedMapping = True

                                If lc.EntityGroupLineItem_Columns.Count <> lc.Report_LineItems.Report.ReportEntityGroups.Count Then
                                    For Each reg In lc.Report_LineItems.Report.ReportEntityGroups
                                        If lc.EntityGroupLineItem_Columns.Where(Function(ff) ff.EntityGroupID = reg.EntityGroupID).Count = 0 Then
                                            Dim egc As New EntityGroupLineItem_Columns
                                            egc.EntityGroupID = reg.EntityGroupID
                                            egc.LineItemColumnID = lc.LineItemColumnID

                                            lc.EntityGroupLineItem_Columns.Add(egc)

                                        End If
                                    Next
                                End If

                                For Each egc In lc.EntityGroupLineItem_Columns
                                    egc.CountDataType = Request.Form("ECountDataType_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                    egc.CountParameter = Request.Form("ECountParameter_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                    egc.TableName = Request.Form("ETableName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                    egc.ColumnName = Request.Form("EColumnName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                    egc.StoredProc = Request.Form("EStoredProc_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                    egc.Updated = False


                                    Dim Eold As String = egc.SchemaFieldName
                                    egc.SchemaFieldName = Request.Form("ESchemaFieldName_" & egc.EntityGroupLineItem_ColumnID.ToString)

                                    If Not egc.SchemaFieldName Is Nothing Then
                                        If (egc.SchemaFieldName.Contains("+") Or egc.SchemaFieldName.Contains("-") Or egc.SchemaFieldName.Contains("/") Or egc.SchemaFieldName.Contains("*")) And egc.SchemaFieldName.Contains("[") And egc.SchemaFieldName.Contains("]") Then

                                            If Eold <> egc.SchemaFieldName Then
                                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("+", "+,")
                                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("-", "-,")
                                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("/", "/,")
                                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("*", "*,")
                                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("[", "")
                                                egc.SchemaFieldName = egc.SchemaFieldName.Replace("]", "")

                                                Dim fields = egc.SchemaFieldName.Split(",")
                                                egc.SchemaFieldName = Request.Form("ESchemaFieldName_" & egc.EntityGroupLineItem_ColumnID.ToString)
                                                Dim expression As String = ""
                                                Dim count As Integer = 1
                                                For Each fs In fields

                                                    Dim operation As String
                                                    Dim fieldName As String
                                                    If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                                                        operation = fs.Substring(fs.Length - 1)
                                                        fieldName = fs.Substring(0, fs.Length - 1)
                                                    Else
                                                        operation = ""
                                                        fieldName = fs
                                                    End If


                                                    Dim func As String = "dbo.getRLIV_" & lc.Report_LineItemID.ToString & "_" & lc.LineItemColumnID.ToString & "_" & egc.EntityGroupLineItem_ColumnID.ToString & "_" & count.ToString
                                                    Try
                                                        Dim schema = db.Schemata.First(Function(f) f.FieldName = fieldName)
                                                        If Not schema Is Nothing Then
                                                            'lc.SchemaID = schema.SchemaID
                                                            'lc.TableName = schema.DatabaseTable
                                                            'lc.ColumnName = schema.DatabaseField

                                                            db.CreateStoredFunctionsv3(schema.DatabaseTable, schema.DatabaseField, 1, func, lc.Report_Columns.DataType, lc.QueryWhere)
                                                            expression = expression & func & operation

                                                        End If
                                                    Catch ex As Exception

                                                    End Try
                                                    count = count + 1

                                                Next

                                                egc.Is_CalculatedValue = True
                                                egc.Expression = expression

                                            End If




                                        Else

                                            If Not Request.Form("EUpdateSchema_" & egc.EntityGroupLineItem_ColumnID.ToString) Is Nothing Then
                                                Try
                                                    Dim schema = db.Schemata.First(Function(f) f.FieldName = egc.SchemaFieldName)
                                                    If Not schema Is Nothing Then
                                                        'lc.SchemaID = schema.SchemaID
                                                        egc.TableName = schema.DatabaseTable
                                                        egc.ColumnName = schema.DatabaseField




                                                    End If
                                                Catch ex As Exception

                                                End Try
                                            End If

                                            egc.Expression = Request.Form("EExpression_" & egc.EntityGroupLineItem_ColumnID.ToString)

                                            If Not Request.Form("EIs_Calculated_Value_" & egc.EntityGroupLineItem_ColumnID.ToString) Is Nothing Then
                                                If Request.Form("EIs_Calculated_Value_" & egc.EntityGroupLineItem_ColumnID.ToString) = "Yes" Then
                                                    egc.Is_CalculatedValue = True

                                                Else
                                                    egc.Is_CalculatedValue = False
                                                End If
                                            Else
                                                egc.Is_CalculatedValue = False
                                            End If
                                            If Request.Form("EColumnName_" & egc.EntityGroupLineItem_ColumnID.ToString) = "" Or Request.Form("ETableName_" & egc.EntityGroupLineItem_ColumnID.ToString) = "" Then
                                                egc.StoredProc = ""
                                            End If
                                        End If
                                    End If

                                Next

                            Else
                                lc.EntityGroupBasedMapping = False
                            End If
                        End If

                        Dim old As String = lc.SchemaFieldName
                        lc.SchemaFieldName = Request.Form("SchemaFieldName_" & lc.LineItemColumnID.ToString)

                        If Not lc.SchemaFieldName Is Nothing Then
                            If (lc.SchemaFieldName.Contains("+") Or lc.SchemaFieldName.Contains("-") Or lc.SchemaFieldName.Contains("/") Or lc.SchemaFieldName.Contains("*")) And lc.SchemaFieldName.Contains("[") And lc.SchemaFieldName.Contains("]") Then

                                If old <> lc.SchemaFieldName Then
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("+", "+,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("-", "-,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("/", "/,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("*", "*,")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("[", "")
                                    lc.SchemaFieldName = lc.SchemaFieldName.Replace("]", "")

                                    Dim fields = lc.SchemaFieldName.Split(",")
                                    lc.SchemaFieldName = Request.Form("SchemaFieldName_" & lc.LineItemColumnID.ToString)
                                    Dim expression As String = ""
                                    Dim count As Integer = 1
                                    For Each fs In fields

                                        Dim operation As String
                                        Dim fieldName As String
                                        If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                                            operation = fs.Substring(fs.Length - 1)
                                            fieldName = fs.Substring(0, fs.Length - 1)
                                        Else
                                            operation = ""
                                            fieldName = fs
                                        End If


                                        Dim func As String = "dbo.getRLIV_" & lc.Report_LineItemID.ToString & "_" & lc.LineItemColumnID.ToString & "_" & count.ToString
                                        Try
                                            fieldName = fieldName.Replace("<>", "-")
                                            Dim schema = db.Schemata.First(Function(f) f.FieldName = fieldName)
                                            If Not schema Is Nothing Then
                                                'lc.SchemaID = schema.SchemaID
                                                'lc.TableName = schema.DatabaseTable
                                                'lc.ColumnName = schema.DatabaseField

                                                db.CreateStoredFunctionsv3(schema.DatabaseTable, schema.DatabaseField, 1, func, lc.Report_Columns.DataType, lc.QueryWhere)
                                                expression = expression & func & operation

                                            End If
                                        Catch ex As Exception

                                        End Try
                                        count = count + 1

                                    Next

                                    lc.Is_CalculatedValue = True
                                    lc.Expression = expression

                                End If



                            ElseIf lc.SchemaFieldName.StartsWith("RS_SUM_") Then
                                Dim calculation As String = ""
                                If lc.SchemaFieldName = "RS_SUM_COLUMN" Then
                                    For Each cc In lc.Report_Columns.LineItem_Columns.Where(Function(f) f.LineItemColumnID <> lc.LineItemColumnID)

                                        If cc.Is_CalculatedValue Then
                                            If Not cc.Expression Is Nothing And cc.Expression <> "" Then
                                                calculation = calculation & cc.Expression & "+"
                                            End If

                                        Else
                                            If Not cc.StoredProc Is Nothing And cc.StoredProc <> "" Then

                                                calculation = calculation & cc.StoredProc & "+"
                                            End If
                                        End If




                                    Next
                                    If calculation.Length > 0 Then
                                        calculation = calculation.Substring(0, calculation.Length - 1)

                                        lc.Is_CalculatedValue = True
                                        lc.ProcessTypeID = 2
                                        lc.Expression = calculation


                                    End If

                                ElseIf lc.SchemaFieldName = "RS_SUM_ROW" Then

                                End If

                            Else

                                If Not Request.Form("UpdateSchema_" & lc.LineItemColumnID.ToString) Is Nothing Or Not Request.Form("UpdateAllSchema") Is Nothing Then
                                    Try

                                        Dim schema = db.Schemata.First(Function(f) f.FieldName = lc.SchemaFieldName)
                                        If Not schema Is Nothing Then
                                            'lc.SchemaID = schema.SchemaID
                                            lc.TableName = schema.DatabaseTable
                                            lc.ColumnName = schema.DatabaseField




                                        End If



                                    Catch ex As Exception

                                    End Try
                                End If



                                lc.Expression = Request.Form("Expression_" & lc.LineItemColumnID.ToString)

                                If Not Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) Is Nothing Then
                                    If Request.Form("Is_Calculated_Value_" & lc.LineItemColumnID.ToString) = "Yes" Then
                                        lc.Is_CalculatedValue = True
                                        lc.ProcessTypeID = 2
                                    Else
                                        lc.Is_CalculatedValue = False
                                    End If
                                Else
                                    lc.Is_CalculatedValue = False
                                End If
                                If Request.Form("ColumnName_" & lc.LineItemColumnID.ToString) = "" Or Request.Form("TableName_" & lc.LineItemColumnID.ToString) = "" Then
                                    lc.StoredProc = ""
                                End If
                            End If
                        End If

















                        'Console.WriteLine("Column: " & lc.LineItemColumnID.ToString & "<br/>"
                        '   )
                        Await db.SaveChangesAsync()

                    End If

                End If






            Next







            For Each lc In l.LineItem_Columns
                Dim formula As String = ""
                If lc.Is_CalculatedValue Then
                    If Not lc.Expression Is Nothing Or lc.Expression <> "" Then
                        Dim func As String
                        func = lc.Expression
                        func = func.Replace("+", "|")
                        func = func.Replace("-", "|")
                        func = func.Replace("*", "|")
                        func = func.Replace("/", "|")

                        Dim items = func.Split("|")

                        For Each i In items
                            If Not i.StartsWith("dbo") Then
                                Try
                                    func = lc.Expression.Replace(i, getBaseFormula(i, func))
                                Catch ex As Exception

                                End Try

                            End If
                        Next


                        lc.RuntimeExpression = func
                    End If
                End If

                lc.Updated = True
                Await db.SaveChangesAsync()
            Next


        Next


        'Add New Items
        Dim Add As Integer = CInt(Request.Form("Add"))
        If Add > 0 Then
            For i As Integer = 1 To Add
                Dim item As New Report_LineItems
                item.ReportID = Report.ReportID
                item.Line_itemName = Request.Form("Add_LineItem_Name_" & i.ToString)
                item.Sort_Order = CInt(Request.Form("Add_Sort_Order_" & i.ToString))
                item.ReportSectionID = CInt(Request.Form("Add_ReportSectionID_" & i.ToString))
                item.AggregationTypeID = 1

                If Not Request.Form("Add_Is_Main_" & i.ToString) Is Nothing Then
                    If Request.Form("Add_Is_Main_" & i.ToString) = "Yes" OrElse Request.Form("Add_Is_Main_" & i.ToString) = "1" Then
                        item.Is_Main = True
                    Else
                        item.Is_Main = False
                    End If
                End If
                If Not Request.Form("Add_Is_Title_" & i.ToString) Is Nothing Then
                    If Request.Form("Add_Is_Title_" & i.ToString) = "Yes" OrElse Request.Form("Add_Is_Title_" & i.ToString) = "1" Then
                        item.Is_Title = True
                    Else
                        item.Is_Title = False
                    End If
                End If
                If Not Request.Form("Add_Show_" & i.ToString) Is Nothing Then
                    If Request.Form("Add_Show_" & i.ToString) = "Yes" OrElse Request.Form("Add_Show_" & i.ToString) = "1" Then
                        item.Show = True
                    Else
                        item.Show = False
                    End If
                End If

                db.Report_LineItems.Add(item)
                Await db.SaveChangesAsync()

            Next
        End If

        'Add New Column Items
        Dim AddCol As Integer = CInt(Request.Form("AddCol"))
        If AddCol > 0 Then
            For i As Integer = 1 To AddCol
                Dim item As New Report_Columns
                item.ReportID = Report.ReportID
                item.ColumnName = Request.Form("AddCol_ColumnName_" & i.ToString)
                item.DataType = "decimal"
                item.Sort_Order = CInt(Request.Form("AddCol_Sort_Order_" & i.ToString))

                item.ReportSectionID = CInt(Request.Form("AddCol_ReportSectionID_" & i.ToString))

                If Not Request.Form("AddCol_Show_" & i.ToString) Is Nothing Then
                    If Request.Form("AddCol_Show_" & i.ToString) = "1" Then
                        item.Show = True
                    Else
                        item.Show = False
                    End If
                End If
                If Not Request.Form("AddCol_Is_Calculated_" & i.ToString) Is Nothing Then
                    If Request.Form("AddCol_Is_Calculated_" & i.ToString) = "1" Then
                        item.Is_Calculated = True
                    Else
                        item.Is_Calculated = False
                    End If
                End If

                db.Report_Columns.Add(item)
                Await db.SaveChangesAsync()

            Next
        End If

        ''Update Item Mass
        For Each l In Report.Report_LineItems
            If Not Request.Form("Item_Is_Calculated_Value_" & l.Report_LineItemID.ToString) Is Nothing Then
                If Request.Form("Item_Is_Calculated_Value_" & l.Report_LineItemID.ToString) = "Yes" Then
                    l.Is_Calculated_Value = True
                    For Each lc In l.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)
                        lc.Is_CalculatedValue = True
                    Next
                    If Not Request.Form("ExpressionAll_" & l.Report_LineItemID.ToString) Is Nothing And Request.Form("ExpressionAll_" & l.Report_LineItemID.ToString) <> "" Then
                        For Each lc In l.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)

                            Dim originalFormula = Request.Form("ExpressionAll_" & l.Report_LineItemID.ToString)
                            Dim columnFormula = Request.Form("ExpressionAll_" & l.Report_LineItemID.ToString)
                            Dim formalaItems = Request.Form("ExpressionAll_" & l.Report_LineItemID.ToString)

                            formalaItems = formalaItems.Replace("+", ",")
                            formalaItems = formalaItems.Replace("-", ",")
                            formalaItems = formalaItems.Replace("/", ",")
                            formalaItems = formalaItems.Replace("*", ",")

                            For Each i In formalaItems.Split(",")
                                Try
                                    Dim id = CInt(i)
                                    Dim rl = db.Report_LineItems.Single(Function(f) f.Report_LineItemID = id)

                                    For Each rlc In rl.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)

                                        If rlc.Report_ColumnID = lc.Report_ColumnID Then
                                            If rlc.Is_CalculatedValue Then
                                                columnFormula = columnFormula.Replace(i, rlc.LineItemColumnID.ToString)
                                            Else
                                                columnFormula = columnFormula.Replace(i, rlc.StoredProc)
                                            End If
                                        End If






                                    Next

                                Catch ex As Exception

                                End Try
                            Next
                            lc.Expression = columnFormula

                        Next

                    End If

                ElseIf Request.Form("Item_Is_Calculated_Value_" & l.Report_LineItemID.ToString) = "No" Then
                    l.Is_Calculated_Value = False
                    For Each lc In l.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)
                        lc.Is_CalculatedValue = False
                        lc.Expression = Nothing
                    Next
                End If

            End If
        Next
        Await db.SaveChangesAsync()

        'For Each l In Report.Report_Columns
        '    If Not Request.Form("Column_Is_Calculated_Value_" & l.Report_ColumnID.ToString) Is Nothing Then
        '        If Request.Form("Column_Is_Calculated_Value_" & l.Report_ColumnID.ToString) = "Yes" Then
        '            l.CalculationValue = True
        '            For Each lc In l.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)
        '                lc.Is_CalculatedValue = True
        '            Next
        '            If Not Request.Form("ExpressionAllC_" & l.Report_ColumnID.ToString) Is Nothing And Request.Form("ExpressionAllC_" & l.Report_ColumnID.ToString) <> "" Then
        '                For Each lc In l.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)

        '                    Dim originalFormula = Request.Form("ExpressionAllC_" & l.Report_ColumnID.ToString)
        '                    Dim columnFormula = Request.Form("ExpressionAllC_" & l.Report_ColumnID.ToString)
        '                    Dim formalaItems = Request.Form("ExpressionAllC_" & l.Report_ColumnID.ToString)

        '                    formalaItems = formalaItems.Replace("+", ",")
        '                    formalaItems = formalaItems.Replace("-", ",")
        '                    formalaItems = formalaItems.Replace("/", ",")
        '                    formalaItems = formalaItems.Replace("*", ",")

        '                    For Each i In formalaItems.Split(",")
        '                        Try
        '                            Dim id = CInt(i)
        '                            Dim rl = db.Report_Columns.Single(Function(f) f.Report_ColumnID = id)

        '                            For Each rlc In rl.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)

        '                                If rlc.Report_ColumnID = lc.Report_ColumnID Then
        '                                    If rlc.Is_CalculatedValue Then
        '                                        columnFormula = columnFormula.Replace(i, rlc.LineItemColumnID.ToString)
        '                                    Else
        '                                        columnFormula = columnFormula.Replace(i, rlc.StoredProc)
        '                                    End If
        '                                End If






        '                            Next

        '                        Catch ex As Exception

        '                        End Try
        '                    Next
        '                    lc.Expression = columnFormula

        '                Next

        '            End If

        '        ElseIf Request.Form("Column_Is_Calculated_Value_" & l.Report_ColumnID.ToString) = "No" Then
        '            l.Is_Calculated = False
        '            For Each lc In l.LineItem_Columns.Where(Function(f) f.Report_Columns.Show)
        '                lc.Is_CalculatedValue = False
        '                lc.Expression = Nothing
        '            Next
        '        End If
        '    End If

        '    If Not Request.Form("Column_Is_Static_Value_" & l.Report_ColumnID.ToString) Is Nothing Then
        '        If Request.Form("Column_Is_Static_Value_" & l.Report_ColumnID.ToString) = "Yes" Then
        '            l.Is_Static_Value = True
        '            For Each lc In l.LineItem_Columns
        '                lc.Is_Static_Value = True
        '            Next
        '        ElseIf Request.Form("Column_Is_Static_Value_" & l.Report_ColumnID.ToString) = "No" Then
        '            l.Is_Static_Value = False
        '            For Each lc In l.LineItem_Columns
        '                lc.Is_Static_Value = False
        '            Next
        '        End If
        '    End If

        'Next
        For Each l In Report.Report_LineItems
            l.Updated = False
        Next
        db.SaveChanges()
        For Each l In Report.Report_LineItems
            l.Updated = True
        Next
        db.SaveChanges()

        Await db.SaveChangesAsync()
        'Report = db.Reports.Single(Function(f) f.ReportID = i)
        'Response.Redirect("Builder?report=" & Report.ReportID.ToString & "&formset=" & Request.Form("formset"))
    End Sub

    Public Function getBaseFormula(ByVal item As String, ByVal formula As String) As String

        Try
            Dim id = CInt(item)
            Dim i = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)

            If i.Is_CalculatedValue Then
                If Not i.Expression Is Nothing Or i.Expression <> "" Then
                    Dim func = i.Expression
                    func = func.Replace("+", "|")
                    func = func.Replace("-", "|")
                    func = func.Replace("*", "|")
                    func = func.Replace("/", "|")

                    Dim items = func.Split("|")

                    For Each ii In items
                        If Not ii.StartsWith("dbo") Then
                            formula = i.Expression.Replace(ii, getBaseFormula(ii, formula))
                        End If
                    Next
                End If

            Else
                formula = formula.Replace(item, i.StoredProc)
            End If



        Catch ex As Exception

        End Try

        Return formula

    End Function
    Protected Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click

        btnSaveColumns_Click(sender, e)
    End Sub
    Protected Async Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click





        For Each i In Report.Report_LineItems
            For Each ii In i.LineItem_Columns
                ii.Updated = False
                For Each iii In ii.EntityGroupLineItem_Columns
                    iii.Updated = False

                Next
                'If Not ii.SchemaFieldName Is Nothing Then
                '    If (ii.SchemaFieldName.Contains("+") Or ii.SchemaFieldName.Contains("-") Or ii.SchemaFieldName.Contains("/") Or ii.SchemaFieldName.Contains("*")) Then

                '        If ii.SchemaFieldName.Contains(",") Then

                '        Else
                '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("+", "+,")
                '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("-", "-,")
                '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("/", "/,")
                '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("*", "*,")
                '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("[", "")
                '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("]", "")
                '        End If


                '        Dim fields = ii.SchemaFieldName.Split(",")
                '        'lc.SchemaFieldName = Request.Form("SchemaFieldName_" & lc.LineItemColumnID.ToString)
                '        Dim expression As String = ""
                '        Dim count As Integer = 1
                '        For Each fs In fields

                '            Dim operation As String
                '            Dim fieldName As String
                '            If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                '                operation = fs.Substring(fs.Length - 1)
                '                fieldName = fs.Substring(0, fs.Length - 1)
                '            Else
                '                operation = ""
                '                fieldName = fs
                '            End If


                '            Dim func As String = "dbo.getRLIV_" & ii.Report_LineItemID.ToString & "_" & ii.LineItemColumnID.ToString & "_" & count.ToString
                '            Try
                '                Dim schema = db.Schemata.First(Function(f) f.FieldName = fieldName)
                '                If Not schema Is Nothing Then
                '                    'lc.SchemaID = schema.SchemaID
                '                    'lc.TableName = schema.DatabaseTable
                '                    'lc.ColumnName = schema.DatabaseField

                '                    db.CreateStoredFunctionsv2(schema.DatabaseTable, schema.DatabaseField, 1, func, ii.Report_Columns.DataType)
                '                    expression = expression & func & operation

                '                End If
                '            Catch ex As Exception

                '            End Try
                '            count = count + 1

                '        Next

                '        ii.Is_CalculatedValue = True
                '        ii.Expression = expression
                '    End If

                'End If

            Next
            i.Updated = False

        Next
        Await db.SaveChangesAsync()
        For Each i In Report.Report_LineItems
            For Each ii In i.LineItem_Columns
                ii.Updated = True





                For Each iii In ii.EntityGroupLineItem_Columns
                    iii.Updated = True

                Next
            Next
            i.Updated = True
        Next
        Await db.SaveChangesAsync()
    End Sub

    Protected Async Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click

        For Each r In db.Reports.Where(Function(f) f.Is_Deleted = False And f.Is_Dynamic)

            Try
                Dim db3 As New ReportingSuiteEntities3

                Dim Rp = db3.Reports.Find(r.ReportID)
                For Each i In Rp.Report_LineItems
                    For Each ii In i.LineItem_Columns
                        ii.Updated = False
                        For Each iii In ii.EntityGroupLineItem_Columns
                            iii.Updated = False
                        Next
                        'If Not ii.SchemaFieldName Is Nothing Then
                        '    If (ii.SchemaFieldName.Contains("+") Or ii.SchemaFieldName.Contains("-") Or ii.SchemaFieldName.Contains("/") Or ii.SchemaFieldName.Contains("*")) Then

                        '        If ii.SchemaFieldName.Contains(",") Then

                        '        Else
                        '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("+", "+,")
                        '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("-", "-,")
                        '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("/", "/,")
                        '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("*", "*,")
                        '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("[", "")
                        '            ii.SchemaFieldName = ii.SchemaFieldName.Replace("]", "")
                        '        End If


                        '        Dim fields = ii.SchemaFieldName.Split(",")
                        '        'lc.SchemaFieldName = Request.Form("SchemaFieldName_" & lc.LineItemColumnID.ToString)
                        '        Dim expression As String = ""
                        '        Dim count As Integer = 1
                        '        For Each fs In fields

                        '            Dim operation As String
                        '            Dim fieldName As String
                        '            If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                        '                operation = fs.Substring(fs.Length - 1)
                        '                fieldName = fs.Substring(0, fs.Length - 1)
                        '            Else
                        '                operation = ""
                        '                fieldName = fs
                        '            End If


                        '            Dim func As String = "dbo.getRLIV_" & ii.Report_LineItemID.ToString & "_" & ii.LineItemColumnID.ToString & "_" & count.ToString
                        '            Try
                        '                Dim schema = db.Schemata.First(Function(f) f.FieldName = fieldName)
                        '                If Not schema Is Nothing Then
                        '                    'lc.SchemaID = schema.SchemaID
                        '                    'lc.TableName = schema.DatabaseTable
                        '                    'lc.ColumnName = schema.DatabaseField

                        '                    db.CreateStoredFunctionsv2(schema.DatabaseTable, schema.DatabaseField, 1, func, ii.Report_Columns.DataType)
                        '                    expression = expression & func & operation

                        '                End If
                        '            Catch ex As Exception

                        '            End Try
                        '            count = count + 1

                        '        Next

                        '        ii.Is_CalculatedValue = True
                        '        ii.Expression = expression
                        '    End If

                        'End If

                    Next
                    i.Updated = False

                Next
                Await db3.SaveChangesAsync()
                For Each i In Report.Report_LineItems
                    For Each ii In i.LineItem_Columns
                        ii.Updated = True
                        For Each iii In ii.EntityGroupLineItem_Columns
                            iii.Updated = True

                        Next
                    Next
                    i.Updated = True
                Next
                Await db3.SaveChangesAsync()
            Catch ex As Exception
                Response.Write(ex.Message)
                Response.Write(ex.StackTrace)
            End Try


        Next

    End Sub
    Protected Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click
        Response.Redirect("Builder?report=" & Report.ReportID.ToString)
    End Sub
    Protected Async Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click

        Dim r As New Report
        r.Report_Name = Request.Form("New_Report_Name")
        r.Entity_GroupID = CInt(Request.Form("New_EnityGroupID"))
        r.Sort_Order = CInt(Request.Form("New_Sort_Order"))
        r.Is_Dynamic = True
        r.ReportTypeID = 2
        r.Is_MulitpleEntities = False
        r.LineItemTypeID = 2

        db.Reports.Add(r)
        Await db.SaveChangesAsync()




        Response.Redirect("Builder?report=" & r.ReportID.ToString)
    End Sub
    Protected Async Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click
        Dim sort As Integer = 1

        For Each l In Report.Report_LineItems.OrderBy(Function(f) f.Sort_Order)
            l.Sort_Order = sort
            sort = sort + 1
        Next
        Await db.SaveChangesAsync()

        Response.Redirect("Builder?report=" & Report.ReportID.ToString)
    End Sub
    Protected Async Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click



    End Sub

    Public Function getReportSectionDropdown() As String
        Dim output As String = ""
        For Each rs In Report.ReportSections.OrderBy(Function(f) f.SortOrder)
            output = output & "<option value=" & Chr(34) & rs.ReportSectionID.ToString & Chr(34) & ">" & rs.ReportSectionName & "</option>"


        Next
        Return output

    End Function
    Public Function getEntityGroupsDropdown() As String
        Dim output As String = ""
        For Each rs In db.EntityGroups
            If Report.ReportEntityGroups.Where(Function(f) f.EntityGroupID = rs.EntityGroupID).Count = 0 Then
                output = output & "<option value=" & Chr(34) & rs.EntityGroupID.ToString & Chr(34) & ">" & rs.EntityGroupName & "</option>"
            End If



        Next
        Return output

    End Function
    Protected Async Sub Button9_Click(sender As Object, e As EventArgs) Handles Button9.Click



        If FileUpload1.HasFile Then
            Dim file = Server.MapPath("~/uploads/").ToString & Path.GetFileName(FileUpload1.FileName).ToString
            FileUpload1.SaveAs(file)

            Dim cvs = IO.File.ReadAllText(file, System.Text.Encoding.ASCII)

            Using MyReader As New Microsoft.VisualBasic.
                        FileIO.TextFieldParser(
                          file)
                MyReader.TextFieldType = FileIO.FieldType.Delimited
                MyReader.SetDelimiters(",")
                Dim currentRow As String()
                While Not MyReader.EndOfData
                    Try
                        currentRow = MyReader.ReadFields()

                        If Not currentRow(0) = "Item" Then
                            Try
                                Dim item As New Report_LineItems

                                item.ReportID = Report.ReportID
                                item.Line_itemName = currentRow(0)
                                item.Is_Main = GetBoolByBit(CInt(currentRow(1)))
                                item.Show = GetBoolByBit(CInt(currentRow(2)))
                                item.Sort_Order = CInt(currentRow(3))
                                item.ReportSectionID = CInt(currentRow(4))
                                item.AggregationTypeID = 1
                                item.Is_Title = False

                                Try
                                    item.SchemaFieldName = currentRow(5)
                                Catch ex As Exception

                                End Try
                                Try
                                    item.Is_Calculated_Value = GetBoolByBit(CInt(currentRow(6)))
                                Catch ex As Exception

                                End Try


                                db.Report_LineItems.Add(item)
                                Await db.SaveChangesAsync()
                            Catch ex As Exception

                            End Try

                        End If


                    Catch ex As Microsoft.VisualBasic.
                  FileIO.MalformedLineException
                        MsgBox("Line " & ex.Message &
        "is not valid and will be skipped.")
                    End Try
                End While
            End Using





        End If




    End Sub


    Protected Async Sub Button11_Click(sender As Object, e As EventArgs) Handles Button11.Click
        For Each li In Report.Report_LineItems
            li.Updated = False
            For Each lc In li.LineItem_Columns
                lc.Updated = False
            Next
        Next
        Await db.SaveChangesAsync()

        If FileUpload2.HasFile Then
            Dim file = Server.MapPath("~/uploads/").ToString & Path.GetFileName(FileUpload2.FileName).ToString
            FileUpload2.SaveAs(file)

            Dim cvs = IO.File.ReadAllText(file, System.Text.Encoding.ASCII)

            Using MyReader As New Microsoft.VisualBasic.
                        FileIO.TextFieldParser(
                          file)
                MyReader.TextFieldType = FileIO.FieldType.Delimited
                MyReader.SetDelimiters(",")
                Dim currentRow As String()
                While Not MyReader.EndOfData
                    Try
                        currentRow = MyReader.ReadFields()

                        If Not currentRow(0) = "Report Column" Then
                            Try

                                Dim id = CInt(currentRow(2))
                                Dim lc = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)
                                Dim old As String = lc.SchemaFieldName
                                lc.SchemaFieldName = currentRow(3)
                                Await db.SaveChangesAsync()


                                If Not lc.SchemaFieldName Is Nothing Then
                                    If (lc.SchemaFieldName.Contains("+") Or lc.SchemaFieldName.Contains("-") Or lc.SchemaFieldName.Contains("/") Or lc.SchemaFieldName.Contains("*")) And lc.SchemaFieldName.Contains("[") And lc.SchemaFieldName.Contains("]") Then

                                        lc.SchemaFieldName = Helper.ProcessSchemaName(lc.SchemaFieldName)


                                        lc.SchemaFieldName = lc.SchemaFieldName.Replace("+", "+,")
                                        lc.SchemaFieldName = lc.SchemaFieldName.Replace("-", "-,")
                                        lc.SchemaFieldName = lc.SchemaFieldName.Replace("/", "/,")
                                        lc.SchemaFieldName = lc.SchemaFieldName.Replace("*", "*,")
                                        lc.SchemaFieldName = lc.SchemaFieldName.Replace("[", "")
                                        lc.SchemaFieldName = lc.SchemaFieldName.Replace("]", "")

                                        Dim fields = lc.SchemaFieldName.Split(",")
                                        lc.SchemaFieldName = currentRow(3)
                                        Dim expression As String = ""
                                        Dim count As Integer = 1
                                        For Each fs In fields

                                            Dim operation As String
                                            Dim fieldName As String
                                            If fs.Contains("+") Or fs.Contains("-") Or fs.Contains("/") Or fs.Contains("*") Then
                                                operation = fs.Substring(fs.Length - 1)
                                                fieldName = fs.Substring(0, fs.Length - 1)
                                            Else
                                                operation = ""
                                                fieldName = fs
                                            End If


                                            Dim func As String = "dbo.getRLIV_" & lc.Report_LineItemID.ToString & "_" & lc.LineItemColumnID.ToString & "_" & count.ToString
                                            Try
                                                fieldName = fieldName.Replace("<>", "-")
                                                fieldName = fieldName.Replace("{}", "/")
                                                'Dim schema = db.Schemata.First(Function(f) f.FieldName = fieldName)
                                                'If Not schema Is Nothing Then
                                                '    'lc.SchemaID = schema.SchemaID
                                                '    'lc.TableName = schema.DatabaseTable
                                                '    'lc.ColumnName = schema.DatabaseField

                                                '    db.CreateStoredFunctionsv2(Helper.ProcessDatabaseTable(schema.DatabaseTable, currentRow(4)), schema.DatabaseField, 1, func, lc.Report_Columns.DataType)
                                                '    expression = expression & func & operation

                                                'End If

                                                fieldName = fieldName.Replace("<>", "-")
                                                fieldName = fieldName.Replace("{}", "/")
                                                Dim schema = db.GetSchemaDatabaseColumnByFieldName(fieldName)
                                                Dim tableName = ""
                                                Dim columnName = ""
                                                If Not schema Is Nothing Then
                                                    For Each s In schema

                                                        Dim path = s.path
                                                        path = path.Replace("//", "/")
                                                        path = path.Substring(0, (path.Length - fieldName.Length) - 1)

                                                        Dim group = db.GetSchemaGroupByPath(path)

                                                        For Each g In group
                                                            tableName = g.table_name
                                                            columnName = s.column_name
                                                        Next


                                                    Next
                                                End If

                                                If tableName <> "" And columnName <> "" Then
                                                    db.CreateStoredFunctionsv3(Helper.ProcessDatabaseTable(tableName, currentRow(4)), columnName, 1, func, lc.Report_Columns.DataType, lc.QueryWhere)
                                                    expression = expression & func & operation
                                                End If





                                            Catch ex As Exception
                                                Try
                                                    fieldName = fieldName.Replace("<>", "-")
                                                    fieldName = fieldName.Replace("{}", "/")
                                                    'Dim schema = db.Schemata.First(Function(f) f.FieldName = fieldName)
                                                    'If Not schema Is Nothing Then
                                                    '    'lc.SchemaID = schema.SchemaID
                                                    '    'lc.TableName = schema.DatabaseTable
                                                    '    'lc.ColumnName = schema.DatabaseField

                                                    '    db.CreateStoredFunctionsv2(schema.DatabaseTable, schema.DatabaseField, 1, func, lc.Report_Columns.DataType)
                                                    '    expression = expression & func & operation

                                                    'End If

                                                    fieldName = fieldName.Replace("<>", "-")
                                                    fieldName = fieldName.Replace("{}", "/")
                                                    Dim schema = db.GetSchemaDatabaseColumnByFieldName(fieldName)
                                                    Dim tableName = ""
                                                    Dim columnName = ""
                                                    If Not schema Is Nothing Then
                                                        For Each s In schema

                                                            Dim path = s.path
                                                            path = path.Replace("//", "/")
                                                            path = path.Substring(0, (path.Length - fieldName.Length) - 1)

                                                            Dim group = db.GetSchemaGroupByPath(path)

                                                            For Each g In group
                                                                tableName = g.table_name
                                                                columnName = s.column_name
                                                            Next


                                                        Next
                                                    End If

                                                    If tableName <> "" And columnName <> "" Then
                                                        db.CreateStoredFunctionsv3(tableName, columnName, 1, func, lc.Report_Columns.DataType, lc.QueryWhere)
                                                        expression = expression & func & operation
                                                    End If

                                                Catch ex1 As Exception

                                                End Try

                                            End Try
                                            count = count + 1

                                        Next

                                        lc.Is_CalculatedValue = True
                                        lc.ProcessTypeID = 2
                                        lc.Expression = expression

                                        Await db.SaveChangesAsync

                                    ElseIf lc.SchemaFieldName.StartsWith("RS_SUM_") Then
                                        Dim calculation As String = ""
                                        If lc.SchemaFieldName = "RS_SUM_COLUMN" Then
                                            For Each cc In lc.Report_Columns.LineItem_Columns.Where(Function(f) f.LineItemColumnID <> lc.LineItemColumnID)

                                                If cc.Is_CalculatedValue Then
                                                    If Not cc.Expression Is Nothing And cc.Expression <> "" Then
                                                        calculation = calculation & cc.Expression & "+"
                                                    End If

                                                Else
                                                    If Not cc.StoredProc Is Nothing And cc.StoredProc <> "" Then

                                                        calculation = calculation & cc.StoredProc & "+"
                                                    End If
                                                End If




                                            Next
                                            If calculation.Length > 0 Then
                                                calculation = calculation.Substring(0, calculation.Length - 1)

                                                lc.Is_CalculatedValue = True
                                                lc.ProcessTypeID = 2
                                                lc.Expression = calculation


                                            End If

                                        ElseIf lc.SchemaFieldName = "RS_SUM_ROW" Then

                                        End If
                                        Await db.SaveChangesAsync
                                    Else

                                        Try
                                            ''Dim schema = db.Schemata.First(Function(f) f.FieldName = lc.SchemaFieldName)
                                            'If Not schema Is Nothing Then
                                            '    'lc.SchemaID = schema.SchemaID

                                            'End If
                                            'Dim schemas = db.GetSchemaDatabaseColumnByFieldName(lc.SchemaFieldName)
                                            'For Each s In schemas
                                            '    Dim path = s.path
                                            '    path = path.Replace("//", "/")
                                            '    path = path.Substring(0, (path.Length - lc.SchemaFieldName.Length) - 1)

                                            '    Dim group = db.GetSchemaGroupByPath(path)

                                            '    For Each g In group
                                            '        lc.TableName = g.table_name
                                            '        lc.ColumnName = s.column_name
                                            '    Next

                                            'Next

                                            Dim schema = db.GetSchemaDatabaseColumnByFieldName(lc.SchemaFieldName)

                                            If Not schema Is Nothing Then
                                                For Each s In schema

                                                    Dim path = s.path
                                                    path = path.Replace("//", "/")
                                                    path = path.Substring(0, (path.Length - lc.SchemaFieldName.Length) - 1)

                                                    Dim group = db.GetSchemaGroupByPath(path)

                                                    For Each g In group
                                                        lc.TableName = Helper.ProcessDatabaseTable(g.table_name, currentRow(4))
                                                        lc.ColumnName = s.column_name
                                                        lc.ProcessTypeID = 1

                                                    Next


                                                Next
                                            End If

                                            Await db.SaveChangesAsync()


                                        Catch ex As Exception

                                        End Try


                                    End If


                                End If







                            Catch ex As Exception

                            End Try

                        End If


                    Catch ex As Microsoft.VisualBasic.
                  FileIO.MalformedLineException
                        MsgBox("Line " & ex.Message &
        "is not valid and will be skipped.")
                    End Try
                End While
            End Using





        End If


        For Each li In Report.Report_LineItems
            li.Updated = True
            For Each lc In li.LineItem_Columns
                lc.Updated = True
            Next
        Next
        Await db.SaveChangesAsync()

    End Sub

    Protected Async Sub Button10_Click(sender As Object, e As EventArgs) Handles Button10.Click




        For Each rs In Report.ReportSections
            For Each rc In rs.Report_Columns
                If rc.Is_Calculated Then
                    For Each li In rc.LineItem_Columns
                        Try
                            Dim match = db.LineItem_Columns.Single(Function(f) f.Report_LineItemID = li.Report_LineItemID And f.Report_ColumnID = rc.CalculatedFromReportColumnID)
                            li.TableName = match.TableName
                            li.ColumnName = match.ColumnName
                            li.Is_CalculatedValue = match.Is_CalculatedValue
                            li.StoredProc = match.StoredProc
                            li.Expression = match.Expression
                            li.SchemaFieldName = match.SchemaFieldName

                        Catch ex As Exception

                        End Try



                    Next
                End If
            Next
        Next
        Await db.SaveChangesAsync()


    End Sub

    Function GetBoolByBit(ByVal bit As Integer) As Boolean
        If bit = 1 Then
            Return True
        Else
            Return False

        End If
    End Function


End Class

Public Class ReportBuilderWorkspaceInsights
    Public Property ColumnCount As Integer
    Public Property VisibleColumnCount As Integer
    Public Property LineItemCount As Integer
    Public Property VisibleLineItemCount As Integer
    Public Property MappingCount As Integer
    Public Property ConfiguredMappingCount As Integer
    Public Property SectionCount As Integer
    Public Property SecurityRuleCount As Integer
    Public Property VisibleColumnPercent As Integer
    Public Property VisibleLineItemPercent As Integer
    Public Property ConfigurationPercent As Integer
    Public Property MappingPercent As Integer
End Class
