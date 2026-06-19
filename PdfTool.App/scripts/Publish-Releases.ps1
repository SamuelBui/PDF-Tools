param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.2",
    [string]$Publisher = "PDF Tools",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "PdfTool.App.csproj"
$releaseRoot = Join-Path $projectRoot "artifacts\\release"
$exeDir = Join-Path $releaseRoot "PdfTool.App-$Version-exe-$Runtime"
$portableDir = Join-Path $releaseRoot "PdfTool.App-$Version-portable-$Runtime"
$portableZip = Join-Path $releaseRoot "PdfTool.App-$Version-portable-$Runtime.zip"
$setupExe = Join-Path $releaseRoot "PdfTool.App-$Version-setup-$Runtime.exe"
$installerScript = Join-Path $releaseRoot "PdfTool.App-$Version-setup.iss"
$iconPath = Join-Path $projectRoot "Assets\\PdfTools.ico"

function Copy-PdfiumSupportFiles {
    param(
        [string]$SourceDirectory,
        [string]$DestinationDirectory
    )

    $patterns = @(
        "pdfium.dll",
        "pdfium_x64.dll",
        "pdfium_x86.dll",
        "PDFiumSharp.dll",
        "PDFiumSharp.Wpf.dll"
    )

    foreach ($pattern in $patterns) {
        Get-ChildItem -LiteralPath $SourceDirectory -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $DestinationDirectory $_.Name) -Force
        }
    }
}

function Assert-WorkspacePath {
    param([string]$PathToCheck)

    $resolvedRoot = [System.IO.Path]::GetFullPath($projectRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($PathToCheck)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside the project workspace: $resolvedPath"
    }
}

function Reset-Directory {
    param([string]$TargetPath)

    Assert-WorkspacePath -PathToCheck $TargetPath
    if (Test-Path -LiteralPath $TargetPath) {
        Remove-Item -LiteralPath $TargetPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $TargetPath | Out-Null
}

function Resolve-InnoCompiler {
    $command = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe",
        "C:\\Program Files\\Inno Setup 6\\ISCC.exe",
        "C:\\Program Files (x86)\\Inno Setup 5\\ISCC.exe",
        "C:\\Program Files\\Inno Setup 5\\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
Reset-Directory -TargetPath $exeDir
Reset-Directory -TargetPath $portableDir

foreach ($filePath in @($portableZip, $setupExe, $installerScript)) {
    if (Test-Path -LiteralPath $filePath) {
        Assert-WorkspacePath -PathToCheck $filePath
        Remove-Item -LiteralPath $filePath -Force
    }
}

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

$restoreArgs = @(
    "restore",
    $projectFile,
    "--configfile", (Join-Path $projectRoot "NuGet.Config"),
    "-p:NuGetAudit=false"
)

if ($SelfContained) {
    $restoreArgs += @("-r", $Runtime)
}

Write-Host "Restoring publish dependencies..."
dotnet @restoreArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

$selfContainedText = if ($SelfContained) { "true" } else { "false" }
$modeLabel = if ($SelfContained) { "Self-contained" } else { "Framework-dependent" }

$publishCommon = @(
    "publish",
    $projectFile,
    "-c", $Configuration,
    "--no-restore",
    "--self-contained", $selfContainedText,
    "/p:PublishTrimmed=false",
    "/p:DebugType=None",
    "/p:DebugSymbols=false"
)

if ($SelfContained) {
    $publishCommon += @("-r", $Runtime)
}

Write-Host "Publishing standalone executable..."
dotnet @publishCommon `
    "-o" $exeDir `
    "/p:PublishSingleFile=true" `
    "/p:IncludeNativeLibrariesForSelfExtract=true"
if ($LASTEXITCODE -ne 0) {
    throw "Standalone publish failed with exit code $LASTEXITCODE."
}

Write-Host "Publishing portable package..."
dotnet @publishCommon `
    "-o" $portableDir `
    "/p:PublishSingleFile=false" `
    "/p:IncludeNativeLibrariesForSelfExtract=false"
if ($LASTEXITCODE -ne 0) {
    throw "Portable publish failed with exit code $LASTEXITCODE."
}

Write-Host "Copying PDFium support files next to the standalone executable..."
Copy-PdfiumSupportFiles -SourceDirectory $portableDir -DestinationDirectory $exeDir

Write-Host "Creating portable zip..."
Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $portableZip -Force

$installerStatus = "Not created. Inno Setup compiler was not found."
$innoCompiler = Resolve-InnoCompiler
if ($innoCompiler) {
    if (-not (Test-Path -LiteralPath $iconPath)) {
        throw "Installer icon was not found: $iconPath"
    }

    Write-Host "Creating Inno Setup installer..."
    $releaseRootFull = [System.IO.Path]::GetFullPath($releaseRoot)
    $portableDirFull = [System.IO.Path]::GetFullPath($portableDir)
    $iconPathFull = [System.IO.Path]::GetFullPath($iconPath)
    $setupBaseName = [System.IO.Path]::GetFileNameWithoutExtension($setupExe)

    $innoScriptContent = @"
#define MyAppName "PDF Utility Tool"
#define MyAppVersion "$Version"
#define MyAppPublisher "$Publisher"
#define MyAppExeName "PdfTool.App.exe"

[Setup]
AppId={{9B6B3A3C-874D-4F8A-BF55-7A47C0A22E29}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=$releaseRootFull
OutputBaseFilename=$setupBaseName
SetupIconFile=$iconPathFull
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
VersionInfoVersion=$Version
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "$portableDirFull\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
"@

    $innoScriptContent | Set-Content -Path $installerScript -Encoding ASCII
    & $innoCompiler $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $setupExe)) {
        throw "Installer setup was not created: $setupExe"
    }

    Remove-Item -LiteralPath $installerScript -Force
    $installerStatus = $setupExe
}

$summary = @"
PDF Tool $Version release packages created.

Mode:
$modeLabel

Standalone EXE:
$exeDir

Portable folder:
$portableDir

Portable ZIP:
$portableZip

Installer setup:
$installerStatus
"@

$summary | Set-Content -Path (Join-Path $releaseRoot "publish-summary.txt")
Write-Host $summary
