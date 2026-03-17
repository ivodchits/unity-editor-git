<#
.SYNOPSIS
    Exports the Git Editor UPM package as a standalone .unitypackage file.

.DESCRIPTION
    Creates a .unitypackage (gzip tar) that can be imported into any Unity project
    via Assets > Import Package. Files land under Assets/GitEditor/.

.PARAMETER OutputPath
    Where to write the .unitypackage file. Defaults to GitEditor.unitypackage
    in the same directory as this script.

.EXAMPLE
    .\ExportUnityPackage.ps1
    .\ExportUnityPackage.ps1 -OutputPath "C:\Shared\GitEditor.unitypackage"
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "GitEditor.unitypackage")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Get-StableGuid([string]$seed) {
    $md5    = [System.Security.Cryptography.MD5]::Create()
    $bytes  = [System.Text.Encoding]::UTF8.GetBytes($seed)
    $hash   = $md5.ComputeHash($bytes)
    return  [System.BitConverter]::ToString($hash).Replace("-","").ToLower()
}

function Get-MetaContent([string]$guid, [string]$assetPath) {
    $ext = [System.IO.Path]::GetExtension($assetPath).ToLower()

    $importer = switch ($ext) {
        ".cs"      { "MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleNameVariant: " }
        ".asmdef"  { "AssemblyDefinitionImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleNameVariant: " }
        ".json"    { "TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleNameVariant: " }
        default    { "DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleNameVariant: " }
    }

    return "fileFormatVersion: 2`nguid: $guid`n$importer`n"
}

# ---------------------------------------------------------------------------
# File map: source (relative to script) -> target path inside Assets/
# ---------------------------------------------------------------------------

$packageRoot = $PSScriptRoot

$fileMap = @(
    # asmdef
    @{ Src = "Editor\GitEditor.Editor.asmdef"; Dst = "Assets/GitEditor/Editor/GitEditor.Editor.asmdef" }

    # Core
    @{ Src = "Editor\Core\GitResult.cs";        Dst = "Assets/GitEditor/Editor/Core/GitResult.cs" }
    @{ Src = "Editor\Core\GitCommandRunner.cs"; Dst = "Assets/GitEditor/Editor/Core/GitCommandRunner.cs" }
    @{ Src = "Editor\Core\GitSettings.cs";      Dst = "Assets/GitEditor/Editor/Core/GitSettings.cs" }

    # Models
    @{ Src = "Editor\Models\GitFileChange.cs";  Dst = "Assets/GitEditor/Editor/Models/GitFileChange.cs" }
    @{ Src = "Editor\Models\GitCommit.cs";      Dst = "Assets/GitEditor/Editor/Models/GitCommit.cs" }
    @{ Src = "Editor\Models\GitBranch.cs";      Dst = "Assets/GitEditor/Editor/Models/GitBranch.cs" }
    @{ Src = "Editor\Models\GitStash.cs";       Dst = "Assets/GitEditor/Editor/Models/GitStash.cs" }
    @{ Src = "Editor\Models\GitDiffHunk.cs";    Dst = "Assets/GitEditor/Editor/Models/GitDiffHunk.cs" }
    @{ Src = "Editor\Models\AssetDiffEntry.cs"; Dst = "Assets/GitEditor/Editor/Models/AssetDiffEntry.cs" }

    # Services
    @{ Src = "Editor\Services\GitStatusService.cs";  Dst = "Assets/GitEditor/Editor/Services/GitStatusService.cs" }
    @{ Src = "Editor\Services\GitLogService.cs";     Dst = "Assets/GitEditor/Editor/Services/GitLogService.cs" }
    @{ Src = "Editor\Services\GitBranchService.cs";  Dst = "Assets/GitEditor/Editor/Services/GitBranchService.cs" }
    @{ Src = "Editor\Services\GitDiffService.cs";    Dst = "Assets/GitEditor/Editor/Services/GitDiffService.cs" }
    @{ Src = "Editor\Services\GitStashService.cs";   Dst = "Assets/GitEditor/Editor/Services/GitStashService.cs" }
    @{ Src = "Editor\Services\GitRemoteService.cs";     Dst = "Assets/GitEditor/Editor/Services/GitRemoteService.cs" }
    @{ Src = "Editor\Services\UnityYamlDiffParser.cs"; Dst = "Assets/GitEditor/Editor/Services/UnityYamlDiffParser.cs" }

    # Window
    @{ Src = "Editor\Window\GitEditorWindow.cs";                        Dst = "Assets/GitEditor/Editor/Window/GitEditorWindow.cs" }
    @{ Src = "Editor\Window\Sections\StagingSectionDrawer.cs";          Dst = "Assets/GitEditor/Editor/Window/Sections/StagingSectionDrawer.cs" }
    @{ Src = "Editor\Window\Sections\CommitSectionDrawer.cs";           Dst = "Assets/GitEditor/Editor/Window/Sections/CommitSectionDrawer.cs" }
    @{ Src = "Editor\Window\Sections\PullPushSectionDrawer.cs";         Dst = "Assets/GitEditor/Editor/Window/Sections/PullPushSectionDrawer.cs" }
    @{ Src = "Editor\Window\Sections\HistorySectionDrawer.cs";          Dst = "Assets/GitEditor/Editor/Window/Sections/HistorySectionDrawer.cs" }
    @{ Src = "Editor\Window\Sections\BranchesSectionDrawer.cs";         Dst = "Assets/GitEditor/Editor/Window/Sections/BranchesSectionDrawer.cs" }
    @{ Src = "Editor\Window\Sections\InfoSectionDrawer.cs";             Dst = "Assets/GitEditor/Editor/Window/Sections/InfoSectionDrawer.cs" }
    @{ Src = "Editor\Window\Sections\ConsoleSectionDrawer.cs";          Dst = "Assets/GitEditor/Editor/Window/Sections/ConsoleSectionDrawer.cs" }
    @{ Src = "Editor\Window\Popups\FileHistoryPopup.cs";                Dst = "Assets/GitEditor/Editor/Window/Popups/FileHistoryPopup.cs" }
    @{ Src = "Editor\Window\Popups\CreateBranchPopup.cs";              Dst = "Assets/GitEditor/Editor/Window/Popups/CreateBranchPopup.cs" }
    @{ Src = "Editor\Window\Popups\AssetDiffPopup.cs";                 Dst = "Assets/GitEditor/Editor/Window/Popups/AssetDiffPopup.cs" }
    @{ Src = "Editor\Window\Shared\GitStyles.cs";                       Dst = "Assets/GitEditor/Editor/Window/Shared/GitStyles.cs" }
    @{ Src = "Editor\Window\Shared\GitIcons.cs";                        Dst = "Assets/GitEditor/Editor/Window/Shared/GitIcons.cs" }
)

