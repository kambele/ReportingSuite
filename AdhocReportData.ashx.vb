Imports System.Configuration
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Web
Imports System.Web.SessionState
Imports Newtonsoft.Json

Public Class AdhocReportData
    Implements IHttpHandler
    Implements IRequiresSessionState

    Public Sub ProcessRequest(ByVal context As HttpContext) Implements IHttpHandler.ProcessRequest
        context.Response.ContentType = "application/json"
        context.Response.ContentEncoding = System.Text.Encoding.UTF8
        context.Response.Cache.SetCacheability(HttpCacheability.NoCache)
        context.Response.Cache.SetNoStore()
        context.Response.TrySkipIisCustomErrors = True

        Try
            Dim action = Convert.ToString(context.Request("action")).Trim().ToLowerInvariant()
            Select Case action
                Case "create"
                    RequirePost(context)
                    RequireCreateToken(context)
                    If Not AdhocReportService.IsCurrentUserAdmin(context) Then
                        Throw New UnauthorizedAccessException("Only Reporting Suite administrators can create adhoc reports.")
                    End If

                    Dim createRequest As New AdhocReportCreateRequest With {
                        .ReportName = Convert.ToString(context.Request("reportName")),
                        .ReportTitle = Convert.ToString(context.Request("reportTitle")),
                        .SectionId = ReadLong(context.Request("sectionId"), "sectionId"),
                        .AdhocQuery = Convert.ToString(context.Request("adhocQuery"))
                    }
                    WriteJson(context, AdhocReportService.CreateAdhocReport(createRequest))
                Case "edit"
                    RequirePost(context)
                    RequireCreateToken(context)
                    If Not AdhocReportService.IsCurrentUserAdmin(context) Then
                        Throw New UnauthorizedAccessException("Only Reporting Suite administrators can edit adhoc reports.")
                    End If

                    Dim updateRequest As New AdhocReportUpdateRequest With {
                        .ReportId = ReadLong(context.Request("reportId"), "reportId"),
                        .ReportName = Convert.ToString(context.Request("reportName")),
                        .ReportTitle = Convert.ToString(context.Request("reportTitle")),
                        .SectionId = ReadLong(context.Request("sectionId"), "sectionId"),
                        .SortOrder = ReadInteger(context.Request("sortOrder"), 0),
                        .AdhocQuery = Convert.ToString(context.Request("adhocQuery"))
                    }
                    WriteJson(context, AdhocReportService.UpdateAdhocReport(updateRequest))
                Case "schema"
                    Dim reportId = ReadLong(context.Request("reportId"), "reportId")
                    ReportAccessService.DemandReportAccess(context, reportId)
                    WriteJson(context, AdhocReportService.GetMetadata(reportId))
                Case "data"
                    Dim reportId = ReadLong(context.Request("reportId"), "reportId")
                    ReportAccessService.DemandReportAccess(context, reportId)
                    Dim request = New AdhocReportPageRequest With {
                        .ReportId = reportId,
                        .Draw = ReadInteger(context.Request("draw"), 0),
                        .Start = ReadInteger(context.Request("start"), 0),
                        .Length = ReadInteger(context.Request("length"), 25),
                        .Search = Convert.ToString(context.Request("search[value]")),
                        .OrderColumnIndex = ReadInteger(context.Request("order[0][column]"), 0),
                        .OrderDirection = Convert.ToString(context.Request("order[0][dir]")),
                        .Filters = ReadFilters(context.Request("filters"))
                    }
                    WriteJson(context, AdhocReportService.GetPage(request))
                Case "insights"
                    RequirePost(context)
                    Dim reportId = ReadLong(context.Request("reportId"), "reportId")
                    ReportAccessService.DemandReportAccess(context, reportId)
                    Dim insightRequest = New AdhocReportInsightRequest With {
                        .ReportId = reportId,
                        .GroupColumnIndex = ReadInteger(context.Request("groupColumn"), -1),
                        .SecondaryGroupColumnIndex = ReadInteger(context.Request("secondaryGroupColumn"), -1),
                        .MeasureColumnIndex = ReadInteger(context.Request("measureColumn"), -1),
                        .Limit = ReadInteger(context.Request("limit"), 12),
                        .Search = Convert.ToString(context.Request("search")),
                        .Filters = ReadFilters(context.Request("filters"))
                    }
                    WriteJson(context, AdhocReportService.GetInsights(insightRequest))
                Case "distinct"
                    Dim reportId = ReadLong(context.Request("reportId"), "reportId")
                    ReportAccessService.DemandReportAccess(context, reportId)
                    Dim columnIndex = ReadInteger(context.Request("column"), -1)
                    Dim values = AdhocReportService.GetDistinctValues(reportId, columnIndex, Convert.ToString(context.Request("term")))
                    WriteJson(context, New With {.values = values})
                Case "export"
                    RequirePost(context)
                    Dim reportId = ReadLong(context.Request("reportId"), "reportId")
                    ReportAccessService.DemandReportAccess(context, reportId)
                    Dim exportRequest = New AdhocReportPageRequest With {
                        .ReportId = reportId,
                        .Search = Convert.ToString(context.Request("search")),
                        .OrderColumnIndex = ReadInteger(context.Request("orderColumn"), 0),
                        .OrderDirection = Convert.ToString(context.Request("orderDirection")),
                        .Filters = ReadFilters(context.Request("filters"))
                    }
                    Dim exportResult = AdhocReportService.Export(exportRequest, context.Request("format"))
                    context.Response.Clear()
                    context.Response.ContentType = exportResult.ContentType
                    context.Response.AddHeader("Content-Disposition", "attachment; filename=""" & exportResult.FileName & """")
                    context.Response.AddHeader("Content-Length", exportResult.Content.Length.ToString(CultureInfo.InvariantCulture))
                    context.Response.BinaryWrite(exportResult.Content)
                    context.ApplicationInstance.CompleteRequest()
                Case Else
                    Throw New ArgumentException("Select a valid adhoc report action.")
            End Select
        Catch ex As UnauthorizedAccessException
            WriteError(context, 403, ex.Message)
        Catch ex As KeyNotFoundException
            WriteError(context, 404, ex.Message)
        Catch ex As ArgumentException
            WriteError(context, 400, ex.Message)
        Catch ex As ConfigurationErrorsException
            context.Trace.Warn("AdhocReport", "Adhoc report configuration error.", ex)
            WriteError(context, 400, ex.Message)
        Catch ex As SqlException
            context.Trace.Warn("AdhocReport", "Database operation failed while loading an adhoc report.", ex)
            WriteError(context, 500, "The report could not be loaded. Review the saved query and application trace.")
        Catch ex As Exception
            context.Trace.Warn("AdhocReport", "Unexpected error while loading an adhoc report.", ex)
            WriteError(context, 500, "The report could not be loaded. Review the application trace for details.")
        End Try
    End Sub

    Private Shared Function ReadFilters(ByVal value As String) As List(Of AdhocReportColumnFilter)
        If String.IsNullOrWhiteSpace(value) Then
            Return New List(Of AdhocReportColumnFilter)()
        End If

        Try
            Dim filters = JsonConvert.DeserializeObject(Of List(Of AdhocReportColumnFilter))(value)
            Return If(filters, New List(Of AdhocReportColumnFilter)())
        Catch ex As JsonException
            Throw New ArgumentException("The report filters are invalid.", "filters", ex)
        End Try
    End Function

    Private Shared Sub RequirePost(ByVal context As HttpContext)
        If Not String.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) Then
            Throw New ArgumentException("This operation requires POST.")
        End If
    End Sub

    Private Shared Sub RequireCreateToken(ByVal context As HttpContext)
        Dim expected = Convert.ToString(context.Session("AdhocCreateRequestToken"))
        Dim supplied = Convert.ToString(context.Request("requestToken"))
        If expected.Length = 0 OrElse Not String.Equals(expected, supplied, StringComparison.Ordinal) Then
            Throw New UnauthorizedAccessException("The create request has expired. Refresh the page and try again.")
        End If
    End Sub

    Private Shared Function ReadLong(ByVal value As String, ByVal parameterName As String) As Long
        Dim parsed As Long
        If Not Long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) OrElse parsed <= 0 Then
            Throw New ArgumentException("Select a valid adhoc report.", parameterName)
        End If
        Return parsed
    End Function

    Private Shared Function ReadInteger(ByVal value As String, ByVal fallback As Integer) As Integer
        Dim parsed As Integer
        If Integer.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) Then
            Return parsed
        End If
        Return fallback
    End Function

    Private Shared Sub WriteError(ByVal context As HttpContext, ByVal statusCode As Integer, ByVal message As String)
        context.Response.StatusCode = statusCode
        WriteJson(context, New With {.error = message})
    End Sub

    Private Shared Sub WriteJson(ByVal context As HttpContext, ByVal value As Object)
        Dim settings As New JsonSerializerSettings With {
            .StringEscapeHandling = StringEscapeHandling.EscapeHtml,
            .DateFormatHandling = DateFormatHandling.IsoDateFormat
        }
        context.Response.Write(JsonConvert.SerializeObject(value, Formatting.None, settings))
    End Sub

    Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
        Get
            Return False
        End Get
    End Property
End Class
