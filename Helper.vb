Imports System.Data.SqlClient

Public Class Helper
    Public Shared Function getBaseFormula(ByVal item As String, ByVal formula As String, ByVal db As ReportingSuiteEntities3) As String

        Try
            Dim id = CInt(item)
            Dim i = db.LineItem_Columns.Single(Function(f) f.LineItemColumnID = id)

            If i.Is_CalculatedValue Then
                If Not i.Expression Is Nothing Or i.Expression <> "" Then
                    Dim func = i.Expression
                    func = func.Replace("+", "|")
                    func = func.Replace("-", "|")
                    func = func.Replace("*", "|")
                    func = func.Replace("/", "|")

                    Dim items = func.Split("|")

                    For Each ii In items
                        If Not ii.StartsWith("dbo") Then
                            formula = i.Expression.Replace(ii, getBaseFormula(ii, formula, db))
                        End If
                    Next
                End If

            Else
                formula = formula.Replace(item, i.StoredProc)
            End If



        Catch ex As Exception

        End Try

        Return formula

    End Function

    Public Shared Function ProcessSchemaName(ByVal text As String) As String
        If text.Contains("/") Or text.Contains("-") Then
            text = text.Replace("]+[", "][PLUS][")
            text = text.Replace("]-[", "][MINUS][")
            text = text.Replace("]/[", "][DIVIDE][")
            text = text.Replace("]*[", "][MULTIPLY][")

            text = text.Replace("-", "<>")
            text = text.Replace("/", "{}")

            text = text.Replace("][PLUS][", "]+[")
            text = text.Replace("][MINUS][", "]-[")
            text = text.Replace("][DIVIDE][", "]/[")
            text = text.Replace("][MULTIPLY][", "]*[")
        End If


        Return text
    End Function

    Public Shared Function ProcessDatabaseTable(ByVal table As String, ByVal fixed As String) As String
        If Not fixed Is Nothing And fixed <> "" Then
            Return fixed
        Else
            Return table
        End If
    End Function


    Public Shared Function ExecuteQuery(ByVal query As String) As Boolean
        Dim connectionString = ConfigurationManager.AppSettings.Get("DB")
        Dim conn As New SqlConnection(connectionString)
        Dim exec = False
        Try
            Dim cmd As SqlCommand = New SqlCommand(query, conn)
            cmd = New SqlCommand(query, conn)
            conn.Open()
            Dim exc = cmd.ExecuteNonQuery()
            If exc = 1 Then
                exec = True
            End If
        Catch ex As Exception
        Finally
            conn.Close()
        End Try

        Return exec
    End Function
End Class
