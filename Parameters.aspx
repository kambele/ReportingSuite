<%@ Page Async="true" Title="Report Parameters" Language="vb" AutoEventWireup="false" MasterPageFile="~/Site.Master" CodeBehind="Parameters.aspx.vb" Inherits="ReportingSuite.Parameters" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <section class="card" aria-labelledby="parameters-title">
        <div class="card-header header-elements-inline">
            <div>
                <h5 id="parameters-title" class="card-title">Business intelligence report parameters</h5>
                <p class="text-muted mb-0">Maintain the reporting period used by dynamic reports.</p>
            </div>
            <span class="badge badge-light border-left-primary border-left-3 px-3 py-2"><i class="icon-equalizer2 mr-1"></i>Administration</span>
        </div>

        <div class="card-body">
            <div class="table-responsive reporting-table-scroll">
                <table class="table table-striped table-bordered table-hover">
                    <thead>
                        <tr>
                            <th scope="col">Name</th>
                            <th scope="col">Group</th>
                            <th scope="col">Year from</th>
                            <th scope="col">Year to</th>
                            <th scope="col">Quarter from</th>
                            <th scope="col">Quarter to</th>
                        </tr>
                    </thead>
                    <tbody>
                        <% For Each report In db.Reports.Where(Function(item) item.Is_Deleted = False And item.Is_Dynamic = True And item.LineItemTypeID = 2) %>
                        <tr>
                            <th scope="row"><%: report.Report_Name %></th>
                            <% If report.Entity_GroupID IsNot Nothing Then %>
                            <td><%: report.EntityGroup.EntityGroupName %></td>
                            <% Else %>
                            <td><span class="text-muted">Not assigned</span></td>
                            <% End If %>
                            <td><input class="form-control form-control-sm" type="number" name="YearFrom_<%: report.ReportID.ToString() %>" value="<%: report.YearFrom.ToString() %>" aria-label="Year from for <%: report.Report_Name %>" /></td>
                            <td><input class="form-control form-control-sm" type="number" name="YearTo_<%: report.ReportID.ToString() %>" value="<%: report.YearTo.ToString() %>" aria-label="Year to for <%: report.Report_Name %>" /></td>
                            <td><input class="form-control form-control-sm" type="number" min="1" max="4" name="QuarterFrom_<%: report.ReportID.ToString() %>" value="<%: report.QuarterFrom.ToString() %>" aria-label="Quarter from for <%: report.Report_Name %>" /></td>
                            <td><input class="form-control form-control-sm" type="number" min="1" max="4" name="QuarterTo_<%: report.ReportID.ToString() %>" value="<%: report.QuarterTo.ToString() %>" aria-label="Quarter to for <%: report.Report_Name %>" /></td>
                        </tr>
                        <% Next %>
                    </tbody>
                    <tfoot>
                        <tr>
                            <td colspan="2">
                                <asp:Button ID="Button2" runat="server" Text="Update Multiple Entity Views" CssClass="btn btn-light" />
                            </td>
                            <td><input class="form-control form-control-sm" type="number" name="YearFrom_Multiple" value="2020" aria-label="Multiple entity year from" /></td>
                            <td><input class="form-control form-control-sm" type="number" name="YearTo_Multiple" value="<%: Date.Now.Year.ToString() %>" aria-label="Multiple entity year to" /></td>
                            <td><input class="form-control form-control-sm" type="number" min="1" max="4" name="QuarterFrom_Multiple" value="1" aria-label="Multiple entity quarter from" /></td>
                            <td><input class="form-control form-control-sm" type="number" min="1" max="4" name="QuarterTo_Multiple" value="4" aria-label="Multiple entity quarter to" /></td>
                        </tr>
                    </tfoot>
                </table>
            </div>

            <div class="d-flex justify-content-end mt-3">
                <asp:Button ID="Button1" runat="server" Text="Save Changes" CssClass="btn btn-primary" />
            </div>
        </div>
    </section>
</asp:Content>
