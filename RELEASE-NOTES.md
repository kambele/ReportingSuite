# Reporting Suite Modernisation and Analytics Enhancements

Prepared: 7 October 2026  
Release status: Release candidate for controlled deployment and user acceptance testing  
Platform: ASP.NET Web Forms on .NET Framework 4.8, SQL Server, Windows authentication

## Overview

This release establishes a modernized Reporting Suite baseline without changing existing reporting business rules or established report routes. It introduces the Limitless user interface, a division-oriented dashboard, self-service Adhoc reporting and analytics, direct rendering from the advanced ACoA stored procedure, a separate analytical warehouse path, and proactive Report Builder dependency health checks.

## User-facing enhancements

### Limitless interface and navigation

- Replaced the shared application shell with the compatible Limitless layout and Icomoon icon set.
- Standardized page headers, navigation, cards, forms, tables, alerts, modals, validation feedback, and responsive behavior.
- Kept the top navigation fixed while the page scrolls.
- Made the left navigation independently scrollable and preserved its scroll position between page loads.
- Added responsive sidebar and header controls for smaller screens.
- Improved table containment, responsive overflow, DataTable controls, card collapse behavior, and fullscreen presentation.
- Retained Windows-authenticated user context and existing report URLs.

### Interactive home dashboard

- Redesigned the default page as a responsive reporting workspace.
- Added Dashboard and division tabs generated from configured sections.
- Added report totals, division totals, Adhoc report totals, and direct access to the Adhoc catalogue.
- Added report search within the active division.
- Displays reports as consistent cards while preserving their configured navigation targets.
- Automatically routes reports containing `AdhocQuery` to the Adhoc Report page.
- Retains the existing embedded Power BI reporting overview.

### Adhoc Report workspace

- Added an Adhoc report catalogue and on-demand report viewer.
- Added administrator-only New Report and Edit Report modals.
- Added an SQL editor for maintaining a single read-only `SELECT` statement.
- Added lazy loading and server-side paging, ordering, searching, and filtering.
- Added distinct-value filters with multiple selection and an advanced filter modal covering all returned columns.
- Added CSV and Excel export for matching rows, with a 100,000-row safety limit.
- Redesigned the report table inside a responsive, contained card.
- Made the Report Insights and DataTable cards collapsible; Report Insights is collapsed initially.
- Added grouped insights including matching-row counts, distinct groups, totals, and leading values.
- Added primary and secondary grouping, measure selection, and configurable top-result limits.
- Added line, area, horizontal bar, column, mixed, pie, donut, and funnel charts.
- Added ECharts, D3, and C3 rendering choices and a fullscreen modal showing all supported chart types.

### Power BI views for Adhoc reports

- Saving a new Adhoc report now creates a stable SQL view named:

  `dbo.vw_AdhocReport_<ReportID>`

- Editing and saving an Adhoc report updates the same view with `CREATE OR ALTER VIEW`, preserving the stable Power BI object name and existing view permissions.
- The report definition and SQL view are created or updated in one database transaction; neither change is retained if publishing fails.
- The generated view name is displayed on the Adhoc Report page and returned by the save endpoint.
- `ORDER BY` remains prohibited at the outermost query level because consumers control their own ordering.
- Valid `ORDER BY` clauses inside subqueries and window expressions are now accepted.
- Existing Adhoc reports receive their Power BI view the next time they are edited and saved.

### Advanced ACoA Report Renderer

- Added a dedicated `ReportRender.aspx` page whose available reports are filtered and authorized against the current Windows identity.
- Removed dependence on a selectable database-view catalogue for this workflow.
- Executes `[dbo].[get_ACoA_Reports_Advance_v2]` directly.
- Exposes report, entity group/type, submission status, year range, quarter range, entity selection, and report-column selection parameters based on the ACoA report definition.
- Provides three presentation modes:
  - Dashboard
  - Completed aggregated report
  - Detailed records
- Supports optional grouping in detailed mode.
- Adds dashboard KPIs, line-item trends, latest-period comparisons, and period-over-period indicators.
- Applies configurable result limits to protect application responsiveness.

### ACoA analytical warehouse

