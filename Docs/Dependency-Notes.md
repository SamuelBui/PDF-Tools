# Dependency Notes - PdfTool.App

## Sensitive dependencies

| Dependency | Why it matters |
|---|---|
| PDFsharp | Protect, unlock, split, merge, low-level PDF operations |
| PDFium.Windows | Native PDFium DLL used for rendering/preview |
| PDFiumSharp | PDFium .NET wrapper used for thumbnail/render operations |
| PDFiumSharp.Wpf | WPF image rendering bridge for PDFium |
| ClosedXML | Excel/CSV batch workflows |
| Microsoft.Extensions.DependencyInjection | App service composition |

## Upgrade rules

Before upgrading PDF-related dependencies:

1. Create a branch.
2. Update only one dependency group at a time.
3. Regenerate `PdfTool.App/packages.lock.json`.
4. Run the release checklist.
5. Test on a clean Windows machine.
6. Confirm PDFium native DLL still loads.
7. Confirm PDFsharp security/owner-password logic still works.

## PDFsharp caution

The app may rely on low-level PDFsharp behavior for owner-password access checks. Do not upgrade PDFsharp casually without testing Protect, Unlock, Split, Merge, and password-protected PDF workflows.

## PDFium caution

PDFium dependencies affect:

- thumbnail rendering
- page preview
- compression inspection
- compression validation

After any PDFium package change, run the native dependency smoke test.
