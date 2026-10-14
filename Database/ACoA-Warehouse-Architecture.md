# ACoA reporting warehouse baseline

## Purpose

The warehouse separates analytical reads from the ReportingSuite operational database. ReportingSuite remains the system of record; the warehouse stores traceable results from `[dbo].[get_ACoA_Reports_Advance_v2]` for Power BI and other read-heavy analytical tools.

The first implementation deliberately stores a flattened, immutable snapshot because it preserves the procedure's populated measures and provides the shortest safe path to analytics. Rows where `LineItemValue` is database `NULL` are excluded from physical storage; legitimate numeric zero values are retained. It does not enumerate or read database views.

## Parameter contract

The page exposes all seven parameters represented by `ACoA_Quarterly_Returns.rsd`:

| RSD parameter | v2 procedure mapping |
| --- | --- |
| `@Report_id` | `@Report_id` |
| `@quarter_from`, `@quarter_to` | Expanded to the inclusive CSV supplied to `@quarter` |
| `@year_from`, `@year_to` | Expanded to the inclusive CSV supplied to `@year` |
| `@entity_type` | `@entity_type` |
| `@entities` | `@entities` |

The v2 procedure also requires `@status` and `@ReportColumnID`; both are explicit page parameters and are recorded with every load batch.

## Data flow

1. An administrator or protected background job selects a canonical parameter set.
2. ReportingSuite executes the v2 procedure against the operational database.
3. The loader validates the returned 38-column contract and configured row limit.
4. A load-batch audit record is created in the separate warehouse database.
5. The loader separates populated `LineItemValue` rows from database-null rows.
6. Only populated rows are bulk-loaded into `dw.FactACoAReportSnapshot` inside a transaction.
7. The batch records extracted, loaded, and skipped-null row counts and is marked `Completed`; partial writes roll back and failures are recorded.
8. Analytical users read only from the `analytics` schema.

## Recommended schedule grain

Use one scheduled load per:

- report;
- entity type;
- year;
- quarter;
- submission-status policy.

Scheduled loads should select all eligible entities and all report columns. This canonical grain prevents overlapping partial extracts and makes refresh duration, reconciliation, and retention predictable. The administrator page can run wider or narrower diagnostic extracts, but those should not replace the canonical schedule.

## Power BI consumption

- Use `analytics.vw_ACoAReportCurrent` for the latest value at the report/period/entity/line-item/column/status grain.
- Use `analytics.vw_ACoAReportSnapshots` when load history, restatements, or audit comparison is required.
- Configure incremental refresh on `LoadCompletedAtUtc` or the reporting `Year` and `Quarter` fields.
- Keep Power BI credentials read-only and grant access only to the `analytics` schema.
- Hide technical identifiers and load metadata in the semantic model unless they are required for audit pages.

## Recommended next warehouse phase

After the flattened baseline is stable, introduce conformed dimensions and a narrow numeric fact:

- `DimPeriod` for year and quarter;
- `DimEntity` keyed by a durable operational entity identifier (preferred) or governed licence-number mapping;
- `DimEntityGroup`;
- `DimReport`;
- `DimLineItem`;
- `DimReportColumn`;
- `FactACoAReport` with `LineItemValue`, status, load-batch key, and dimension surrogate keys.

The current procedure result does not expose the operational entity-instance ID or report-column ID. Add those stable identifiers to a future versioned procedure before building durable slowly-changing dimensions; entity name, licence number, and column name alone are weaker business keys.

## Operational recommendations

1. Run scheduling in a background worker, SQL Agent proxy, or enterprise scheduler—not by automating the Web Forms page.
2. Use a dedicated least-privilege Windows service identity for warehouse writes.
3. Use a different read-only identity for Power BI.
4. Keep source and warehouse connection secrets outside source-controlled configuration and encrypt network traffic.
5. Alert on failed or zero-row canonical loads and on unexpected column-contract changes.
6. Reconcile `ExtractedRowCount = LoadedRowCount + SkippedNullValueRowCount` for every completed batch and retain load metadata longer than detailed snapshots.
7. Define snapshot retention and partition/compression policies after measuring production volume.
8. Back up and monitor the warehouse independently from ReportingSuite.

## Deployment sequence

1. Have a DBA create the separate `ReportingWarehouse` database and its backup/recovery policy.
2. Apply `Database/ReportingWarehouse.sql` while connected to that database.
3. Grant the ReportingSuite application identity the documented loader permissions.
4. Grant Power BI a separate read-only identity on the `analytics` schema.
5. Validate a bounded extraction from `ReportWarehouse.aspx`.
6. Load one snapshot and reconcile its batch row count.
7. Implement canonical scheduled jobs using `ACoAReportWarehouseService.LoadSnapshot`.
