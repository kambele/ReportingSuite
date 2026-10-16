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

<asp:Content ID="Content2" ContentPlaceHolderID="PageScripts" runat="server">
    <script type="text/javascript">
        (function () {
            var host = document.querySelector('[data-ssrs-frame-host]');
            if (!host) {
                return;
            }

            var frame = host.querySelector('.report-native-frame');
            var loadingText = host.querySelector('[data-ssrs-loading-text]');
            var warning = host.querySelector('[data-ssrs-warning]');
            var loadCompleted = false;

            function finishLoading() {
                loadCompleted = true;
                host.classList.add('is-loaded');
            }

            function showLoadWarning() {
                if (loadCompleted || host.classList.contains('is-loaded')) {
                    return;
                }

                host.classList.add('is-waiting');
                if (loadingText) {
                    loadingText.textContent = 'SSRS is taking longer than expected to respond...';
                }
                if (warning) {
                    warning.hidden = false;
                }
            }

            if (frame) {
                frame.addEventListener('load', finishLoading);
                frame.addEventListener('error', showLoadWarning);
            }

            window.setTimeout(showLoadWarning, 30000);
        }());
    </script>
</asp:Content>
