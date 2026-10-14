Public Class ReportPage
    Inherits System.Web.UI.Page

    Public report As String

    Sub Page_Load(ByVal Sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        report = Request.QueryString("report")

        If String.IsNullOrWhiteSpace(report) Then
            ShowReportMessage("No report was specified.")
            Return
        End If

        report = NormalizeReportPath(report)

        Dim configuredReport As ReportingSuite.Report = Nothing
        Try
            Using db As New ReportingSuiteEntities3()
                Dim requestedReportId As Long
                If Long.TryParse(Request.QueryString("reportId"), requestedReportId) AndAlso requestedReportId > 0 Then
                    configuredReport = db.Reports.SingleOrDefault(
                        Function(item) item.ReportID = requestedReportId AndAlso Not item.Is_Deleted)
                    If configuredReport IsNot Nothing AndAlso
                       Not NormalizeReportPath(configuredReport.ReportURL).Equals(report, StringComparison.OrdinalIgnoreCase) Then
                        configuredReport = Nothing
                    End If
                Else
                    configuredReport = db.Reports.
                        Where(Function(item) Not item.Is_Deleted AndAlso item.ReportURL IsNot Nothing).
                        ToList().
                        SingleOrDefault(Function(item) NormalizeReportPath(item.ReportURL).
                            Equals(report, StringComparison.OrdinalIgnoreCase))
                End If
            End Using
        Catch ex As InvalidOperationException
            Trace.Warn("ReportAccess", "More than one report is configured for the same SSRS path.", ex)
            Response.StatusCode = 409
            Response.TrySkipIisCustomErrors = True
            ShowReportMessage("This report path is configured more than once. Ask an administrator to correct the report catalogue.")
            Return
        Catch ex As Exception
            Trace.Warn("ReportAccess", "Unable to resolve the requested report.", ex)
            Response.StatusCode = 503
            Response.TrySkipIisCustomErrors = True
            ShowReportMessage("Report access could not be validated because the Reporting Suite database is unavailable.")
            Return
        End Try

        If configuredReport Is Nothing Then
            Response.StatusCode = 404
            Response.TrySkipIisCustomErrors = True
            ShowReportMessage("The requested report is not registered in the Reporting Suite catalogue.")
            Return
        End If

        Dim denialReason As String = Nothing
        Try
            If Not ReportAccessService.CanAccessReport(Context, configuredReport.ReportID, denialReason) Then
                Response.StatusCode = 403
                Response.TrySkipIisCustomErrors = True
                ShowReportMessage(denialReason)
                Return
            End If
        Catch ex As Exception
            Trace.Warn("ReportAccess", "Unable to authorize the requested report.", ex)
            Response.StatusCode = 503
            Response.TrySkipIisCustomErrors = True
            ShowReportMessage("Report access could not be validated. Review the application trace or contact an administrator.")
            Return
        End Try

        Dim reportServerUrl1 As String = GetAppSetting("ReportServer1")
        If String.IsNullOrWhiteSpace(reportServerUrl1) Then
            reportServerUrl1 = GetAppSetting("ReportServer")
        End If

        Dim reportServerUrl2 As String = GetAppSetting("ReportServer2")

        If String.IsNullOrWhiteSpace(reportServerUrl1) AndAlso String.IsNullOrWhiteSpace(reportServerUrl2) Then
            ShowReportMessage("ReportServer1 and ReportServer2 are not configured in Web.config.")
            Return
        End If

        ConfigureNativeReportLink(openNativeSsrsLink1, reportServerUrl1, report)
        ConfigureNativeReportLink(openNativeSsrsLink2, reportServerUrl2, report)

        If Not String.IsNullOrWhiteSpace(reportServerUrl1) Then
            Dim nativeReportUrl As String = BuildNativeReportUrl(reportServerUrl1, report)
            nativeSsrsFrameLiteral.Text = "<iframe class=""report-native-frame"" title=""Native SSRS Report"" src=""" & System.Web.HttpUtility.HtmlAttributeEncode(nativeReportUrl) & """></iframe>"
        Else
            nativeSsrsFrameLiteral.Visible = False
        End If
    End Sub

    Private Shared Function NormalizeReportPath(ByVal value As String) As String
        Dim normalized As String = Convert.ToString(value).Trim()
        If normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) Then
            normalized = normalized.Substring("http://".Length)
        ElseIf normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            normalized = normalized.Substring("https://".Length)
        End If
        Return normalized.TrimStart("/"c)
    End Function

    Private Shared Function GetAppSetting(ByVal key As String) As String
        Return System.Configuration.ConfigurationManager.AppSettings(key)
    End Function

    Private Sub ConfigureNativeReportLink(ByVal link As System.Web.UI.WebControls.HyperLink, ByVal reportServerUrl As String, ByVal reportPath As String)
        If String.IsNullOrWhiteSpace(reportServerUrl) Then
            link.Visible = False
            Return
        End If

        link.NavigateUrl = BuildNativeReportUrl(reportServerUrl, reportPath)
        link.Attributes("rel") = "noopener"
    End Sub

    Private Shared Function BuildNativeReportUrl(ByVal reportServerUrl As String, ByVal reportPath As String) As String
        Dim cleanReportPath As String = reportPath.Trim().TrimStart("/"c)
        Dim encodedSegments As New List(Of String)

        For Each segment As String In cleanReportPath.Split("/"c)
            If Not String.IsNullOrWhiteSpace(segment) Then
                encodedSegments.Add(System.Web.HttpUtility.UrlEncode(segment))
            End If
        Next

        Return reportServerUrl.TrimEnd("/"c) & "/Pages/ReportViewer.aspx?%2f" & String.Join("%2f", encodedSegments) & "&rs:Command=Render"
    End Function

    Private Sub ShowReportMessage(ByVal message As String)
        nativeReportMessage.Visible = True
        reportStatusMessage.Text = Server.HtmlEncode(message)
        openNativeSsrsLink1.Enabled = False
        openNativeSsrsLink2.Enabled = False
        nativeSsrsFrameLiteral.Visible = False
    End Sub
End Class
