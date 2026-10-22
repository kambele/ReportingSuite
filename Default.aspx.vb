Imports System.Data.Entity
Imports System.Globalization
Imports System.Security.Cryptography
Imports System.Text
Imports System.Web

Public Class _Default
    Inherits Page

    Private ReadOnly _db As New ReportingSuiteEntities3()

    Public Property DashboardSections As New List(Of Section)()
    Public Property TotalReports As Integer
    Public Property AdhocReportCount As Integer
    Public Property CustomReportCount As Integer
    Public Property DynamicReportCount As Integer
    Public Property DynamicReports As New List(Of Report)()
    Public Property DynamicReportGroups As New List(Of DynamicReportGroupViewModel)()
    Public Property DefaultSectionId As Long
    Public Property IsWorkspaceAdministrator As Boolean
    Public Property WorkspaceDisplayName As String = "Authenticated user"
    Public Property WorkspaceFirstName As String = "User"
    Public Property WorkspaceStorageKey As String = "anonymous"

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        Dim accountName = ReportAccessService.GetCurrentWindowsAccount(Context)
        WorkspaceStorageKey = BuildWorkspaceStorageKey(accountName)
        Dim shell = TryCast(Master, SiteMaster)
        If shell IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(shell.UserDisplayName) AndAlso
           Not shell.UserDisplayName.Equals("Authenticated user", StringComparison.OrdinalIgnoreCase) Then
            WorkspaceDisplayName = shell.UserDisplayName
        Else
            WorkspaceDisplayName = GetFriendlyUserName(accountName)
        End If
        WorkspaceFirstName = WorkspaceDisplayName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        If String.IsNullOrWhiteSpace(WorkspaceFirstName) Then
            WorkspaceFirstName = "User"
        End If

        Try
            IsWorkspaceAdministrator = ReportAccessService.IsCurrentUserAdministrator(Context)
        Catch ex As Exception
            Trace.Warn("Workspace", "Unable to determine the workspace administrator state.", ex)
            IsWorkspaceAdministrator = False
        End Try

        Try
            DashboardSections = _db.Sections.
                Include(Function(item) item.Reports).
                OrderBy(Function(item) item.SectionName).
                ToList()

            DynamicReports = _db.Reports.
                Include(Function(item) item.EntityGroup).
                Where(Function(report) Not report.Is_Deleted AndAlso report.Is_Dynamic).
                ToList()

            Dim customReportIds As List(Of Long) = DashboardSections.
                SelectMany(Function(section) section.Reports).
                Where(Function(report) Not report.Is_Deleted AndAlso Not report.Is_Dynamic).
                Select(Function(report) report.ReportID).
                ToList()
            Dim dynamicReportIds As List(Of Long) = DynamicReports.
                Select(Function(report) report.ReportID).
                ToList()
            Dim accessibleReportIds As HashSet(Of Long) =
                ReportAccessService.GetAccessibleReportIds(Context, customReportIds.Concat(dynamicReportIds))
            For Each section As Section In DashboardSections
                section.Reports = section.Reports.
                    Where(Function(report) Not report.Is_Deleted AndAlso
                                           Not report.Is_Dynamic AndAlso
                                           accessibleReportIds.Contains(report.ReportID)).
                    ToList()
            Next

            DynamicReports = DynamicReports.
                Where(Function(report) accessibleReportIds.Contains(report.ReportID)).
                OrderBy(Function(report) GetDynamicReportGroupName(report)).
                ThenBy(Function(report) report.Sort_Order.GetValueOrDefault(Integer.MaxValue)).
                ThenBy(Function(report) GetReportName(report)).
                ToList()

            DashboardSections = DashboardSections.
                Where(Function(section) GetActiveReports(section).Count > 0).
                ToList()

            Dim customReports = DashboardSections.SelectMany(Function(section) GetActiveReports(section)).ToList()
            CustomReportCount = customReports.Count
            DynamicReportCount = DynamicReports.Count
            TotalReports = CustomReportCount + DynamicReportCount
            AdhocReportCount = customReports.Where(Function(report) IsAdhocReport(report)).Count()
            DynamicReportGroups = BuildDynamicReportGroups(DynamicReports)
            DefaultSectionId = If(DashboardSections.Count > 0, DashboardSections(0).SectionID, 0)
        Catch ex As Exception
            Trace.Warn("Dashboard", "Unable to load the report catalogue.", ex)
            DashboardSections = New List(Of Section)()
            DynamicReports = New List(Of Report)()
            DynamicReportGroups = New List(Of DynamicReportGroupViewModel)()
            TotalReports = 0
            AdhocReportCount = 0
            CustomReportCount = 0
            DynamicReportCount = 0
            DefaultSectionId = 0
        End Try
    End Sub

    Public Shared Function GetActiveReports(ByVal section As Section) As List(Of Report)
        If section Is Nothing OrElse section.Reports Is Nothing Then
            Return New List(Of Report)()
        End If

        Return section.Reports.
            Where(Function(report) Not report.Is_Deleted).
            OrderBy(Function(report) report.Sort_Order).
            ThenBy(Function(report) report.Report_Name).
            ToList()
    End Function

    Public Shared Function IsAdhocReport(ByVal report As Report) As Boolean
        Return SiteMaster.IsAdhocReport(report)
    End Function

    Public Shared Function GetReportUrl(ByVal report As Report) As String
        Return SiteMaster.GetReportUrl(report)
    End Function

    Public Shared Function GetReportName(ByVal report As Report) As String
        If report Is Nothing Then
            Return "Report"
        End If
        If Not String.IsNullOrWhiteSpace(report.Report_Title) Then
            Return report.Report_Title
        End If
        If Not String.IsNullOrWhiteSpace(report.Report_Name) Then
            Return report.Report_Name
        End If
        Return "Report " & report.ReportID.ToString()
    End Function

    Public Shared Function GetReportDescription(ByVal report As Report) As String
        If report IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(report.Report_Description) Then
            Return report.Report_Description.Trim()
        End If
        If report IsNot Nothing AndAlso report.Is_Dynamic Then
            Return "Preview the configured columns and line items that make up this dynamic report."
        End If
        Return "Open this report to view the latest available reporting information."
    End Function

    Public Shared Function GetReportTypeLabel(ByVal report As Report) As String
        If report IsNot Nothing AndAlso report.Is_Dynamic Then
            Return "Dynamic template"
        End If
        If IsAdhocReport(report) Then
            Return "Custom query"
        End If
        Return "Custom report"
    End Function

    Public Shared Function GetReportKind(ByVal report As Report) As String
        If report IsNot Nothing AndAlso report.Is_Dynamic Then
            Return "dynamic"
        End If
        Return If(IsAdhocReport(report), "custom adhoc", "custom")
    End Function

    Public Shared Function GetReportIconCssClass(ByVal report As Report) As String
        If report IsNot Nothing AndAlso report.Is_Dynamic Then
            Return "is-dynamic"
        End If
        Return If(IsAdhocReport(report), "is-adhoc", "is-custom")
    End Function

    Public Shared Function GetReportIcon(ByVal report As Report) As String
        If report IsNot Nothing AndAlso report.Is_Dynamic Then
            Return "icon-insert-template"
        End If
        Return If(IsAdhocReport(report), "icon-table2", "icon-file-text2")
    End Function

    Public Shared Function GetPercentage(ByVal value As Integer, ByVal total As Integer) As Integer
        If value <= 0 OrElse total <= 0 Then
            Return 0
        End If

        Return Math.Min(100, Math.Max(1, CInt(Math.Round((CDbl(value) / CDbl(total)) * 100, MidpointRounding.AwayFromZero))))
    End Function

    Public Shared Function GetSectionDescription(ByVal section As Section) As String
        If section IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(section.Description) Then
            Return section.Description.Trim()
        End If
        Return "Reports available to this division."
    End Function

    Public Shared Function GetSearchText(ByVal report As Report) As String
        If report Is Nothing Then
            Return String.Empty
        End If
        Return String.Join(" ", New String() {
            report.Report_Name,
            report.Report_Title,
            report.Report_Description,
            GetDynamicReportGroupName(report),
            GetReportTypeLabel(report)
        }).ToLowerInvariant()
    End Function

    Public Shared Function GetDynamicReportGroupName(ByVal report As Report) As String
        If report IsNot Nothing AndAlso report.EntityGroup IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(report.EntityGroup.EntityGroupName) Then
            Return report.EntityGroup.EntityGroupName.Trim()
        End If
        Return "Other dynamic reports"
    End Function

    Private Shared Function BuildDynamicReportGroups(ByVal reports As IEnumerable(Of Report)) As List(Of DynamicReportGroupViewModel)
        Dim groups As New Dictionary(Of String, DynamicReportGroupViewModel)(StringComparer.OrdinalIgnoreCase)
        For Each report As Report In If(reports, Enumerable.Empty(Of Report)())
            Dim name As String = GetDynamicReportGroupName(report)
            Dim key As String = If(report.Entity_GroupID.HasValue,
                                   report.Entity_GroupID.Value.ToString(CultureInfo.InvariantCulture),
                                   "other")
            If Not groups.ContainsKey(key) Then
                groups(key) = New DynamicReportGroupViewModel With {
                    .GroupKey = key,
                    .GroupName = name,
                    .Reports = New List(Of Report)()
                }
            End If
            groups(key).Reports.Add(report)
        Next

        Return groups.Values.OrderBy(Function(group) group.GroupName).ToList()
    End Function

    Public Shared Function LoadDynamicReportPreview(ByVal reportId As Long) As DynamicReportPreviewResult
        If reportId <= 0 Then
            Throw New ArgumentOutOfRangeException("reportId")
        End If

        Dim context As HttpContext = HttpContext.Current
        ReportAccessService.DemandReportAccess(context, reportId)

        Using database As New ReportingSuiteEntities3()
            Dim report As Report = database.Reports.
                Include(Function(item) item.EntityGroup).
                AsNoTracking().
                SingleOrDefault(Function(item) item.ReportID = reportId AndAlso
                                                Not item.Is_Deleted AndAlso
                                                item.Is_Dynamic)
            If report Is Nothing Then
                Throw New InvalidOperationException("The selected dynamic report template is unavailable.")
            End If

            Dim sectionLookup As Dictionary(Of Long, String) = database.ReportSections.
                AsNoTracking().
                Where(Function(item) item.ReportID.HasValue AndAlso item.ReportID.Value = reportId).
                ToList().
                ToDictionary(Function(item) item.ReportSectionID,
                             Function(item) If(String.IsNullOrWhiteSpace(item.ReportSectionName),
                                               "Report section",
                                               item.ReportSectionName.Trim()))

            Dim columnEntities As List(Of Report_Columns) = database.Report_Columns.
                AsNoTracking().
                Where(Function(item) item.ReportID.HasValue AndAlso item.ReportID.Value = reportId).
                OrderBy(Function(item) item.Sort_Order).
                ThenBy(Function(item) item.Report_ColumnID).
                ToList()

            Dim lineItemEntities As List(Of Report_LineItems) = database.Report_LineItems.
                AsNoTracking().
                Where(Function(item) item.ReportID.HasValue AndAlso item.ReportID.Value = reportId).
                OrderBy(Function(item) item.Sort_Order).
                ThenBy(Function(item) item.Report_LineItemID).
                ToList()

            Dim lineItemIds As List(Of Long) = lineItemEntities.Select(Function(item) item.Report_LineItemID).ToList()
            Dim columnIds As List(Of Long) = columnEntities.Select(Function(item) item.Report_ColumnID).ToList()
            Dim mappedColumnsByLineItem As New Dictionary(Of Long, HashSet(Of Long))()
            If lineItemIds.Count > 0 AndAlso columnIds.Count > 0 Then
                Dim mappings As List(Of LineItem_Columns) = database.LineItem_Columns.
                    AsNoTracking().
                    Where(Function(item) item.Report_LineItemID.HasValue AndAlso
                                          lineItemIds.Contains(item.Report_LineItemID.Value) AndAlso
                                          item.Report_ColumnID.HasValue AndAlso
                                          columnIds.Contains(item.Report_ColumnID.Value)).
                    ToList()
                For Each mapping As LineItem_Columns In mappings
                    Dim lineItemId As Long = mapping.Report_LineItemID.Value
                    If Not mappedColumnsByLineItem.ContainsKey(lineItemId) Then
                        mappedColumnsByLineItem(lineItemId) = New HashSet(Of Long)()
                    End If
                    mappedColumnsByLineItem(lineItemId).Add(mapping.Report_ColumnID.Value)
                Next
            End If

            Dim columns As List(Of DynamicReportColumnPreview) = columnEntities.
                Select(Function(item) New DynamicReportColumnPreview With {
                    .ColumnId = item.Report_ColumnID,
                    .Name = If(String.IsNullOrWhiteSpace(item.ColumnName), "Report value", item.ColumnName.Trim()),
                    .DataType = If(String.IsNullOrWhiteSpace(item.DataType), "Value", item.DataType.Trim()),
                    .SectionName = ResolveReportSectionName(item.ReportSectionID, sectionLookup),
                    .Visible = item.Show,
                    .Calculated = item.Is_Calculated,
                    .StaticValue = item.Is_Static_Value
                }).
                ToList()

            Dim lineItems As List(Of DynamicReportLineItemPreview) = lineItemEntities.
                Select(Function(item) New DynamicReportLineItemPreview With {
                    .LineItemId = item.Report_LineItemID,
                    .Name = If(String.IsNullOrWhiteSpace(item.Line_itemName), "Unnamed line item", item.Line_itemName.Trim()),
                    .SectionName = ResolveReportSectionName(item.ReportSectionID, sectionLookup),
                    .Visible = item.Show.GetValueOrDefault(False),
                    .Main = item.Is_Main,
                    .Title = item.Is_Title.GetValueOrDefault(False),
                    .Calculated = item.Is_Calculated_Value.GetValueOrDefault(False),
                    .StaticValue = item.Is_Static_Value,
                    .MappedColumnIds = ResolveMappedColumnIds(item.Report_LineItemID, mappedColumnsByLineItem)
                }).
                ToList()

            Return New DynamicReportPreviewResult With {
                .ReportId = report.ReportID,
                .Name = GetReportName(report),
                .Description = GetReportDescription(report),
                .EntityGroup = GetDynamicReportGroupName(report),
                .Columns = columns,
                .LineItems = lineItems
            }
        End Using
    End Function

    Private Shared Function ResolveReportSectionName(ByVal reportSectionId As Long?,
                                                     ByVal sectionLookup As IDictionary(Of Long, String)) As String
        If reportSectionId.HasValue AndAlso sectionLookup.ContainsKey(reportSectionId.Value) Then
            Return sectionLookup(reportSectionId.Value)
        End If
        Return "General"
    End Function

    Private Shared Function ResolveMappedColumnIds(ByVal lineItemId As Long,
                                                   ByVal mappingLookup As IDictionary(Of Long, HashSet(Of Long))) As List(Of Long)
        If mappingLookup.ContainsKey(lineItemId) Then
            Return mappingLookup(lineItemId).OrderBy(Function(columnId) columnId).ToList()
        End If
        Return New List(Of Long)()
    End Function

    Private Shared Function GetFriendlyUserName(ByVal accountName As String) As String
        If String.IsNullOrWhiteSpace(accountName) Then
            Return "Authenticated user"
        End If

        Dim shortName = accountName
        Dim separatorIndex = shortName.LastIndexOf("\"c)
        If separatorIndex >= 0 AndAlso separatorIndex < shortName.Length - 1 Then
            shortName = shortName.Substring(separatorIndex + 1)
        End If

        shortName = shortName.Replace(".", " ").Replace("_", " ").Trim()
        Return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(shortName.ToLowerInvariant())
    End Function

    Private Shared Function BuildWorkspaceStorageKey(ByVal accountName As String) As String
        If String.IsNullOrWhiteSpace(accountName) Then
            Return "anonymous"
        End If

        Using algorithm = SHA256.Create()
            Dim hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(accountName.Trim().ToLowerInvariant()))
            Return BitConverter.ToString(hash, 0, 8).Replace("-", String.Empty).ToLowerInvariant()
        End Using
    End Function

    Protected Overrides Sub OnUnload(ByVal e As EventArgs)
        _db.Dispose()
        MyBase.OnUnload(e)
    End Sub
End Class

Public Class DynamicReportGroupViewModel
    Public Property GroupKey As String
    Public Property GroupName As String
    Public Property Reports As List(Of Report)
End Class

Public Class DynamicReportPreviewResult
    Public Property ReportId As Long
    Public Property Name As String
    Public Property Description As String
    Public Property EntityGroup As String
    Public Property Columns As List(Of DynamicReportColumnPreview)
    Public Property LineItems As List(Of DynamicReportLineItemPreview)
End Class

Public Class DynamicReportColumnPreview
    Public Property ColumnId As Long
    Public Property Name As String
    Public Property DataType As String
    Public Property SectionName As String
    Public Property Visible As Boolean
    Public Property Calculated As Boolean
    Public Property StaticValue As Boolean
End Class

Public Class DynamicReportLineItemPreview
    Public Property LineItemId As Long
    Public Property Name As String
    Public Property SectionName As String
    Public Property Visible As Boolean
    Public Property Main As Boolean
    Public Property Title As Boolean
    Public Property Calculated As Boolean
    Public Property StaticValue As Boolean
    Public Property MappedColumnIds As List(Of Long)
End Class