- Added an administrator-only `ReportWarehouse.aspx` page and reusable warehouse service.
- Extracts data exclusively through `[dbo].[get_ACoA_Reports_Advance_v2]`.
- Exposes the complete parameter set required for validation and warehouse loading.
- Supports extraction validation before committing a load.
- Stores snapshots in a separate SQL Server database and rejects configurations that point the source and warehouse connections to the same database.
- Stores only rows where `LineItemValue` is not database `NULL`; valid zero values are retained.
- Records load batches, requested parameters, row counts, skipped-null counts, status, duration, requesting user, and failure details.
- Uses a transaction for snapshot insertion and load completion updates.
- Adds warehouse connectivity, schema, and permission health checks plus recent-load history.
- Provides indexed fact storage and two reader-facing analytical views:
  - `[analytics].[vw_ACoAReportSnapshots]`
  - `[analytics].[vw_ACoAReportCurrent]`
- Provides separate least-privilege loader and reader database roles.
- Includes architecture and scheduling guidance for Power BI and other analytical consumers.

### Report Builder Workspace

- Redesigned Builder as a responsive administration workspace with catalogue KPIs, report-level insights, configuration coverage charts, readiness indicators, sticky section navigation, report search, and preview actions.
- Added a Report Health check to the Report Builder.
- Evaluates calculated and inherited line-item dependencies before a report is published or extracted.
- Detects direct self-reference and multi-item circular dependency chains that could exceed SQL Server's nesting limit.
- Shows a healthy, warning, or blocking diagnostic summary with the dependency path.
- Highlights affected mappings directly in the builder.
- Made report catalogue rendering tolerant of missing group, line-item type, or report-type relationships, preventing the earlier null-reference failure.
- Added report-level security settings for Active Directory users and groups by activating the existing `ReportRoles` and `RoleTypes` model.
- Preserves authenticated access when no rules exist and automatically preserves administrator access when a report is restricted.

## Security and data safeguards

- Adhoc report creation and editing are restricted to Reporting Suite administrators.
- Report access is enforced server-side against the current Windows identity for SSRS rendering, Adhoc page/data endpoints, and the ACoA renderer.
- Restricted reports are removed from the dashboard and dynamic division navigation for users who do not have access.
- Manual warehouse loading remains restricted to administrators.
- Adhoc saves use a session-bound request token.
- Adhoc SQL is limited to one read-only `SELECT`; data-changing, DDL, execution, and external data-source keywords are rejected.
- Column names used for filtering and ordering are obtained from inspected result metadata and quoted by the application.
- Warehouse source and destination databases are verified as separate databases.
- Warehouse write and analytical read permissions are separated into database roles.
- Error responses avoid returning connection credentials or raw database exception details to the browser.

## Deployment requirements

1. Deploy the rebuilt application files, including the Limitless assets, updated master page, new pages, handlers, services, scripts, and styles.
2. Confirm the application targets .NET Framework 4.8 and that Windows authentication is enabled in IIS.
3. Apply `Database/ReportSecurity.sql` to the ReportingSuite database and grant the application identity the required read/write access to `ReportRoles` plus read access to `Reports`, `RoleTypes`, and `Users`.
4. Confirm the `dbo.Reports.AdhocQuery` column exists as `varchar(max)` or a compatible large text type.
5. Grant the Reporting Suite application database principal the permissions required to publish Adhoc views:
   - `CREATE VIEW` in the ReportingSuite database.
   - `ALTER` on schema `dbo`, or an equivalent DBA-managed publishing arrangement.
6. Grant the Power BI database principal read-only `SELECT` permission on the published Adhoc views. Do not grant it Adhoc report maintenance permissions.
7. Create and configure a separate warehouse database by applying `Database/ReportingWarehouse.sql` while connected to that warehouse database—not to ReportingSuite.
8. Configure the `ReportingWarehouse` connection string securely for the IIS application.
9. Create the appropriate SQL Server login/database user and add only the loader identity to `ReportingWarehouseLoader`.
10. Add Power BI or analytical reader identities to `ReportingWarehouseReader`.
11. Protect production connection strings outside source control and apply the production configuration transform, including `debug="false"`.

## Compatibility and operational notes

