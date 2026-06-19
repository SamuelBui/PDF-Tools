# Packaging

Use the release script to build Windows x64 release packages:

- `portable`: self-contained folder plus a `.zip`
- `setup`: Inno Setup installer `.exe`
- `exe`: self-contained standalone executable folder

Run:

```powershell
& ".\scripts\Publish-Releases.ps1"
```

Release output is written to:

```text
artifacts\release
```

Notes:

- The standalone publish now copies `pdfium.dll`, `pdfium_x64.dll`, `pdfium_x86.dll`, and the related `PDFiumSharp` assemblies next to `PdfTool.App.exe` because thumbnail rendering depends on those native files being discoverable at runtime.
- The portable package is intended for copying to another machine without installation.
- The setup installer is built with Inno Setup when `ISCC.exe` is available.
- All packages target `win-x64`.
