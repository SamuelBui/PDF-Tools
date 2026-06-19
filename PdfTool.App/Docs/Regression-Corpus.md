# PDF Regression Corpus

The regression corpus for `PdfTool.App` is generated and executed by:

- `PdfToolSmoke/PdfToolSmoke.csproj`

The generated corpus is intentionally not committed. It can be recreated at any time by running the smoke harness.

## Goals

The corpus verifies the core app flows:

- `Protect`
- `Unlock`
- `Split`
- `Merge`
- `Compress`

It uses repeatable sample data instead of relying on manual PDFs from a local machine.

## Generated Files

The harness creates these folders under `PdfToolSmoke/RegressionCorpus/`:

- `Input`
- `Derived`
- `Assets`
- `Results`

It also writes:

- `corpus-manifest.json`
- `regression-report.json`

## Current Cases

- `01-text-report.pdf`: text/layout document for protect and merge checks
- `02-vector-diagrams.pdf`: vector document for merge, split, and rotate checks
- `03-mixed-brochure.pdf`: mixed document for split and compression checks
- `04-scan-color.pdf`: color scan used by Safe and Balanced compression
- `05-scan-lowcolor.pdf`: low-color scan used by Strong compression
- `06-protected-restricted.pdf`: protected file with user and owner passwords
- `99-invalid.pdf`: pseudo-PDF for negative validation checks

## Run

Build the harness:

```powershell
dotnet build PdfToolSmoke\PdfToolSmoke.csproj --configfile PdfTool.App\NuGet.Config
```

Generate the corpus and run all regression checks:

```powershell
dotnet run --project PdfToolSmoke\PdfToolSmoke.csproj --configfile PdfTool.App\NuGet.Config
```

Generate the corpus only:

```powershell
dotnet run --project PdfToolSmoke\PdfToolSmoke.csproj --configfile PdfTool.App\NuGet.Config -- --generate-only
```

## Checks

- `Protect` accepts a valid text PDF and rejects an invalid pseudo-PDF
- `Unlock` rejects the user password and accepts the owner password
- `Split` verifies extract, remove, and rotate output
- `Merge` verifies basic merge and merge with reorder plus rotate
- `Compress` verifies Safe, Balanced, and Strong compression scenarios

## Future Cases

- Add runtime tests for locked files and wrong passwords
- Add real-world malformed PDFs
- Add session auto-restore coverage
- Add per-document before/after compression thresholds
