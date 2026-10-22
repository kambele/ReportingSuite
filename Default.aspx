<%@ Page Title="Workspace" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.vb" Inherits="ReportingSuite._Default" %>

<asp:Content ID="Head" ContentPlaceHolderID="HeadContent" runat="server">
    <meta name="description" content="Personalised NAMFISA Reporting Suite workspace, insights, dashboards, and report catalogue" />
    <link href="Content/reporting-workspace.css?v=20261022.1" rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <main class="reporting-home" data-report-count="<%: TotalReports %>" data-workspace-admin="<%=If(IsWorkspaceAdministrator, "true", "false") %>" data-workspace-key="<%: WorkspaceStorageKey %>">
        <section class="workspace-hero" aria-labelledby="workspace-title">
            <div class="workspace-hero-copy">
                <span class="workspace-eyebrow">Personalised reporting workspace</span>
                <h1 id="workspace-title"><span class="js-workspace-greeting">Welcome</span>, <%: WorkspaceFirstName %></h1>
                <p>Access reporting tools, monitor the catalogue, and continue with the reports that matter to you.</p>
                <div class="workspace-identity-row">
                    <span><i class="icon-user-check mr-1" aria-hidden="true"></i><%: WorkspaceDisplayName %></span>
                    <span><i class="icon-shield-check mr-1" aria-hidden="true"></i><%=If(IsWorkspaceAdministrator, "Administrator workspace", "Permission-filtered workspace") %></span>
                </div>
            </div>
            <div class="workspace-hero-search">
                <label for="workspace-global-search">Find a report or module</label>
                <div class="workspace-search-control">
                    <i class="icon-search4" aria-hidden="true"></i>
                    <input id="workspace-global-search" type="search" class="form-control" placeholder="Search your reporting workspace" autocomplete="off" />
                    <kbd>/</kbd>
                </div>
                <div class="workspace-hero-actions">
                    <a href="#report-catalogue" class="btn btn-light"><i class="icon-stack-text mr-2"></i>Browse reports</a>
                    <a href="ReportRender.aspx" class="btn btn-warning"><i class="icon-stats-dots mr-2"></i>Open ACoA renderer</a>
                </div>
            </div>
        </section>

        <section class="workspace-section" aria-labelledby="quick-access-title">
            <header class="workspace-section-heading">
                <div>
                    <span class="workspace-eyebrow">Quick access</span>
                    <h2 id="quick-access-title">Functional modules</h2>
                    <p>Pin the tools you use most. Your arrangement is retained on this browser.</p>
                </div>
                <button type="button" class="btn btn-light btn-sm js-reset-workspace"><i class="icon-reset mr-1"></i>Reset layout</button>
            </header>

            <div class="workspace-module-grid js-workspace-module-grid">
                <article class="workspace-module-card" data-module-id="adhoc">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin Adhoc reports" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="AdhocReport.aspx" class="workspace-module-link"><span class="workspace-module-icon is-blue"><i class="icon-table2"></i></span><span class="workspace-module-copy"><strong>Adhoc reports</strong><small>Explore, filter, chart, and export managed query reports.</small></span><i class="icon-arrow-up-right2 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <article class="workspace-module-card" data-module-id="acoa-renderer">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin ACoA Report Renderer" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="ReportRender.aspx" class="workspace-module-link"><span class="workspace-module-icon is-gold"><i class="icon-stats-dots"></i></span><span class="workspace-module-copy"><strong>ACoA Report Renderer</strong><small>Run parameterised, grouped reports and analytical dashboards.</small></span><i class="icon-arrow-up-right2 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <article class="workspace-module-card" data-module-id="pension-dashboard">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin Pension Fund Dashboard" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="PF_Dashboard.aspx" class="workspace-module-link"><span class="workspace-module-icon is-purple"><i class="icon-pie-chart6"></i></span><span class="workspace-module-copy"><strong>Pension Fund Dashboard</strong><small>Open pension fund datasets and monitoring views.</small></span><i class="icon-arrow-up-right2 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <article class="workspace-module-card" data-module-id="power-bi-portal">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin Power BI Portal" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="https://namfisabiportal/powerbiportal" target="_blank" rel="noopener noreferrer" class="workspace-module-link" aria-label="Open Power BI Portal in a new tab"><span class="workspace-module-icon is-gold"><i class="icon-stats-bars2"></i></span><span class="workspace-module-copy"><strong>Power BI Portal</strong><small>Open NAMFISA dashboards and analytical reports.</small></span><i class="icon-new-tab2 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <article class="workspace-module-card" data-module-id="catalogue">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin Report catalogue" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="#report-catalogue" class="workspace-module-link"><span class="workspace-module-icon is-green"><i class="icon-stack-text"></i></span><span class="workspace-module-copy"><strong>Report catalogue</strong><small>Browse <%: TotalReports %> reports available to your account.</small></span><i class="icon-arrow-down8 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <article class="workspace-module-card" data-module-id="warehouse">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin ACoA Data Warehouse" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="ReportWarehouse.aspx" class="workspace-module-link"><span class="workspace-module-icon is-red"><i class="icon-database-add"></i></span><span class="workspace-module-copy"><strong>ACoA Data Warehouse</strong><small>Validate and load analytical snapshots for downstream tools.</small></span><i class="icon-arrow-up-right2 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <% If IsWorkspaceAdministrator Then %>
                <article class="workspace-module-card" data-module-id="builder">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin Report Builder" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="Builder.aspx" class="workspace-module-link"><span class="workspace-module-icon is-navy"><i class="icon-wrench3"></i></span><span class="workspace-module-copy"><strong>Report Builder</strong><small>Configure reports, mappings, formulas, and report security.</small></span><span class="workspace-module-badge">Admin</span><i class="icon-arrow-up-right2 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <article class="workspace-module-card" data-module-id="parameters">
                    <button type="button" class="workspace-pin js-module-pin" aria-label="Pin Report Parameters" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                    <a href="Parameters.aspx" class="workspace-module-link"><span class="workspace-module-icon is-slate"><i class="icon-equalizer2"></i></span><span class="workspace-module-copy"><strong>Report parameters</strong><small>Maintain shared business intelligence report parameters.</small></span><span class="workspace-module-badge">Admin</span><i class="icon-arrow-up-right2 workspace-module-arrow" aria-hidden="true"></i></a>
                </article>
                <% End If %>
            </div>

            <div class="workspace-saved-panel js-saved-panel d-none">
                <div class="workspace-saved-heading"><strong><i class="fa fa-star mr-2" aria-hidden="true"></i>Pinned reports</strong><span class="js-saved-count"></span></div>
                <div class="workspace-saved-list js-saved-reports"></div>
            </div>
        </section>

        <section class="workspace-section" aria-labelledby="insights-title">
            <header class="workspace-section-heading"><div><span class="workspace-eyebrow">Your access</span><h2 id="insights-title">Reporting insights</h2><p>Select an insight to filter the report catalogue.</p></div></header>
            <div class="workspace-insight-grid">
                <button type="button" class="workspace-insight-card is-primary js-workspace-filter" data-kind="all"><span class="workspace-insight-icon"><i class="icon-stack-text"></i></span><span><strong><%: TotalReports %></strong><small>Available reports</small></span><i class="icon-arrow-right8"></i></button>
                <button type="button" class="workspace-insight-card is-info js-workspace-filter" data-kind="division"><span class="workspace-insight-icon"><i class="icon-office"></i></span><span><strong><%: DashboardSections.Count %></strong><small>Accessible divisions</small></span><i class="icon-arrow-right8"></i></button>
                <button type="button" class="workspace-insight-card is-success js-workspace-filter" data-kind="custom"><span class="workspace-insight-icon"><i class="icon-file-text2"></i></span><span><strong><%: CustomReportCount %></strong><small>Custom reports</small></span><i class="icon-arrow-right8"></i></button>
                <button type="button" class="workspace-insight-card is-gold js-workspace-filter" data-kind="dynamic"><span class="workspace-insight-icon"><i class="icon-insert-template"></i></span><span><strong><%: DynamicReportCount %></strong><small>Dynamic templates</small></span><i class="icon-arrow-right8"></i></button>
            </div>
        </section>

        <section id="system-dashboards" class="workspace-section" aria-labelledby="system-dashboards-title">
            <header class="workspace-section-heading"><div><span class="workspace-eyebrow">System dashboards</span><h2 id="system-dashboards-title">Workspace overview</h2><p>A live summary of the reports currently available to you.</p></div></header>
            <div class="workspace-dashboard-grid">
                <article class="workspace-dashboard-panel">
                    <header><div><h3>Report mix</h3><p>Distribution of your accessible catalogue.</p></div><span class="workspace-panel-icon"><i class="icon-pie-chart6"></i></span></header>
                    <div class="workspace-mix">
                        <div class="workspace-mix-ring" style="--workspace-value:<%: GetPercentage(DynamicReportCount, TotalReports) %>"><strong><%: TotalReports %></strong><span>Total</span></div>
                        <div class="workspace-mix-legend">
                            <button type="button" class="js-workspace-filter" data-kind="custom"><span class="is-custom"></span><strong>Custom</strong><em><%: CustomReportCount %></em></button>
                            <button type="button" class="js-workspace-filter" data-kind="dynamic"><span class="is-dynamic"></span><strong>Dynamic</strong><em><%: DynamicReportCount %></em></button>
                        </div>
                    </div>
                </article>
                <article class="workspace-dashboard-panel">
                    <header><div><h3>Division coverage</h3><p>Reports available in each business area.</p></div><span class="workspace-panel-icon"><i class="icon-office"></i></span></header>
                    <div class="workspace-division-list">
                        <% For Each section In DashboardSections.Take(6) %>
                        <a href="#division-<%: section.SectionID %>" class="js-division-shortcut" data-section-id="<%: section.SectionID %>"><span><strong><%: section.SectionName %></strong><small><%: GetActiveReports(section).Count %> reports</small></span><span class="workspace-division-track"><i style="width:<%: GetPercentage(GetActiveReports(section).Count, TotalReports) %>%"></i></span></a>
                        <% Next %>
                    </div>
                </article>
                <article class="workspace-dashboard-panel workspace-recent-panel">
                    <header><div><h3>Recently opened</h3><p>Your activity on this browser.</p></div><button type="button" class="btn btn-link btn-sm js-clear-recent">Clear</button></header>
                    <div class="workspace-recent-list js-recent-reports"><div class="workspace-empty-state"><i class="icon-history"></i><strong>No recent reports yet</strong><span>Reports you open from this workspace will appear here.</span></div></div>
                </article>
            </div>
        </section>

        <section id="report-catalogue" class="workspace-section workspace-catalogue" aria-labelledby="catalogue-title">
            <header class="workspace-section-heading workspace-catalogue-heading">
                <div><span class="workspace-eyebrow">Report catalogue</span><h2 id="catalogue-title">Reports available to you</h2><p>Search, filter, pin, and open reports without leaving your workspace.</p></div>
                <div class="workspace-catalogue-search"><i class="icon-search4" aria-hidden="true"></i><input id="dashboard-report-search" type="search" class="form-control" placeholder="Search reports" aria-label="Search reports" autocomplete="off" /></div>
            </header>
            <div class="workspace-filter-row" aria-label="Report type filters">
                <button type="button" class="workspace-filter is-active js-catalogue-filter" data-kind="all" aria-pressed="true">All reports <span><%: TotalReports %></span></button>
                <button type="button" class="workspace-filter js-catalogue-filter" data-kind="custom" aria-pressed="false">Custom reports <span><%: CustomReportCount %></span></button>
                <button type="button" class="workspace-filter js-catalogue-filter" data-kind="dynamic" aria-pressed="false">Dynamic templates <span><%: DynamicReportCount %></span></button>
                <button type="button" class="workspace-filter js-catalogue-filter" data-kind="adhoc" aria-pressed="false">Custom queries <span><%: AdhocReportCount %></span></button>
                <span class="workspace-result-summary js-workspace-result-summary"><%: TotalReports %> reports</span>
            </div>

            <% If DashboardSections.Count > 0 OrElse DynamicReports.Count > 0 Then %>
            <div class="workspace-division-tabs" role="navigation" aria-label="Report divisions">
                <ul class="nav nav-tabs nav-tabs-highlight mb-0" role="tablist">
                    <% For Each section In DashboardSections %>
                    <li class="nav-item" data-section-tab="<%: section.SectionID %>"><a href="#division-<%: section.SectionID %>" class="nav-link <%=If(section.SectionID = DefaultSectionId, "active", "") %>" data-toggle="tab" role="tab"><i class="icon-folder mr-2"></i><%: section.SectionName %><span class="badge badge-light badge-pill ml-2"><%: GetActiveReports(section).Count %></span></a></li>
                    <% Next %>
                    <% If DynamicReports.Count > 0 Then %>
                    <li class="nav-item" data-section-tab="dynamic"><a href="#division-dynamic" class="nav-link <%=If(DashboardSections.Count = 0, "active", "") %>" data-toggle="tab" role="tab"><i class="icon-insert-template mr-2"></i>Dynamic templates<span class="badge badge-light badge-pill ml-2"><%: DynamicReportCount %></span></a></li>
                    <% End If %>
                </ul>
            </div>
            <div class="tab-content workspace-report-tabs">
                <% For Each section In DashboardSections %>
                <div class="tab-pane fade <%=If(section.SectionID = DefaultSectionId, "show active", "") %>" id="division-<%: section.SectionID %>" role="tabpanel" data-section-panel="<%: section.SectionID %>">
                    <div class="workspace-division-heading"><div><h3><%: section.SectionName %></h3><p><%: GetSectionDescription(section) %></p></div><span class="badge badge-primary"><%: GetActiveReports(section).Count %> reports</span></div>
                    <div class="workspace-report-grid">
                        <% For Each report In GetActiveReports(section) %>
                        <article class="workspace-report-item" data-report-id="<%: report.ReportID %>" data-kind="<%: GetReportKind(report) %>" data-search="<%: GetSearchText(report) %>" data-title="<%: GetReportName(report) %>" data-url="<%: GetReportUrl(report) %>" data-section="<%: section.SectionName %>">
                            <button type="button" class="workspace-report-pin js-report-pin" aria-label="Pin <%: GetReportName(report) %>" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                            <a class="workspace-report-link js-workspace-report-link" href="<%: GetReportUrl(report) %>"><span class="workspace-report-icon <%: GetReportIconCssClass(report) %>"><i class="<%: GetReportIcon(report) %>"></i></span><span class="workspace-report-copy"><strong><%: GetReportName(report) %></strong><small><%: GetReportDescription(report) %></small><em><%: GetReportTypeLabel(report) %></em></span><i class="icon-arrow-up-right2 workspace-report-arrow" aria-hidden="true"></i></a>
                        </article>
                        <% Next %>
                    </div>
                    <div class="workspace-section-empty d-none"><i class="icon-search4"></i><strong>No reports match your filters</strong><span>Try another search term or report type.</span></div>
                </div>
                <% Next %>

                <% If DynamicReports.Count > 0 Then %>
                <div class="tab-pane fade <%=If(DashboardSections.Count = 0, "show active", "") %>" id="division-dynamic" role="tabpanel" data-section-panel="dynamic">
                    <div class="workspace-division-heading"><div><h3>Dynamic report templates</h3><p>Select a report to preview its configured line items and columns.</p></div><span class="badge badge-warning"><%: DynamicReportCount %> templates</span></div>
                    <% For Each dynamicGroup In DynamicReportGroups %>
                    <section class="workspace-dynamic-group" data-dynamic-group="<%: dynamicGroup.GroupKey %>">
                        <header class="workspace-dynamic-group-heading"><div><i class="icon-office mr-2" aria-hidden="true"></i><strong><%: dynamicGroup.GroupName %></strong></div><span><%: dynamicGroup.Reports.Count %> templates</span></header>
                        <div class="workspace-report-grid">
                            <% For Each report In dynamicGroup.Reports %>
                            <article class="workspace-report-item" data-report-id="<%: report.ReportID %>" data-kind="<%: GetReportKind(report) %>" data-search="<%: GetSearchText(report) %>" data-title="<%: GetReportName(report) %>" data-url="#" data-section="<%: dynamicGroup.GroupName %>">
                                <button type="button" class="workspace-report-pin js-report-pin" aria-label="Pin <%: GetReportName(report) %>" aria-pressed="false"><i class="fa fa-star-o" aria-hidden="true"></i></button>
                                <button type="button" class="workspace-report-link js-workspace-report-link js-dynamic-report-preview" data-report-id="<%: report.ReportID %>" aria-label="Preview <%: GetReportName(report) %>"><span class="workspace-report-icon is-dynamic"><i class="icon-insert-template"></i></span><span class="workspace-report-copy"><strong><%: GetReportName(report) %></strong><small><%: GetReportDescription(report) %></small><em>Dynamic template</em></span><i class="icon-eye8 workspace-report-arrow" aria-hidden="true"></i></button>
                            </article>
                            <% Next %>
                        </div>
                    </section>
                    <% Next %>
                    <div class="workspace-section-empty d-none"><i class="icon-search4"></i><strong>No dynamic templates match your filters</strong><span>Try another search term or select all reports.</span></div>
                </div>
                <% End If %>
            </div>
            <div class="workspace-catalogue-empty d-none"><i class="icon-search4"></i><strong>No matching reports</strong><span>Clear the search or select another report type.</span></div>
            <% Else %>
            <div class="workspace-catalogue-empty"><i class="icon-lock2"></i><strong>No reports are currently available</strong><span>Contact a Reporting Suite administrator if you require access.</span></div>
            <% End If %>
        </section>
    </main>

    <div id="dynamic-report-preview-modal" class="modal fade dynamic-report-preview-modal" tabindex="-1" role="dialog" aria-labelledby="dynamic-report-preview-title" aria-hidden="true">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header dynamic-report-preview-header">
                    <div><span class="dynamic-report-preview-kicker"><i class="icon-insert-template mr-2" aria-hidden="true"></i>Dynamic report template</span><h2 id="dynamic-report-preview-title" class="modal-title js-dynamic-preview-title">Report structure</h2><p class="js-dynamic-preview-description mb-0"></p></div>
                    <button type="button" class="close text-white" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body p-0">
                    <div class="dynamic-report-preview-loading js-dynamic-preview-loading"><span class="spinner-border text-primary" role="status" aria-hidden="true"></span><strong>Loading report template…</strong><small>Retrieving configured line items and columns.</small></div>
                    <div class="alert alert-danger m-3 d-none js-dynamic-preview-error" role="alert"><i class="icon-warning22 mr-2" aria-hidden="true"></i><span>The report template could not be loaded.</span></div>
                    <div class="dynamic-report-preview-content d-none js-dynamic-preview-content">
                        <div class="dynamic-report-preview-toolbar">
                            <div class="dynamic-report-preview-summary js-dynamic-preview-summary"></div>
                            <label class="dynamic-report-preview-search"><i class="icon-search4" aria-hidden="true"></i><input type="search" class="form-control js-dynamic-line-search" placeholder="Filter line items" aria-label="Filter report line items" /></label>
                        </div>
                        <div class="dynamic-report-column-list js-dynamic-preview-columns" aria-label="Report columns"></div>
                        <div class="table-responsive dynamic-report-template-table-wrap">
                            <table class="table table-hover table-sm dynamic-report-template-table mb-0">
                                <thead class="js-dynamic-preview-table-head"></thead>
                                <tbody class="js-dynamic-preview-table-body"></tbody>
                            </table>
                        </div>
                        <div class="dynamic-report-preview-empty d-none js-dynamic-preview-empty"><i class="icon-insert-template"></i><strong>No report line items are configured</strong><span>The template exists, but its layout has not yet been defined in Report Builder.</span></div>
                    </div>
                </div>
                <div class="modal-footer"><span class="text-muted mr-auto"><i class="icon-info22 mr-1" aria-hidden="true"></i>This preview shows structure only; it does not execute report data.</span><button type="button" class="btn btn-light" data-dismiss="modal">Close</button></div>
            </div>
        </div>
    </div>
</asp:Content>

<asp:Content ID="Scripts" ContentPlaceHolderID="PageScripts" runat="server">
    <script src="Scripts/reporting-dashboard.js?v=20261022.2"></script>
</asp:Content>
