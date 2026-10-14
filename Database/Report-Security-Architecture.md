# Report security architecture

## Access model

Reporting Suite uses the existing `dbo.ReportRoles` and `dbo.RoleTypes` tables to store report-level allow rules. Rules support two role types:

- `Active Directory User` for a Windows account such as `NAMFISA\username`.
- `Active Directory Group` for a Windows group such as `NAMFISA\Reporting - Finance`.

A report with no `ReportRoles` rows remains available to all Windows-authenticated Reporting Suite users. Once at least one rule exists, the report is available only to a matching AD user or group. Reporting Suite administrators always retain access so a report cannot be administratively orphaned.

Authorization is enforced server-side on the standard SSRS viewer, the Adhoc report page and data handler, and the ACoA report renderer. The dashboard and dynamic sidebar also remove inaccessible reports, but navigation filtering is only a usability measure and is not the security boundary.

## AD evaluation

The application reads the current IIS Windows identity. User rules compare the configured account with the authenticated account, while group rules use the Windows principal's `IsInRole` evaluation. Domain-qualified names are recommended. Nested group resolution follows the Windows token issued by IIS and Active Directory.

The application does not bind to Active Directory or store AD passwords. Windows authentication must remain enabled and anonymous authentication must remain disabled for the application.

## Deployment

1. Back up the ReportingSuite database.
2. Apply `Database/ReportSecurity.sql` to the ReportingSuite database. The script seeds the two role types, removes exact duplicate rules, and creates a unique filtered index.
3. Deploy `ReportAccessService.vb`, the revised report pages, and the Report Builder workspace assets.
4. Ensure the IIS application identity can read `dbo.Users`, `dbo.Reports`, `dbo.ReportRoles`, and `dbo.RoleTypes`.
5. Ensure the application identity can insert and delete `dbo.ReportRoles` and insert missing `dbo.RoleTypes` rows. Existing report configuration permissions may already cover these tables; grant only the missing operations.
6. Recycle the application pool and complete the access tests below.

## Acceptance tests

- An authenticated user can open an unrestricted report.
- A directly addressed restricted report returns HTTP 403 for a user who is not listed and is not in an allowed group.
- The same restricted report opens for a configured user and for a member of a configured group.
- Restricted Adhoc metadata, data, insights, distinct-value, and export requests all return HTTP 403 to an unauthorized user.
- Restricted reports do not appear on the dashboard or dynamic division navigation for unauthorized users.
- Reporting Suite administrators can still open and edit every report.
- Disabling the restricted mode removes the report's allow rules and restores authenticated access.

## Operational recommendation

Prefer AD groups over individual users for production access. Groups reduce ongoing administration and provide a clearer owner-controlled lifecycle. Review restricted reports and group ownership at least quarterly, and remove stale individual-user assignments promptly.
