Imports System.Data.Entity
Imports System.Globalization
Imports System.Security.Cryptography
Imports System.Text

Public Class _Default
    Inherits Page

    Private ReadOnly _db As New ReportingSuiteEntities3()

    Public Property DashboardSections As New List(Of Section)()
    Public Property TotalReports As Integer
    Public Property AdhocReportCount As Integer
    Public Property StandardReportCount As Integer
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

            Dim reportIds As List(Of Long) = DashboardSections.
                SelectMany(Function(section) section.Reports).
                Where(Function(report) Not report.Is_Deleted).
                Select(Function(report) report.ReportID).
                ToList()
            Dim accessibleReportIds As HashSet(Of Long) =
                ReportAccessService.GetAccessibleReportIds(Context, reportIds)
            For Each section As Section In DashboardSections
                section.Reports = section.Reports.
                    Where(Function(report) Not report.Is_Deleted AndAlso accessibleReportIds.Contains(report.ReportID)).
                    ToList()
            Next

            DashboardSections = DashboardSections.
                Where(Function(section) GetActiveReports(section).Count > 0).
                ToList()

            Dim reports = DashboardSections.SelectMany(Function(section) GetActiveReports(section)).ToList()
            TotalReports = reports.Count
            AdhocReportCount = reports.Where(Function(report) IsAdhocReport(report)).Count()
            StandardReportCount = Math.Max(TotalReports - AdhocReportCount, 0)
            DefaultSectionId = If(DashboardSections.Count > 0, DashboardSections(0).SectionID, 0)
        Catch ex As Exception
            Trace.Warn("Dashboard", "Unable to load the report catalogue.", ex)
            DashboardSections = New List(Of Section)()
            TotalReports = 0
            AdhocReportCount = 0
            StandardReportCount = 0
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
        Return "Open this report to view the latest available reporting information."
    End Function

    Public Shared Function GetReportTypeLabel(ByVal report As Report) As String
        If IsAdhocReport(report) Then
            Return "Adhoc query"
        End If
        If report IsNot Nothing AndAlso report.ReportTypeID.HasValue AndAlso report.ReportTypeID.Value = 3 Then
            Return "Direct report"
        End If
        Return "Reporting Services"
    End Function

    Public Shared Function GetReportKind(ByVal report As Report) As String
        Return If(IsAdhocReport(report), "adhoc", "standard")
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
        Return String.Join(" ", New String() {report.Report_Name, report.Report_Title, report.Report_Description}).ToLowerInvariant()
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
