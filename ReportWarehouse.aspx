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
            <span class="badge badge-primary badge-pill"><i class="icon-user-check mr-1" aria-hidden="true"></i>Authenticated reporting user</span>
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
                <div class="form-group report-parameter-period">
                    <label for="<%= yearsListBox.ClientID %>">Years <span class="text-danger">*</span></label>
                    <div class="report-checkbox-filter">
                        <div class="report-checkbox-filter-toolbar" role="group" aria-label="Year selection actions">
                            <button type="button" class="btn btn-light btn-sm" data-multiselect-action="select-all"><i class="icon-checkbox-checked2 mr-1" aria-hidden="true"></i>Select all</button>
                            <button type="button" class="btn btn-light btn-sm" data-multiselect-action="clear-all"><i class="icon-checkbox-unchecked2 mr-1" aria-hidden="true"></i>Untick all</button>
                            <span class="report-checkbox-filter-count" data-multiselect-count aria-live="polite"></span>
                        </div>
                        <asp:ListBox ID="yearsListBox" runat="server" CssClass="form-control" SelectionMode="Multiple" Rows="6" AutoPostBack="true"
                            data-checkbox-multiselect="true" data-dependent-refresh="true" data-selection-label="years"
                            data-empty-label="No years selected" data-placeholder="Tick one or more years" />
                    </div>
                    <small class="form-text text-muted">Tick one or more reporting years.</small>
                </div>
                <div class="form-group report-parameter-period">
                    <label for="<%= quartersListBox.ClientID %>">Quarters <span class="text-danger">*</span></label>
                    <div class="report-checkbox-filter">
                        <div class="report-checkbox-filter-toolbar" role="group" aria-label="Quarter selection actions">
                            <button type="button" class="btn btn-light btn-sm" data-multiselect-action="select-all"><i class="icon-checkbox-checked2 mr-1" aria-hidden="true"></i>Select all</button>
                            <button type="button" class="btn btn-light btn-sm" data-multiselect-action="clear-all"><i class="icon-checkbox-unchecked2 mr-1" aria-hidden="true"></i>Untick all</button>
                            <span class="report-checkbox-filter-count" data-multiselect-count aria-live="polite"></span>
                        </div>
                        <asp:ListBox ID="quartersListBox" runat="server" CssClass="form-control" SelectionMode="Multiple" Rows="4" AutoPostBack="true"
                            data-checkbox-multiselect="true" data-dependent-refresh="true" data-selection-label="quarters"
                            data-empty-label="No quarters selected" data-placeholder="Tick one or more quarters" />
                    </div>
                    <small class="form-text text-muted">Tick any combination of quarters.</small>
                </div>
                <div class="form-group report-parameter-entities">
                    <label for="<%= entitiesListBox.ClientID %>">Entities</label>
                    <div class="report-checkbox-filter">
                        <div class="report-checkbox-filter-toolbar" role="group" aria-label="Entity selection actions">
                            <button type="button" class="btn btn-light btn-sm" data-multiselect-action="select-all"><i class="icon-checkbox-checked2 mr-1" aria-hidden="true"></i>Select all</button>
                            <button type="button" class="btn btn-light btn-sm" data-multiselect-action="clear-all"><i class="icon-checkbox-unchecked2 mr-1" aria-hidden="true"></i>Untick all</button>
                            <span class="report-checkbox-filter-count" data-multiselect-count aria-live="polite"></span>
                        </div>
                        <asp:ListBox ID="entitiesListBox" runat="server" CssClass="form-control" SelectionMode="Multiple" Rows="6"
                            data-checkbox-multiselect="true" data-selection-label="entities"
                            data-empty-label="All eligible entities" data-placeholder="Tick entities or leave empty for all" />
                    </div>
                    <small class="form-text text-muted">No entities ticked means all eligible entities in the selected type and period.</small>
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
                            <asp:BoundField DataField="YearSelection" HeaderText="Years" />
                            <asp:BoundField DataField="QuarterSelection" HeaderText="Quarters" />
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
                    <p class="mb-0 text-muted">Run the reusable warehouse service from a protected background job. Do not schedule the interactive web page itself.</p>
                </div>
            </div>
        </div>
    </div>
</asp:Content>
