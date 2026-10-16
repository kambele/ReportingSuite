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

        Dim nativeReportUrl1 As String = Nothing
        Dim nativeReportUrl2 As String = Nothing

        ConfigureNativeReportLink(openNativeSsrsLink1, reportServerUrl1, report, nativeReportUrl1)
        ConfigureNativeReportLink(openNativeSsrsLink2, reportServerUrl2, report, nativeReportUrl2)

        Dim embeddedReportUrl As String = SelectEmbeddedReportUrl(nativeReportUrl1, nativeReportUrl2)
        If Not String.IsNullOrWhiteSpace(embeddedReportUrl) Then
            nativeSsrsFrameLiteral.Text = BuildNativeReportFrame(embeddedReportUrl)
        ElseIf IsMixedContentUrl(nativeReportUrl1) OrElse IsMixedContentUrl(nativeReportUrl2) Then
            ShowEmbeddingMessage("The report cannot be embedded because Reporting Suite is using HTTPS while the configured SSRS endpoint is using HTTP. Open the report in Native SSRS, or configure the SSRS URL with HTTPS.")
        Else
            ShowEmbeddingMessage("No valid SSRS report server URL is configured. Review ReportServer1 and ReportServer2 in Web.config.")
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

    Private Sub ConfigureNativeReportLink(ByVal link As System.Web.UI.WebControls.HyperLink,
                                          ByVal reportServerUrl As String,
                                          ByVal reportPath As String,
                                          ByRef nativeReportUrl As String)
        If Not TryBuildNativeReportUrl(reportServerUrl, reportPath, nativeReportUrl) Then
            link.Visible = False
            Return
        End If

        link.NavigateUrl = nativeReportUrl
        link.Attributes("rel") = "noopener"
    End Sub

    Private Shared Function TryBuildNativeReportUrl(ByVal reportServerUrl As String,
                                                    ByVal reportPath As String,
                                                    ByRef nativeReportUrl As String) As Boolean
        nativeReportUrl = Nothing

        Dim reportServerUri As Uri = Nothing
        If String.IsNullOrWhiteSpace(reportServerUrl) OrElse
           Not Uri.TryCreate(reportServerUrl.Trim(), UriKind.Absolute, reportServerUri) OrElse
           (Not reportServerUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) AndAlso
            Not reportServerUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) Then
            Return False
        End If

        Dim cleanReportPath As String = Convert.ToString(reportPath).Trim().TrimStart("/"c)
        If String.IsNullOrWhiteSpace(cleanReportPath) Then
            Return False
        End If

        Dim encodedSegments As New List(Of String)

        For Each segment As String In cleanReportPath.Split("/"c)
            If Not String.IsNullOrWhiteSpace(segment) Then
                encodedSegments.Add(System.Web.HttpUtility.UrlEncode(segment).Replace("+", "%20"))
            End If
        Next

        If encodedSegments.Count = 0 Then
            Return False
        End If

        ' SSRS URL access executes reports directly from the ReportServer endpoint.
        ' /Pages/ReportViewer.aspx belongs to the web portal and is not below /ReportServer.
        Dim reportServerEndpoint As String = reportServerUri.GetLeftPart(UriPartial.Path).TrimEnd("/"c)
        nativeReportUrl = reportServerEndpoint & "?%2F" & String.Join("%2F", encodedSegments) &
                          "&rs:Command=Render&rs:Embed=true&rc:Toolbar=true"
        Return True
    End Function

    Private Function SelectEmbeddedReportUrl(ByVal primaryUrl As String, ByVal secondaryUrl As String) As String
        If Not String.IsNullOrWhiteSpace(primaryUrl) AndAlso Not IsMixedContentUrl(primaryUrl) Then
            Return primaryUrl
        End If

        If Not String.IsNullOrWhiteSpace(secondaryUrl) AndAlso Not IsMixedContentUrl(secondaryUrl) Then
            Return secondaryUrl
        End If

        Return Nothing
    End Function

    Private Function IsMixedContentUrl(ByVal value As String) As Boolean
        If String.IsNullOrWhiteSpace(value) OrElse Not IsSecurePageRequest() Then
            Return False
        End If

        Dim targetUri As Uri = Nothing
        Return Uri.TryCreate(value, UriKind.Absolute, targetUri) AndAlso
               targetUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsSecurePageRequest() As Boolean
        If Request.IsSecureConnection Then
            Return True
        End If

        Dim forwardedProtocol As String = Convert.ToString(Request.Headers("X-Forwarded-Proto"))
        If String.IsNullOrWhiteSpace(forwardedProtocol) Then
            Return False
        End If

        Return forwardedProtocol.Split(","c)(0).Trim().Equals("https", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function BuildNativeReportFrame(ByVal nativeReportUrl As String) As String
        Dim encodedUrl As String = System.Web.HttpUtility.HtmlAttributeEncode(nativeReportUrl)
        Return "<div class=""report-native-frame-host"" data-ssrs-frame-host>" &
               "<div class=""report-native-loading"" data-ssrs-loading role=""status"" aria-live=""polite"">" &
               "<i class=""icon-spinner2 spinner mr-2"" aria-hidden=""true""></i>" &
               "<span data-ssrs-loading-text>Connecting to SSRS and loading the report...</span></div>" &
               "<iframe id=""native-ssrs-frame"" class=""report-native-frame"" title=""Native SSRS report"" src=""" & encodedUrl & """ onload=""this.parentNode.classList.add('is-loaded');"" allowfullscreen></iframe>" &
               "<div class=""alert alert-warning report-native-frame-warning"" data-ssrs-warning role=""alert"" hidden>" &
               "<i class=""icon-warning22 mr-2"" aria-hidden=""true""></i>" &
               "SSRS did not finish loading in this window. Use a Native SSRS button above; if the native page works, check SSRS iframe policy, Windows authentication, and HTTPS configuration." &
               "</div></div>"
    End Function

    Private Sub ShowEmbeddingMessage(ByVal message As String)
        nativeReportMessage.Visible = True
        reportStatusMessage.Text = Server.HtmlEncode(message)
        nativeSsrsFrameLiteral.Visible = False
    End Sub

    Private Sub ShowReportMessage(ByVal message As String)
        nativeReportMessage.Visible = True
        reportStatusMessage.Text = Server.HtmlEncode(message)
        openNativeSsrsLink1.Enabled = False
        openNativeSsrsLink2.Enabled = False
        nativeSsrsFrameLiteral.Visible = False
    End Sub
End Class
