Imports ReportingSuite.ReportingSuiteEntities3

Public Class Ajax
    Inherits System.Web.UI.Page

    Public action As String
    Public parameter As String
    Public parameter2 As String

    Public db As New ReportingSuiteEntities3
    Public ers As New VSABEntities
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load

        Try
            action = Request.QueryString("action")
            parameter = Request.QueryString("parameter")
        Catch ex As Exception

        End Try

    End Sub



End Class