<%@ Page Title="Report Viewer" Language="vb" AutoEventWireup="false" MasterPageFile="~/Site.Master" CodeBehind="Report.aspx.vb" Inherits="ReportingSuite.ReportPage" EnableEventValidation="false" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <section class="report-native-shell" aria-labelledby="report-viewer-title">
        <div class="report-native-toolbar">
            <div>
                <h5 id="report-viewer-title" class="mb-1"><i class="icon-file-stats2 text-primary mr-2"></i>SSRS report viewer</h5>
                <p class="report-native-note">Large reports or broad parameter selections may take more than two minutes to render.</p>
            </div>
            <div class="report-native-actions">
                <asp:HyperLink ID="openNativeSsrsLink1" runat="server" CssClass="btn btn-primary" Target="_blank" Text="Open in Native SSRS 1" />
                <asp:HyperLink ID="openNativeSsrsLink2" runat="server" CssClass="btn btn-light" Target="_blank" Text="Open in Native SSRS 2" />
            </div>
        </div>

        <asp:Panel ID="nativeReportMessage" runat="server" CssClass="alert alert-warning report-native-alert" Visible="false">
            <i class="icon-warning22 mr-2" aria-hidden="true"></i><asp:Literal ID="reportStatusMessage" runat="server" />
        </asp:Panel>

        <asp:Literal ID="nativeSsrsFrameLiteral" runat="server" />
    </section>
</asp:Content>
