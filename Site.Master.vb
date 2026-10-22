Imports ReportingSuite.ReportingSuiteEntities3
Imports System.Globalization
Imports System.IO
Imports System.Text.RegularExpressions

Public Class SiteMaster
    Inherits MasterPage

    Public db As New ReportingSuiteEntities3
    Public user As User
    Public userName As String
    Public IsAdmin As Boolean
    Public UserDisplayName As String = "Authenticated user"
    Public UserInitials As String = "AU"
    Public NavigationSections As New List(Of Section)
    Public HasChartOfAccountsReports As Boolean
    Public HasQuarterlyChartOfAccountsReports As Boolean
    Public HasMonthlyChartOfAccountsReports As Boolean
    Public HasDynamicReports As Boolean
    Public DynamicReportCount As Integer

    Private ReadOnly RegisteredReportUrls As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Private Shared ReadOnly QuarterlyChartOfAccountsReportPaths As String() = {
        "ERS-Reports/OCoA_Quarterly_Returns",
        "ERS-Reports/OCoA_Quarterly_Returns_Per_Entity",
        "ERS-Reports/OCoA_Quarterly_Returns_Extra",
        "ERS-Reports/OCoA_Quarterly_Returns_ExtraAdditional",
        "ERS-Reports/OCoA_Quarterly_Returns_By_Entity",
        "ERS-Reports/OCoA_Quarterly_Returns_Multipe_Entities",
        "ERS-Reports/OCoA_Quarterly_Returns_Multipe_Entities_Per_Entity"
    }

    Private Shared ReadOnly MonthlyChartOfAccountsReportPaths As String() = {
        "ERS-Reports/OCoA_Monthly_Returns_Per_Entity"
    }

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        userName = GetWindowsUserName()
        UserDisplayName = GetFriendlyUserName(userName)
        UserInitials = GetInitials(UserDisplayName)

        Try
            user = db.Users.SingleOrDefault(Function(item) item.Username = userName)
            IsAdmin = user IsNot Nothing AndAlso user.Is_Admin
        Catch
            IsAdmin = False
        End Try

        Try
            NavigationSections = db.Sections.Include("Reports").OrderBy(Function(section) section.SectionName).ToList()
            Dim navigationReportIds As List(Of Long) = NavigationSections.
                SelectMany(Function(section) section.Reports).
                Where(Function(report) Not report.Is_Deleted).
                Select(Function(report) report.ReportID).
                ToList()
            Dim dynamicReportIds As List(Of Long) = db.Reports.
                Where(Function(report) Not report.Is_Deleted AndAlso report.Is_Dynamic).
                Select(Function(report) report.ReportID).
                ToList()
            Dim accessibleReportIds As HashSet(Of Long) =
                ReportAccessService.GetAccessibleReportIds(Context, navigationReportIds.Concat(dynamicReportIds))
            For Each section As Section In NavigationSections
                section.Reports = section.Reports.
                    Where(Function(report) Not report.Is_Deleted AndAlso
                                           Not report.Is_Dynamic AndAlso
                                           accessibleReportIds.Contains(report.ReportID)).
                    ToList()
            Next

            DynamicReportCount = dynamicReportIds.
                Where(Function(reportId) accessibleReportIds.Contains(reportId)).
                Count()
            HasDynamicReports = DynamicReportCount > 0

            NavigationSections = NavigationSections.
                Where(Function(section) section.Reports IsNot Nothing AndAlso
                                        section.Reports.Count > 0).
                OrderBy(Function(section) section.SectionName).
                ToList()
        Catch ex As Exception
            Trace.Warn("Navigation", "Unable to load or authorize the report catalogue.", ex)
            NavigationSections = New List(Of Section)()
            DynamicReportCount = 0
            HasDynamicReports = False
        End Try

        Try
            RegisteredReportUrls.Clear()
            For Each report As Report In NavigationSections.SelectMany(Function(section) section.Reports)
                Dim normalizedPath As String = NormalizeCatalogueReportPath(report.ReportURL)
                If normalizedPath.Length > 0 AndAlso Not RegisteredReportUrls.ContainsKey(normalizedPath) Then
                    RegisteredReportUrls(normalizedPath) = GetReportUrl(report)
                End If
            Next

            HasQuarterlyChartOfAccountsReports =
                QuarterlyChartOfAccountsReportPaths.Any(Function(path) HasRegisteredReport(path))
            HasMonthlyChartOfAccountsReports =
                MonthlyChartOfAccountsReportPaths.Any(Function(path) HasRegisteredReport(path))
            HasChartOfAccountsReports =
                HasQuarterlyChartOfAccountsReports OrElse HasMonthlyChartOfAccountsReports
        Catch ex As Exception
            Trace.Warn("Navigation", "Unable to build the grouped Chart of Accounts navigation.", ex)
            ResetChartOfAccountsNavigation()
        End Try

        If HasChartOfAccountsReports Then
            NavigationSections = NavigationSections.
                Where(Function(section) Not If(section.SectionName, String.Empty).
                    Trim().Equals("Chart of Accounts", StringComparison.OrdinalIgnoreCase)).
                ToList()
        End If
    End Sub

    Private Sub ResetChartOfAccountsNavigation()
        RegisteredReportUrls.Clear()
        HasChartOfAccountsReports = False
        HasQuarterlyChartOfAccountsReports = False
        HasMonthlyChartOfAccountsReports = False
    End Sub

    Public Function HasRegisteredReport(ByVal reportPath As String) As Boolean
        Return RegisteredReportUrls.ContainsKey(NormalizeCatalogueReportPath(reportPath))
    End Function

    Public Function GetRegisteredReportUrl(ByVal reportPath As String) As String
        Dim targetUrl As String = Nothing
        If RegisteredReportUrls.TryGetValue(NormalizeCatalogueReportPath(reportPath), targetUrl) Then
            Return targetUrl
        End If

        Return "#"
    End Function

    Public Function GetPageTitle() As String
        Dim configuredTitle = Convert.ToString(Page.Title).Trim()
        If configuredTitle.Equals("Home Page", StringComparison.OrdinalIgnoreCase) Then
            Return "Dashboard"
        End If

        If configuredTitle.Length > 0 Then
            Return configuredTitle
        End If

        Dim pageName = Path.GetFileNameWithoutExtension(Request.Path)
        Select Case pageName.ToLowerInvariant()
            Case "default"
                Return "Dashboard"
            Case "pf_dashboard"
                Return "Pension Fund Dashboard"
            Case "builder"
                Return "Report Builder"
            Case Else
                Return Regex.Replace(pageName.Replace("_", " "), "(?<=[a-z])([A-Z])", " $1")
        End Select
    End Function

    Public Shared Function IsAdhocReport(ByVal report As Report) As Boolean
        Return report IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(report.AdhocQuery)
    End Function

    Public Shared Function GetReportUrl(ByVal report As Report) As String
        If report Is Nothing Then
            Return "#"
        End If

        If IsAdhocReport(report) Then
            Return "AdhocReport.aspx?reportId=" & report.ReportID.ToString(CultureInfo.InvariantCulture)
        End If

        If report.ReportTypeID.HasValue AndAlso report.ReportTypeID.Value = 3 Then
            Return Convert.ToString(report.ReportURL)
        End If

        Return "Report?report=" & HttpUtility.UrlEncode(Convert.ToString(report.ReportURL)) &
               "&reportId=" & report.ReportID.ToString(CultureInfo.InvariantCulture)
    End Function

    Private Shared Function NormalizeCatalogueReportPath(ByVal value As String) As String
        If String.IsNullOrWhiteSpace(value) Then
            Return String.Empty
        End If

        Return value.Trim().Replace("\", "/").TrimStart("/"c)
    End Function

    Private Function GetWindowsUserName() As String
        If Request.LogonUserIdentity IsNot Nothing AndAlso
           Not String.IsNullOrWhiteSpace(Request.LogonUserIdentity.Name) Then
            Return Request.LogonUserIdentity.Name
        End If

        If Context.User IsNot Nothing AndAlso Context.User.Identity IsNot Nothing Then
            Return Convert.ToString(Context.User.Identity.Name)
        End If

        Return String.Empty
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

    Private Shared Function GetInitials(ByVal displayName As String) As String
        Dim words = displayName.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
        If words.Length = 0 Then
            Return "AU"
        End If

        If words.Length = 1 Then
            Return words(0).Substring(0, Math.Min(2, words(0).Length)).ToUpperInvariant()
        End If

        Return (words(0).Substring(0, 1) & words(words.Length - 1).Substring(0, 1)).ToUpperInvariant()
    End Function

    Protected Overrides Sub OnUnload(ByVal e As EventArgs)
        db.Dispose()
        MyBase.OnUnload(e)
    End Sub
End Class
