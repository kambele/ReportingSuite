Imports System.Data.Entity
Imports System.Globalization

Public Class AdhocReport
    Inherits Page

    Private ReadOnly _db As New ReportingSuiteEntities3()

    Public Property ReportId As Long
    Public Property SelectedReport As Report
    Public Property AdhocReports As New List(Of Report)()
    Public Property AvailableSections As New List(Of Section)()
    Public Property IsAdminUser As Boolean
    Public Property CreateRequestToken As String = String.Empty
    Public Property LoadError As String = String.Empty
    Public Property PowerBiQueryDefinition As String = String.Empty
    Public Property PowerBiQueryError As String = String.Empty

    Public ReadOnly Property PowerBiViewName As String
        Get
            If ReportId <= 0 Then
                Return String.Empty
            End If

            Return AdhocReportService.GetPowerBiViewName(ReportId)
        End Get
    End Property

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        Dim rawId = If(Request.QueryString("reportId"), String.Empty)
        If rawId.Length > 0 Then
            Dim parsedReportId As Long
            If Not Long.TryParse(rawId, NumberStyles.Integer, CultureInfo.InvariantCulture, parsedReportId) Then
                LoadError = "Select a valid adhoc report."
                Return
            End If
            ReportId = parsedReportId
        End If

        Try
            IsAdminUser = AdhocReportService.IsCurrentUserAdmin(Context)
            If IsAdminUser Then
                AvailableSections = _db.Sections.AsNoTracking().OrderBy(Function(section) section.SectionID).ToList()
                CreateRequestToken = Convert.ToString(Session("AdhocCreateRequestToken"))
                If String.IsNullOrWhiteSpace(CreateRequestToken) Then
                    CreateRequestToken = Guid.NewGuid().ToString("N")
                    Session("AdhocCreateRequestToken") = CreateRequestToken
                End If
            End If

            If ReportId > 0 Then
                SelectedReport = _db.Reports.AsNoTracking().SingleOrDefault(
                    Function(report) report.ReportID = ReportId AndAlso
                                     Not report.Is_Deleted AndAlso
                                     report.AdhocQuery IsNot Nothing AndAlso
                                     report.AdhocQuery.Trim() <> String.Empty)
                If SelectedReport Is Nothing Then
                    LoadError = "The selected adhoc report is unavailable or does not have an AdhocQuery."
                Else
                    Dim denialReason As String = Nothing
                    If Not ReportAccessService.CanAccessReport(Context, ReportId, denialReason) Then
                        Response.StatusCode = 403
                        Response.TrySkipIisCustomErrors = True
                        SelectedReport = Nothing
                        LoadError = denialReason
                    Else
                        Try
                            PowerBiQueryDefinition = AdhocReportService.GetPowerBiQueryDefinition(ReportId)
                        Catch ex As Exception
                            Trace.Warn("AdhocReport", "Unable to generate the Power BI query definition.", ex)
                            PowerBiQueryError =
                                "The Power BI query could not be generated. Validate the Adhoc report schema and warehouse configuration."
                        End Try
                    End If
                End If
            Else
                AdhocReports = _db.Reports.AsNoTracking().
                    Where(Function(report) Not report.Is_Deleted AndAlso
                                           report.AdhocQuery IsNot Nothing AndAlso
                                           report.AdhocQuery.Trim() <> String.Empty).
                    OrderBy(Function(report) report.Report_Name).
                    ToList()
                Dim accessibleReportIds As HashSet(Of Long) = ReportAccessService.GetAccessibleReportIds(
                    Context,
                    AdhocReports.Select(Function(report) report.ReportID))
                AdhocReports = AdhocReports.
                    Where(Function(report) accessibleReportIds.Contains(report.ReportID)).
                    ToList()
            End If
        Catch ex As Exception
            Trace.Warn("AdhocReport", "Unable to load the adhoc report catalogue.", ex)
            LoadError = "The adhoc report catalogue could not be loaded. Review the application trace for details."
        End Try
    End Sub

    Public Shared Function GetReportName(ByVal report As Report) As String
        Return _Default.GetReportName(report)
    End Function

    Public Shared Function GetReportDescription(ByVal report As Report) As String
        Return _Default.GetReportDescription(report)
    End Function

    Protected Overrides Sub OnUnload(ByVal e As EventArgs)
        _db.Dispose()
        MyBase.OnUnload(e)
    End Sub
End Class
