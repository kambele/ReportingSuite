<%@ Page Async="true" Title="" Language="vb" AutoEventWireup="false" MasterPageFile="~/Site.Master" CodeBehind="PF_Dashboard.aspx.vb" Inherits="ReportingSuite.PF_Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="right_col" role="main">
        <div class="">
            <div class="page-title">
                <div class="title_left">
                    <h3>Pension Fund -  Dashboard</h3>
                </div>

                <div class="title_right">
                    <div class="col-md-5 col-sm-5 col-xs-12 form-group pull-right top_search">
                        <div class="input-group">
                            <input type="text" class="form-control" placeholder="Search for...">
                            <span class="input-group-btn">
                                <button class="btn btn-default" type="button">Go!</button>
                            </span>
                        </div>
                    </div>
                </div>
            </div>
            <div class="col-md-12 col-sm-12 col-xs-12">
                <div class="x_panel">
                    <div class="x_title">
                        <h2>Funds at period ( <%=DateTime.Parse(startDate).ToString("dd MMM yyyy") %> to <%=DateTime.Parse(endDate).ToString("dd MMM yyyy") %> ) </h2>
                        <ul class="nav navbar-right panel_toolbox">
                            <li><a href="#" data-toggle="modal" data-target="#new-dashboard" class="btn btn-primary"><i class="fa fa-plus"></i>New Dashboard Dataset</a>

                            </li>
                            <li>
                                <asp:Button OnClick="Button7_Click" ID="Button7" runat="server" Text="Update Funds from ERS" CssClass="btn btn-primary " />
                            </li>
                            <li>
                                <asp:Button OnClick="Button1_Click" ID="Button1" runat="server" Text="Update Submissions" CssClass="btn btn-primary " />
                            </li>
                            <li class="dropdown">
                                <a href="#" class="dropdown-toggle" data-toggle="dropdown" role="button" aria-expanded="false"><i class="fa fa-wrench"></i></a>
                                <ul class="dropdown-menu" role="menu">
                                    <li><a href="#">Settings 1</a>
                                    </li>
                                    <li><a href="#">Settings 2</a>
                                    </li>
                                </ul>
                            </li>
                            <li><a class="close-link"><i class="fa fa-close"></i></a>
                            </li>
                        </ul>

                        <div class="clearfix"></div>
                    </div>
                    <div class="x_content">

                        <table class="table table-striped table-bordered">
                            <thead>
                                <tr>
                                    <th colspan="5">A. CORPORATE DETAILS</th>
                                    <%
                                        For Each st In db.PF_SubmissionTypes
                                    %>
                                    <th colspan="3"><%=st.PF_SubmissionType %></th>
                                    <%
                                        Next
                                    %>
                                    <th colspan="3">G. INDICATORS FOR SUPERVISORY CONCERNS</th>
                                    <th colspan="2"></th>
                                </tr>

                                <tr>
                                    <th>Registration Number</th>
                                    <th style="width: 20%;">Fund name</th>

                                    <th>Principal Officer</th>
                                    <th>Financial Year End</th>
                                    <th>
                                        <asp:Button OnClick="Button3_Click" ID="Button3" runat="server" Text="Save" CssClass="btn btn-primary " /></th>

                                    <%
                                        For Each st In db.PF_SubmissionTypes
                                    %>
                                    <th>L S</th>

                                                                            <th>C S</th>

                                                                            <th>N S</th>
                                    <%
                                        Next
                                    %>

                                    <th>Rating Description</th>
                                    <th>Rating Code</th>
                                    <th>Last Date Reviewed</th>

                                    <th>Portfolio</th>
                                    <th>Interim Alloc</th>
                                </tr>
                            </thead>


                            <tbody>
                                <%

                                    For Each f In db.PF_Funds.Where(Function(f0) f0.Status = status)


                                %>
                                <tr>
                                    <td><%=f.RegNo %></td>
                                    <td><%=f.FundName %></td>
                                    <td><%=f.PrincipalOfficer %></td>
                                    <td>
                                        <%Try
                                        %><%=DateTime.Parse(f.FinYearEnd).ToString("dd MMM") %>
                                        <%
                                            Catch ex As Exception

                                            End Try %>
                                    </td>

                                    <td>
                                        <a href="#" data-toggle="modal" data-target="#edit-dashboard-<%=f.FundID.ToString %>" class="btn btn-primary"><i class="fa fa-edit"></i></a>

                                        <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="edit-dashboard-<%=f.FundID.ToString %>">
                                            <div class="modal-dialog modal-lg">
                                                <div class="modal-content">

                                                    <div class="modal-header">
                                                        <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                                            <span aria-hidden="true">×</span>
                                                        </button>
                                                        <h4 class="modal-title"><%=f.FundName %> </h4>
                                                    </div>
                                                    <div class="modal-body">
                                                        <div class="x_panel">
                                                            <div class="x_title">

                                                                <h4 class="modal-title"><%=f.FundName %> </h4>
                                                                <% Try
                                                                %>
                                                                <h6>Financial Year End: <%=DateTime.Parse(f.FinYearEnd).ToString("dd MMM") %></h6>
                                                                <%
                                                                    Catch ex As Exception

                                                                    End Try %>

                                                                <%
                                                                    For Each st In db.PF_SubmissionTypes
                                                                        Dim v1 As String = ""
                                                                        Dim v2 As String = ""
                                                                        Dim v3 As String = ""


                                                                        Try
                                                                            Dim dd As String = DateTime.Parse(f.FinYearEnd).ToString("dd MMM") & " " & year
                                                                            Dim ddd = Date.Parse(dd)
                                                                            Dim submission = db.PF_Fund_Submissions.Where(Function(ff) ff.FundID = f.FundID And ff.PF_SubmissionTypeID = st.PF_SubmissionTypeID And ff.ReportingDate = ddd).OrderByDescending(Function(ff) ff.ReportingDate).First()


                                                                            If Not submission.LastSubmitted Is Nothing Then
                                                                                v1 = DateTime.Parse(submission.LastSubmitted).ToString("MM/dd/yyyy")
                                                                            End If
                                                                            If Not submission.DateSubmitted Is Nothing Then
                                                                                v2 = DateTime.Parse(submission.DateSubmitted).ToString("MM/dd/yyyy")
                                                                            End If
                                                                            If Not submission.NextDue Is Nothing Then
                                                                                v3 = DateTime.Parse(submission.NextDue).ToString("MM/dd/yyyy")
                                                                            End If



                                                                        Catch ex As Exception

                                                                        End Try
                                                                %>
                                                                <ul class="nav navbar-right panel_toolbox">
                                                                    <li><a class="collapse-link"><i class="fa fa-chevron-up"></i></a>
                                                                    </li>
                                                                    <li class="dropdown">
                                                                        <a href="#" class="dropdown-toggle" data-toggle="dropdown" role="button" aria-expanded="false"><i class="fa fa-wrench"></i></a>
                                                                        <ul class="dropdown-menu" role="menu">
                                                                            <li><a href="#">Settings 1</a>
                                                                            </li>
                                                                            <li><a href="#">Settings 2</a>
                                                                            </li>
                                                                        </ul>
                                                                    </li>
                                                                    <li><a class="close-link"><i class="fa fa-close"></i></a>
                                                                    </li>
                                                                </ul>
                                                                <div class="clearfix"></div>
                                                            </div>
                                                            <div class="x_content form-horizontal form-label-left input_mask">
                                                                <br />

                                                                <table class="table table-striped table-bordered">
                                                                    <thead>

                                                                        <tr>
                                                                            <th colspan="3"><%=st.PF_SubmissionType %></th>
                                                                        </tr>
                                                                        <tr>



                                                                            <th>Last submission</th>

                                                                            <th>Date Submitted</th>

                                                                            <th>Next Due</th>

                                                                        </tr>
                                                                    </thead>
                                                                    <tbody>
                                                                        <tr>
                                                                            <td>


                                                                                <div class="form-group">

                                                                                    <div class="col-md-12 col-sm-12 col-xs-12">
                                                                                        <div class="col-md-11 xdisplay_inputx form-group row has-feedback">
                                                                                            <input value="<%=v1 %>" name="<%=f.FundID.ToString %>_<%=st.PF_SubmissionTypeID %>_Lastsubmitted" type="text" class="form-control has-feedback-left datepicker" aria-describedby="inputSuccess2Status">
                                                                                            <span class="fa fa-calendar-o form-control-feedback left" aria-hidden="true"></span>
                                                                                        </div>



                                                                                    </div>
                                                                                </div>
                                                                            </td>

                                                                            <td>

                                                                                <div class="form-group">

                                                                                    <div class="col-md-12 col-sm-12 col-xs-12">
                                                                                        
                                                                                         <div class="col-md-11 xdisplay_inputx form-group row has-feedback">
                                                                                            <input value="<%=v2 %>" name="<%=f.FundID.ToString %>_<%=st.PF_SubmissionTypeID %>_DateSubmitted" type="text" class="form-control has-feedback-left datepicker" aria-describedby="inputSuccess2Status">
                                                                                            <span class="fa fa-calendar-o form-control-feedback left" aria-hidden="true"></span>
                                                                                        </div>
                                                                                    </div>
                                                                                </div>
                                                                            </td>

                                                                            <td>
                                                                                <div class="form-group">

                                                                                    <div class="col-md-12 col-sm-12 col-xs-12">
                                                                                        <div class="col-md-11 xdisplay_inputx form-group row has-feedback">
                                                                                            <input value="<%=v3 %>" name="<%=f.FundID.ToString %>_<%=st.PF_SubmissionTypeID %>_Nextdue" type="text" class="form-control has-feedback-left datepicker" aria-describedby="inputSuccess2Status">
                                                                                            <span class="fa fa-calendar-o form-control-feedback left" aria-hidden="true"></span>
                                                                                        </div>
                                                                                    </div>
                                                                                </div>

                                                                            </td>
                                                                        </tr>
                                                                    </tbody>



                                                                </table>


                                                                <%
                    Next
                                                                %>
                                                                <table class="table table-striped table-bordered">
                                                                    <thead>

                                                                        <tr>
                                                                            <th colspan="3">G. INDICATORS FOR SUPERVISORY CONCERNS</th>
                                                                        </tr>
                                                                        <tr>

                                                                            <th>Rating</th>

                                                                            <th>Portfolio</th>

                                                                            <th>Interim Alloc</th>

                                                                        </tr>
                                                                    </thead>
                                                                    <tfoot>
                                                                        <tr>
                                                                            <td>
                                                                                <div class="form-group">

                                                                                    <div class="col-md-12 col-sm-12 col-xs-12">


                                                                                        <select class="form-control" name="PF_IndicatorTypeID">
                                                                                            <%For Each e In db.PF_IndicatorTypes
                                                                                            %>
                                                                                            <option value="<%=f.FundID.ToString %>_<%=e.PF_IndicatorTypeID %>"><%=e.RatingDescription
                                                                                            %></option>
                                                                                            <%
                                                                                                Next %>
                                                                                        </select>
                                                                                    </div>
                                                                                </div>
                                                                            </td>
                                                                            <td>
                                                                                <div class="form-group">

                                                                                    <div class="col-md-12 col-sm-12 col-xs-12">
                                                                                        <select class="form-control" name="PF_PortfolioTypeID">
                                                                                            <%For Each e In db.PF_PortfolioTypes
                                                                                            %>
                                                                                            <option value="<%=f.FundID.ToString %>_<%=e.PF_PortfolioTypeID %>"><%=e.PortfolioName
                                                                                            %></option>
                                                                                            <%
                                                                                                Next %>
                                                                                        </select>
                                                                                    </div>
                                                                                </div>
                                                                            </td>
                                                                            <td>
                                                                                <div class="form-group">

                                                                                    <div class="col-md-12 col-sm-12 col-xs-12">
                                                                                        <select class="form-control" name="PF_InterimAllocationTypeID">
                                                                                            <%For Each e In db.PF_InterimAllocationTypes
                                                                                            %>
                                                                                            <option value="<%=f.FundID.ToString %>_<%=e.PF_InterimAllocationTypeID %>"><%=e.InterimAllocation
                                                                                            %></option>
                                                                                            <%
                                                                                                Next %>
                                                                                        </select>
                                                                                    </div>
                                                                                </div>
                                                                            </td>
                                                                        </tr>
                                                                    </tfoot>


                                                                </table>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="modal-footer">
                                                        <button type="button" class="btn btn-default" data-dismiss="modal">Close</button>

                                                        <asp:Button OnClick="Button5_Click" ID="Button2" runat="server" Text="Save" CssClass="btn btn-primary" />
                                                    </div>

                                                </div>
                                            </div>
                                        </div>
                                    </td>


                                    <%
                                        For Each st In db.PF_SubmissionTypes
                                            Dim v1 As String = ""
                                            Dim v2 As String = ""
                                            Dim v3 As String = ""


                                            Try

                                                Dim dd As String = ""
                                                If st.IsFinYearReportingDate Then
                                                    If Not f.FinYearEnd Is Nothing Then
                                                        dd = DateTime.Parse(f.FinYearEnd).ToString("dd MMM") & " " & year
                                                        Dim ddd = Date.Parse(dd)
                                                        Dim submission = db.PF_Fund_Submissions.Where(Function(ff) ff.FundID = f.FundID And ff.PF_SubmissionTypeID = st.PF_SubmissionTypeID And ff.ReportingDate = ddd).OrderByDescending(Function(ff) ff.ReportingDate).First()
                                                        If Not submission.LastSubmitted Is Nothing Then
                                                            v1 = DateTime.Parse(submission.LastSubmitted).ToString("dd MMM<br/>yyyy")
                                                        End If
                                                        If Not submission.DateSubmitted Is Nothing Then
                                                            v2 = DateTime.Parse(submission.DateSubmitted).ToString("dd MMM<br/>yyyy")
                                                        End If
                                                        If Not submission.NextDue Is Nothing Then
                                                            v3 = DateTime.Parse(submission.NextDue).ToString("dd MMM<br/>yyyy")
                                                        End If
                                                    End If

                                                Else
                                                    If getQuarter() = 1 Then
                                                        dd = "31 Mar " & year
                                                    End If
                                                    If getQuarter() = 2 Then
                                                        dd = "30 Jun " & year
                                                    End If
                                                    If getQuarter() = 3 Then
                                                        dd = "30 Sep " & year
                                                    End If
                                                    If getQuarter() = 4 Then
                                                        dd = "31 Dec " & year
                                                    End If
                                                    Dim submission = db.PF_Fund_Submissions.Where(Function(ff) ff.FundID = f.FundID And ff.PF_SubmissionTypeID = st.PF_SubmissionTypeID).OrderByDescending(Function(ff) ff.ReportingDate).First()
                                                    If Not submission.LastSubmitted Is Nothing Then
                                                        v1 = DateTime.Parse(submission.LastSubmitted).ToString("dd MMM<br/>yyyy")
                                                    End If
                                                    If Not submission.DateSubmitted Is Nothing Then
                                                        v2 = DateTime.Parse(submission.DateSubmitted).ToString("dd MMM<br/>yyyy")
                                                    End If
                                                    If Not submission.NextDue Is Nothing Then
                                                        v3 = DateTime.Parse(submission.NextDue).ToString("dd MMM<br/>yyyy")
                                                    End If
                                                End If








                                            Catch ex As Exception

                                            End Try
                            %>
                                    <td>
                                        <%=v1 %></td>

                                    <td><%=v2 %></td>

                                    <td><%=v3 %></td>
                                    <%


                                        Next
                                    %>

                                    <td></td>
                                    <td></td>
                                    <td></td>

                                    <td></td>
                                    <td></td>
                                </tr>
                                <%
                    Next

                                %>
                            </tbody>
                        </table>
                    </div>
                </div>
            </div>

        </div>
    </div>


    <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="new-dashboard">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">

                <div class="modal-header">
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">×</span>
                    </button>
                    <h4 class="modal-title">Create Dashboard Dataset</h4>
                </div>
                <div class="modal-body">
                    <div class="x_panel">
                        <div class="x_title">
                            <h3><small></small></h3>
                            <ul class="nav navbar-right panel_toolbox">
                                <li><a class="collapse-link"><i class="fa fa-chevron-up"></i></a>
                                </li>
                                <li class="dropdown">
                                    <a href="#" class="dropdown-toggle" data-toggle="dropdown" role="button" aria-expanded="false"><i class="fa fa-wrench"></i></a>
                                    <ul class="dropdown-menu" role="menu">
                                        <li><a href="#">Settings 1</a>
                                        </li>
                                        <li><a href="#">Settings 2</a>
                                        </li>
                                    </ul>
                                </li>
                                <li><a class="close-link"><i class="fa fa-close"></i></a>
                                </li>
                            </ul>
                            <div class="clearfix"></div>
                        </div>
                        <div class="x_content form-horizontal form-label-left input_mask">
                            <br />

                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Year</label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <input type="number" class="form-control " name="Period_From">
                                </div>
                            </div>

                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-default" data-dismiss="modal">Close</button>

                    <asp:Button OnClick="Button5_Click" ID="Button5" runat="server" Text="Create Dataset" CssClass="btn btn-primary" />
                </div>

            </div>
        </div>
    </div>
</asp:Content>
