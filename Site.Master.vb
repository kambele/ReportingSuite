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
            NavigationSections = db.Sections.Include("Reports").OrderBy(Function(section) section.SectionID).ToList()
            Dim navigationReportIds As List(Of Long) = NavigationSections.
                SelectMany(Function(section) section.Reports).
                Where(Function(report) Not report.Is_Deleted).
                Select(Function(report) report.ReportID).
                ToList()
            Dim accessibleReportIds As HashSet(Of Long) =
                ReportAccessService.GetAccessibleReportIds(Context, navigationReportIds)
            For Each section As Section In NavigationSections
                section.Reports = section.Reports.
                    Where(Function(report) Not report.Is_Deleted AndAlso accessibleReportIds.Contains(report.ReportID)).
                    ToList()
            Next
        Catch
            NavigationSections = New List(Of Section)()
        End Try
    End Sub

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
