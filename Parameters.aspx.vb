Imports ReportingSuite.ReportingSuiteEntities3

Public Class Parameters
    Inherits System.Web.UI.Page

    Public db As New ReportingSuiteEntities3

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load

    End Sub

    Protected Async Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        For Each r In db.Reports.Where(Function(f) f.Is_Deleted = False And f.Is_Dynamic = True And f.LineItemTypeID = 2)
            r.Updated = False
        Next
        Await db.SaveChangesAsync()
        For Each r In db.Reports.Where(Function(f) f.Is_Deleted = False And f.Is_Dynamic = True And f.LineItemTypeID = 2)
            r.YearFrom = CInt(Request.Form("YearFrom_" & r.ReportID.ToString))
            r.YearTo = CInt(Request.Form("YearTo_" & r.ReportID.ToString))
            r.QuarterFrom = CInt(Request.Form("QuarterFrom_" & r.ReportID.ToString))
            r.QuarterTo = CInt(Request.Form("QuarterTo_" & r.ReportID.ToString))
            r.Updated = True
        Next
        Await db.SaveChangesAsync()
        Dim db2 As New ReportingSuiteEntities3
        For Each r In db.Reports.Where(Function(f) f.Is_Deleted = False And f.Is_Dynamic = True And f.LineItemTypeID = 2)
            Dim name As String = ""
            If r.Is_MulitpleEntities Then
                For Each rr In r.ReportEntityGroups
                    name = "dbo.VIEW_" & rr.EntityGroup.EntityGroupName.Replace(" ", "_").Replace("-", "") & "_" & r.ReportCode
                    db2.UpdateBIViews(r.ReportID, r.YearFrom, r.YearTo, r.QuarterFrom, r.QuarterTo, name, rr.EntityGroupID)
                Next
            Else
                name = "dbo.VIEW_" & r.EntityGroup.EntityGroupName.Replace(" ", "_").Replace("-", "") & "_" & r.ReportCode


                db2.UpdateBIViews(r.ReportID, r.YearFrom, r.YearTo, r.QuarterFrom, r.QuarterTo, name, r.Entity_GroupID)
            End If
        Next
        Await db2.SaveChangesAsync()

        'Response.Redirect("Parameters")
    End Sub


    Protected Async Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click

        For Each r In db.Reports.Where(Function(f) f.Is_Deleted = False And f.Is_Dynamic = True And f.LineItemTypeID = 2)
            Try
                Dim YearFrom = CInt(Request.Form("YearFrom_Multiple"))
                Dim YearTo = CInt(Request.Form("YearTo_Multiple"))
                Dim QuarterFrom = CInt(Request.Form("QuarterFrom_Multiple"))
                Dim QuarterTo = CInt(Request.Form("QuarterTo_Multiple"))

                r.YearFrom = YearFrom
                r.YearTo = YearTo
                r.QuarterFrom = QuarterFrom
                r.QuarterTo = QuarterTo

                Dim entities As String = ""
                For Each er In r.ReportEntityGroups
                    entities = entities & er.EntityGroupID.ToString & ","
                Next
                entities = entities.Substring(0, entities.Length - 1)

                Dim name = r.Report_Name.Replace(" ", "_").Replace("-", "").Replace("(", "").Replace(")", "").Replace(":", "")
                Dim db2 As New ReportingSuiteEntities3
                db2.UpdateBIGroupViewFinal(r.ReportID, YearFrom, YearTo, QuarterFrom, QuarterTo, name, entities)


                db2.SaveChanges()
                db2.Database.Connection.Close()
            Catch ex As Exception

            End Try

        Next

    End Sub
End Class