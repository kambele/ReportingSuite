Imports System.Globalization
Imports System.Web
Imports Newtonsoft.Json

Public Class DynamicReportPreviewData
    Implements IHttpHandler

    Public Sub ProcessRequest(ByVal context As HttpContext) Implements IHttpHandler.ProcessRequest
        context.Response.ContentType = "application/json"
        context.Response.ContentEncoding = System.Text.Encoding.UTF8
        context.Response.Cache.SetCacheability(HttpCacheability.NoCache)
        context.Response.Cache.SetNoStore()
        context.Response.TrySkipIisCustomErrors = True

        Try
            If Not context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) Then
                context.Response.StatusCode = 405
                context.Response.Headers("Allow") = "GET"
                WriteError(context, "Use GET to preview a dynamic report template.")
                Return
            End If

            Dim reportId As Long
            If Not Long.TryParse(context.Request.QueryString("reportId"),
                                 NumberStyles.Integer,
                                 CultureInfo.InvariantCulture,
                                 reportId) OrElse reportId <= 0 Then
                context.Response.StatusCode = 400
                WriteError(context, "Select a valid dynamic report template.")
                Return
            End If

            WriteJson(context, _Default.LoadDynamicReportPreview(reportId))
        Catch ex As UnauthorizedAccessException
            context.Response.StatusCode = 403
            WriteError(context, "You do not have permission to preview this report template.")
        Catch ex As Exception
            context.Trace.Warn("DynamicReportPreview", "Unable to load the dynamic report template.", ex)
            context.Response.StatusCode = 500
            WriteError(context, "The dynamic report template could not be loaded.")
        End Try
    End Sub

    Private Shared Sub WriteJson(ByVal context As HttpContext, ByVal value As Object)
        context.Response.Write(JsonConvert.SerializeObject(value))
    End Sub

    Private Shared Sub WriteError(ByVal context As HttpContext, ByVal message As String)
        WriteJson(context, New With {.error = message})
    End Sub

    Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
        Get
            Return False
        End Get
    End Property
End Class
