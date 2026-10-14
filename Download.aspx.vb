Imports ReportingSuite.ReportingSuiteEntities3

Public Class Download
    Inherits System.Web.UI.Page
    Public action As String
    Public parameter As String
    Public parameter2 As String
    Public db As New ReportingSuiteEntities3
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        Try
            action = Request.QueryString("action")
            parameter = Request.QueryString("parameter")

            If action = "import_template" Then
                Dim rcID As Integer = CInt(Request.QueryString("parameter"))
                Dim rc = db.Report_Columns.Single(Function(f) f.Report_ColumnID = rcID)

                Dim content As String = "Item,IsMain,Show,SortOrder,Section" & vbCrLf & "First Item,1,1,1," & rc.ReportSectionID.ToString & vbCrLf

                Response.ContentType = "text/csv"
                Response.AppendHeader("Content-Disposition", "attachment; filename=Import_" & rc.ReportID.ToString & ".csv")
                Response.Write(content)
                Response.End()

            End If
            If action = "schema_import_template" Then
                Dim rcID As Integer = CInt(Request.QueryString("parameter"))
                Dim r = db.Reports.Single(Function(f) f.ReportID = rcID)

                Dim content As String = "Report Column,Report Item,LineItemColumnID,Schema Name,Database Table" & vbCrLf
                For Each rc In r.Report_Columns
                    For Each rl In r.Report_LineItems

                        Dim lc = db.LineItem_Columns.Single(Function(f) f.Report_ColumnID = rc.Report_ColumnID And f.Report_LineItemID = rl.Report_LineItemID)


                        content = content & rc.ColumnName & "," & rl.Line_itemName & "," & lc.LineItemColumnID & ",," & vbCrLf
                    Next
                Next


                Response.ContentType = "text/csv"
                Response.AppendHeader("Content-Disposition", "attachment; filename=SchemaImport_" & r.ReportID.ToString & ".csv")
                Response.Write(content)
                Response.End()

            End If



        Catch ex As Exception

        End Try
    End Sub

End Class