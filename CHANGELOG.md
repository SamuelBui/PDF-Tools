# Changelog

All notable changes to this project will be documented in this file.

This project uses a simple semantic versioning style:

- `v1.0.1` = bug fix only
- `v1.1.0` = backward-compatible feature or UX improvement
- `v2.0.0` = major change that may alter existing workflows

---

## [Unreleased]

### Added
- Added a root `PdfTools.slnx` solution for opening the app and smoke harness together.
- Rewrote the root README as complete product documentation.
- Added MIT license metadata from the GitHub repository.

### Changed
- Replaced personal contact/publisher defaults with repository-oriented support and configurable packaging metadata.

### Fixed

### Removed
- Removed generated build, release, and regression artifacts from the working tree.
- Removed obsolete `tmp-test` project exclusions.

### Known Issues

---

## [1.0.2] - 2026-05-26

### Added
- Added NuGet lock-file support for reproducible package restore.
- Added release checklist for local/manual release validation.
- Added documented build commands using locked restore mode.

### Changed
- Stabilized dependency restore for the WPF/.NET 8 PDF utility app.
- Documented sensitive dependencies used by PDF operations and native PDF rendering.
- Documented the local release validation workflow.

### Fixed
- No PDF feature fix in this dependency/release-notes update.

### Known Issues
- PDF compression behavior depends on document structure and image content.
- Strong compression may rasterize pages if enabled by the compression workflow.
- Extreme compression rasterizes every page and can reduce text search, links, annotations, and vector sharpness.
- PDFium native dependency must be validated on a clean Windows machine.
- OCR, redaction, and digital signing are not currently supported.

### Dependencies
- PDFsharp: 6.2.4
- Microsoft.Extensions.DependencyInjection: 9.0.0
- ClosedXML: 0.104.0
- PDFium.Windows: 1.0.0
- PDFiumSharp: 1.4660.0-alpha1
- PDFiumSharp.Wpf: 1.4660.0-alpha1

### Test Summary
- Automated smoke regression: 14/14 passed
- Protect PDF: covered by smoke tests, manual release test pending
- Unlock PDF: covered by smoke tests, manual release test pending
- Split / Page Organizer: covered by smoke tests, manual release test pending
- Merge PDF: covered by smoke tests, manual release test pending
- Compress PDF: covered by smoke tests, manual release test pending
- Thumbnail rendering with PDFium: manual release test pending
- Session restore: manual release test pending
- Native dependency smoke test: manual release test pending
