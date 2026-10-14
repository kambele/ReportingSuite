Imports ReportingSuite.ReportingSuiteEntities3

Public Class PF_Dashboard
    Inherits System.Web.UI.Page
    Public db As New ReportingSuiteEntities3
    Public db2 As New ReportingSuiteEntities3

    Public startDate As Date
    Public endDate As Date
    Public currentDate As Date
    Public status As String
    Public funds As PF_Funds
    Public year As Integer

    Public submission_date As New SubmissionDates
    Public all As New GetAllSubmissionPacksDates
    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        year = Date.Now.Year
        startDate = Date.Parse("1 Jan " & year.ToString)
        endDate = Date.Parse("31 Dec " & year.ToString)
        status = "Active"
        currentDate = DateTime.Today



    End Sub

    Protected Async Sub Button7_Click(sender As Object, e As EventArgs)
        For Each f In db.GetPFDashboardCompanies
            Dim fund As PF_Funds
            Try
                fund = db.PF_Funds.Single(Function(ff) f.entity_instance_id = ff.entity_instance_id)

                fund.FundName = f.Company_Name
                fund.PrincipalOfficer = f.item_poname
                fund.RegNo = f.License_Number
                fund.Status = f.Company_Status
                Try
                    fund.FinYearEnd = Date.Parse(f.Financial_Year_End)
                Catch ex1 As Exception
                    fund.FinYearEnd = Nothing
                End Try


            Catch ex As Exception
                fund = New PF_Funds
                fund.FundName = f.Company_Name
                fund.entity_instance_id = f.entity_instance_id
                fund.schema_instance_id = f.owner_schema_instance_id
                fund.PrincipalOfficer = f.item_poname
                fund.RegNo = f.License_Number
                fund.Status = f.Company_Status
                Try
                    fund.FinYearEnd = Date.Parse(f.Financial_Year_End)
                Catch ex1 As Exception
                    fund.FinYearEnd = Nothing
                End Try


                db.PF_Funds.Add(fund)





            End Try


        Next
        Await db.SaveChangesAsync()




    End Sub

    Protected Async Sub Button5_Click(sender As Object, e As EventArgs)
        If Not Request.Form("Period_From") Is Nothing Then

            For Each type In db.PF_SubmissionTypes.Where(Function(f) f.PF_SubmissionTypeID <> 3)
                For Each fund In db.PF_Funds

                    If Not fund.FinYearEnd Is Nothing Then
                        Dim dd As String = DateTime.Parse(fund.FinYearEnd).ToString("dd MMM") & " " & Request.Form("Period_From")
                        Dim ddd = Date.Parse(dd)
                        Try

                            Dim submission = db.PF_Fund_Submissions.Where(Function(f) f.FundID = fund.FundID And f.ReportingDate = ddd And f.PF_SubmissionTypeID = type.PF_SubmissionTypeID).OrderByDescending(Function(f) f.ReportingDate).First()
                            submission.NextDue = DateTime.Parse(submission.ReportingDate).AddYears(1)



                        Catch ex As Exception
                            Dim submission = New PF_Fund_Submissions
                            submission.FundID = fund.FundID
                            submission.PF_SubmissionTypeID = type.PF_SubmissionTypeID
                            submission.ReportingDate = ddd
                            submission.Is_ERS = False
                            submission.NextDue = DateTime.Parse(submission.ReportingDate).AddYears(1)

                            db.PF_Fund_Submissions.Add(submission)

                        End Try
                    End If









                Next
            Next


            Await db.SaveChangesAsync()
        End If
    End Sub

    Protected Async Sub Button1_Click(sender As Object, e As EventArgs)
        For Each type In db.PF_SubmissionTypes
            For Each sp In all.GetAllSubmissionPacksDates(startDate, endDate, "3", type.formset_id, "1,2,3,5,6")
                Try
                    Dim sb = db.PF_Fund_Submissions.Single(Function(f) f.submission_pack_instance_id = sp.submission_pack_instance_id)
                    sb.NextDue = DateTime.Parse(sb.ReportingDate).AddMonths(type.MonthNextDue)

                    Dim ss = db.PF_Fund_Submissions.Where(Function(f1) f1.PF_SubmissionTypeID = type.PF_SubmissionTypeID And f1.FundID = sb.FundID And f1.ReportingDate <> sb.ReportingDate).OrderByDescending(Function(f2) f2.ReportingDate).First()
                    sb.LastSubmitted = ss.DateSubmitted
                    sb.DateSubmitted = sp.Submission_Date
                    sb.Is_ERS = True
                Catch ex As Exception
                    Dim sb = New PF_Fund_Submissions

                    sb.ReportingDate = sp.end_date
                    sb.NextDue = DateTime.Parse(sb.ReportingDate).AddMonths(type.MonthNextDue)
                    Dim id = Long.Parse(sp.entity_instance_id)


                    sb.submission_pack_instance_id = sp.submission_pack_instance_id
                    sb.DateSubmitted = sp.Submission_Date

                    sb.FundID = db.PF_Funds.First(Function(f) f.entity_instance_id = id).FundID
                    sb.PF_SubmissionTypeID = type.PF_SubmissionTypeID
                    sb.Is_ERS = True

                    db.PF_Fund_Submissions.Add(sb)

                End Try
            Next
        Next

        Await db.SaveChangesAsync()
    End Sub

    Protected Async Sub Button3_Click(sender As Object, e As EventArgs)
        For Each f In db.PF_Funds.Where(Function(f0) f0.Status = status)
            For Each st In db.PF_SubmissionTypes.Where(Function(ff) ff.PF_SubmissionTypeID <> 3)
                Try
                    Dim dd As String = DateTime.Parse(f.FinYearEnd).ToString("dd MMM") & " " & year
                    Dim ddd = Date.Parse(dd)
                    Dim submission = db.PF_Fund_Submissions.Where(Function(ff) ff.FundID = f.FundID And ff.Is_ERS = 0 And ff.PF_SubmissionTypeID = st.PF_SubmissionTypeID And ff.ReportingDate = ddd).OrderByDescending(Function(ff) ff.ReportingDate).First()



                    submission.LastSubmitted = Request.Form(f.FundID.ToString & "_" & st.PF_SubmissionTypeID.ToString & "_Lastsubmitted")
                    submission.DateSubmitted = Request.Form(f.FundID.ToString & "_" & st.PF_SubmissionTypeID.ToString & "_DateSubmitted")
                    submission.NextDue = Request.Form(f.FundID.ToString & "_" & st.PF_SubmissionTypeID.ToString & "_NextDue")


                Catch ex As Exception

                End Try
            Next
        Next
        Await db.SaveChangesAsync

    End Sub


    Public Function getQuarter() As Integer
        Return Math.Ceiling(DateTime.Today.Month / 3)
    End Function

End Class