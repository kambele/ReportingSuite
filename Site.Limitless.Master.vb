Imports ReportingSuite.ReportingSuiteEntities3

Public Class SiteMaster
    Inherits MasterPage

    Public db As New ReportingSuiteEntities3
    Public user As User
    Public userName As String
    Public IsAdmin As Boolean
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        Try
            userName = Request.LogonUserIdentity.Name
            user = db.Users.Single(Function(f) f.Username = userName)
            If user.Is_Admin Then
                IsAdmin = True
            Else
                IsAdmin = False

            End If

        Catch ex As Exception
            IsAdmin = False
        End Try
    End Sub
End Class