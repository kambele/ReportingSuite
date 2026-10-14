<%@ Page EnableEventValidation="false" Async="true" Title="" Language="vb" AutoEventWireup="false" MasterPageFile="~/Site.Master" CodeBehind="Builder.aspx.vb" Inherits="ReportingSuite.Builder" %>

<asp:Content ID="BuilderHead" ContentPlaceHolderID="HeadContent" runat="server">
    <link href="Content/report-builder-workspace.css?v=20261014.4" rel="stylesheet" type="text/css" />
</asp:Content>

<asp:Content ID="BuilderScripts" ContentPlaceHolderID="PageScripts" runat="server">
    <script src="Scripts/report-builder-workspace.js?v=20261014.4"></script>
</asp:Content>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="report-builder-workspace" role="main" data-report-selected="<%=If(Report Is Nothing, "false", "true") %>" data-report-id="<%=If(Report Is Nothing, "", Report.ReportID.ToString()) %>">
        <div class="builder-workspace-body">
            <section class="builder-workspace-hero" aria-labelledby="builder-workspace-title">
                <div>
                    <span class="builder-workspace-eyebrow">Administration workspace</span>
                    <h2 id="builder-workspace-title"><%=If(Report Is Nothing, "Report Builder Workspace", Server.HtmlEncode(Report.Report_Name)) %></h2>
                    <p>
                        <% If Report Is Nothing Then %>
                        Configure, secure, validate, and maintain the reporting catalogue from one responsive workspace.
                        <% Else %>
                        Review configuration readiness, mapping coverage, formula health, and Active Directory access before publishing this report.
                        <% End If %>
                    </p>
                </div>
                <div class="builder-workspace-actions">
                    <% If Report Is Nothing Then %>
                    <button type="button" class="btn btn-primary" data-toggle="modal" data-target="#new-report"><i class="icon-plus3 mr-2"></i>New report</button>
                    <% Else %>
                    <a href="Builder.aspx" class="btn btn-light"><i class="icon-arrow-left8 mr-2"></i>All reports</a>
                    <button type="button" class="btn btn-light" data-toggle="modal" data-target="#report-security"><i class="icon-shield-check mr-2"></i>Report security</button>
                    <a href="<%: GetPreviewUrl() %>" class="btn btn-primary" target="_blank" rel="noopener"><i class="icon-eye4 mr-2"></i>Preview report</a>
                    <% End If %>
                </div>
            </section>

            <% If Not String.IsNullOrWhiteSpace(BuilderSuccessMessage) Then %>
            <div class="alert alert-success alert-styled-left border-left-success" role="status"><i class="icon-checkmark-circle mr-2"></i><%: BuilderSuccessMessage %></div>
            <% End If %>
            <% If Not String.IsNullOrWhiteSpace(BuilderErrorMessage) Then %>
            <div class="alert alert-danger alert-styled-left border-left-danger" role="alert"><i class="icon-alert mr-2"></i><%: BuilderErrorMessage %></div>
            <% End If %>
            <% If Report Is Nothing Then
            %>
            <div class="builder-insight-grid" aria-label="Report catalogue insights">
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-files-empty"></i></span>
                    <strong class="builder-insight-value"><%: BuilderReportCount.ToString("N0") %></strong>
                    <span class="builder-insight-label">Active reports in the catalogue</span>
                </article>
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-checkmark3"></i></span>
                    <strong class="builder-insight-value"><%: BuilderConfiguredCount.ToString("N0") %></strong>
                    <span class="builder-insight-label">Reports with core type and entity configuration</span>
                </article>
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-shield-check"></i></span>
                    <strong class="builder-insight-value"><%: BuilderSecuredCount.ToString("N0") %></strong>
                    <span class="builder-insight-label">Reports restricted to AD users or groups</span>
                </article>
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-database4"></i></span>
                    <strong class="builder-insight-value"><%: BuilderAdhocCount.ToString("N0") %></strong>
                    <span class="builder-insight-label">Adhoc reports with managed SQL definitions</span>
                </article>
            </div>
            <div class="col-md-12 col-sm-12 col-xs-12">
                <div class="x_panel">
                    <div class="x_title">
                        <h2><i class="icon-stack-text mr-2 text-primary"></i>Report catalogue</h2>
                        <ul class="nav navbar-right panel_toolbox">
                            <li><a href="#" data-toggle="modal" data-target="#new-report" class="btn btn-primary btn-sm"><i class="icon-plus3 mr-1"></i>New report</a>

                            </li>
                            <li>
                                <asp:Button ID="Button7" runat="server" Text="Update business intelligence" CssClass="btn btn-light btn-sm" Visible />
                            </li>
                            <li>
                                <asp:Button ID="Button6" runat="server" Text="Update stored functions" CssClass="btn btn-light btn-sm" />
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
                        <div class="form-group mb-3">
                            <label class="sr-only" for="builder-report-search">Search reports</label>
                            <div class="input-group">
                                <div class="input-group-prepend"><span class="input-group-text"><i class="icon-search4"></i></span></div>
                                <input id="builder-report-search" type="search" class="form-control" placeholder="Search reports, divisions, or types" autocomplete="off" />
                            </div>
                        </div>
                        <div class="table-responsive">
                        <table id="builder-report-table" class="table table-striped table-hover builder-report-table">
                            <thead>
                                <tr>
                                    <th>Name</th>
                                    <th>Group</th>
                                    <th>Type</th>
                                    <th>Status</th>
                                    <th>Action</th>
                                </tr>
                            </thead>


                            <tbody>
                                <% For Each r In BuilderReports

                                %>
                                <tr>
                                    <td><%: r.Report_Name %></td>
                                    <td><%: GetReportGroupName(r) %></td>
                                    <td><%: GetReportTypeName(r) %></td>
                                    <td><span class="badge badge-success"><%If Not r.Is_Deleted Then %>Active<% Else %>Deleted<% End If %></span></td>
                                    <td class="text-right"><a href="Builder.aspx?report=<%=r.ReportID.ToString %>" class="btn btn-sm btn-primary"><i class="icon-pencil7 mr-1"></i>Open workspace</a></td>
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
            <%
                Else
            %>
            <nav id="builder-workspace-nav" class="builder-workspace-nav" aria-label="Report configuration sections" role="tablist">
                <button type="button" role="tab" class="builder-workspace-nav-item active" data-builder-view-target="overview" aria-selected="true"><i class="icon-dashboard"></i><span><strong>Overview</strong><small>Readiness and health</small></span></button>
                <button type="button" role="tab" class="builder-workspace-nav-item" data-builder-view-target="settings" aria-selected="false"><i class="icon-cog3"></i><span><strong>Report settings</strong><small>Identity and scope</small></span></button>
                <button type="button" role="tab" class="builder-workspace-nav-item" data-builder-view-target="columns" aria-selected="false"><i class="icon-table2"></i><span><strong>Columns</strong><small><%: WorkspaceInsights.ColumnCount.ToString("N0") %> configured</small></span></button>
                <button type="button" role="tab" class="builder-workspace-nav-item" data-builder-view-target="designer" aria-selected="false"><i class="icon-grid6"></i><span><strong>Form designer</strong><small><%: WorkspaceInsights.LineItemCount.ToString("N0") %> line items</small></span></button>
            </nav>

            <section class="builder-workspace-view active" data-builder-view="overview" aria-label="Report overview">

            <div class="builder-insight-grid" aria-label="Selected report insights">
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-meter2"></i></span>
                    <strong class="builder-insight-value"><%: WorkspaceInsights.ConfigurationPercent.ToString() %>%</strong>
                    <span class="builder-insight-label">Configuration readiness across core report settings</span>
                </article>
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-table2"></i></span>
                    <strong class="builder-insight-value"><%: WorkspaceInsights.VisibleColumnCount.ToString("N0") %> / <%: WorkspaceInsights.ColumnCount.ToString("N0") %></strong>
                    <span class="builder-insight-label">Visible columns configured for report output</span>
                </article>
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-list-numbered"></i></span>
                    <strong class="builder-insight-value"><%: WorkspaceInsights.VisibleLineItemCount.ToString("N0") %> / <%: WorkspaceInsights.LineItemCount.ToString("N0") %></strong>
                    <span class="builder-insight-label">Visible line items across <%: WorkspaceInsights.SectionCount.ToString("N0") %> sections</span>
                </article>
                <article class="builder-insight-card">
                    <span class="builder-insight-icon"><i class="icon-shield-check"></i></span>
                    <strong class="builder-insight-value"><%=If(ReportSecurityRestricted, WorkspaceInsights.SecurityRuleCount.ToString("N0"), "Open") %></strong>
                    <span class="builder-insight-label"><%=If(ReportSecurityRestricted, "AD access rules applied", "Available to authenticated users") %></span>
                </article>
            </div>

            <div class="builder-workspace-grid">
                <section class="builder-workspace-card" aria-labelledby="builder-coverage-title">
                    <header class="builder-workspace-card-header">
                        <h5 id="builder-coverage-title"><i class="icon-stats-bars2 mr-2 text-primary"></i>Configuration coverage</h5>
                        <span class="text-muted font-size-sm"><%: WorkspaceInsights.ConfiguredMappingCount.ToString("N0") %> configured mappings</span>
                    </header>
                    <div class="builder-workspace-card-body">
                        <div class="builder-chart-row">
                            <span class="builder-chart-label">Visible columns</span>
                            <span class="builder-chart-track"><span class="builder-chart-bar" style="width:<%: WorkspaceInsights.VisibleColumnPercent.ToString() %>%"></span></span>
                            <span class="builder-chart-value"><%: WorkspaceInsights.VisibleColumnPercent.ToString() %>%</span>
                        </div>
                        <div class="builder-chart-row">
                            <span class="builder-chart-label">Visible line items</span>
                            <span class="builder-chart-track"><span class="builder-chart-bar" style="width:<%: WorkspaceInsights.VisibleLineItemPercent.ToString() %>%"></span></span>
                            <span class="builder-chart-value"><%: WorkspaceInsights.VisibleLineItemPercent.ToString() %>%</span>
                        </div>
                        <div class="builder-chart-row">
                            <span class="builder-chart-label">Mapped cells</span>
                            <span class="builder-chart-track"><span class="builder-chart-bar" style="width:<%: WorkspaceInsights.MappingPercent.ToString() %>%"></span></span>
                            <span class="builder-chart-value"><%: WorkspaceInsights.MappingPercent.ToString() %>%</span>
                        </div>
                    </div>
                </section>
                <section class="builder-workspace-card" aria-labelledby="builder-readiness-title">
                    <header class="builder-workspace-card-header">
                        <h5 id="builder-readiness-title"><i class="icon-pulse2 mr-2 text-primary"></i>Release readiness</h5>
                    </header>
                    <div class="builder-workspace-card-body builder-readiness">
                        <div class="builder-donut" style="--value:<%: WorkspaceInsights.ConfigurationPercent.ToString() %>"><strong><%: WorkspaceInsights.ConfigurationPercent.ToString() %>%</strong></div>
                        <ul class="builder-status-list flex-fill">
                            <li><span>Formula health</span><strong class="<%=If(ReportHealth IsNot Nothing AndAlso ReportHealth.IsHealthy, "text-success", "text-danger") %>"><%=If(ReportHealth IsNot Nothing AndAlso ReportHealth.IsHealthy, "Healthy", "Review") %></strong></li>
                            <li><span>Mappings</span><strong><%: WorkspaceInsights.ConfiguredMappingCount.ToString("N0") %> / <%: WorkspaceInsights.MappingCount.ToString("N0") %></strong></li>
                            <li><span>Access mode</span><strong><%=If(ReportSecurityRestricted, "Restricted", "Open") %></strong></li>
                        </ul>
                    </div>
                </section>
            </div>

            <% If ReportHealth IsNot Nothing Then %>
            <div class="col-lg-12 col-md-12 col-sm-12 col-xs-12">
                <% If ReportHealth.HasCheckError Then %>
                <div class="alert alert-warning" role="status">
                    <h4><i class="fa fa-exclamation-triangle"></i> Report health check unavailable</h4>
                    <p class="mb-0"><%=Server.HtmlEncode(ReportHealth.CheckErrorMessage) %></p>
                </div>
                <% ElseIf ReportHealth.IsHealthy Then %>
                <div class="alert alert-success" role="status">
                    <h4><i class="fa fa-check-circle"></i> Report health: healthy</h4>
                    <p>No recursive calculated or inherited line-item dependencies were found.</p>
                    <small>
                        Checked <%=ReportHealth.EvaluatedMappingCount.ToString("N0") %> mappings,
                        <%=ReportHealth.DynamicMappingCount.ToString("N0") %> dynamic roots and
                        <%=ReportHealth.EvaluatedDependencyCount.ToString("N0") %> reachable dependencies.
                    </small>
                </div>
                <% Else %>
                <div class="alert alert-danger" role="alert">
                    <h4><i class="fa fa-ban"></i> Report health: recursive formula detected</h4>
                    <p>
                        This report can exceed SQL Server's nesting limit during rendering or warehouse extraction.
                        Correct each dependency cycle before publishing or loading the report.
                    </p>
                    <ul>
                        <% For Each issue As ReportingSuite.ReportHealthIssue In ReportHealth.Issues %>
                        <li>
                            <strong><%=Server.HtmlEncode(issue.Title) %></strong><br />
                            <code><%=Server.HtmlEncode(issue.Detail) %></code>
                            <% If issue.PrimaryLineItemColumnId > 0 Then %>
                            <a href="#" class="btn btn-danger btn-xs"
                                data-toggle="modal"
                                data-target="#col-<%=issue.PrimaryLineItemColumnId.ToString() %>">
                                Review mapping <%=issue.PrimaryLineItemColumnId.ToString() %>
                            </a>
                            <% End If %>
                        </li>
                        <% Next %>
                    </ul>
                    <small>
                        Checked <%=ReportHealth.EvaluatedMappingCount.ToString("N0") %> mappings and
                        <%=ReportHealth.EvaluatedDependencyCount.ToString("N0") %> reachable dependencies.
                    </small>
                </div>
                <% End If %>
            </div>
            <% End If %>
            </section>
            <div class=" col-lg-12 col-md-12 col-sm-12 col-xs-12">
                <section class="builder-workspace-view" data-builder-view="settings" aria-label="Report settings">
                <div class="x_panel builder-settings-panel">
                    <div class="x_title">
                        <h3><small><%=Report.Report_Name  %></small></h3>
                        <ul class="nav navbar-right panel_toolbox">
                            <li><a href="#" class="btn btn-default btn-xs" data-toggle="modal" data-target="#add-report-section"><i class="fa fa-plus"></i>Add Report Section</a>
                            </li>
                            <li><a class="close-link"><i class="fa fa-close"></i></a>
                            </li>
                        </ul>
                        <div class="clearfix"></div>
                        <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="add-report-section">
                            <div class="modal-dialog modal-lg">
                                <div class="modal-content">

                                    <div class="modal-header">
                                        <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                            <span aria-hidden="true">×</span>
                                        </button>
                                        <h4 class="modal-title" id="">Add New Report Section</h4>
                                    </div>
                                    <div class="modal-body">
                                        <div class="x_panel">
                                            <div class="x_title">
                                                <h3><%=Report.Report_Name %></h3>
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
                                                    <label class="control-label col-md-3 col-sm-3 col-xs-12">Report Section Name</label>
                                                    <div class="col-md-9 col-sm-9 col-xs-12">
                                                        <input type="text" class="form-control" placeholder="Report Section Name" name="Add_ReportSections_ReportSectionName" value="">
                                                    </div>
                                                </div>
                                                <div class="form-group">
                                                    <label class="control-label col-md-3 col-sm-3 col-xs-12">Sort Order</label>
                                                    <div class="col-md-9 col-sm-9 col-xs-12">
                                                        <input type="number" class="form-control" placeholder="Sort Order" name="Add_ReportSections_Sort_Order" value="">
                                                    </div>
                                                </div>
                                                <div class="form-group">
                                                    <label class="control-label col-md-3 col-sm-3 col-xs-12">Show</label>
                                                    <div class="col-md-9 col-sm-9 col-xs-12">
                                                        <select class="form-control" name="Add_ReportSections_Show">
                                                            <option value="1">Yes</option>
                                                            <option value="1">No</option>
                                                        </select>
                                                    </div>
                                                </div>

                                            </div>
                                        </div>
                                    </div>
                                    <div class="modal-footer">
                                        <button type="button" class="btn btn-default" data-dismiss="modal">Close</button>
                                        <button type="submit" class="btn btn-primary">Save changes</button>
                                    </div>

                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="x_content">
                        <div class="form-horizontal form-label-left">
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12" for="first-name">
                                    Report Name <span class="required">*</span>
                                </label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <input type="text" name="Report_Name" class="form-control col-md-12 col-xs-12" value="<%=Report.Report_Name %>" />


                                </div>
                            </div>
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12" for="ReportCode">
                                    <span class="required">*</span>
                                </label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <input type="text" name="ReportCode" class="form-control col-md-12 col-xs-12" value="<%=Report.ReportCode %>" />


                                </div>
                            </div>
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12" for="Sort_Order">
                                </label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <input type="number" name="Sort_Order" class="form-control col-md-12 col-xs-12" value="<%=Report.Sort_Order %>" />


                                </div>
                            </div>
                            <%If Report.Is_MulitpleEntities = False Then
                            %>
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Entity Group</label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <select class="form-control" name="EntityGroupID">
                                        <%For Each e In db.EntityGroups
                                        %>
                                        <option <%If e.EntityGroupID = Report.Entity_GroupID Then %>selected="selected" <% End If %> value="<%=e.EntityGroupID %>"><%=e.EntityGroupName
                                        %></option>
                                        <%
                                            Next %>
                                    </select>
                                </div>
                            </div>
                            <%
                                End If
                            %>

                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Active?</label>

                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <select class="form-control" name="Is_Deleted">
                                        <option <%If Not Report.Is_Deleted Then %> selected="selected" <% End If %> value="0">Active</option>
                                        <option <%If Report.Is_Deleted Then %> selected="selected" <% End If %> value="1">Deleted</option>

                                    </select>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Belong to</label>

                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <select class="form-control" name="Is_MultipleEntities">
                                        <option <%If Not Report.Is_MulitpleEntities Then %> selected="selected" <% End If %> value="0">Single Entities</option>
                                        <option <%If Report.Is_MulitpleEntities Then %> selected="selected" <% End If %> value="1">Multiple Entities</option>

                                    </select>
                                </div>
                            </div>
                            <%If Report.Is_MulitpleEntities Then
                            %>
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Belong to</label>

                                <div class="col-md-9 col-sm-9 col-xs-12" id="addMultiple">
                                    <%For Each e In Report.ReportEntityGroups
                                    %>
                                    <select name="Multipe_EntityGroupID_<%=e.ReportEntityGroupID.ToString %>" class="form-control">
                                        <option value="-1">--Remove--</option>
                                        <%For each ee In db.ReportEntityGroups
                                        %>
                                        <option <%If ee.EntityGroupID = e.EntityGroupID Then %> selected="selected" <%End If %>value="<%=ee.EntityGroupID.ToString %>"><%=ee.EntityGroup.EntityGroupName %></option>
                                        <%
                                            Next %>
                                    </select>
                                    <%
                                        Next %>
                                </div>
                            </div>
                            <div class="form-group">

                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <input type="hidden" name="AddMultipleEntities" id="AddMultipleEntities" value="0" />
                                    <a href="javascript: addMultiple();" class="btn btn-primary btn-xs">Add Entity Group</a>
                                </div>
                            </div>

                            <script type="text/javascript">
                                var countMultiple = 0;

                                function addMultiple() {
                                    countMultiple++;
                                    document.getElementById("AddMultipleEntities").value = countMultiple;
                                    $('#addMultiple').append('<select class="form-control" name="AddMultiple_EntityGroupID_' + countMultiple + '" class="form-group col-lg-12" ><%=getEntityGroupsDropdown()%></select>');
                                }
                            </script>

                            <%

                                End If%>
                            <div class="form-group">
                                <div class="col-md-12 col-sm-12 col-xs-12">
                                    <asp:Button ID="Button1" runat="server" Text="Save" CssClass="btn btn-success" />
                                    <a class="btn btn-secondary" href="Download.aspx?action=schema_import_template&parameter=<%=Report.ReportID.ToString %>"><i class="fa fa-download"></i>Download Schema Import Template</a>


                                </div>
                            </div>
                        </div>


                    </div>
                </div>
                </section>
                <section class="builder-workspace-view" data-builder-view="columns" aria-label="Report columns">
                    <header class="builder-view-heading"><div><span class="builder-workspace-eyebrow">Output structure</span><h3>Report columns and sections</h3><p>Configure section columns without mixing them into the form-design workspace.</p></div><button type="button" class="btn btn-primary btn-sm" data-toggle="modal" data-target="#add-report-section"><i class="icon-plus3 mr-1"></i>Add section</button></header>
                <%If Report.LineItemTypeID = 2 Then
                %>


                <%For Each rs In Report.ReportSections.OrderBy(Function(f) f.SortOrder)
                %>
                <div class="x_panel">
                    <div class="x_title">
                        <h3><small><%=rs.ReportSectionName %> (<%=rs.ReportSectionID.ToString %>)</small> </h3>
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
                    <div class="x_content">
                        <table class="table ">
                            <thead>
                                <tr>
                                    <th>Name</th>
                                    <th>Order</th>
                                    <th>Show</th>
                                    <th>Calculated</th>
                                    <th>Data Type</th>
                                    <th>Import Template</th>
                                </tr>
                            </thead>


                            <tbody id="Report_columnns_<%=rs.ReportSectionID.ToString %>">
                                <% For Each c In rs.Report_Columns.OrderBy(Function(f) f.Sort_Order)


                                %>
                                <tr>
                                    <td>
                                        <input name="RC_ColumnName_<%=c.Report_ColumnID.ToString %>" type="text" value="<%=c.ColumnName %>" class=" form-control col-lg-12" /></td>
                                    <td>
                                        <input name="RC_Sort_Order_<%=c.Report_ColumnID.ToString %>" type="text" value="<%=c.Sort_Order.ToString %>" class="form-control col-lg-12" /></td>

                                    <td>
                                        <select name="RC_Show_<%=c.Report_ColumnID.ToString %>" class="form-control col-lg-12">
                                            <option <%If c.Show = 1 Then %> selected="selected" <% End If %> value="1">Yes</option>
                                            <option <%If c.Show = 0 Then %> selected="selected" <% End If %> value="0">No</option>
                                        </select>

                                    </td>
                                    <td>

                                        <% If c.Is_Calculated Then
                                        %>
                                        <div class="form-group col-lg-4">
                                            <select name="RC_CalculatedFromReportColumnID_<%=c.Report_ColumnID.ToString %>" class="form-control ">
                                                <%
                                                    For Each by In rs.Report_Columns.Where(Function(f) f.Is_Calculated = False).OrderBy(Function(f) f.Sort_Order)
                                                %>
                                                <option <%If c.CalculatedFromReportColumnID = by.Report_ColumnID Then %> selected="selected" <% End If %> value="<%=by.Report_ColumnID.ToString %>"><%=by.ColumnName %></option>
                                                <%
                                                    Next
                                                %>
                                            </select>
                                        </div>
                                        <div class="form-group col-lg-4">
                                            <select name="RC_CalculatedByID_<%=c.Report_ColumnID.ToString %>" class="form-control ">
                                                <%
                                                    For Each by In db.CalculatedBies
                                                %>
                                                <option <%If c.CalculatedByID = by.CalculatedByID Then %> selected="selected" <% End If %> value="<%=by.CalculatedByID.ToString %>"><%=by.CalculatedBy1 %></option>
                                                <%
                                                    Next
                                                %>
                                            </select>
                                        </div>
                                        <div class="form-group col-lg-4">
                                            <input name="RC_CalculationValue_<%=c.Report_ColumnID.ToString %>" type="text" value="<%=c.CalculationValue.ToString %>" class=" form-control " />

                                        </div>
                                        <%
                                            End If%>

                                    </td>
                                    <td>
                                        <select name="RC_DateType_<%=c.Report_ColumnID.ToString %>" class="form-control ">
                                            <option value="decimal" <% If c.DataType = "decimal" Then %> selected="selected" <% End If %>>Decimal</option>
                                            <option value="text" <% If c.DataType = "text" Then %> selected="selected" <% End If %>>Text</option>
                                            <option value="date" <% If c.DataType = "date" Then %> selected="selected" <% End If %>>Date</option>
                                            <option value="datetime" <% If c.DataType = "datetime" Then %> selected="selected" <% End If %>>DateTime</option>
                                        </select>
                                    </td>
                                    <td>
                                        <a href="Download.aspx?action=import_template&parameter=<%=c.Report_ColumnID.ToString %>"><i class="fa fa-download"></i>Download (CSV)</a>


                                    </td>
                                </tr>
                                <%
                                    Next
                                %>
                            </tbody>
                            <tfoot>
                                <tr>
                                    <td colspan=""><a href="javascript: addItemCol('<%=rs.ReportSectionID.ToString %>','<%=rs.Report_Columns.Count.ToString %>');" class="btn btn-xs btn-primary"><i class="fa fa-plus"></i>New Column Item</a></td>
                                </tr>
                            </tfoot>
                        </table>

                        <asp:Button ID="btnSaveColumns" runat="server" Text="Save Changes" CssClass="btn btn-primary" />
                    </div>
                </div>

                <%

                    Next %>
                <input type="hidden" name="AddCol" id="AddCol" value="0" />
                <script type="text/javascript">
                    var countCol = 0;
                    var order = 0;
                    function addItemCol(section, sort) {
                        countCol++;

                        if (order == 0) {
                            order = sort + 1;
                        }
                        document.getElementById("AddCol").value = countCol;
                        $('#Report_columnns_' + section).append('<tr><td><input name="AddCol_ColumnName_' + countCol + '" class="form-group col-lg-12" type="text"/></td><td><input type="text" value="' + order + '" name="AddCol_Sort_Order_' + countCol + '" class="form-group col-lg-12" /><input type="hidden" name="AddCol_ReportSectionID_' + countCol + '" value="' + section + '"/></td> <td><select name="AddCol_Show_' + countCol + '"><option value="0">No</option><option value="1">Yes</option></select></td><td><select name="AddCol_Is_Calculated_' + countCol + '"><option value="0">No</option><option value="1">Yes</option></select></td></tr>');
                        order++;
                    }
                </script>

                <%

                    End If %>
                </section>
            </div>

            <section class="builder-workspace-view" data-builder-view="designer" aria-label="Form designer">
            <div class="col-lg-12 col-md-12 col-sm-12 col-xs-12">
                <div class="x_panel builder-designer-panel">
                    <div class="x_title">
                        <h2><span class="builder-workspace-eyebrow">Layout and mappings</span><strong>Form designer</strong><small>Configure line items without loading the entire matrix into one continuous page.</small></h2>
                        <ul class="nav navbar-right panel_toolbox builder-designer-actions">
                            <li class="builder-import-tools">
                                <details>
                                    <summary class="btn btn-default btn-sm"><i class="fa fa-upload" aria-hidden="true"></i> Import tools</summary>
                                    <div class="builder-import-menu">
                                        <label><span>Line items file</span><asp:FileUpload ID="FileUpload1" runat="server" CssClass="form-control" /></label>
                                        <asp:Button ID="Button9" runat="server" Text="Import line items" CssClass="btn btn-primary btn-sm" />
                                        <label><span>Schema items file</span><asp:FileUpload ID="FileUpload2" runat="server" CssClass="form-control" /></label>
                                        <asp:Button ID="Button11" runat="server" Text="Import schema items" CssClass="btn btn-primary btn-sm" />
                                    </div>
                                </details>
                            </li>
                            <li><a href="javascript:addItem();" class="btn btn-primary btn-sm"><i class="fa fa-plus-circle" aria-hidden="true"></i> Add item</a></li>
                        </ul>

                        <div class="clearfix"></div>
                    </div>
                    <div class="x_content">
                        <div class="builder-design-toolbar" aria-label="Form designer controls">
                            <label class="builder-design-search">
                                <span>Find line items</span>
                                <span class="builder-input-with-icon"><i class="fa fa-search" aria-hidden="true"></i><input type="search" class="form-control js-builder-design-search" placeholder="Search by line item name" autocomplete="off" /></span>
                            </label>
                            <label>
                                <span>Show</span>
                                <select class="form-control js-builder-design-filter">
                                    <option value="all">All line items</option>
                                    <option value="visible">Visible only</option>
                                    <option value="hidden">Hidden only</option>
                                    <option value="title">Title rows</option>
                                    <option value="calculated">Calculated rows</option>
                                </select>
                            </label>
                            <label>
                                <span>Rows per page</span>
                                <select class="form-control js-builder-design-page-size">
                                    <option value="25">25</option>
                                    <option value="50">50</option>
                                    <option value="100">100</option>
                                    <option value="all">All</option>
                                </select>
                            </label>
                            <label class="builder-formset-field">
                                <span>Form set</span>
                                <input type="text" id="formset" name="formset" value="<%=Request.QueryString("formset") %>" class="form-control" placeholder="Optional form set" />
                            </label>
                            <button type="button" class="btn btn-default builder-density-toggle js-builder-density" aria-pressed="false"><i class="fa fa-compress" aria-hidden="true"></i> Compact rows</button>
                            <button type="button" class="btn btn-default builder-order-reset js-builder-order-reset" disabled="disabled" title="Restore the row order from when this page was opened"><i class="fa fa-undo" aria-hidden="true"></i> Undo order</button>
                            <div class="builder-design-result js-builder-design-count" aria-live="polite"></div>
                            <div class="builder-order-status js-builder-order-status" aria-live="polite"><i class="fa fa-arrows-v" aria-hidden="true"></i> Drag rows to reorder</div>
                        </div>
                        <div class="builder-design-table-shell">
                        <table class="table-striped builder-designer-table js-builder-designer-table" style="width: 100%">
                            <thead>
                                <tr>
                                    <th style="width: 7%;">Order</th>

                                    <th style="width: 20%;">Item</th>
                                    <th style="width: 5%;">Main</th>
                                    <th style="width: 5%;">Title</th>
                                    <th style="width: 5%;">Show</th>
                                    <th style="width: 5%;">Section</th>
                                    <th style="width: 5%;"></th>
                                    <%For Each c In Report.Report_Columns.Where(Function(f) f.Show).OrderBy(Function(f) f.Sort_Order)
                                    %>
                                    <th>
                                        <a href="#" class="btn btn-success btn-xs col-lg-12 col-md-12" data-toggle="modal" data-target="#calcAllC-<%=c.Report_ColumnID.ToString %>"><i class="fa fa-calculator"></i>More</a>
                                        <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="calcAllC-<%=c.Report_ColumnID %>">
                                            <div class="modal-dialog modal-lg">
                                                <div class="modal-content">

                                                    <div class="modal-header">
                                                        <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                                            <span aria-hidden="true">×</span>
                                                        </button>
                                                        <h4 class="modal-title">Calculated value formula</h4>
                                                    </div>
                                                    <div class="modal-body">
                                                        <div class="x_panel">
                                                            <div class="x_title">
                                                                <h3><%=c.ColumnName %> </h3>
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
                                                                <div class="builder-ajax-form" id="Form-ReportColumn-<%=c.Report_ColumnID.ToString %>" data-action="Action/?operation=submitReportColumnUpdate&amp;item=<%=c.Report_ColumnID.ToString %>" data-method="post">

                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Add Month to Reporting Period</label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <input type="number" class="form-control" name="AddMonthToReportingPeriod_<%=c.Report_ColumnID.ToString %>" value="<%=c.AddMonthToReportingPeriod.ToString %>">
                                                                        </div>
                                                                    </div>

                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Calculated Value</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">

                                                                            <select id="Column_Is_Calculated_Value_<%=c.Report_ColumnID.ToString %>" name="Column_Is_Calculated_Value_<%=c.Report_ColumnID.ToString %>">
                                                                                <option value="">-- Select -- </option>
                                                                                <option <%If c.Is_Calculated Then %> selected="selected" <% Else %><% End If%> value="Yes">Yes</option>
                                                                                <option <%If Not c.Is_Calculated Then %> selected="selected" <% Else %><% End If%> value="No">No</option>
                                                                            </select>

                                                                        </div>

                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Static Value</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">


                                                                            <select id="Column_Is_Static_Value_<%=c.Report_ColumnID.ToString %>" name="Column_Is_Static_Value_<%=c.Report_ColumnID.ToString %>">
                                                                                <option value="">-- Select -- </option>
                                                                                <option <%If c.Is_Static_Value Then %> selected="selected" <% Else %><% End If%> value="Yes">Yes</option>
                                                                                <option <%If Not c.Is_Static_Value Then %> selected="selected" <% Else %><% End If%> value="No">No</option>
                                                                            </select>
                                                                        </div>

                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Inherited Value</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                            <select class="form-control" name="Is_Inherited_<%=c.Report_ColumnID.ToString %>" onchange="return inheritColumnReportList('<%=c.Report_ColumnID.ToString %>');">
                                                                                <option <%If Not c.Is_Inherited Then %> selected="selected" <% End If %> value="0">No</option>
                                                                                <option <%If c.Is_Inherited Then %> selected="selected" <% End If %> value="1">Yes</option>
                                                                            </select>
                                                                        </div>

                                                                    </div>

                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Where Query</label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <textarea type="text" class="form-control" id="QueryWhere_<%=c.Report_ColumnID.ToString %>" placeholder="eit.name IN('Entity Name 1','Entity Name 2')" name="QueryWhere_<%=c.Report_ColumnID.ToString %>"><%=c.QueryWhere %></textarea>
                                                                        </div>
                                                                    </div>
                                                                    <%--<div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Schema Field Names (Line separated)</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                            <textarea  id="SchemaFieldName_<%=c.Report_ColumnID.ToString %>" class="form-control" name="SchemaFieldName_<%=c.Report_ColumnID.ToString %>" placeholder=""></textarea>
                                                                            
                                                                        </div>

                                                                    </div>--%>

                                                                    <div class="form-group" id="Is_Inherited_ReportColumn_ReportList_<%=c.Report_ColumnID.ToString %>">
                                                                    </div>
                                                                    <div class="form-group" id="Is_Inherited_ReportColumn_ColumnList_<%=c.Report_ColumnID.ToString %>">
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <button onclick="return PostForm('Form-ReportColumn-<%=c.Report_ColumnID.ToString %>','Form-ReportColumn-<%=c.Report_ColumnID.ToString %>','<%=HttpContext.Current.Request.Url.AbsoluteUri %>');" type="button" class="btn btn-primary">Save Changes</button>
                                                                        </div>
                                                                    </div>
                                                                </div>


                                                                <% If c.Is_Calculated Then
                                                                %>
                                                                <a href="javascript: calcAllC('+','<%=c.Report_ColumnID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-plus"></i>Plus
                                                                </a>
                                                                <a href="javascript: calcAllC('-','<%=c.Report_ColumnID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-minus"></i>Minus
                                                                </a>
                                                                <a href="javascript: calcAllC('*','<%=c.Report_ColumnID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-m"></i>Multiply
                                                                </a>
                                                                <a href="javascript: calcAllC('/','<%=c.Report_ColumnID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-di"></i>Divide
                                                                </a>
                                                                <a href="javascript: calcAllC('','<%=c.Report_ColumnID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-right"></i>Equal
                                                                </a>
                                                                <a href="javascript: parseAllC('+','<%=c.Report_ColumnID.ToString %>','<%=HttpContext.Current.Request.Url.AbsoluteUri %>');" class="btn btn-app">
                                                                    <i class="fa fa-arrow-right"></i>Parse
                                                                </a>

                                                                <div class="ln_solid"></div>

                                                                <div class="form-group">
                                                                    <div class="col-md-9 col-sm-9 col-xs-12">
                                                                        <select class="form-control" id="schema_allC_<%=c.Report_ColumnID.ToString %>">

                                                                            <%For Each f In Report.Report_Columns
                                                                            %>
                                                                            <option value="<%=f.Report_ColumnID.ToString %>"><%=f.ColumnName %> </option>

                                                                            <%
                                                                                Next%>
                                                                        </select>

                                                                    </div>
                                                                </div>

                                                                <div class="form-group">
                                                                    <div class="builder-ajax-form" id="Form-CalcLineItemColumnAllC-<%=c.Report_ColumnID.ToString() %>" data-action="Action/?operation=submitColumnLineItemCalcAllC&amp;item=<%=c.Report_ColumnID.ToString() %>" data-method="post">
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">

                                                                            <textarea readonly="readonly" id="calcAllC_<%=c.Report_ColumnID.ToString %>" class="form-control" name="formulaDisplay"><%=c.Expression %></textarea>
                                                                            <input type="hidden" id="jExpressionAllC_<%=c.Report_ColumnID.ToString %>" name="ExpressionAllC_<%=c.Report_ColumnID.ToString %>" value="" />
                                                                        </div>
                                                                    </div>
                                                                </div>
                                                                <p><a href="javascript: clearAllC('<%=c.Report_ColumnID.ToString %>')" class="btn ">Clear Formula</a></p>
                                                                <%
                                                                    End If %>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="modal-footer">
                                                        <button id="Form-ReportColumn-<%=c.Report_ColumnID.ToString %>-button" type="button" class="btn btn-default" data-dismiss="modal">Close</button>
                                                    </div>

                                                </div>
                                            </div>
                                        </div>

                                    </th>
                                    <%

                                        Next %>
                                </tr>
                            </thead>


                            <tbody id="Report_Items">
                                <% For Each c In Report.Report_LineItems.OrderBy(Function(f) f.Sort_Order)
                                        Sort = c.Sort_Order
                                %>
                                <tr class="js-builder-design-row" data-line-item-id="<%=c.Report_LineItemID.ToString %>" data-name="<%=Server.HtmlEncode(c.Line_itemName) %>" data-visible="<%=If(c.Show = 1, "true", "false") %>" data-title="<%=If(c.Is_Title = 1, "true", "false") %>" data-calculated="<%=If(c.Is_Calculated_Value.GetValueOrDefault(), "true", "false") %>">

                                    <td>
                                        <div class="builder-order-control">
                                            <button type="button" class="builder-drag-handle js-builder-drag-handle" aria-label="Drag <%=Server.HtmlEncode(c.Line_itemName) %> to change its order" title="Drag to reorder; use Up or Down arrow while focused for keyboard sorting"><i class="fa fa-bars" aria-hidden="true"></i></button>
                                            <input onchange="return updateFieldOnChange('Report_LineItems','Sort_Order',this, 'Report_LineItemID','<%=c.Report_LineItemID.ToString %>');" id="Sort_Order_<%=c.Report_LineItemID.ToString %>" type="text" inputmode="numeric" aria-label="Sort order for <%=Server.HtmlEncode(c.Line_itemName) %>" value="<%=c.Sort_Order.ToString %>" class="form-group col-lg-12" />
                                        </div>
                                    </td>

                                    <td>
                                        <input onchange="return updateFieldOnChange('Report_LineItems','Line_itemName',this, 'Report_LineItemID','<%=c.Report_LineItemID.ToString %>');" id="Line_itemName_<%=c.Report_LineItemID.ToString %>" type="text" value="<%=c.Line_itemName %>" class="form-group col-lg-12" /></td>
                                    <td>
                                        <select onchange="return updateFieldOnChange('Report_LineItems','Is_Main',this, 'Report_LineItemID','<%=c.Report_LineItemID.ToString %>');" class="form-group " id="Is_Main_<%=c.Report_LineItemID.ToString %>">
                                            <option <%If c.Is_Main = 1 Then %> selected="selected" <% End If %> value="1">Yes</option>
                                            <option <%If c.Is_Main = 0 Then %> selected="selected" <% End If %> value="0">No</option>
                                        </select>

                                    </td>
                                    <td>
                                        <select onchange="return updateFieldOnChange('Report_LineItems','Is_Title',this, 'Report_LineItemID','<%=c.Report_LineItemID.ToString %>');" class="form-group " id="Is_Title_<%=c.Report_LineItemID.ToString %>">
                                            <option <%If c.Is_Title = 1 Then %> selected="selected" <% End If %> value="1">Yes</option>
                                            <option <%If c.Is_Title = 0 Then %> selected="selected" <% End If %> value="0">No</option>
                                        </select>

                                    </td>
                                    <td>
                                        <select onchange="return updateFieldOnChange('Report_LineItems','Show',this, 'Report_LineItemID','<%=c.Report_LineItemID.ToString %>');" class="form-group " id="Show_<%=c.Report_LineItemID.ToString %>">
                                            <option <%If c.Show = 1 Then %> selected="selected" <% End If %> value="1">Yes</option>
                                            <option <%If c.Show = 0 Then %> selected="selected" <% End If %> value="0">No</option>
                                        </select>

                                    </td>
                                    <td>
                                        <select onchange="return updateFieldOnChange('Report_LineItems','ReportSectionID',this, 'Report_LineItemID','<%=c.Report_LineItemID.ToString %>');" class="form-group " id="ReportSectionID_<%=c.Report_LineItemID.ToString %>">
                                            <%For Each rs In c.Report.ReportSections.OrderBy(Function(f) f.SortOrder)
                                            %>
                                            <option <%If c.ReportSectionID = rs.ReportSectionID Then %> selected="selected" <% End If %> value="<%=rs.ReportSectionID.ToString %>"><%=rs.ReportSectionName %></option>
                                            <%
                                                Next %>
                                        </select>

                                    </td>
                                    <td>
                                        <a href="#" class="btn btn-success btn-xs col-lg-12 col-md-12" data-toggle="modal" data-target="#calcAllR-<%=c.Report_LineItemID.ToString %>"><i class="fa fa-calculator"></i>More</a>
                                        <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="calcAllR-<%=c.Report_LineItemID.ToString %>">
                                            <div class="modal-dialog modal-lg">
                                                <div class="modal-content">

                                                    <div class="modal-header">
                                                        <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                                            <span aria-hidden="true">×</span>
                                                        </button>
                                                        <h4 class="modal-title">Calculated value formula</h4>
                                                    </div>
                                                    <div class="modal-body">
                                                        <div class="x_panel">
                                                            <div class="x_title">
                                                                <h3><%=c.Line_itemName %> </h3>
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
                                                                <div class="builder-ajax-form" id="Form-ReportRow-<%=c.Report_LineItemID.ToString %>" data-action="Action/?operation=submitReportRowUpdate&amp;item=<%=c.Report_LineItemID.ToString %>" data-method="post">

                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Add Month to Reporting Period</label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <input type="number" class="form-control" name="AddMonthToReportingPeriod_<%=c.Report_LineItemID.ToString %>" value="<%=c.AddMonthToReportingPeriod.ToString %>">
                                                                        </div>
                                                                    </div>

                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Calculated Value</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">

                                                                            <select id="Row_Is_Calculated_Value_<%=c.Report_LineItemID.ToString %>" name="Row_Is_Calculated_Value_<%=c.Report_LineItemID.ToString %>">
                                                                                <option value="">-- Select -- </option>
                                                                                <option <%If c.Is_Calculated_Value Then %> selected="selected" <% Else %><% End If%> value="Yes">Yes</option>
                                                                                <option <%If Not c.Is_Calculated_Value Then %> selected="selected" <% Else %><% End If%> value="No">No</option>
                                                                            </select>

                                                                        </div>

                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Static Value</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">


                                                                            <select id="Row_Is_Static_Value_<%=c.Report_LineItemID.ToString %>" name="Row_Is_Static_Value_<%=c.Report_LineItemID.ToString %>">
                                                                                <option value="">-- Select -- </option>
                                                                                <option <%If c.Is_Static_Value Then %> selected="selected" <% Else %><% End If%> value="Yes">Yes</option>
                                                                                <option <%If Not c.Is_Static_Value Then %> selected="selected" <% Else %><% End If%> value="No">No</option>
                                                                            </select>
                                                                        </div>

                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Inherited Value</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                            <select class="form-control" name="Is_Inherited_<%=c.Report_LineItemID.ToString %>" onchange="return inheritRowReportList('<%=c.Report_LineItemID.ToString %>');">
                                                                                <option <%If Not c.Is_Inherited Then %> selected="selected" <% End If %> value="0">No</option>
                                                                                <option <%If c.Is_Inherited Then %> selected="selected" <% End If %> value="1">Yes</option>
                                                                            </select>
                                                                        </div>

                                                                    </div>
                                                                    <div class="form-group" id="Is_Inherited_ReportRow_ReportList_<%=c.Report_LineItemID.ToString %>">
                                                                    </div>
                                                                    <div class="form-group" id="Is_Inherited_ReportRow_RowList_<%=c.Report_LineItemID.ToString %>">
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Where Query</label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <textarea type="text" class="form-control" id="QueryWhere_<%=c.Report_LineItemID.ToString %>" placeholder="eit.name IN('Entity Name 1','Entity Name 2')" name="QueryWhere_<%=c.Report_LineItemID.ToString %>"><%=c.QueryWhere %></textarea>
                                                                        </div>
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <button onclick="return PostForm('Form-ReportRow-<%=c.Report_LineItemID.ToString %>','Form-ReportRow-<%=c.Report_LineItemID.ToString %>','<%=HttpContext.Current.Request.Url.AbsoluteUri %>');" type="button" class="btn btn-primary">Save Changes</button>
                                                                        </div>
                                                                    </div>
                                                                </div>


                                                                <% If c.Is_Calculated_Value Then
                                                                %>
                                                                <a href="javascript: calcAllR('+','<%=c.Report_LineItemID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-plus"></i>Plus
                                                                </a>
                                                                <a href="javascript: calcAllR('-','<%=c.Report_LineItemID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-minus"></i>Minus
                                                                </a>
                                                                <a href="javascript: calcAllR('*','<%=c.Report_LineItemID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-m"></i>Multiply
                                                                </a>
                                                                <a href="javascript: calcAllR('/','<%=c.Report_LineItemID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-di"></i>Divide
                                                                </a>
                                                                <a href="javascript: calcAllR('','<%=c.Report_LineItemID.ToString %>');" class="btn btn-app">
                                                                    <i class="fa fa-right"></i>Equal
                                                                </a>
                                                                <a href="javascript: parseAllR('+','<%=c.Report_LineItemID.ToString %>','<%=HttpContext.Current.Request.Url.AbsoluteUri %>');" class="btn btn-app">
                                                                    <i class="fa fa-arrow-right"></i>Parse
                                                                </a>

                                                                <div class="ln_solid"></div>

                                                                <div class="form-group">
                                                                    <div class="col-md-9 col-sm-9 col-xs-12">
                                                                        <select class="form-control" id="schema_allR_<%=c.Report_LineItemID.ToString %>">

                                                                            <%For Each f In Report.Report_LineItems.Where(Function(r) r.Report_LineItemID <> c.Report_LineItemID)
                                                                            %>
                                                                            <option value="<%=f.Report_LineItemID.ToString %>"><%=f.Line_itemName %> </option>

                                                                            <%
                                                                                Next%>
                                                                        </select>

                                                                    </div>
                                                                </div>

                                                                <div class="form-group">
                                                                    <div class="builder-ajax-form" id="Form-CalcLineItemColumnAllR-<%=c.Report_LineItemID.ToString() %>" data-action="Action/?operation=submitColumnLineItemCalcAllR&amp;item=<%=c.Report_LineItemID.ToString() %>" data-method="post">
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">

                                                                            <textarea readonly="readonly" id="calcAllR_<%=c.Report_LineItemID.ToString %>" class="form-control" name="formulaDisplay_<%=c.Report_LineItemID.ToString %>"><%=c.Expression %></textarea>
                                                                            <input type="hidden" id="jExpressionAllR_<%=c.Report_LineItemID.ToString %>" name="ExpressionAllR_<%=c.Report_LineItemID.ToString %>" value="<%=c.Expression %>" />
                                                                        </div>
                                                                    </div>
                                                                </div>
                                                                <p><a href="javascript: clearAllR('<%=c.Report_LineItemID.ToString %>')" class="btn ">Clear Formula</a></p>
                                                                <%
                                                                    End If %>
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="modal-footer">
                                                        <button id="Form-ReportRow-<%=c.Report_LineItemID.ToString %>-button" type="button" class="btn btn-default" data-dismiss="modal">Close</button>
                                                    </div>

                                                </div>
                                            </div>
                                        </div>

                                    </td>




                                    <%For Each cc In c.LineItem_Columns.Where(Function(f) f.Show).OrderBy(Function(f) f.Sort_Order)

                                    %>
                                    <td>
                                        <%If c.Line_itemName <> "" Then %>
                                        <div style="padding: 2px;">

                                            <%If c.Is_Title Then
                                            %><%=cc.ColumnName %><%
                                                                     Else

                                                                         Dim theme As String = "primary"
                                                                         If cc.Is_Inherited And Not cc.Inherited_Lineitem_ColumnID Is Nothing Then
                                                                             theme = "success"
                                                                         End If
                                                                         If cc.StoredProc <> "" Then
                                                                             theme = "success"
                                                                         End If
                                                                         Dim mappingHealthTitle As String = String.Empty
                                                                         If ReportHealth IsNot Nothing AndAlso ReportHealth.IsRecursiveMapping(cc.LineItemColumnID) Then
                                                                             theme = "danger"
                                                                             mappingHealthTitle = "This mapping participates in a recursive dependency."
                                                                         End If

                                            %><a href="#" class="btn btn-<%=theme %> btn-xs col-lg-12 col-md-12" title="<%=Server.HtmlEncode(mappingHealthTitle) %>" data-toggle="modal" data-target="#col-<%=cc.LineItemColumnID.ToString %>"><i class="fa fa-edit"></i> <%=cc.Report_Columns.ColumnName %></a><%
                                                                                                                                                                                                                                                                If cc.EntityGroupBasedMapping Then
                                                                                                                                                                                                                                                                    For Each egc In cc.EntityGroupLineItem_Columns
                                            %>
                                            <p>
                                                <a data-toggle="modal" data-target="#ecol-<%=egc.EntityGroupLineItem_ColumnID.ToString %>" href="#" class="btn <%If egc.StoredProc = "" Then %>btn-primary<% Else %>btn-success<% End If %> btn-xs col-lg-12 col-md-12"><i class="fa fa-edit"></i><%=egc.EntityGroup.EntityGroupName %></a>
                                            </p>
                                            <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="ecol-<%=egc.EntityGroupLineItem_ColumnID.ToString %>">
                                                <div class="modal-dialog modal-lg">
                                                    <div class="modal-content">

                                                        <div class="modal-header">
                                                            <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                                                <span aria-hidden="true">×</span>
                                                            </button>
                                                            <h4 class="modal-title" id="myModalLabel2">Edit Line Item Column </h4>
                                                        </div>
                                                        <div class="modal-body">
                                                            <div class="x_panel">
                                                                <div class="x_title">
                                                                    <h3><%=c.Line_itemName %> : <%=cc.Sort_Order.ToString %> <small><%=cc.Report_Columns.ColumnName %></small> : <small><%=egc.EntityGroup.EntityGroupName %></small></h3>
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
                                                                    <div class="builder-ajax-form" id="Form-EReportRow-<%=egc.EntityGroupLineItem_ColumnID.ToString %>" data-action="Action/?operation=submitEReportRowUpdate&amp;item=<%=egc.EntityGroupLineItem_ColumnID.ToString %>" data-method="post">

                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Aggregation Type</label>

                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <select class="form-control" name="EAggregationTypeID_<%=egc.EntityGroupLineItem_ColumnID.ToString %>">
                                                                                <%For Each a In db.AggregationTypes
                                                                                %><option <%If egc.AggregationTypeID = a.AggregationTypeID Then %> selected="selected" <% End If %> value="<%=a.AggregationTypeID %>"><%=a.AggregationTypeName %></option>
                                                                                <%
                                                                                    Next %>
                                                                            </select>
                                                                        </div>
                                                                    </div>

                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Schema Field Name</label>
                                                                        <div class="col-md-6 col-sm-6 col-xs-12">
                                                                            <input id="ESchemaField_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" onchange="eupdate_select('<%=egc.EntityGroupLineItem_ColumnID.ToString %>');" type="text" class="form-control" placeholder="Schema Field Name (From Vizor Builder)" name="ESchemaFieldName_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" value="<%=egc.SchemaFieldName %>">
                                                                        </div>
                                                                        <div class="col-md-3 col-sm-3 col-xs-12">
                                                                            <a href="javascript: esmart_schema_search('<%=egc.EntityGroupLineItem_ColumnID.ToString %>'); " class="btn btn-default btn-xs"><i class="fa fa-search"></i>Smart Search!!</a>
                                                                        </div>

                                                                    </div>
                                                                    <div id="ESmart_Schema_Ajax_<%=egc.EntityGroupLineItem_ColumnID.ToString %>"></div>
                                                                    <div id="ESchema_Ajax_<%=egc.EntityGroupLineItem_ColumnID.ToString %>">
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">COUNT Parameter</label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <input type="text" class="form-control" placeholder="COUNT Parameter" name="ECountParameter_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" value="<%=egc.CountParameter %>">
                                                                        </div>
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">COUNT Data Type</label>

                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <select class="form-control" name="ECountDataType_<%=egc.EntityGroupLineItem_ColumnID.ToString %>">
                                                                                <option value="">--Select--</option>
                                                                                <option <%If egc.CountDataType = "bit" Then %> selected="selected" <% End If %> value="bit">bit</option>
                                                                                <option <%If egc.CountDataType = "int" Then %> selected="selected" <% End If %> value="int">int</option>
                                                                                <option <%If egc.CountDataType = "bigint" Then %> selected="selected" <% End If %> value="bigint">bigint</option>
                                                                                <option <%If egc.CountDataType = "varchar" Then %> selected="selected" <% End If %> value="varchar">varchar</option>
                                                                                <option <%If egc.CountDataType = "datetime" Then %> selected="selected" <% End If %> value="datetime">date</option>
                                                                            </select>
                                                                        </div>
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Calculated Value</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                            <label>
                                                                                <input value="Yes" type="checkbox" class="flat" name="EIs_Calculated_Value_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" <%If egc.Is_CalculatedValue Then %> checked="checked" <% Else %><% End If%>>
                                                                            </label>
                                                                        </div>

                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Update From Schema</label>
                                                                        <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                            <label>
                                                                                <input value="Yes" type="checkbox" class="flat" name="EUpdateSchema_<%=egc.EntityGroupLineItem_ColumnID.ToString %>">
                                                                            </label>
                                                                        </div>

                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Database Table Name </label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <input id="ETableName_<%=egc.EntityGroupLineItem_ColumnID.ToString%>" type="text" class="form-control" placeholder="Database Table Name" name="ETableName_<%=egc.EntityGroupLineItem_ColumnID.ToString%>" value="<%=egc.TableName %>">
                                                                        </div>
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Database column Name </label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <input id="EColumnName_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" type="text" class="form-control" placeholder="Database Column Name" name="EColumnName_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" value="<%=egc.ColumnName %>">
                                                                        </div>
                                                                    </div>
                                                                    <div class="form-group">
                                                                        <label class="control-label col-md-3 col-sm-3 col-xs-12">Database Function</label>
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <input type="text" class="form-control" readonly="readonly" placeholder="Database Function" name="EStoredProc_<%=egc.EntityGroupLineItem_ColumnID.ToString%>" value="<%=egc.StoredProc %>">
                                                                        </div>
                                                                    </div>
                                                                    <input name="EExpression_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" type="hidden" id="EExpression_<%=egc.EntityGroupLineItem_ColumnID.ToString %>" value="<%=egc.Expression %>" />

                                                                    <div class="ln_solid"></div>
                                                                        <div class="form-group">
    <div class="col-md-9 col-sm-9 col-xs-12">
        <button onclick="return PostForm('Form-EReportRow-<%=egc.EntityGroupLineItem_ColumnID.ToString %>','Form-EReportRow-<%=egc.EntityGroupLineItem_ColumnID.ToString %>','<%=HttpContext.Current.Request.Url.AbsoluteUri %>');" type="button" class="btn btn-primary">Save Changes</button>
    </div>
