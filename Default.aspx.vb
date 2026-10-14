Imports System.Data.Entity

Public Class _Default
    Inherits Page

    Private ReadOnly _db As New ReportingSuiteEntities3()

    Public Property DashboardSections As New List(Of Section)()
    Public Property TotalReports As Integer
    Public Property AdhocReportCount As Integer

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        Try
            DashboardSections = _db.Sections.
                Include(Function(item) item.Reports).
                OrderBy(Function(item) item.SectionID).
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

            Dim reports = DashboardSections.SelectMany(Function(section) GetActiveReports(section)).ToList()
            TotalReports = reports.Count
            AdhocReportCount = reports.Where(Function(report) IsAdhocReport(report)).Count()
        Catch ex As Exception
            Trace.Warn("Dashboard", "Unable to load the report catalogue.", ex)
            DashboardSections = New List(Of Section)()
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

    Protected Overrides Sub OnUnload(ByVal e As EventArgs)
        _db.Dispose()
        MyBase.OnUnload(e)
    End Sub
End Class
