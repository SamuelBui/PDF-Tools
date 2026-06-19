# v1.0 Stabilization Test Cases

## Crash and Logging

- Launch the app and confirm a log file is created under `%LocalAppData%\PdfTool.App\Logs`.
- Run one successful operation in each tab and verify `INFO` entries are appended.
- Force a handled failure, for example:
  - Protect with output path locked
  - Merge with an input file open in another viewer
  - Split with missing output folder
  - Compress a protected PDF
- Verify the failure is logged with `ERROR` and exception details when applicable.

## Safe Output Writes

- For `Protect PDF`, run protect and unlock against an existing output file.
- Verify the output file is fully replaced and no `.tmp` file remains in the output folder.
- For `Merge PDF`, merge into an existing output file.
- For `Page Organizer`, test:
  - Extract selected pages
  - Remove selected pages
  - Rotate selected pages
  - Split by ranges
- Verify the final output opens correctly and no temp file is left behind.

## Session Restore

- Open the app, set up state in all four tabs, close the app, and reopen it.
- Verify the app restores:
  - selected tab
  - window size and position
  - Protect batch items and passwords
  - Split page order, selection, and rotation
  - Merge queue order and page arrangement
  - Compress queue and profile

## Locked File Handling

- Open a source PDF in another app and try:
  - Protect
  - Merge
  - Split
  - Compress
- Verify the app blocks the operation cleanly and does not leave partial output.

## Output Integrity

- For every successful operation, confirm:
  - output file exists
  - output file opens
  - expected page count is preserved
  - expected permissions or rotation changes are present

## Native Dependency Smoke Test

- Verify thumbnail rendering still works in `Page Organizer` and `Merge PDF`.
- Verify no `pdfium.dll` load error occurs on startup or when previewing PDFs.