</div>
</div>
                                                                </div>
                                                            </div>
                                                        </div>
                                                        <div class="modal-footer">
                                                            <button type="button" class="btn btn-default" data-dismiss="modal">Close</button>
                                                        </div>

                                                    </div>
                                                </div>
                                            </div>
                                            <%
                                                    Next
                                                Else
                                            %>

                                            <%
                                                    End If


                                                End If %>

                                            <% %>


                                            <%If cc.Is_CalculatedValue Then
                                                    If cc.Expression Is Nothing Or cc.Expression = "" Then
                                            %>
                                            <a href="#" class="btn btn-warning btn-xs col-lg-12 col-md-12" data-toggle="modal" data-target="#calc-<%=cc.LineItemColumnID.ToString %>"><i class="fa fa-calculator"></i>Calc</a>
                                            <%
                                                Else
                                            %>
                                            <a href="#" class="btn btn-success btn-xs col-lg-12 col-md-12" data-toggle="modal" data-target="#calc-<%=cc.LineItemColumnID.ToString %>"><i class="fa fa-calculator"></i>Calc</a>
                                            <%
                                                End If
                                            %>
                                            <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="calc-<%=cc.LineItemColumnID.ToString %>">
                                                <div class="modal-dialog modal-lg">
                                                    <div class="modal-content">

                                                        <div class="modal-header">
                                                            <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                                                <span aria-hidden="true">×</span>
                                                            </button>
                                                            <h4 class="modal-title">Calculated value formula</h4>
                                                        </div>
                                                        <div class="modal-body">
                                                            <div class="x_panel">
                                                                <div class="x_title">
                                                                    <h3><%=c.Line_itemName %> : <%=cc.Sort_Order.ToString %> <small><%=cc.Report_Columns.ColumnName %></small></h3>
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
                                                                <p>
                                                                    <button type="button" onclick="return changeCalc('1','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-primary">Basic Calculator</button>
                                                                    <button type="button" onclick="return changeCalc('2','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-primary">Advanced Calculator</button>
                                                                </p>
                                                                <div class="x_content form-horizontal form-label-left input_mask">
                                                                    <br />

                                                                    <div id="basicCalc_<%=cc.LineItemColumnID.ToString %>">
                                                                        <a href="javascript: calc('+','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-plus"></i>Plus
                                                                        </a>
                                                                        <a href="javascript: calc('-','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-minus"></i>Minus
                                                                        </a>
                                                                        <a href="javascript: calc('*','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-times"></i>Multiply
                                                                        </a>
                                                                        <a href="javascript: calc('/','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-divide"></i>Divide
                                                                        </a>

                                                                        <a href="javascript: calc('','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-equals"></i>Equal
                                                                        </a>
                                                                        <a href="javascript: parse('+','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-arrow-right"></i>Parse
                                                                        </a>

                                                                    </div>
                                                                    <div id="advanceCalc_<%=cc.LineItemColumnID.ToString %>" style="display: none;">
                                                                        <a href="javascript: calc('+','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-plus"></i>Plus
                                                                        </a>
                                                                        <a href="javascript: calc('-','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-minus"></i>Minus
                                                                        </a>
                                                                        <a href="javascript: calc('*','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-times"></i>Multiply
                                                                        </a>
                                                                        <a href="javascript: calc('/','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-divide"></i>Divide
                                                                        </a>
                                                                        <a href="javascript: calc('(','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">(
                                                                        </a>
                                                                        <a href="javascript: calc(')','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">)
                                                                        </a>
                                                                        <a href="javascript: calc('','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-equals"></i>Equal
                                                                        </a>
                                                                        <a href="javascript: parse('+','<%=cc.LineItemColumnID.ToString %>');" class="btn btn-app">
                                                                            <i class="fa fa-arrow-right"></i>Parse
                                                                        </a>

                                                                    </div>


                                                                    <div class="ln_solid"></div>

                                                                    <div class="form-group">
                                                                        <div class="col-md-9 col-sm-9 col-xs-12">
                                                                            <select class="form-control" id="schema_<%=cc.LineItemColumnID.ToString %>">
                                                                                <option value=""></option>

                                                                                <%For Each f In Report.Report_LineItems
                                                                                        For Each ff In f.LineItem_Columns.Where(Function(ffff) ffff.StoredProc <> "" Or ffff.Is_CalculatedValue Or ffff.Is_Inherited)
                                                                                %>
                                                                                <%If ff.Is_CalculatedValue Then
                                                                                %>
                                                                                <option value="<%=ff.LineItemColumnID.ToString %>"><%=f.Line_itemName %> : <%=ff.Report_Columns.ColumnName %></option>

                                                                                <%
                                                                                    ElseIf ff.Is_Inherited Then
                                                                                %>
                                                                                <%--<option value="<%=ff.Inherited_Lineitem_ColumnID.ToString %>"><%=f.Line_itemName %> : <%=ff.Report_Columns.ColumnName %> (Original)</option>--%>
                                                                                <option value="<%=ff.LineItemColumnID.ToString %>"><%=f.Line_itemName %> : <%=ff.Report_Columns.ColumnName %></option>

                                                                                <%
                                                                                    Else %>
                                                                                <option value="<%=ff.StoredProc %>"><%=f.Line_itemName %> : <%=ff.Report_Columns.ColumnName %></option>
                                                                                <% End if
                                                                                        Next
                                                                                    Next%>
                                                                            </select>

                                                                        </div>
                                                                    </div>

                                                                    <%
                                                                        Dim Expression As String = ""
                                                                        Dim Expression2 As String = ""
                                                                        Dim Expression3 As String = ""
                                                                        If cc.ProcessTypeID = 4 Then
                                                                            If Not cc.Expression Is Nothing And cc.Expression <> "" Then
                                                                                Dim s = cc.Expression.Split(",")
                                                                                Expression = s(0)
                                                                                Expression2 = s(1)
                                                                                Expression3 = s(2)
                                                                            End If
                                                                        Else
                                                                            Expression = cc.Expression
                                                                        End If

                                                                    %>

                                                                    <div class="form-group">
                                                                        <div class="builder-ajax-form" id="Form-CalcLineItemColumn-<%=cc.LineItemColumnID.ToString() %>" data-action="Action/?operation=submitColumnLineItemCalc&amp;item=<%=cc.LineItemColumnID.ToString() %>" data-method="post">
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">

                                                                                <textarea id="calc_<%=cc.LineItemColumnID.ToString %>" class="form-control" name="formulaDisplay"><%=Expression %></textarea><br />
                                                                                <div id="advanceCalcExp_<%=cc.LineItemColumnID.ToString %>" style="display: none;">
                                                                                    <textarea id="calc2_<%=cc.LineItemColumnID.ToString %>" class="form-control" name="Expression2_<%=cc.LineItemColumnID.ToString %>"><%=Expression2 %></textarea><br />
                                                                                    <textarea id="calc3_<%=cc.LineItemColumnID.ToString %>" class="form-control" name="Expression3_<%=cc.LineItemColumnID.ToString %>"><%=Expression3 %></textarea><br />

                                                                                </div>
                                                                                <input type="hidden" id="jExpression_<%=cc.LineItemColumnID.ToString %>" name="Expression_<%=cc.LineItemColumnID.ToString %>" value="" />
                                                                                <input type="hidden" id="jType_<%=cc.LineItemColumnID.ToString %>" name="Type_<%=cc.LineItemColumnID.ToString %>" value="1" />
                                                                            </div>
                                                                        </div>
                                                                    </div>
                                                                    <p><a href="javascript: clear('<%=cc.LineItemColumnID.ToString %>')" class="btn ">Clear Formula</a></p>
                                                                </div>
                                                            </div>
                                                        </div>
                                                        <div class="modal-footer">
                                                            <button id="calc-<%=cc.LineItemColumnID.ToString %>-button" type="button" class="btn btn-default" data-dismiss="modal">Close</button>
                                                        </div>

                                                    </div>
                                                </div>
                                            </div>
                                            <%
                                                End If %>


                                            <!-- Small modal -->

                                            <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="col-<%=cc.LineItemColumnID.ToString %>">
                                                <div class="modal-dialog modal-lg">
                                                    <div class="modal-content">

                                                        <div class="modal-header">
                                                            <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                                                <span aria-hidden="true">×</span>
                                                            </button>
                                                            <h4 class="modal-title" id="myModalLabel2">Edit Line Item Column</h4>
                                                        </div>
                                                        <div class="modal-body">
                                                            <div class="x_panel">
                                                                <div class="x_title">
                                                                    <h3><%=c.Line_itemName %> : <%=cc.Sort_Order.ToString %> <small><%=cc.Report_Columns.ColumnName %> (LC ID: <%=cc.LineItemColumnID.ToString%>) </small></h3>
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
                                                                <div class="builder-ajax-form" id="Form-LineItemColumn-<%=cc.LineItemColumnID.ToString() %>" data-action="Action/?operation=submitColumnLineItemUpdate&amp;item=<%=cc.LineItemColumnID.ToString() %>" data-method="post">
                                                                    <div class="x_content form-horizontal form-label-left input_mask">
                                                                        <br />
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Single or Multiple Map</label>

                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <select class="form-control" name="EntityGroupBasedMapping_<%=cc.LineItemColumnID.ToString %>">
                                                                                    <option <%If Not cc.EntityGroupBasedMapping Then %> selected="selected" <% End If %> value="0">Single Entity Mapping</option>
                                                                                    <option <%If cc.EntityGroupBasedMapping Then %> selected="selected" <% End If %> value="1">Multiple Entities Mapping</option>
                                                                                </select>
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Aggregation Type</label>

                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <select class="form-control" name="AggregationTypeID_<%=cc.LineItemColumnID.ToString %>">
                                                                                    <%For Each a In db.AggregationTypes
                                                                                    %><option <%If cc.AggregationTypeID = a.AggregationTypeID Then %> selected="selected" <% End If %> value="<%=a.AggregationTypeID %>"><%=a.AggregationTypeName %></option>
                                                                                    <%
                                                                                        Next %>
                                                                                </select>
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Schema Field Name</label>
                                                                            <div class="col-md-6 col-sm-6 col-xs-12">
                                                                                <input id="SchemaField_<%=cc.LineItemColumnID.ToString %>" onchange="update_select('<%=cc.LineItemColumnID.ToString %>');" type="text" class="form-control" placeholder="Schema Field Name (From Vizor Builder)" name="SchemaFieldName_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.SchemaFieldName %>">
                                                                            </div>
                                                                            <div class="col-md-3 col-sm-3 col-xs-12">
                                                                                <a href="javascript: update_select('<%=cc.LineItemColumnID.ToString %>');" class="btn btn-default btn-xs"><i class="fa fa-search"></i>Lookup</a>
                                                                            </div>
                                                                            <div class="col-md-3 col-sm-3 col-xs-12">
                                                                                <a href="javascript: smart_schema_search('<%=cc.LineItemColumnID.ToString %>');" class="btn btn-default btn-xs"><i class="fa fa-search"></i>Smart Search!!</a>
                                                                            </div>
                                                                            <div class="col-md-3 col-sm-3 col-xs-12">
                                                                                <a href="javascript: smart_schema_suggest('<%=cc.LineItemColumnID.ToString %>');" class="btn btn-default btn-xs"><i class="fa fa-search"></i>Smart Suggest!!</a>
                                                                            </div>
                                                                        </div>
                                                                        <div id="Smart_Schema_Ajax_<%=cc.LineItemColumnID.ToString %>"></div>
                                                                        <div id="Smart_Suggest_Ajax_<%=cc.LineItemColumnID.ToString %>"></div>
                                                                        <div id="Schema_Ajax_<%=cc.LineItemColumnID.ToString %>">
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">COUNT Parameter</label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <input type="text" class="form-control" placeholder="COUNT Parameter" name="CountParameter_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.CountParameter %>">
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Add Month to Reporting Period</label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <input type="number" class="form-control" name="AddMonthToReportingPeriod_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.AddMonthToReportingPeriod.ToString %>">
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">COUNT Data Type</label>

                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <select class="form-control" name="CountDataType_<%=cc.LineItemColumnID.ToString %>">
                                                                                    <option value="">--Select--</option>
                                                                                    <option <%If cc.CountDataType = "bit" Then %> selected="selected" <% End If %> value="bit">bit</option>
                                                                                    <option <%If cc.CountDataType = "int" Then %> selected="selected" <% End If %> value="int">int</option>
                                                                                    <option <%If cc.CountDataType = "bigint" Then %> selected="selected" <% End If %> value="bigint">bigint</option>
                                                                                    <option <%If cc.CountDataType = "varchar" Then %> selected="selected" <% End If %> value="varchar">varchar</option>
                                                                                    <option <%If cc.CountDataType = "datetime" Then %> selected="selected" <% End If %> value="datetime">date</option>
                                                                                </select>
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Calculated Value</label>
                                                                            <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                                <label>
                                                                                    <input id="Is_Calculated_Value_<%=cc.LineItemColumnID.ToString %>" value="Yes" type="checkbox" class="flat" name="Is_Calculated_Value_<%=cc.LineItemColumnID.ToString %>" <%If cc.Is_CalculatedValue Then %> checked="checked" <% Else %><% End If%>>
                                                                                </label>
                                                                            </div>

                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Static Value</label>
                                                                            <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                                <label>
                                                                                    <input id="Is_Static_Value_<%=cc.LineItemColumnID.ToString %>" value="Yes" type="checkbox" class="flat" name="Is_Static_Value_<%=cc.LineItemColumnID.ToString %>" <%If cc.Is_Static_Value Then %> checked="checked" <% Else %><% End If%>>
                                                                                </label>
                                                                            </div>

                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Update From Schema</label>
                                                                            <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                                <label>
                                                                                    <input value="Yes" type="checkbox" class="flat" name="UpdateSchema_<%=cc.LineItemColumnID.ToString %>">
                                                                                </label>
                                                                            </div>

                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Schema ID</label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <input type="text" class="form-control" placeholder="Database Table Name" name="SchemaID_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.Sort_Order.ToString %>">
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Inherited Value</label>
                                                                            <div class="checkbox col-md-9 col-sm-9 col-xs-12">
                                                                                <select class="form-control" name="Is_Inherited_<%=cc.LineItemColumnID.ToString %>" onchange="inherit('<%=cc.LineItemColumnID.ToString %>')">
                                                                                    <option <%If Not cc.Is_Inherited Then %> selected="selected" <% End If %> value="0">No</option>
                                                                                    <option <%If cc.Is_Inherited Then %> selected="selected" <% End If %> value="1">Yes</option>
                                                                                </select>
                                                                            </div>

                                                                        </div>
                                                                        <%If cc.Is_Inherited And Not cc.EntityGroupBasedMapping And Not cc.Inherited_Lineitem_ColumnID Is Nothing Then
                                                                        %>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Is Inherited Value</label>
                                                                            <div class="checkbox col-md-9 col-sm-9 col-xs-12">

                                                                                <p>Report: <%=cc.LineItem_Columns2.Report_Columns.Report.Report_Name %></p>
                                                                                <p>Item: <%=cc.LineItem_Columns2.Report_LineItems.Line_itemName %> - <%=cc.LineItem_Columns2.Report_Columns.ColumnName %> </p>
                                                                            </div>

                                                                        </div>
                                                                        <%
                                                                            Else
                                                                                cc.Is_Inherited = False
                                                                            End If %>
                                                                        <div class="form-group" id="Is_Inherited_<%=cc.LineItemColumnID.ToString %>">
                                                                        </div>
                                                                        <div class="form-group" id="Is_Inherited_Item_<%=cc.LineItemColumnID.ToString %>">
                                                                        </div>


                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Database Table Name </label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <input id="TableName_<%=cc.LineItemColumnID.ToString %>" type="text" class="form-control" placeholder="Database Table Name" name="TableName_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.TableName %>">
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Database column Name </label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <input id="ColumnName_<%=cc.LineItemColumnID.ToString %>" type="text" class="form-control" placeholder="Database Column Name" name="ColumnName_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.ColumnName %>">
                                                                            </div>
                                                                        </div>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Database Function</label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <input type="text" class="form-control" placeholder="Database Function" name="StoredProc_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.StoredProc %>">
                                                                            </div>
                                                                        </div>


                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Expression</label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <textarea type="text" class="form-control" id="Expression_<%=cc.LineItemColumnID.ToString %>" placeholder="Expression" name="Expression_<%=cc.LineItemColumnID.ToString %>"><%=cc.Expression %></textarea>
                                                                            </div>
                                                                        </div>

                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Where Query</label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <textarea type="text" class="form-control" id="QueryWhere_<%=cc.LineItemColumnID.ToString %>" placeholder="eit.name IN('Entity Name 1','Entity Name 2')" name="QueryWhere_<%=cc.LineItemColumnID.ToString %>"><%=cc.QueryWhere %></textarea>
                                                                            </div>
                                                                        </div>

                                                                        <% If cc.Is_Static_Value Then
                                                                        %>
                                                                        <div class="form-group">
                                                                            <label class="control-label col-md-3 col-sm-3 col-xs-12">Static Value</label>
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <textarea type="text" class="form-control" id="Static_Value_<%=cc.LineItemColumnID.ToString %>" placeholder="" name="Expression_<%=cc.LineItemColumnID.ToString %>"><%=cc.Expression %></textarea>
                                                                            </div>
                                                                        </div>
                                                                        <%
                                                                            End If %>


                                                                        <div class="ln_solid"></div>

                                                                        <div class="form-group">
                                                                            <div class="col-md-9 col-sm-9 col-xs-12">
                                                                                <button onclick="return PostForm('Form-LineItemColumn-<%=cc.LineItemColumnID.ToString() %>','col-<%=cc.LineItemColumnID.ToString %>');" type="button" class="btn btn-primary">Save Changes</button>
                                                                            </div>
                                                                        </div>
                                                                    </div>

                                                                </div>
                                                            </div>
                                                        </div>
                                                        <div class="modal-footer">
                                                            <button id="col-<%=cc.LineItemColumnID.ToString %>-button" type="button" class="btn btn-default" data-dismiss="modal">Close</button>
                                                        </div>

                                                    </div>
                                                </div>
                                            </div>

                                            <% If cc.Is_Static_Value Then
                                            %>
                                            <input type="text" name="Is_Static_Value_<%=cc.LineItemColumnID.ToString %>" value="<%=cc.Static_Value %>" />
                                            <%
                                                End If %>
                                        </div>
                                        <%End If %>
                                    </td>


                                    <%

                                        Next %>

                                    <%
                                        db.SaveChanges()
                                    %>

                                    <td>
                                        <button class="btn btn-default btn-xs" data-toggle="modal" data-target="#add-dim-<%=c.Report_LineItemID.ToString %>"><i class="fa fa-plus"></i>Add</button>

                                        <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="add-dim-<%=c.Report_LineItemID.ToString %>">
                                            <div class="modal-dialog modal-lg">
                                                <div class="modal-content">

                                                    <div class="modal-header">
                                                        <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                                                            <span aria-hidden="true">×</span>
                                                        </button>
                                                        <h4 class="modal-title">Add Line Item Dimension</h4>
                                                    </div>
                                                    <div class="modal-body">
                                                        <div class="x_panel">
                                                            <div class="x_title">
                                                                <h3><%=c.Line_itemName %> : Add New</h3>
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
                                                            </div>
                                                        </div>
                                                    </div>
                                                    <div class="modal-footer">
                                                        <button type="button" class="btn btn-default" data-dismiss="modal">Close</button>
                                                    </div>

                                                </div>
                                            </div>
                                        </div>

                                    </td>
                                </tr>
                                <%
                                    Next
                                %>
                            </tbody>
                            <tfoot>
                                <tr>
                                    <td colspan="<%=8 + Report.Report_Columns.Count %>"><a href="javascript: addItem();" class="btn btn-xs btn-primary"><i class="fa fa-plus"></i>New Item</a></td>
                                </tr>
                            </tfoot>
                        </table>
                        </div>
                        <div class="builder-design-pager" aria-label="Form designer pagination">
                            <button type="button" class="btn btn-default js-builder-page-prev"><i class="fa fa-chevron-left" aria-hidden="true"></i> Previous</button>
                            <span class="js-builder-page-status" aria-live="polite">Page 1 of 1</span>
                            <button type="button" class="btn btn-default js-builder-page-next">Next <i class="fa fa-chevron-right" aria-hidden="true"></i></button>
                        </div>
                        <input type="hidden" name="Add" id="Add" value="0" />
                        <script type="text/javascript">
                            var count = 0;

                            var formulaCount = 0;

                            function addItem() {
                                count++;
                                document.getElementById("Add").value = count;
                                var s = count + <%=Sort%>;
                                $('#Report_Items').append('<tr class="js-builder-design-row is-new" data-name="" data-visible="true" data-title="false" data-calculated="false"><td><div class="builder-order-control"><span class="builder-drag-handle is-disabled" aria-hidden="true"><i class="fa fa-bars"></i></span><input value="' + s + '" name="Add_Sort_Order_' + count + '" aria-label="New item sort order" class="form-group col-lg-12" type="text"/></div></td><td><input type="text" name="Add_LineItem_Name_' + count + '" class="form-group col-lg-12" placeholder="New line item name" /></td> <td><select class="form-group " name="Add_Is_Main_' + count + '" ><option value="1">Yes</option><option value="0">No</option></select></td> <td><select class="form-group " name="Add_Is_Title_' + count + '" ><option value="0">No</option><option value="1">Yes</option></select></td><td><select class="form-group" name="Add_Show_' + count + '" ><option value="1">Yes</option><option value="0">No</option></select> <input type="hidden" name="Add_AggregationTypeID_' + count + '" value="1" /></td> <td><select class="form-group " name="Add_ReportSectionID_' + count + '" ><%=getReportSectionDropdown()%></select></td></tr>');
                            }

                            function clear(item) {
                                document.getElementById('calc_' + item).value = '';
                                document.getElementById('calc2_' + item).value = '';
                                document.getElementById('calc3_' + item).value = '';
                            }
                            function calc(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;

                                $('#calc_' + item).append('' + list[INDEX].value + '' + sign)

                                if (list[INDEX].value == "") {
                                    $('#calc2_' + item).append('' + sign)
                                } else {
                                    $('#calc2_' + item).append('' + formulaCount + '' + sign)
                                    $('#calc3_' + item).append('' + list[INDEX].value + '|')
                                }


                                if (INDEX > 0) {
                                    formulaCount++;
                                }
                            }

                            function changeCalc(type, item) {
                                if (type == '1') {
                                    document.getElementById('basicCalc_'+item).style.display = 'block';
                                    document.getElementById('advanceCalc_' + item).style.display = 'none';
                                    document.getElementById('advanceCalcExp_' + item).style.display = 'none';
                                } else {
                                    document.getElementById('basicCalc_' + item).style.display = 'none';
                                    document.getElementById('advanceCalc_' + item).style.display = 'block';
                                    document.getElementById('advanceCalcExp_' + item).style.display = 'block';
                                }
                                document.getElementById("jType_" + item).value = type;
                            }

                            function parse(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;
                                document.getElementById("jExpression_" + item).value = document.getElementById('calc_' + item).value;


                                //alert("Parsed Okay");

                                var f = document.getElementById("Form-CalcLineItemColumn-" + item);

                                postFormAjax(f, 'calc-' + item);


                            }



                            function clearAll(item) {
                                document.getElementById('calcAll_' + item).value = '';
                            }
                            function calcAll(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_all_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;


                                $('#calcAll_' + item).append('' + list[INDEX].value + '' + sign)
                            }

                            function parseAll(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_all_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;
                                //document.getElementById("ExpressionAll_" + item).value = document.getElementById('calcAll_' + item).value;


                                alert("Parsed Okay");
                            }
                            function parseAllC(sign, item, url) {
                                // Gets your select
                                var list = document.getElementById('schema_allC_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;
                                document.getElementById("jExpressionAllC_" + item).value = document.getElementById('calcAllC_' + item).value;


                                //alert("Parsed Okay");

                                var f = document.getElementById("Form-CalcLineItemColumnAllC-" + item);

                                postFormAjax(f, 'Form-ReportColumn-' + item, url);
                            }
                            function parseAllR(sign, item, url) {
                                // Gets your select
                                var list = document.getElementById('schema_allR_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;
                                document.getElementById("jExpressionAllR_" + item).value = document.getElementById('calcAllR_' + item).value;


                                //alert("Parsed Okay");

                                var f = document.getElementById("Form-CalcLineItemColumnAllR-" + item);

                                postFormAjax(f, 'Form-ReportRow-' + item, url);
                            }
                            function calcAllC(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_allC_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;


                                $('#calcAllC_' + item).append('' + list[INDEX].value + '' + sign)
                            }
                            function calcAllR(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_allR_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;


                                $('#calcAllR_' + item).append('' + list[INDEX].value + '' + sign)
                            }
                            function clearAllC(item) {
                                document.getElementById('calcAllC_' + item).value = '';
                            }
                            function clearAllR(item) {
                                document.getElementById('calcAllR_' + item).value = '';
                            }
                            function calcAll(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_allC_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;


                                $('#calcAllC_' + item).append('' + list[INDEX].value + '' + sign)
                            }

                            function parseAll(sign, item) {
                                // Gets your select
                                var list = document.getElementById('schema_allC_' + item);

                                // Get the index of selected item, first item 0, second item 1 etc ...
                                var INDEX = list.selectedIndex;
                                //document.getElementById("ExpressionAll_" + item).value = document.getElementById('calcAll_' + item).value;


                                alert("Parsed Okay");
                            }

                        </script>
                        <div class="builder-designer-savebar">
                            <label class="builder-update-schema">
                                <input value="Yes" type="checkbox" class="flat" name="UpdateAllSchema">
                                <span><strong>Update all schema mappings</strong><small>Apply schema changes across every line item when saving.</small></span>
                            </label>
                            <div class="builder-designer-save-actions">
                            <asp:Button ID="Button2" runat="server" Text="Save changes" CssClass="btn btn-primary" />
                            <asp:Button ID="Button3" runat="server" Text="Update Functions" CssClass="btn btn-default" />
                            <asp:Button ID="Button10" runat="server" Text="Update Calculations" CssClass="btn btn-default" />
                            <asp:Button ID="Button4" runat="server" Text="Refresh Report" CssClass="btn btn-default" />
                            <asp:Button ID="Button8" runat="server" Text="Auto Sort" CssClass="btn btn-default" />
                            </div>
                        </div>

                    </div>
                </div>
            </div>
            </section>

            <%
                End If %>
        </div>
    </div>
    <% If Report IsNot Nothing Then %>
    <div class="modal fade" tabindex="-1" role="dialog" aria-labelledby="report-security-title" aria-hidden="true" id="report-security">
        <div class="modal-dialog modal-lg" role="document">
            <div class="modal-content">
                <div class="modal-header bg-light">
                    <div>
                        <h5 class="modal-title" id="report-security-title"><i class="icon-shield-check text-primary mr-2"></i>Report security</h5>
                        <p class="text-muted mb-0 font-size-sm">Control access to <%: Report.Report_Name %> with Windows Active Directory identities.</p>
                    </div>
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close"><span aria-hidden="true">&times;</span></button>
                </div>
                <div class="modal-body">
                    <div class="alert alert-info alert-styled-left border-left-info">
                        Reporting Suite administrators always retain access. Security is checked again on the server whenever the report or its data endpoint is opened.
                    </div>
                    <div class="builder-security-mode">
                        <div class="custom-control custom-switch">
                            <input id="report-security-restricted" name="ReportSecurityRestricted" value="1" type="checkbox" class="custom-control-input" <% If ReportSecurityRestricted Then %>checked="checked"<% End If %> />
                            <label class="custom-control-label font-weight-semibold" for="report-security-restricted">Restrict this report to configured AD users and groups</label>
                        </div>
                        <p class="builder-modal-help mb-0 mt-2">When disabled, every Windows-authenticated Reporting Suite user can open the report.</p>
                    </div>

                    <div id="report-security-fields" class="mt-3">
                        <div class="form-group">
                            <label class="font-weight-semibold" for="report-security-users">Active Directory users</label>
                            <textarea id="report-security-users" name="ReportSecurityUsers" class="form-control" rows="4" placeholder="NAMFISA\username&#10;username@namfisa.com"><%: ReportSecurityUsersText %></textarea>
                            <span class="form-text text-muted">Enter one user per line. Domain-qualified names are recommended.</span>
                        </div>
                        <div class="form-group mb-0">
                            <label class="font-weight-semibold" for="report-security-groups">Active Directory groups</label>
                            <textarea id="report-security-groups" name="ReportSecurityGroups" class="form-control" rows="4" placeholder="NAMFISA\Reporting - Finance&#10;NAMFISA\Reporting - Supervisors"><%: ReportSecurityGroupsText %></textarea>
                            <span class="form-text text-muted">Membership is evaluated through the current Windows identity. Nested group handling follows the IIS/AD token.</span>
                        </div>
                    </div>

                    <% If ReportSecurityRules.Count > 0 Then %>
                    <div class="mt-3">
                        <span class="font-weight-semibold font-size-sm">Current access list</span>
                        <div class="builder-security-list">
                            <% For Each accessRule As ReportingSuite.ReportAccessRuleInfo In ReportSecurityRules %>
                            <span class="builder-security-chip"><i class="<%=If(accessRule.IsGroup, "icon-users4", "icon-user") %>"></i><%: accessRule.Grantee %></span>
                            <% Next %>
                        </div>
                    </div>
                    <% End If %>
                </div>
                <div class="modal-footer">
                    <span class="mr-auto text-muted font-size-sm"><i class="icon-windows8 mr-1"></i>Signed in as <%: CurrentWindowsAccount %></span>
                    <button type="button" class="btn btn-light" data-dismiss="modal">Cancel</button>
                    <button type="submit" name="builderAction" value="saveSecurity" class="btn btn-primary"><i class="icon-floppy-disk mr-2"></i>Save security</button>
                </div>
            </div>
        </div>
    </div>
    <% End If %>
    <div class="modal fade bs-example-modal-sm" tabindex="-1" role="dialog" aria-hidden="true" id="new-report">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">

                <div class="modal-header">
                    <button type="button" class="close" data-dismiss="modal" aria-label="Close">
                        <span aria-hidden="true">×</span>
                    </button>
                    <h4 class="modal-title">Create New Report</h4>
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
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Report Name</label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <input type="text" class="form-control" placeholder="Report Name" name="New_Report_Name">
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Report Name</label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <select class="form-control" name="New_EnityGroupID">
                                        <%For Each e In db.EntityGroups
                                        %>
                                        <option value="<%=e.EntityGroupID %>"><%=e.EntityGroupName
                                        %></option>
                                        <%
                                            Next %>
                                    </select>
                                </div>
                            </div>
                            <div class="form-group">
                                <label class="control-label col-md-3 col-sm-3 col-xs-12">Sort Order</label>
                                <div class="col-md-9 col-sm-9 col-xs-12">
                                    <input type="text" class="form-control" placeholder="Sort Order" name="New_Sort_Order">
                                </div>
                            </div>

                        </div>
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-default" data-dismiss="modal">Close</button>

                    <asp:Button ID="Button5" runat="server" Text="Create Report" CssClass="btn btn-primary" />
                </div>

            </div>
        </div>
    </div>
    <script type="text/javascript">

        function smart_schema_search(id) {

            $("#Smart_Schema_Ajax_" + id).empty().append('<p>Smart Searching Schema, please wait...!</p>').load('Ajax.aspx?action=smart_schema&id=' + encodeURIComponent(id) + '&prefix=');
        }
        function smart_schema_suggest(id) {

            $("#Smart_Suggest_Ajax_" + id).empty().append('<p>Smart Suggesting, please wait...!</p>').load('Ajax.aspx?action=smart_suggest&id=' + encodeURIComponent(id) + '&prefix=&formset=<%=Request.QueryString("formset")%>');
        }
        function esmart_schema_search(id) {

            $("#ESmart_Schema_Ajax_" + id).empty().append('<p>Smart Searching Schema, please wait...!</p>').load('Ajax.aspx?action=smart_schema&id=' + encodeURIComponent(id) + '&prefix=e');
        }

        function inherit(id) {

            $("#Is_Inherited_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_report&id=' + encodeURIComponent(id) + '&prefix=');
        }
        function inherit_item(id, item) {

            $("#Is_Inherited_Item_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_item&id=' + encodeURIComponent(item.value) + '&prefix=&parameter=' + id);
        }

        function inheritReportColumn(id) {

            $("#Is_Inherited_ReportColumn_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_report_column&id=' + encodeURIComponent(id) + '&prefix=');
        }

        function inheritRowReportList(id) {

            $("#Is_Inherited_ReportRow_ReportList_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_report_row_list&id=' + encodeURIComponent(id) + '&prefix=');
        }
        function inheritColumnReportList(id) {

            $("#Is_Inherited_ReportColumn_ReportList_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_report_column_list&id=' + encodeURIComponent(id) + '&prefix=');
        }
        function inheritReportRow(id, item) {

            $("#Is_Inherited_ReportRow_RowList_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_report_row_item&id=' + encodeURIComponent(item.value) + '&param=' + id + '&prefix=');
        }
        function inheritReportColumn(id, item) {

            $("#Is_Inherited_ReportColumn_ColumnList_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_report_column_item&id=' + encodeURIComponent(item.value) + '&param=' + id + '&prefix=');
        }
        function inherit_itemReportColumn(id, item) {

            $("#Is_Inherited_Item_ReportColumn_" + id).empty().append('<p>Loading, please wait...!</p>').load('Ajax.aspx?action=inherit_item_report_column&id=' + encodeURIComponent(item.value) + '&prefix=&parameter=' + id);
        }

        function smart_update_select_value(id, item) {
            var result = "" + item.value;
            //alert(result);
            var results = result.split(',');

            document.getElementById('TableName_' + id).value = results[0];
            document.getElementById('ColumnName_' + id).value = results[1];
            document.getElementById('SchemaField_' + id).value = results[2];

            if (results[3] != "") {
                document.getElementById('Expression_' + id).value = results[3];
                document.getElementById('Is_Calculated_Value_' + id).checked = true;
            }


        }
        function esmart_update_select_value(id, item) {
            var result = "" + item.value;
            //alert(result);
            var results = result.split(',');

            document.getElementById('ETableName_' + id).value = results[0];
            document.getElementById('EColumnName_' + id).value = results[1];
            document.getElementById('ESchemaField_' + id).value = results[2];
        }
        function update_select(id) {
            var field = document.getElementById('SchemaField_' + id).value;
            //field = field.replace(" ", "+");
            $("#Schema_Ajax_" + id).empty().append('<p>Searching Schema, please wait...!</p>').load('Ajax.aspx?action=schema&parameter=' + encodeURIComponent(field) + '&id=' + encodeURIComponent(id) + '&field=Schema_Ajax&prefix=');
        }
        function eupdate_select(id) {
            var field = document.getElementById('ESchemaField_' + id).value;
            //field = field.replace(" ", "+");
            $("#ESchema_Ajax_" + id).empty().append('<p>Searching Schema, please wait...!</p>').load('Ajax.aspx?action=schema&parameter=' + encodeURIComponent(field) + '&id=' + encodeURIComponent(id) + '&field=ESchema_Ajax&prefix=e');
        }
        function update_select_value(id, item) {
            var result = "" + item.value;
            //alert(result);
            var results = result.split(',');

            document.getElementById('TableName_' + id).value = results[0];
            document.getElementById('ColumnName_' + id).value = results[1];
            document.getElementById('formset').value = results[2];
        }
        function eupdate_select_value(id, item) {
            var result = "" + item.value;
            //alert(result);
            var results = result.split(',');

            document.getElementById('ETableName_' + id).value = results[0];
            document.getElementById('EColumnName_' + id).value = results[1];
        }
    </script>
    <script type="text/javascript">

        function submitReportColumnUpdate(form) {

        }
        function submitColumnLineItemUpdate(form) {
            postFormAjax(form)
        }
        function PostForm(id, modal, url) {
            var f = document.getElementById(id);

            postFormAjax(f, modal, url);
        }

        function postFormAjax(form, modal, url) {
            if (!form) {
                return false;
            }

            var $form = $(form);
            var action = form.action || $form.attr('data-action');
            var method = form.method || $form.attr('data-method') || 'post';
            var formdata = new FormData();
            var $buttons = $form.find('button, input[type="submit"]');
            var $status = $form.find('.builder-modal-save-status');

            $form.find(':input[name]').each(function () {
                var input = $(this);
                var type = String(input.attr('type') || '').toLowerCase();
                var name = input.attr('name');
                if (input.prop('disabled') || ((type === 'checkbox' || type === 'radio') && !input.prop('checked'))) {
                    return;
                }
                if (type === 'file') {
                    $.each(this.files || [], function (_, file) { formdata.append(name, file); });
                    return;
                }
                if (input.is('select[multiple]')) {
                    $.each(input.val() || [], function (_, value) { formdata.append(name, value); });
                    return;
                }
                formdata.append(name, input.val() == null ? '' : input.val());
            });

            if (!$status.length) {
                $status = $('<div class="builder-modal-save-status" role="status" aria-live="polite"></div>').appendTo($form);
            }
            $buttons.prop('disabled', true);
            $status.removeClass('is-error is-success').html('<i class="fa fa-spinner fa-spin" aria-hidden="true"></i> Saving changes…');

            $.ajax(action, {
                type: method,
                data: formdata,  // data to submit
                cache: false,
                contentType: false,
                processData: false,
                enctype: 'multipart/form-data',

                success: function (data, status, xhr) {
                    $status.addClass('is-success').html('<i class="fa fa-check-circle" aria-hidden="true"></i> Changes saved.');
                    if (url == null) {
                        $('#' + modal + "-button").click();
                    } else {
                        //window.location.href = url;
                    }

                },
                error: function (jqXhr, textStatus, errorMessage) {
                    $status.addClass('is-error').html('<i class="fa fa-exclamation-triangle" aria-hidden="true"></i> The changes could not be saved. Please try again.');
                },
                complete: function () {
                    $buttons.prop('disabled', false);
                }
            });
            return false;
        }
    </script>


    <script type="text/javascript">
        function updateFieldOnChange(table, field, id, idfield, idvalue) {
            var value = document.getElementById(field+"_"+idvalue).value;
            return $.ajax({
                url: "Action/?operation=updateFieldOnChange",
                type: "POST",
                data: {
                    table: table,
                    field: field,
                    value: value,
                    idfield: idfield,
                    idvalue: idvalue
                }
            }).fail(function () {
                $('.js-builder-order-status').removeClass('is-saving is-saved').addClass('is-error')
                    .html('<i class="fa fa-exclamation-triangle" aria-hidden="true"></i> A Designer change could not be saved');
            });
        }
    </script>

</asp:Content>