- Existing report URLs and standard report rendering routes are preserved.
- Existing Adhoc reports are not bulk-published automatically; save each report once to create its stable Power BI view.
- SQL Server must support `CREATE OR ALTER VIEW`.
- A SQL view does not guarantee row order. Power BI should apply its required sorting explicitly.
- Cross-database Adhoc queries may require the Power BI identity to have access to referenced databases, depending on ownership chaining and database security configuration.
- The application identity needs view-publishing permission only if views are created at runtime. A future dedicated reporting schema would allow narrower permissions than `ALTER` on `dbo`.
- The warehouse page supports manual validation and loading. Recurring scheduling must be implemented through a protected background worker, SQL Agent-compatible process, or enterprise scheduler; the web page itself should not be automated.
- Warehouse snapshots are append-only analytical records. Retention and purge policy should be agreed before long-term scheduled loading begins.

## Validation completed

- The ReportingSuite project rebuild completed successfully under .NET Framework 4.8.
- Adhoc SQL validation was checked for nested subquery ordering, window ordering, top-level ordering, comments, and string literals.
- SQL Server view publishing was verified with a nested ordered query inside a transaction; the verification object was rolled back and no test view remained.
- Warehouse code validates the stored-procedure result contract before writing data.
- Builder catalogue and selected-report workspace pages were smoke-tested through local IIS, including the new security modal, insights, charts, stylesheet, and script assets.
- Dashboard, Adhoc catalogue, standard SSRS viewer, and ACoA renderer returned HTTP 200 under an authorized Windows account after report security enforcement was added.
- The optional full ASP.NET precompilation check could not complete on the development workstation because its machine-level ASP.NET configuration references the unavailable `System.Data.Entity.Design.AspNet.EntityDesignerBuildProvider`. This is an environment prerequisite and was not introduced by the release.

## Recommended user acceptance tests

1. Verify the fixed header and independently scrollable navigation on desktop and mobile-width screens.
2. Open reports from the dashboard, division tabs, and sidebar and confirm existing routes are unchanged.
3. Create an Adhoc report with an ordered `TOP` subquery or `ROW_NUMBER() OVER (ORDER BY ...)` and confirm the report saves.
4. Confirm an outermost `ORDER BY` is rejected with a clear validation message.
5. Confirm the corresponding `dbo.vw_AdhocReport_<ReportID>` view is created and readable by the intended Power BI identity.
6. Exercise paging, simple and advanced multi-select filters, insights, secondary grouping, every chart type, collapse/fullscreen behavior, and CSV/Excel export.
7. Run the ACoA renderer in dashboard, aggregate, and detail modes across representative parameter combinations.
8. Validate a warehouse extract, load a controlled snapshot, reconcile extracted/loaded/skipped counts, and query both analytical views.
9. Open a healthy report and a deliberately recursive test report in Builder and verify the health status, coverage charts, readiness indicators, and dependency path.
10. Restrict a test report to an AD user and an AD group; verify allowed identities can render it and an unlisted identity receives HTTP 403 from both the page and data endpoints.
11. Confirm non-administrators cannot access report maintenance or warehouse loading functions.

## Primary implementation files

- `Site.Master`, `Site.Master.vb`
- `Content/reporting-suite-limitless.css`
- `Content/report-builder-workspace.css`
- `Scripts/reporting-suite-limitless.js`
- `Scripts/report-builder-workspace.js`
- `Default.aspx`, `Default.aspx.vb`, `Scripts/reporting-dashboard.js`
- `AdhocReport.aspx`, `AdhocReport.aspx.vb`
- `AdhocReportData.ashx`, `AdhocReportData.ashx.vb`
- `AdhocReportService.vb`
- `ReportRender.aspx`, `ReportRender.aspx.vb`
- `ReportWarehouse.aspx`, `ReportWarehouse.aspx.vb`
- `ACoAReportWarehouseService.vb`
- `Database/ReportingWarehouse.sql`
- `Database/ACoA-Warehouse-Architecture.md`
- `Builder.aspx`, `Builder.aspx.vb`
- `ReportHealthService.vb`
- `ReportAccessService.vb`
- `Report.aspx`, `Report.aspx.vb`
- `Database/ReportSecurity.sql`
- `Database/Report-Security-Architecture.md`
- `Data.edmx`, `Report.vb`
- `ReportingSuite.vbproj`

## Rollback considerations

- Retain a backup of the application directory and ReportingSuite database before deployment.
- Re-deploying the previous application build does not remove generated `dbo.vw_AdhocReport_<ReportID>` views or warehouse snapshots.
- If view removal is required, review Power BI dependencies and remove only explicitly approved view objects.
- Warehouse rollback should preserve load-batch audit records unless the business owner approves deletion under the warehouse retention policy.
