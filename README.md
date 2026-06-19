# PDF Tools

PDF Tools is a Windows desktop application for everyday PDF work: protect files, unlock owner-password documents, split and organize pages, merge multiple PDFs, preview pages, and reduce file size with practical compression profiles.

The app is built with WPF on .NET 8 and is designed for local document processing. PDF files are handled on the user's machine; there is no cloud upload workflow in the application.

## Features

- Protect PDFs with user and owner passwords.
- Configure document permissions such as printing, copying, editing, forms, and annotations.
- Unlock PDFs when the correct owner password is available.
- Process single files or batches for protection and unlock workflows.
- Split PDFs by page ranges, selected pages, odd/even pages, or organizer selections.
- Reorder, rotate, extract, and remove pages from a visual page organizer.
- Merge multiple PDFs and reorder documents or pages before export.
- Render page thumbnails and previews using PDFium.
- Compress PDFs with Safe, Balanced, Strong, and Extreme profiles.
- Optimize embedded images while preserving text/vector structure where possible.
- Clear local recent files, session state, logs, and temporary PDFium copies from the About tab.
- Package releases as a portable ZIP or a Windows setup installer.

## Compression Profiles

| Profile | Best for | Behavior |
|---|---|---|
| Safe | Documents where readability and structure matter most | Light image downsampling, JPEG quality 85/100, preserve text, links, vector objects, and color space where possible |
| Balanced | General file-size reduction with good visual quality | Moderate image downsampling, JPEG quality 70/100, preserve text, links, vector objects, and color space where possible |
| Strong | Image-heavy PDFs that need more reduction | Stronger image optimization and selective rasterization for scan-like pages when needed |
| Extreme | Maximum reduction when editability/searchability is not required | Rasterizes every page; text search, links, annotations, and vector sharpness may be lost |

Compression results depend on the structure of each PDF. Text-only files may not shrink much because there is little image data to optimize. Scan-heavy files usually benefit the most.

## Architecture

```text
PdfTool.App/
  Models/       Domain models for protection, splitting, merging, compression, sessions
  Services/     PDF operations, PDFium rendering, logging, recent files, app session state
  ViewModels/   MVVM state and commands for the WPF interface
  Views/        Main WPF window and UI layout
  Helpers/      File access, safe writes, passwords, PDF security, PDFium temp handling
  Assets/       Application icon
  scripts/      Release packaging script

PdfToolSmoke/
  Console smoke/regression harness for PDF workflows and compression checks

Docs/
  Release checklist and dependency notes
```

## Technology

- .NET 8 WPF for the desktop application.
- PDFsharp for PDF structure operations such as protection, split, merge, and low-level document updates.
- PDFium, PDFiumSharp, and PDFiumSharp.Wpf for rendering, thumbnails, PDF inspection, and rasterized output paths.
- ClosedXML for spreadsheet-based batch workflows.
- Microsoft.Extensions.DependencyInjection for service composition.

Special thanks to the PDFsharp and PDFium contributors and maintainers. Their work makes this project possible.

## Requirements

For users:

- Windows 10 or later, x64.
- No separate .NET runtime is required for the default self-contained release packages.

For developers:

- Windows 10 or later.
- .NET 8 SDK.
- Visual Studio 2022 or another editor that supports WPF projects.
- Inno Setup 6, only if you want to build the installer package.

Open `PdfTools.slnx` from the repository root to work with both the desktop app and the smoke harness.

## Build From Source

Restore with the pinned dependency graph:

```powershell
dotnet restore PdfTools.slnx --configfile PdfTool.App\NuGet.Config --locked-mode -r win-x64
```

Build the app:

```powershell
dotnet build PdfTools.slnx -c Release --no-restore
```

Run the app during development:

```powershell
dotnet run --project PdfTool.App\PdfTool.App.csproj
```

## Regression Smoke Harness

Build the smoke harness:

```powershell
dotnet build PdfToolSmoke\PdfToolSmoke.csproj --configfile PdfTool.App\NuGet.Config
```

Run the default regression checks:

```powershell
dotnet run --project PdfToolSmoke\PdfToolSmoke.csproj
```

Compress one PDF from the command line:

```powershell
dotnet run --project PdfToolSmoke\PdfToolSmoke.csproj -- --compress-file "C:\path\input.pdf" "C:\path\output.pdf" balanced
```

Valid compression profile names are `safe`, `balanced`, `strong`, and `extreme`.

## Packaging

Create a portable ZIP and Inno Setup installer:

```powershell
powershell -ExecutionPolicy Bypass -File PdfTool.App\scripts\Publish-Releases.ps1
```

The script writes generated release output under:

```text
PdfTool.App\artifacts\release\
```

Generated release artifacts are intentionally ignored by Git. Attach the ZIP and setup executable to a GitHub Release instead of committing them to the repository.

## Privacy And Local Data

The application is intended to process PDFs locally. It may store local convenience data such as recent file entries, session state, temporary PDFium copies, and logs.

Use `About > Privacy & Cache > Clear Local Data` to clear those local records from inside the application.

Passwords should be treated as sensitive data. The app is designed to avoid persisting generated or typed passwords unless a workflow explicitly requires exporting a report or batch mapping.

## Documentation

- `CHANGELOG.md` lists release notes and known issues.
- `Docs/Release-Checklist.md` describes manual validation before publishing a release.
- `Docs/Dependency-Notes.md` explains dependency upgrade risk areas.
- `PdfTool.App/Docs/` contains focused test notes for major PDF workflows.

## Known Limitations

- OCR is not currently implemented.
- Digital signing is not currently implemented.
- True redaction is not currently implemented.
- Extreme compression rasterizes every page and can remove searchability, links, annotations, and vector sharpness.
- Some advanced or unusual PDF image/color-space structures are intentionally skipped by object-level image optimization to avoid damaging the document.

## License

No open-source license has been published yet. Until a `LICENSE` file is added, the source code is shared for review only and all rights are reserved by the project owner.
