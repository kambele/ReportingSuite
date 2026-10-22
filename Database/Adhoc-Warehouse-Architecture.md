# Adhoc reporting warehouse

## Purpose

Adhoc reports can return unrelated column sets, so they share one JSON-backed physical table instead of creating one warehouse table per report. The operational Adhoc view remains the source; Power BI reads completed snapshots from the separate `ReportingWarehouse` database.

## Data model

- `etl.AdhocReportLoadBatch` records status, source view, row counts, duration, user and errors.
- `etl.AdhocReportSchema` records a SHA-256 schema fingerprint and ordered column metadata for each schema version.
- `dw.AdhocReport` stores one row per source result row. `RowPayload` contains the source columns as JSON.
- `analytics.vw_AdhocReportCurrent` exposes the latest successful batch for each report.
- `analytics.vw_AdhocReportSnapshots` exposes all successful historical batches.

The warehouse fact is keyed by `ReportID`, `LoadBatchID`, `SchemaVersion` and `SourceRowNumber`. A row hash supports future comparison and change detection. `BusinessKey` is reserved for a future administrator-selected durable source key.

## Load flow

1. Select an accessible Adhoc report on `AdhocReportWarehouse.aspx`.
2. Validate the source `dbo.vw_AdhocReport_<ReportID>` and the configured row limit.
3. Create a `Running` batch record in the warehouse.
4. Execute the source view outside the warehouse transaction so linked-server queries do not require MSDTC.
5. Calculate the ordered schema definition and reuse or create its schema version.
6. Serialize every source row to JSON and bulk-load it into `dw.AdhocReport`.
7. Commit the rows and mark the batch `Completed`; failures roll back detailed rows and mark the batch `Failed`.

## Power BI

Filter either analytics view by `ReportID`, parse `RowPayload` with `Json.Document`, and expand the fields required by that report's semantic model. Refresh against `analytics.vw_AdhocReportCurrent` for the latest dataset or the snapshots view for audit/history.

The Power BI identity should be a member only of `ReportingWarehouseReader`. The application or scheduled-loader identity should be a member of `ReportingWarehouseLoader`.

## Deployment

1. Apply the updated `Database/ReportingWarehouse.sql` to the existing separate warehouse database.
2. Confirm the Reporting Suite identity is a member of `ReportingWarehouseLoader`.
3. Open `AdhocReportWarehouse.aspx` and verify the green health status.
4. Validate and load a bounded Adhoc report.
5. Reconcile the recent-load counts and query `analytics.vw_AdhocReportCurrent`.
6. Schedule `AdhocReportWarehouseService.LoadSnapshot` in a protected background job; do not automate the web page.
