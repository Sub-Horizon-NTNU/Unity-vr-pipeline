# Run after ROV > Release > Build Windows pilot tools preview.
# RuntimeRoot is the bundled GStreamer folder from v1 (or an equivalent complete MSVC x64 runtime).
param([string]$RuntimeRoot)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$build = Join-Path $repo 'Builds\pilot-tools-preview'
if (!$RuntimeRoot) { $RuntimeRoot = Join-Path $repo 'Builds\v1\GStreamer' }
$report = Get-Content -Raw -LiteralPath (Join-Path $build 'build-report.json') | ConvertFrom-Json
if (!$report.success -or !(Test-Path -LiteralPath (Join-Path $build 'Unity-vr-pipeline-preview.exe'))) { throw 'Build the pilot tools preview first.' }
foreach ($relative in @('bin\gstreamer-1.0-0.dll', 'lib\gstreamer-1.0\gstlibav.dll', 'libexec\gstreamer-1.0\gst-plugin-scanner.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $RuntimeRoot $relative))) { throw "RuntimeRoot is incomplete: $relative" }
}
$destination = Join-Path $build 'GStreamer'
if ([IO.Path]::GetFullPath($RuntimeRoot).TrimEnd('\') -eq [IO.Path]::GetFullPath($destination).TrimEnd('\')) { throw 'Source runtime must be outside the preview folder.' }
if (!(Test-Path -LiteralPath $destination)) { New-Item -ItemType Directory -Path $destination | Out-Null }
Get-ChildItem -LiteralPath $RuntimeRoot | Copy-Item -Destination $destination -Recurse -Force
$launcher = @'
@echo off
setlocal
cd /d "%~dp0"
set "PATH=%~dp0GStreamer\bin;%PATH%"
set "GST_PLUGIN_SYSTEM_PATH_1_0=%~dp0GStreamer\lib\gstreamer-1.0"
set "GST_PLUGIN_PATH_1_0=%~dp0GStreamer\lib\gstreamer-1.0"
set "GST_PLUGIN_SCANNER_1_0=%~dp0GStreamer\libexec\gstreamer-1.0\gst-plugin-scanner.exe"
if not exist "%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\preview" mkdir "%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\preview"
set "GST_REGISTRY_1_0=%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\preview\registry.bin"
"%~dp0Unity-vr-pipeline-preview.exe" %*
exit /b %ERRORLEVEL%
'@
[IO.File]::WriteAllText((Join-Path $build 'Start-preview.cmd'), $launcher.Replace("`r`n", "`n").Replace("`n", "`r`n"), [Text.Encoding]::ASCII)
Copy-Item -LiteralPath (Join-Path $repo 'scripts\Setup-Firewall.ps1'), (Join-Path $repo 'LICENSE.md'), (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'UnityProject\Assets\ROV\Resources\RovHudFont-LICENSE.txt') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'docs\PILOT_TOOLS.md') -Destination (Join-Path $build 'PILOT_TOOLS.md') -Force
Write-Output "Preview ready: $build"
