# Protect And Split Test Cases

## Protect

- Single file: valid PDF, strong user password, optional owner password, output path writable.
- Single file: output path equals input path.
- Single file: input PDF is invalid or renamed from another file type.
- Single file: input PDF is already protected and requires a password.
- Single file: input PDF is open in another viewer.
- Batch mode: one item missing password.
- Batch mode: one item has output path locked by another process.
- Batch mode: one item is not a valid PDF.
- Batch mode: one item is already protected and must be unlocked first.
- Batch mode: select shared output folder, then validate and run.

## Split

- Load valid PDF, confirm page count and thumbnails appear.
- Load invalid PDF and verify the organizer stays empty with a clear validation message.
- Load protected PDF and verify the organizer stays empty with a clear validation message.
- Load PDF that is open in another viewer and verify the app does not continue.
- Page list selection: `1, 3-5, 9`.
- Page list selection: out-of-range values like `0, 999`.
- Extract selected pages with `Separate files per page`.
- Extract selected pages with `Group contiguous ranges`.
- Remove selected pages until only one page remains.
- Attempt to remove all pages and verify it is blocked.
- Reorder pages, rotate a subset, then extract and verify the output order.
