<%@ Page Title="ACoA Report Renderer" Language="vb" AutoEventWireup="false" MasterPageFile="~/Site.Master" CodeBehind="ReportRender.aspx.vb" Inherits="ReportingSuite.ReportRenderPage" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <meta name="robots" content="noindex,nofollow" />
    <script src="Content/Template/global_assets/js/plugins/visualization/echarts/echarts.min.js"></script>
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Panel ID="accessMessagePanel" runat="server" CssClass="alert alert-warning reporting-view-message" Visible="false" role="alert">
        <i class="icon-warning22 mr-2" aria-hidden="true"></i>
        <asp:Literal ID="accessMessageLiteral" runat="server" />
    </asp:Panel>

    <div class="report-render-layout report-render-procedure-layout">
        <section class="report-view-results" aria-live="polite">
            <asp:Panel ID="emptyStatePanel" runat="server" CssClass="card report-view-empty-state">
                <div class="card-body text-center">
                    <span class="report-view-empty-icon"><i class="icon-table2" aria-hidden="true"></i></span>
                    <h2 class="h5 mt-3 mb-2">The report is unavailable</h2>
                    <p class="text-muted mb-0">Choose valid parameters and refresh the report, or confirm that the reporting database is available.</p>
                </div>
            </asp:Panel>

            <asp:Panel ID="resultsPanel" runat="server" CssClass="card" Visible="false">
                <div class="card-header bg-white report-view-results-header">
                    <div>
                        <p class="text-uppercase text-muted font-size-xs font-weight-semibold mb-1">Stored procedure report</p>
                        <h2 class="card-title mb-1"><asp:Literal ID="selectedViewTitleLiteral" runat="server" /></h2>
                        <div class="report-view-meta"><asp:Literal ID="viewMetaLiteral" runat="server" /></div>
                    </div>
                    <div class="report-view-toolbar">
                        <div>
                            <label for="<%= viewModeDropDown.ClientID %>" class="font-size-sm font-weight-semibold mb-1">Display format</label>
                            <asp:DropDownList ID="viewModeDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                        </div>
                        <asp:Panel ID="groupByPanel" runat="server">
                            <label for="<%= groupByDropDown.ClientID %>" class="font-size-sm font-weight-semibold mb-1">Group records by</label>
                            <asp:DropDownList ID="groupByDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                        </asp:Panel>
                        <asp:Button ID="refreshButton" runat="server" CssClass="btn btn-light report-view-refresh" Text="Refresh" ToolTip="Reload this report" />
                    </div>
                </div>

                <asp:Panel ID="parametersPanel" runat="server" CssClass="report-parameter-panel" Visible="false">
                    <div class="report-parameter-heading">
                        <div>
                            <h3><i class="icon-equalizer2 mr-2" aria-hidden="true"></i>Report parameters</h3>
                            <p>Filter the completed report before values are rendered from the advanced ACoA stored procedure.</p>
                        </div>
                        <div class="report-parameter-summary"><asp:Literal ID="parameterSummaryLiteral" runat="server" /></div>
                    </div>

                    <div class="report-parameter-grid">
                        <div class="form-group">
                            <label for="<%= reportParameterDropDown.ClientID %>">Report</label>
                            <asp:DropDownList ID="reportParameterDropDown" runat="server" CssClass="form-control" />
                        </div>
                        <div class="form-group">
                            <label for="<%= entityGroupDropDown.ClientID %>">Entity group</label>
                            <asp:DropDownList ID="entityGroupDropDown" runat="server" CssClass="form-control" />
                        </div>
                        <asp:Panel ID="statusParameterPanel" runat="server" CssClass="form-group">
                            <label for="<%= statusDropDown.ClientID %>">Submission status</label>
                            <asp:DropDownList ID="statusDropDown" runat="server" CssClass="form-control" />
                        </asp:Panel>
                        <div class="form-group">
                            <label for="<%= yearFromDropDown.ClientID %>">Year from</label>
                            <asp:DropDownList ID="yearFromDropDown" runat="server" CssClass="form-control" />
                        </div>
                        <div class="form-group">
                            <label for="<%= yearToDropDown.ClientID %>">Year to</label>
                            <asp:DropDownList ID="yearToDropDown" runat="server" CssClass="form-control" />
                        </div>
                        <div class="form-group">
                            <label for="<%= quarterFromDropDown.ClientID %>">Quarter from</label>
                            <asp:DropDownList ID="quarterFromDropDown" runat="server" CssClass="form-control" />
                        </div>
                        <div class="form-group">
                            <label for="<%= quarterToDropDown.ClientID %>">Quarter to</label>
                            <asp:DropDownList ID="quarterToDropDown" runat="server" CssClass="form-control" />
                        </div>
                        <div class="form-group report-parameter-entities">
                            <label for="<%= entitiesListBox.ClientID %>">Entities</label>
                            <asp:ListBox ID="entitiesListBox" runat="server" CssClass="form-control" SelectionMode="Multiple" Rows="4" />
                            <small class="form-text text-muted">Leave empty for all entities. Hold Ctrl to select more than one.</small>
                        </div>
                        <asp:Panel ID="reportColumnsParameterPanel" runat="server" CssClass="form-group report-parameter-columns">
                            <label for="<%= reportColumnsListBox.ClientID %>">Report columns</label>
                            <asp:ListBox ID="reportColumnsListBox" runat="server" CssClass="form-control" SelectionMode="Multiple" Rows="4" />
                            <small class="form-text text-muted">Leave empty for all report columns. Hold Ctrl to select more than one.</small>
                        </asp:Panel>
                    </div>

                    <div class="report-parameter-actions">
                        <asp:Button ID="applyParametersButton" runat="server" CssClass="btn btn-primary" Text="Apply parameters" CausesValidation="false" />
                        <asp:HyperLink ID="clearParametersLink" runat="server" CssClass="btn btn-light">Clear</asp:HyperLink>
                    </div>
                </asp:Panel>

                <asp:Panel ID="renderNoticePanel" runat="server" CssClass="alert alert-warning m-3 mb-0" Visible="false" role="status">
                    <i class="icon-info22 mr-2" aria-hidden="true"></i>
                    <asp:Literal ID="renderNoticeLiteral" runat="server" />
                </asp:Panel>

                <div class="card-body report-view-output">
                    <asp:Literal ID="viewContentLiteral" runat="server" />
                </div>
            </asp:Panel>
        </section>
    </div>

    <script>
        (function () {
            if (!window.echarts) {
                return;
            }

            var chartElements = document.querySelectorAll('[data-report-dashboard-chart]');
            var chartInstances = [];

            Array.prototype.forEach.call(chartElements, function (element) {
                try {
                    var option = JSON.parse(element.getAttribute('data-chart-option'));
                    var chart = window.echarts.init(element);
                    chart.setOption(option);
                    chartInstances.push(chart);
                } catch (error) {
                    element.className += ' report-dashboard-chart-error';
                    element.innerHTML = '<div class="alert alert-warning mb-0">This chart could not be displayed.</div>';
                }
            });

            if (chartInstances.length > 0) {
                var resizeTimer;
                window.addEventListener('resize', function () {
                    window.clearTimeout(resizeTimer);
                    resizeTimer = window.setTimeout(function () {
                        Array.prototype.forEach.call(chartInstances, function (chart) {
                            chart.resize();
                        });
                    }, 150);
                });
            }
        }());
    </script>
</asp:Content>
