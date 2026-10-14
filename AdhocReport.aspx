<%@ Page Title="Adhoc Reports" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AdhocReport.aspx.vb" Inherits="ReportingSuite.AdhocReport" %>

<asp:Content ID="Head" ContentPlaceHolderID="HeadContent" runat="server">
    <meta name="description" content="Server-paged adhoc report viewer" />
</asp:Content>

<asp:Content ID="Body" ContentPlaceHolderID="MainContent" runat="server">
    <div id="adhoc-page-config" class="d-none"
         data-endpoint="<%: ResolveUrl("~/AdhocReportData.ashx") %>"
         data-request-token="<%: CreateRequestToken %>"
         data-ace-base="<%: ResolveUrl("~/Content/Template/global_assets/js/plugins/editors/ace") %>"></div>

    <% If IsAdminUser Then %>
    <div class="d-flex flex-wrap justify-content-end mb-3 adhoc-admin-actions">
        <% If SelectedReport IsNot Nothing Then %>
        <button type="button" class="btn btn-light mr-2" data-toggle="modal" data-target="#edit-adhoc-report-modal"><i class="icon-pencil7 mr-2"></i>Edit report</button>
        <% End If %>
        <button type="button" class="btn bg-teal-400" data-toggle="modal" data-target="#new-adhoc-report-modal"><i class="icon-plus3 mr-2"></i>New Adhoc report</button>
    </div>
    <% End If %>

    <% If ReportId <= 0 Then %>
    <section class="card" aria-labelledby="adhoc-catalog-title">
        <div class="card-header header-elements-inline">
            <div>
                <h5 id="adhoc-catalog-title" class="card-title">Adhoc report catalogue</h5>
                <p class="text-muted mb-0">Select a configured report to load its data on demand.</p>
            </div>
            <span class="badge badge-info"><%: AdhocReports.Count %> reports</span>
        </div>
        <div class="card-body">
            <% If LoadError.Length > 0 Then %>
            <div class="alert alert-danger border-0 alert-dismissible"><i class="icon-warning22 mr-2"></i><%: LoadError %></div>
            <% ElseIf AdhocReports.Count = 0 Then %>
            <div class="adhoc-empty-state text-center py-5">
                <i class="icon-table2 icon-3x text-muted mb-3"></i>
                <h5>No adhoc reports are configured yet</h5>
                <p class="text-muted mb-0">Add a read-only SELECT statement to the <code>AdhocQuery</code> column of an active report.</p>
            </div>
            <% Else %>
            <div class="row">
                <% For Each report In AdhocReports %>
                <div class="col-md-6 col-xl-4">
                    <a class="card dashboard-report-card" href="AdhocReport.aspx?reportId=<%: report.ReportID %>">
                        <div class="card-body">
                            <div class="d-flex align-items-start">
                                <span class="dashboard-report-icon bg-info"><i class="icon-table2"></i></span>
                                <div class="min-width-0 flex-fill">
                                    <h6 class="font-weight-semibold text-default mb-1"><%: GetReportName(report) %></h6>
                                    <p class="text-muted dashboard-report-description mb-3"><%: GetReportDescription(report) %></p>
                                    <span class="badge badge-info">Adhoc query</span>
                                </div>
                                <i class="icon-arrow-right8 text-muted ml-2"></i>
                            </div>
                        </div>
                    </a>
                </div>
                <% Next %>
            </div>
            <% End If %>
        </div>
    </section>
    <% ElseIf SelectedReport Is Nothing Then %>
    <div class="alert alert-danger border-0">
        <i class="icon-warning22 mr-2"></i><%: LoadError %>
        <a href="AdhocReport.aspx" class="alert-link ml-1">Return to the adhoc report catalogue.</a>
    </div>
    <% Else %>
    <div id="adhoc-report-app" data-report-id="<%: ReportId %>">
        <div class="row adhoc-report-workspace">
        <div class="col-12">
        <section id="adhoc-insights-card" class="card card-collapsed adhoc-collapsible-card w-100" aria-labelledby="adhoc-insights-title">
            <div class="card-header adhoc-insights-header">
                <div class="adhoc-collapse-heading" role="button" tabindex="0" aria-controls="adhoc-insights-content" aria-expanded="false">
                    <h5 id="adhoc-insights-title" class="card-title"><i class="icon-stats-growth mr-2 text-primary"></i>Report insights</h5>
                    <p id="adhoc-insights-subtitle" class="text-muted mb-0">Group the displayed report and compare the leading results.</p>
                </div>
                <div class="header-elements d-flex align-items-center">
                    <button id="adhoc-refresh-insights" type="button" class="btn btn-light btn-sm mr-2" disabled><i class="icon-sync mr-2"></i>Refresh insights</button>
                    <button id="adhoc-view-all-charts" type="button" class="btn bg-teal-400 btn-sm" disabled><i class="icon-enlarge7 mr-2"></i>View all charts</button>
                    <div class="list-icons ml-2">
                        <a href="#" class="list-icons-item" data-action="collapse" data-toggle="tooltip" title="Expand or collapse analytics" aria-label="Expand or collapse analytics" aria-expanded="false"></a>
                    </div>
                </div>
            </div>
            <div id="adhoc-insights-content" class="card-body">
                <div class="row align-items-end adhoc-insight-controls">
                    <div class="col-md-6 col-xl-2">
                        <div class="form-group">
                            <label for="adhoc-group-column" class="font-weight-semibold">Group by</label>
                            <select id="adhoc-group-column" class="form-control" data-no-select2="true" disabled>
                                <option value="">Loading columns&hellip;</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-6 col-xl-2">
                        <div class="form-group">
                            <label for="adhoc-secondary-group-column" class="font-weight-semibold">Secondary group</label>
                            <select id="adhoc-secondary-group-column" class="form-control" data-no-select2="true" disabled>
                                <option value="-1">None</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-6 col-xl-2">
                        <div class="form-group">
                            <label for="adhoc-measure-column" class="font-weight-semibold">Measure</label>
                            <select id="adhoc-measure-column" class="form-control" data-no-select2="true" disabled>
                                <option value="-1">Row count</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-6 col-xl-2">
                        <div class="form-group">
                            <label for="adhoc-chart-library" class="font-weight-semibold">Library</label>
                            <select id="adhoc-chart-library" class="form-control" data-no-select2="true" disabled>
                                <option value="echarts">ECharts</option>
                                <option value="d3">D3</option>
                                <option value="c3">C3</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-6 col-xl-2">
                        <div class="form-group">
                            <label for="adhoc-chart-type" class="font-weight-semibold">Chart</label>
                            <select id="adhoc-chart-type" class="form-control" data-no-select2="true" disabled>
                                <option value="line">Line</option>
                                <option value="area">Area</option>
                                <option value="bar">Bar</option>
                                <option value="column">Columns</option>
                                <option value="mixed">Mixed</option>
                                <option value="pie">Pie</option>
                                <option value="donut">Donut</option>
                                <option value="funnel">Funnel</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-6 col-xl-2">
                        <div class="form-group">
                            <label for="adhoc-group-limit" class="font-weight-semibold">Top</label>
                            <select id="adhoc-group-limit" class="form-control" data-no-select2="true" disabled>
                                <option value="8">8</option>
                                <option value="12" selected="selected">12</option>
                                <option value="20">20</option>
                                <option value="30">30</option>
                                <option value="50">50</option>
                            </select>
                        </div>
                    </div>
                </div>

                <div id="adhoc-insight-error" class="alert alert-warning border-0 d-none" role="alert"><i class="icon-warning22 mr-2"></i><span></span></div>
                <div id="adhoc-insight-loading" class="adhoc-insight-loading text-center py-5"><i class="icon-spinner4 spinner icon-2x text-primary mb-3"></i><div>Preparing report insights&hellip;</div></div>
                <div id="adhoc-insight-content" class="d-none">
                    <div class="row align-items-stretch adhoc-insight-layout">
                        <div class="col-lg-3">
                            <div class="row adhoc-insight-summary">
                                <div class="col-12">
                                    <div class="adhoc-insight-stat border-left-primary">
                                        <span class="adhoc-insight-stat-label">Matching rows</span>
                                        <strong id="adhoc-insight-rows">0</strong>
                                    </div>
                                </div>
                                <div class="col-12">
                                    <div class="adhoc-insight-stat border-left-info">
                                        <span class="adhoc-insight-stat-label">Distinct groups</span>
                                        <strong id="adhoc-insight-groups">0</strong>
                                        <small id="adhoc-insight-secondary-groups" class="text-muted d-block"></small>
                                    </div>
                                </div>
                                <div class="col-12">
                                    <div class="adhoc-insight-stat border-left-success">
                                        <span id="adhoc-insight-total-label" class="adhoc-insight-stat-label">Total</span>
                                        <strong id="adhoc-insight-total">0</strong>
                                    </div>
                                </div>
                                <div class="col-12">
                                    <div class="adhoc-insight-stat border-left-warning">
                                        <span class="adhoc-insight-stat-label">Leading group</span>
                                        <strong id="adhoc-insight-leader">&mdash;</strong>
                                        <small id="adhoc-insight-leader-value" class="text-muted d-block"></small>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="col-lg-9">
                            <div class="adhoc-insight-visualization">
                                <div id="adhoc-primary-chart" class="adhoc-primary-chart" role="img" aria-label="Grouped report chart"></div>
                                <p id="adhoc-chart-caption" class="text-muted font-size-sm text-center mb-0"></p>
                            </div>
                        </div>
                    </div>
                </div>
                <div id="adhoc-insight-empty" class="adhoc-empty-state text-center py-5 d-none">
                    <i class="icon-stats-bars2 icon-3x text-muted mb-3"></i>
                    <h6>No grouped data to display</h6>
                    <p class="text-muted mb-0">Change the grouping or clear report filters and try again.</p>
                </div>
            </div>
        </section>
        </div>

        <div class="col-12">
        <div id="adhoc-data-card" class="card adhoc-report-card adhoc-collapsible-card w-100">
            <div class="card-header header-elements-inline">
                <div class="adhoc-collapse-heading" role="button" tabindex="0" aria-controls="adhoc-data-content" aria-expanded="true">
                    <h5 class="card-title"><%: GetReportName(SelectedReport) %></h5>
                    <p class="text-muted mb-0"><%: GetReportDescription(SelectedReport) %></p>
                    <p class="text-muted font-size-sm mb-0 mt-1"><i class="icon-database mr-1"></i>Power BI view: <code><%: PowerBiViewName %></code></p>
                </div>
                <div class="header-elements">
                    <div class="list-icons">
                        <a href="#" class="list-icons-item" data-action="collapse" data-toggle="tooltip" title="Expand or collapse report data" aria-label="Expand or collapse report data" aria-expanded="true"></a>
                        <a id="adhoc-refresh" href="#" class="list-icons-item" data-toggle="tooltip" title="Refresh data"><i class="icon-sync"></i></a>
                        <a class="list-icons-item" data-action="fullscreen" data-toggle="tooltip" title="Fullscreen"></a>
                    </div>
                </div>
            </div>

            <div id="adhoc-data-content" class="adhoc-card-collapsible-content">
            <div class="card-body adhoc-filter-bar">
                <div class="row align-items-end">
                    <div class="col-md-4 col-xl-3">
                        <div class="form-group mb-md-0">
                            <label for="adhoc-filter-column" class="font-weight-semibold">Distinct filter column</label>
                            <select id="adhoc-filter-column" class="form-control" data-no-select2="true" disabled>
                                <option value="">Loading columns&hellip;</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-md-8 col-xl-5">
                        <div class="form-group mb-md-0">
                            <label for="adhoc-filter-value" class="font-weight-semibold">Distinct values</label>
                            <select id="adhoc-filter-value" class="form-control adhoc-remote-select" data-no-select2="true" multiple="multiple" disabled></select>
                        </div>
                    </div>
                    <div class="col-md-12 col-xl-4">
                        <div class="adhoc-filter-actions">
                            <button id="adhoc-advanced-filter" type="button" class="btn btn-light" data-toggle="modal" data-target="#adhoc-advanced-filter-modal" disabled><i class="icon-filter4 mr-2"></i>Advanced filters</button>
                            <button id="adhoc-clear-filter" type="button" class="btn btn-light" disabled><i class="icon-cross2 mr-2"></i>Clear all</button>
                        </div>
                    </div>
                </div>
                <div class="d-flex flex-wrap align-items-center justify-content-between mt-3">
                    <div id="adhoc-active-filters" class="adhoc-active-filters text-muted font-size-sm">No column filters applied.</div>
                    <div class="btn-group mt-2 mt-md-0" role="group" aria-label="Export all matching report rows">
                        <button id="adhoc-export-csv" type="button" class="btn btn-light" title="Export all matching rows to CSV" disabled><i class="icon-file-spreadsheet mr-2 text-success"></i>Export CSV</button>
                        <button id="adhoc-export-excel" type="button" class="btn btn-light" title="Export all matching rows to Excel" disabled><i class="icon-file-excel mr-2 text-success"></i>Export Excel</button>
                    </div>
                </div>
            </div>

            <div id="adhoc-error" class="alert alert-danger border-0 rounded-0 mb-0 d-none" role="alert"><i class="icon-warning22 mr-2"></i><span></span></div>
            <div id="adhoc-loading" class="adhoc-loading-state text-center py-5"><i class="icon-spinner4 spinner icon-2x text-primary mb-3"></i><div>Preparing the report columns&hellip;</div></div>

            <div id="adhoc-table-container" class="card-body adhoc-datatable-stage d-none">
                <div class="adhoc-datatable-shell">
                    <div class="adhoc-datatable-heading d-flex flex-wrap align-items-center justify-content-between">
                        <div>
                            <h6 class="font-weight-semibold mb-1"><i class="icon-table2 mr-2 text-primary"></i>Report data</h6>
                            <p class="text-muted font-size-sm mb-0">Search, sort, navigate with the keyboard, and choose the columns you need.</p>
                        </div>
                        <span id="adhoc-column-summary" class="badge badge-light border mt-2 mt-md-0">Preparing columns&hellip;</span>
                    </div>
                    <div class="adhoc-table-scroll">
                        <table id="adhoc-report-table" class="table table-striped table-hover table-bordered table-sm" width="100%" aria-describedby="adhoc-table-status" data-datatable-managed="true"></table>
                    </div>
                </div>
            </div>
            <div class="card-footer d-flex flex-wrap align-items-center justify-content-between">
                <span id="adhoc-table-status" class="text-muted font-size-sm">Waiting for report data.</span>
                <a href="AdhocReport.aspx" class="font-size-sm"><i class="icon-arrow-left8 mr-1"></i>All adhoc reports</a>
            </div>
            </div>
        </div>
        </div>
        </div>
        <noscript><div class="alert alert-warning">JavaScript is required to load this adhoc report.</div></noscript>
    </div>
    <% End If %>

    <% If SelectedReport IsNot Nothing Then %>
    <div id="adhoc-advanced-filter-modal" class="modal fade" tabindex="-1" role="dialog" aria-labelledby="adhoc-advanced-filter-title" aria-hidden="true">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header bg-light">
                    <h5 id="adhoc-advanced-filter-title" class="modal-title"><i class="icon-filter4 mr-2"></i>Advanced report filters</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <p class="text-muted">Select one or more distinct values for any report column. Values within a column are combined; different columns are applied together.</p>
                    <div id="adhoc-advanced-filter-fields" class="row"></div>
                </div>
                <div class="modal-footer">
                    <button id="adhoc-clear-advanced" type="button" class="btn btn-light"><i class="icon-reset mr-2"></i>Clear advanced</button>
                    <button type="button" class="btn btn-link" data-dismiss="modal">Cancel</button>
                    <button id="adhoc-apply-advanced" type="button" class="btn bg-teal-400"><i class="icon-checkmark3 mr-2"></i>Apply filters</button>
                </div>
            </div>
        </div>
    </div>

    <div id="adhoc-all-charts-modal" class="modal fade" tabindex="-1" role="dialog" aria-labelledby="adhoc-all-charts-title" aria-hidden="true">
        <div class="modal-dialog adhoc-chart-modal-dialog" role="document">
            <div class="modal-content">
                <div class="modal-header bg-light">
                    <div>
                        <h5 id="adhoc-all-charts-title" class="modal-title"><i class="icon-stats-bars mr-2 text-primary"></i>All report charts</h5>
                        <p id="adhoc-all-charts-subtitle" class="text-muted mb-0 font-size-sm"></p>
                    </div>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="adhoc-chart-modal-grid">
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Line</h6></div>
                            <div id="adhoc-modal-line-chart" class="adhoc-modal-chart" role="img" aria-label="Line chart"></div>
                        </section>
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Area</h6></div>
                            <div id="adhoc-modal-area-chart" class="adhoc-modal-chart" role="img" aria-label="Area chart"></div>
                        </section>
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Bar</h6></div>
                            <div id="adhoc-modal-bar-chart" class="adhoc-modal-chart" role="img" aria-label="Horizontal bar chart"></div>
                        </section>
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Columns</h6></div>
                            <div id="adhoc-modal-column-chart" class="adhoc-modal-chart" role="img" aria-label="Vertical column chart"></div>
                        </section>
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Mixed</h6></div>
                            <div id="adhoc-modal-mixed-chart" class="adhoc-modal-chart" role="img" aria-label="Mixed column and line chart"></div>
                        </section>
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Pie</h6></div>
                            <div id="adhoc-modal-pie-chart" class="adhoc-modal-chart" role="img" aria-label="Pie chart"></div>
                        </section>
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Donut</h6></div>
                            <div id="adhoc-modal-donut-chart" class="adhoc-modal-chart" role="img" aria-label="Donut chart"></div>
                        </section>
                        <section class="card adhoc-chart-tile">
                            <div class="card-header"><h6 class="card-title">Funnel</h6></div>
                            <div id="adhoc-modal-funnel-chart" class="adhoc-modal-chart" role="img" aria-label="Funnel chart"></div>
                        </section>
                    </div>
                </div>
            </div>
        </div>
    </div>
    <% End If %>

    <% If IsAdminUser Then %>
    <div id="new-adhoc-report-modal" class="modal fade" tabindex="-1" role="dialog" aria-labelledby="new-adhoc-report-title" aria-hidden="true">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header bg-light">
                    <h5 id="new-adhoc-report-title" class="modal-title"><i class="icon-plus3 mr-2"></i>New Adhoc report</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <div id="new-adhoc-error" class="alert alert-danger d-none" role="alert"><i class="icon-warning22 mr-2"></i><span></span></div>
                    <div class="row">
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="new-adhoc-name" class="font-weight-semibold">Report name <span class="text-danger">*</span></label>
                                <input id="new-adhoc-name" type="text" class="form-control" maxlength="200" autocomplete="off" required />
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="new-adhoc-title" class="font-weight-semibold">Report title <span class="text-danger">*</span></label>
                                <input id="new-adhoc-title" type="text" class="form-control" maxlength="100" autocomplete="off" required />
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="new-adhoc-section" class="font-weight-semibold">Division <span class="text-danger">*</span></label>
                                <select id="new-adhoc-section" class="form-control" data-no-select2="true" required>
                                    <option value="">Select a division</option>
                                    <% For Each section In AvailableSections %>
                                    <option value="<%: section.SectionID %>"><%: section.SectionName %></option>
                                    <% Next %>
                                </select>
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label class="font-weight-semibold">Report configuration</label>
                                <div class="form-control-plaintext text-muted">Custom type (3) &middot; Next Adhoc sort order &middot; Non-dynamic &middot; Active</div>
                            </div>
                        </div>
                    </div>
                    <input id="new-adhoc-report-type" type="hidden" value="3" />
                    <input id="new-adhoc-is-dynamic" type="hidden" value="false" />
                    <input id="new-adhoc-is-deleted" type="hidden" value="false" />
                    <div class="form-group mb-0">
                        <label for="new-adhoc-query-editor" class="font-weight-semibold">AdhocQuery SQL <span class="text-danger">*</span></label>
                        <div class="alert alert-info border-0 font-size-sm">Enter one read-only <code>SELECT</code> statement and give every result column a unique name. Top-level <code>ORDER BY</code> is not supported, but it is allowed inside valid subqueries and window expressions. Saving also creates a stable SQL view for Power BI.</div>
                        <div id="new-adhoc-query-editor" class="adhoc-sql-editor" role="textbox" aria-label="AdhocQuery SQL editor"></div>
                        <textarea id="new-adhoc-query-fallback" class="form-control adhoc-query-fallback d-none" rows="12" spellcheck="false" aria-label="AdhocQuery SQL"></textarea>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-link" data-dismiss="modal">Cancel</button>
                    <button id="save-adhoc-report" type="button" class="btn bg-teal-400"><i class="icon-floppy-disk mr-2"></i>Create report</button>
                </div>
            </div>
        </div>
    </div>

    <% If SelectedReport IsNot Nothing Then %>
    <div id="edit-adhoc-report-modal" class="modal fade" tabindex="-1" role="dialog" aria-labelledby="edit-adhoc-report-title" aria-hidden="true">
        <div class="modal-dialog modal-xl" role="document">
            <div class="modal-content">
                <div class="modal-header bg-light">
                    <h5 id="edit-adhoc-report-title" class="modal-title"><i class="icon-pencil7 mr-2"></i>Edit Adhoc report</h5>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <div id="edit-adhoc-error" class="alert alert-danger d-none" role="alert"><i class="icon-warning22 mr-2"></i><span></span></div>
                    <input id="edit-adhoc-report-id" type="hidden" value="<%: SelectedReport.ReportID %>" />
                    <div class="row">
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="edit-adhoc-name" class="font-weight-semibold">Report name <span class="text-danger">*</span></label>
                                <input id="edit-adhoc-name" type="text" class="form-control" maxlength="200" autocomplete="off" value="<%: SelectedReport.Report_Name %>" required />
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="edit-adhoc-title" class="font-weight-semibold">Report title <span class="text-danger">*</span></label>
                                <input id="edit-adhoc-title" type="text" class="form-control" maxlength="100" autocomplete="off" value="<%: SelectedReport.Report_Title %>" required />
                            </div>
                        </div>
                        <div class="col-md-6">
                            <div class="form-group">
                                <label for="edit-adhoc-section" class="font-weight-semibold">Division <span class="text-danger">*</span></label>
                                <select id="edit-adhoc-section" class="form-control" data-no-select2="true" required>
                                    <option value="">Select a division</option>
                                    <% For Each section In AvailableSections %>
                                    <option value="<%: section.SectionID %>" <% If SelectedReport.SectionID.GetValueOrDefault() = section.SectionID Then %>selected="selected"<% End If %>><%: section.SectionName %></option>
                                    <% Next %>
                                </select>
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label for="edit-adhoc-sort-order" class="font-weight-semibold">Sort order <span class="text-danger">*</span></label>
                                <input id="edit-adhoc-sort-order" type="number" class="form-control" min="1" max="2147483647" value="<%: If(SelectedReport.Sort_Order.HasValue, SelectedReport.Sort_Order.Value, 1) %>" required />
                            </div>
                        </div>
                        <div class="col-md-3">
                            <div class="form-group">
                                <label class="font-weight-semibold">Configuration</label>
                                <div class="form-control-plaintext text-muted">Existing type &middot; Active</div>
                            </div>
                        </div>
                    </div>
                    <div class="form-group mb-0">
                        <label for="edit-adhoc-query-editor" class="font-weight-semibold">AdhocQuery SQL <span class="text-danger">*</span></label>
                        <div class="alert alert-info border-0 font-size-sm">The revised query must remain one read-only <code>SELECT</code> statement with uniquely named result columns. Top-level <code>ORDER BY</code> is not supported, but it is allowed inside valid subqueries and window expressions. Saving updates the report's Power BI SQL view and reloads the report with the new schema.</div>
                        <div id="edit-adhoc-query-editor" class="adhoc-sql-editor" role="textbox" aria-label="Edit AdhocQuery SQL"></div>
                        <textarea id="edit-adhoc-query-fallback" class="form-control adhoc-query-fallback d-none" rows="12" spellcheck="false" aria-label="Edit AdhocQuery SQL"><%: SelectedReport.AdhocQuery %></textarea>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-link" data-dismiss="modal">Cancel</button>
                    <button id="save-edited-adhoc-report" type="button" class="btn bg-teal-400"><i class="icon-floppy-disk mr-2"></i>Save changes</button>
                </div>
            </div>
        </div>
    </div>
    <% End If %>
    <% End If %>
</asp:Content>

<asp:Content ID="Scripts" ContentPlaceHolderID="PageScripts" runat="server">
    <% If IsAdminUser Then %>
    <script src="Content/Template/global_assets/js/plugins/editors/ace/ace.js"></script>
    <script src="Content/Template/global_assets/js/plugins/editors/ace/mode-sql.js"></script>
    <script src="Content/Template/global_assets/js/plugins/editors/ace/theme-textmate.js"></script>
    <% End If %>
    <% If SelectedReport IsNot Nothing Then %>
    <script src="Content/Template/global_assets/js/plugins/visualization/d3/d3.min.js"></script>
    <script src="Content/Template/global_assets/js/plugins/visualization/c3/c3.min.js"></script>
    <script src="Content/Template/global_assets/js/plugins/visualization/echarts/echarts.min.js"></script>
    <% End If %>
    <script src="Scripts/adhoc-report.js?v=20260916.6"></script>
</asp:Content>
