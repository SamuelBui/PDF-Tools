# Release Checklist - PdfTool.App

Use this checklist before creating a GitHub release.

## 1. Dependency restore

```powershell
dotnet restore PdfTool.App\PdfTool.App.csproj --configfile PdfTool.App\NuGet.Config --locked-mode
```

Expected result:

- Restore succeeds.
- No unexpected dependency graph change.
- `PdfTool.App/packages.lock.json` does not change.

## 2. Release build

```powershell
dotnet build PdfTool.App\PdfTool.App.csproj -c Release --no-restore
```

Expected result:

- Build succeeds.
- No missing native PDFium DLL error during build output preparation.
- Output folder includes required runtime/native dependencies.

## 3. Manual smoke tests

Run the app from the Release output folder and test:

### Protect / Unlock

- Protect a normal PDF.
- Protect with owner password and permission restrictions.
- Reject weak password.
- Unlock using owner password.
- Reject user password when owner-level access is required.

### Split / Page Organizer

- Load normal PDF.
- Load password-protected PDF.
- Select all pages.
- Select odd/even/page-list pages.
- Drag pages to reorder.
- Extract selected pages.
- Remove selected pages.
- Rotate selected pages.
- Confirm output page count and order.

### Merge

- Add multiple PDFs.
- Drag files to reorder in the merge queue.
- Drag pages within a file.
- Insert pages between files.
- Merge output.
- Confirm output opens and page order is correct.
- Test duplicate/locked/password-required validation.

### Compress

- Safe mode: preserve text/vector/link behavior.
- Balanced mode: preserve text/vector/link behavior.
- Strong mode: verify warning if pages are rasterized.
- Extreme mode: verify full-page rasterization warning and readable output quality.
- Confirm output is smaller than input or original is kept.
- Confirm page count is unchanged.
- Confirm output opens and renders.

### Thumbnail / PDFium

- Confirm page thumbnails render.
- Confirm no `pdfium.dll` load error occurs.
- Test on a clean Windows machine if possible.

### Session restore

- Open app.
- Prepare state in Protect, Split, Merge, and Compress.
- Close app.
- Reopen app.
- Confirm session restores safely.
- Confirm sensitive passwords are not persisted unless explicitly intended.

## 4. Documentation

Before release:

- Update `CHANGELOG.md`.
- Confirm dependency versions listed in the changelog match `PdfTool.App/PdfTool.App.csproj`.
- Confirm known issues are current.
- Confirm release date is correct.
- Confirm any local machine paths are not present in public docs.

## 5. Git tag

Use semantic versioning style:

```powershell
git tag v1.0.2
git push origin v1.0.2
```

For release candidates:

```powershell
git tag v1.0.2-rc.1
git push origin v1.0.2-rc.1
```

## 6. GitHub release

On GitHub:

1. Go to Releases.
2. Draft a new release.
3. Select the tag.
4. Copy the relevant section from `CHANGELOG.md`.
5. Attach installer/build artifact if available.
6. Publish release.

## 7. Rollback note

If a dependency upgrade breaks PDF behavior:

1. Revert changes to `PdfTool.App/PdfTool.App.csproj`.
2. Revert changes to `PdfTool.App/packages.lock.json`.
3. Run `dotnet restore PdfTool.App\PdfTool.App.csproj --configfile PdfTool.App\NuGet.Config --locked-mode`.
4. Rebuild and retest.
