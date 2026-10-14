<%@ Page Title="ACoA Data Warehouse" Language="vb" AutoEventWireup="false" MasterPageFile="~/Site.Master" CodeBehind="ReportWarehouse.aspx.vb" Inherits="ReportingSuite.ReportWarehousePage" %>

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
                <p class="text-uppercase text-muted font-size-xs font-weight-semibold mb-1">Procedure-only extraction</p>
                <h1 class="card-title mb-1">ACoA data warehouse</h1>
                <p class="text-muted mb-0">Create traceable analytical snapshots from <code>[dbo].[get_ACoA_Reports_Advance_v2]</code> without querying database views.</p>
            </div>
            <span class="badge badge-primary badge-pill">Administrator</span>
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
                    <p>The seven `.rsd` parameters are shown below. Submission status and report columns are required extensions of the v2 procedure.</p>
                </div>
                <div class="report-parameter-summary"><asp:Literal ID="parameterSummaryLiteral" runat="server" /></div>
            </div>

            <asp:ValidationSummary ID="validationSummary" runat="server" CssClass="alert alert-danger" HeaderText="Correct the following:" />

            <div class="report-parameter-grid">
                <div class="form-group">
                    <label for="<%= reportDropDown.ClientID %>">Report <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="reportDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                </div>
                <div class="form-group">
                    <label for="<%= entityTypeDropDown.ClientID %>">Entity type <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="entityTypeDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                </div>
                <div class="form-group">
                    <label for="<%= statusDropDown.ClientID %>">Submission status <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="statusDropDown" runat="server" CssClass="form-control">
                        <asp:ListItem Text="Approved only" Value="1" />
                        <asp:ListItem Text="Approved and pending approval" Value="3" />
                    </asp:DropDownList>
                </div>
                <div class="form-group">
                    <label for="<%= yearFromDropDown.ClientID %>">Year from <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="yearFromDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                </div>
                <div class="form-group">
                    <label for="<%= yearToDropDown.ClientID %>">Year to <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="yearToDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                </div>
                <div class="form-group">
                    <label for="<%= quarterFromDropDown.ClientID %>">Quarter from <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="quarterFromDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                </div>
                <div class="form-group">
                    <label for="<%= quarterToDropDown.ClientID %>">Quarter to <span class="text-danger">*</span></label>
                    <asp:DropDownList ID="quarterToDropDown" runat="server" CssClass="form-control" AutoPostBack="true" />
                </div>
                <div class="form-group report-parameter-entities">
                    <label for="<%= entitiesListBox.ClientID %>">Entities</label>
                    <asp:ListBox ID="entitiesListBox" runat="server" CssClass="form-control" SelectionMode="Multiple" Rows="6" />
                    <small class="form-text text-muted">Leave empty to warehouse all eligible entities in the selected entity type and period.</small>
                </div>
                <div class="form-group report-parameter-columns">
                    <label for="<%= reportColumnsListBox.ClientID %>">Report columns</label>
                    <asp:ListBox ID="reportColumnsListBox" runat="server" CssClass="form-control" SelectionMode="Multiple" Rows="6" />
                    <small class="form-text text-muted">Leave empty to warehouse every column exposed for the selected report.</small>
                </div>
            </div>

            <div class="report-parameter-actions">
                <asp:Button ID="previewButton" runat="server" CssClass="btn btn-light" Text="Validate extraction" CausesValidation="false" />
                <asp:Button ID="loadWarehouseButton" runat="server" CssClass="btn btn-primary" Text="Load warehouse snapshot" CausesValidation="false"
                    OnClientClick="return window.confirm('Load the selected procedure result into the separate warehouse database?');" />
            </div>
        </div>
    </div>

    <div class="row">
        <div class="col-xl-8">
            <div class="card">
                <div class="card-header bg-white header-elements-inline">
                    <h2 class="card-title"><i class="icon-history mr-2" aria-hidden="true"></i>Recent warehouse loads</h2>
                    <span class="text-muted font-size-sm">UTC timestamps</span>
                </div>
                <div class="table-responsive">
                    <asp:GridView ID="recentLoadsGrid" runat="server" AutoGenerateColumns="false" CssClass="table table-striped table-hover mb-0"
                        GridLines="None" EmptyDataText="No warehouse loads have been recorded yet.">
                        <Columns>
                            <asp:BoundField DataField="LoadBatchID" HeaderText="Batch" />
                            <asp:BoundField DataField="StartedAtUtc" HeaderText="Started" DataFormatString="{0:yyyy-MM-dd HH:mm:ss}" />
                            <asp:BoundField DataField="LoadStatus" HeaderText="Status" />
                            <asp:BoundField DataField="ReportID" HeaderText="Report" />
                            <asp:BoundField DataField="YearFrom" HeaderText="Year from" />
                            <asp:BoundField DataField="YearTo" HeaderText="Year to" />
                            <asp:BoundField DataField="QuarterFrom" HeaderText="Q from" />
                            <asp:BoundField DataField="QuarterTo" HeaderText="Q to" />
                            <asp:BoundField DataField="ExtractedRowCount" HeaderText="Extracted" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="LoadedRowCount" HeaderText="Loaded" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="SkippedNullValueRowCount" HeaderText="Skipped null" DataFormatString="{0:N0}" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>
        <div class="col-xl-4">
            <div class="card">
                <div class="card-header bg-white"><h2 class="card-title"><i class="icon-calendar3 mr-2" aria-hidden="true"></i>Scheduling guidance</h2></div>
                <div class="card-body">
                    <p>Use one canonical schedule per report, entity type, year and quarter. Load all entities and report columns in scheduled runs so analytical snapshots remain comparable.</p>
                    <p class="mb-0 text-muted">Run the reusable warehouse service from a protected background job. Do not schedule the administrator web page itself.</p>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