# ---------------------------------------------------------------------------
# Build temp archive structure
# ---------------------------------------------------------------------------

$tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ("GitEditorPkg_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmpDir | Out-Null

Write-Host "Building package in: $tmpDir"

# UTF-8 without BOM — Unity reads pathname and meta files as plain text;
# a BOM causes Unity to create a folder literally named "<BOM>Assets" instead of "Assets".
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

foreach ($entry in $fileMap) {
    $srcPath = Join-Path $packageRoot $entry.Src
    $dstPath = $entry.Dst   # forward-slash Unity path

    if (-not (Test-Path $srcPath)) {
        Write-Warning "Source not found, skipping: $srcPath"
        continue
    }

    $guid    = Get-StableGuid $dstPath
    $assetDir = Join-Path $tmpDir $guid
    New-Item -ItemType Directory -Path $assetDir | Out-Null

    # pathname file — no trailing newline, no BOM
    [System.IO.File]::WriteAllText((Join-Path $assetDir "pathname"), $dstPath, $utf8NoBom)

    # asset file (the actual source)
    Copy-Item $srcPath (Join-Path $assetDir "asset")

    # asset.meta
    $metaContent = Get-MetaContent $guid $dstPath
    [System.IO.File]::WriteAllText((Join-Path $assetDir "asset.meta"), $metaContent, $utf8NoBom)

    Write-Host "  + $dstPath  [$guid]"
}

# ---------------------------------------------------------------------------
# Create the tar.gz (Windows 10+ has tar.exe)
# ---------------------------------------------------------------------------

Write-Host "`nPacking archive..."

# tar needs relative paths inside tmpDir
Push-Location $tmpDir
try {
    $tarExe = "$env:SystemRoot\System32\tar.exe"
    $tarArgs = @("-czf", $OutputPath) + (Get-ChildItem -Directory | Select-Object -ExpandProperty Name)
    & $tarExe @tarArgs
    if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# Cleanup
# ---------------------------------------------------------------------------

Remove-Item -Recurse -Force $tmpDir
Write-Host "`nDone! Package written to:`n  $OutputPath`n"
Write-Host "Import via: Assets > Import Package > Custom Package..."
