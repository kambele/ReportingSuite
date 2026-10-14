<%@ Page Title="Dashboard" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.vb" Inherits="ReportingSuite._Default" %>

<asp:Content ID="Head" ContentPlaceHolderID="HeadContent" runat="server">
    <meta name="description" content="Reporting Suite dashboard and division report catalogue" />
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="dashboard-workspace">
        <div class="card dashboard-tabs-card">
            <div class="card-header header-elements-inline">
                <div>
                    <h5 class="card-title">Reporting workspace</h5>
                    <span class="text-muted">Open the dashboard or browse reports by division.</span>
                </div>
                <div class="header-elements">
                    <div class="form-group form-group-feedback form-group-feedback-right mb-0 dashboard-report-search">
                        <input id="dashboard-report-search" type="search" class="form-control" placeholder="Find a report" aria-label="Find a report" />
                        <div class="form-control-feedback"><i class="icon-search4 text-muted"></i></div>
                    </div>
                </div>
            </div>

            <div class="dashboard-tab-strip" role="navigation" aria-label="Dashboard and divisions">
                <ul class="nav nav-tabs nav-tabs-highlight mb-0" role="tablist">
                    <li class="nav-item"><a href="#dashboard-panel" class="nav-link active" data-toggle="tab" role="tab"><i class="icon-home4 mr-2"></i>Dashboard</a></li>
                    <% For Each section In DashboardSections %>
                    <li class="nav-item"><a href="#division-<%: section.SectionID %>" class="nav-link" data-toggle="tab" role="tab"><i class="icon-folder mr-2"></i><%: section.SectionName %><span class="badge badge-light badge-pill ml-2"><%: GetActiveReports(section).Count %></span></a></li>
                    <% Next %>
                </ul>
            </div>

            <div class="tab-content">
                <div class="tab-pane fade show active" id="dashboard-panel" role="tabpanel">
                    <div class="card-body border-bottom">
                        <div class="row">
                            <div class="col-sm-6 col-xl-3 mb-3 mb-xl-0">
                                <div class="dashboard-stat bg-primary text-white">
                                    <div><span class="dashboard-stat-value"><%: TotalReports %></span><span class="dashboard-stat-label">Available reports</span></div>
                                    <i class="icon-stack-text"></i>
                                </div>
                            </div>
                            <div class="col-sm-6 col-xl-3 mb-3 mb-xl-0">
                                <div class="dashboard-stat bg-info text-white">
                                    <div><span class="dashboard-stat-value"><%: DashboardSections.Count %></span><span class="dashboard-stat-label">Divisions</span></div>
                                    <i class="icon-office"></i>
                                </div>
                            </div>
                            <div class="col-sm-6 col-xl-3 mb-3 mb-sm-0">
                                <div class="dashboard-stat bg-success text-white">
                                    <div><span class="dashboard-stat-value"><%: AdhocReportCount %></span><span class="dashboard-stat-label">Adhoc reports</span></div>
                                    <i class="icon-table2"></i>
                                </div>
                            </div>
                            <div class="col-sm-6 col-xl-3">
                                <a class="dashboard-stat bg-dark text-white" href="AdhocReport.aspx">
                                    <div><span class="dashboard-stat-value"><i class="icon-arrow-right8"></i></span><span class="dashboard-stat-label">Open adhoc catalogue</span></div>
                                    <i class="icon-folder-search"></i>
                                </a>
                            </div>
                        </div>
                    </div>

                    <section class="powerbi-card" aria-labelledby="dashboard-title">
                        <div class="card-header header-elements-inline">
                            <div>
                                <h5 id="dashboard-title" class="card-title">Reporting overview</h5>
                                <p class="text-muted mb-0">Embedded Power BI dashboard for the Reporting Suite.</p>
                            </div>
                            <span class="badge badge-light border-left-info border-left-3 px-3 py-2"><i class="icon-pie-chart6 mr-1"></i>Power BI</span>
                        </div>
                        <div class="card-body p-0 reporting-embed-body">
                            <iframe title="Reporting Suite Dashboard" src="https://namfisabiportal/powerbiportal/powerbi/Reporting%20Suite?rs:embed=true" loading="lazy" allowfullscreen="true"></iframe>
                        </div>
                    </section>
                </div>

                <% For Each section In DashboardSections %>
                <div class="tab-pane fade" id="division-<%: section.SectionID %>" role="tabpanel">
                    <div class="card-body dashboard-division-heading">
                        <div>
                            <h5 class="mb-1"><%: section.SectionName %></h5>
                            <p class="text-muted mb-0"><%: GetSectionDescription(section) %></p>
                        </div>
                        <span class="badge badge-primary"><%: GetActiveReports(section).Count %> reports</span>
                    </div>
                    <div class="card-body pt-0">
                        <div class="row dashboard-report-grid">
                            <% For Each report In GetActiveReports(section) %>
                            <div class="col-md-6 col-xl-4 dashboard-report-item" data-search="<%: GetSearchText(report) %>">
                                <a class="card dashboard-report-card" href="<%: GetReportUrl(report) %>">
                                    <div class="card-body">
                                        <div class="d-flex align-items-start">
                                            <span class="dashboard-report-icon <%= If(IsAdhocReport(report), "bg-info", "bg-primary") %>"><i class="<%= If(IsAdhocReport(report), "icon-table2", "icon-file-text2") %>"></i></span>
                                            <div class="min-width-0 flex-fill">
                                                <div class="d-flex align-items-start justify-content-between">
                                                    <h6 class="font-weight-semibold mb-1 text-default"><%: GetReportName(report) %></h6>
                                                    <i class="icon-arrow-up-right2 text-muted ml-2"></i>
                                                </div>
                                                <p class="text-muted mb-3 dashboard-report-description"><%: GetReportDescription(report) %></p>
                                                <span class="badge <%= If(IsAdhocReport(report), "badge-info", "badge-light") %>"><%: GetReportTypeLabel(report) %></span>
                                            </div>
                                        </div>
                                    </div>
                                </a>
                            </div>
                            <% Next %>
                        </div>
                        <div class="dashboard-no-results alert alert-light border-left-warning border-left-3 d-none mb-0"><i class="icon-search4 mr-2"></i>No reports match your search in this division.</div>
                    </div>
                </div>
                <% Next %>
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Scripts" ContentPlaceHolderID="PageScripts" runat="server">
    <script src="Scripts/reporting-dashboard.js?v=20260910.1"></script>
</asp:Content>
