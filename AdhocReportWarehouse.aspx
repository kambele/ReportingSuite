<%@ Page Title="Adhoc Data Warehouse" Language="vb" AutoEventWireup="false" MasterPageFile="~/Site.Master" CodeBehind="AdhocReportWarehouse.aspx.vb" Inherits="ReportingSuite.AdhocReportWarehousePage" %>

<asp:Content ID="HeadContent" ContentPlaceHolderID="HeadContent" runat="server">
    <meta name="robots" content="noindex,nofollow" />
</asp:Content>

<asp:Content ID="MainContent" ContentPlaceHolderID="MainContent" runat="server">
    <asp:Panel ID="accessMessagePanel" runat="server" CssClass="alert alert-warning" Visible="false" role="alert">
        <i class="icon-warning22 mr-2" aria-hidden="true"></i>
        <asp:Literal ID="accessMessageLiteral" runat="server" />
    </asp:Panel>

    <div class="card">
        <div class="card-header bg-white header-elements-inline">
            <div>
                <p class="text-uppercase text-muted font-size-xs font-weight-semibold mb-1">Shared JSON snapshot store</p>
                <h1 class="card-title mb-1">Adhoc data warehouse</h1>
                <p class="text-muted mb-0">Warehouse every Adhoc report in one physical table while preserving its source schema and complete load history.</p>
            </div>
            <span class="badge badge-info badge-pill"><i class="icon-database-add mr-1" aria-hidden="true"></i>One shared fact table</span>
        </div>

        <asp:Panel ID="warehouseHealthPanel" runat="server" CssClass="alert alert-info m-3 mb-0" role="status">
            <i class="icon-database-check mr-2" aria-hidden="true"></i>
            <asp:Literal ID="warehouseHealthLiteral" runat="server" />
        </asp:Panel>

        <asp:Panel ID="operationMessagePanel" runat="server" CssClass="alert alert-info m-3 mb-0" Visible="false" role="status">
            <asp:Literal ID="operationMessageLiteral" runat="server" />
        </asp:Panel>

        <div class="report-parameter-panel border-0">
            <div class="report-parameter-heading">
                <div>
                    <h2><i class="icon-equalizer2 mr-2" aria-hidden="true"></i>Warehouse parameters</h2>
                    <p>Select an accessible Adhoc report. The complete result is stored as one JSON document per source row.</p>
                </div>
                <div class="report-parameter-summary"><asp:Literal ID="parameterSummaryLiteral" runat="server" /></div>
            </div>

            <div class="report-parameter-grid">
                <div class="form-group">
                    <label for="<%= reportDropDown.ClientID %>">Adhoc report <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="reportDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                    <small class="form-text text-muted">Only active Adhoc reports available to your Windows account are listed.</small>
                </div>
                <div class="form-group">
                    <label>Warehouse destination</label>
                    <div class="form-control bg-light"><code>dw.AdhocReport</code></div>
                    <small class="form-text text-muted">The report ID and schema version identify each JSON dataset.</small>
                </div>
            </div>

            <div class="report-parameter-actions">
                <asp:Button ID="previewButton" runat="server" CssClass="btn btn-light" Text="Validate extraction" CausesValidation="false" />
                <asp:Button ID="loadWarehouseButton" runat="server" CssClass="btn btn-primary" Text="Load warehouse snapshot" CausesValidation="false"
                    OnClientClick="return window.confirm('Load the complete Adhoc report result into the shared warehouse table?');" />
            </div>
        </div>
    </div>

    <div class="row">
        <div class="col-xl-8">
            <div class="card">
                <div class="card-header bg-white header-elements-inline">
                    <h2 class="card-title"><i class="icon-history mr-2" aria-hidden="true"></i>Recent Adhoc warehouse loads</h2>
                    <span class="text-muted font-size-sm">UTC timestamps</span>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="recentLoadsGrid" runat="server" AutoGenerateColumns="false" CssClass="table table-striped table-hover mb-0"
                        GridLines="None" EmptyDataText="No Adhoc warehouse loads have been recorded yet.">
                        <Columns>
                            <asp:BoundField DataField="LoadBatchID" HeaderText="Batch" />
                            <asp:BoundField DataField="StartedAtUtc" HeaderText="Started" DataFormatString="{0:yyyy-MM-dd HH:mm:ss}" />
                            <asp:BoundField DataField="LoadStatus" HeaderText="Status" />
                            <asp:BoundField DataField="ReportName" HeaderText="Report" />
                            <asp:BoundField DataField="SchemaVersion" HeaderText="Schema" />
                            <asp:BoundField DataField="SourceColumnCount" HeaderText="Columns" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="ExtractedRowCount" HeaderText="Extracted" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="LoadedRowCount" HeaderText="Loaded" DataFormatString="{0:N0}" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>
        <div class="col-xl-4">
            <div class="card">
                <div class="card-header bg-white"><h2 class="card-title"><i class="icon-power2 mr-2" aria-hidden="true"></i>Power BI consumption</h2></div>
                <div class="card-body">
                    <p><code>analytics.vw_AdhocReportCurrent</code> exposes the latest successful snapshot for every report.</p>
                    <p><code>analytics.vw_AdhocReportSnapshots</code> exposes complete historical batches.</p>
                    <p class="mb-0 text-muted">Filter by <code>ReportID</code>, then expand <code>RowPayload</code> as JSON in Power Query.</p>
                </div>
            </div>
            <div class="card">
                <div class="card-header bg-white"><h2 class="card-title"><i class="icon-calendar3 mr-2" aria-hidden="true"></i>Scheduling guidance</h2></div>
                <div class="card-body">
                    <p>Schedule one full snapshot per report after its source data is ready. Schema changes are versioned automatically.</p>
                    <p class="mb-0 text-muted">Run the reusable loader service from a protected background job; do not schedule this web page.</p>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
