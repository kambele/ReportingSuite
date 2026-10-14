<%@ Page Title="About" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="About.aspx.vb" Inherits="ReportingSuite.About" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <section class="reporting-info-card" aria-labelledby="about-title">
        <h2 id="about-title"><i class="icon-info22 text-primary mr-2"></i>About Reporting Suite</h2>
        <p>Reporting Suite provides NAMFISA staff with a central workspace for operational and regulatory reports, reporting dashboards, report parameters, and authorized report administration.</p>
        <p class="text-muted">Access is provided through Windows authentication. Available reports and administration tools depend on the user record and permissions configured in the application.</p>
    </section>
</asp:Content>
