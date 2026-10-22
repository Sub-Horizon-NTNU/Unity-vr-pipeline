param(
    [Parameter(Mandatory=$true)][string]$GStreamerRoot
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$build = Join-Path $repo 'Builds\v1'
$release = Join-Path $repo 'Releases\v1'
$gst = [IO.Path]::GetFullPath($GStreamerRoot)
if (!(Test-Path -LiteralPath (Join-Path $build 'Unity-vr-pipeline-v1.exe'))) { throw 'Build Windows v1 in Unity first.' }
if (!(Test-Path -LiteralPath (Join-Path $gst 'bin\gstreamer-1.0-0.dll'))) { throw 'GStreamerRoot must point to an MSVC x64 runtime.' }
$result = Get-Content -Raw -LiteralPath (Join-Path $build 'unity-build-report.json') | ConvertFrom-Json
if (!$result.success) { throw 'The latest Unity release build did not succeed.' }
New-Item -ItemType Directory -Path $release -Force | Out-Null
$runtime = Join-Path $build 'GStreamer'
foreach ($dir in @('bin','lib\gstreamer-1.0','libexec\gstreamer-1.0','share')) {
    New-Item -ItemType Directory -Path (Join-Path $runtime $dir) -Force | Out-Null
}
Get-ChildItem -LiteralPath (Join-Path $gst 'bin') -Filter '*.dll' -File | Copy-Item -Destination (Join-Path $runtime 'bin') -Force
foreach ($exe in @('gst-inspect-1.0.exe','gst-launch-1.0.exe')) {
    Copy-Item -LiteralPath (Join-Path $gst ('bin\'+$exe)) -Destination (Join-Path $runtime 'bin') -Force
}
Get-ChildItem -LiteralPath (Join-Path $gst 'lib\gstreamer-1.0') -Filter '*.dll' -File | Copy-Item -Destination (Join-Path $runtime 'lib\gstreamer-1.0') -Force
Get-ChildItem -LiteralPath (Join-Path $gst 'libexec\gstreamer-1.0') -Filter '*.exe' -File | Copy-Item -Destination (Join-Path $runtime 'libexec\gstreamer-1.0') -Force
foreach ($dir in @('licenses','gstreamer-1.0')) {
    if (Test-Path -LiteralPath (Join-Path $gst ('share\'+$dir))) {
        Copy-Item -LiteralPath (Join-Path $gst ('share\'+$dir)) -Destination (Join-Path $runtime 'share') -Recurse -Force
    }
}
$launcher = @'
@echo off
setlocal
cd /d "%~dp0"
set "PATH=%~dp0GStreamer\bin;%PATH%"
set "GST_PLUGIN_SYSTEM_PATH_1_0=%~dp0GStreamer\lib\gstreamer-1.0"
set "GST_PLUGIN_PATH_1_0=%~dp0GStreamer\lib\gstreamer-1.0"
set "GST_PLUGIN_SCANNER_1_0=%~dp0GStreamer\libexec\gstreamer-1.0\gst-plugin-scanner.exe"
if not exist "%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\v1" mkdir "%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\v1"
set "GST_REGISTRY_1_0=%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\v1\registry.bin"
"%~dp0Unity-vr-pipeline-v1.exe" %*
exit /b %ERRORLEVEL%
'@
[IO.File]::WriteAllText((Join-Path $build 'Start-v1.cmd'), $launcher.Replace("`r`n","`n").Replace("`n","`r`n"), [Text.Encoding]::ASCII)
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE.md'),(Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'UnityProject\Assets\ROV\Resources\RovHudFont-LICENSE.txt') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'docs\SETUP.md') -Destination (Join-Path $build 'LES-MEG.md') -Force
$gstVersion = & (Join-Path $gst 'bin\gst-launch-1.0.exe') --version | Select-Object -First 1
$commit = (& git -C $repo rev-parse HEAD).Trim()
[ordered]@{version='v1';unity=$result.unityVersion;platform='Windows x64';graphics='Direct3D11';backend='Mono';gstreamer=$gstVersion;sourceCommit=$commit;checks=$result.checks} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $build 'build-info.json') -Encoding utf8

Add-Type -AssemblyName System.IO.Compression
function Write-Zip([string]$Path, [string]$Base, [string[]]$Files, [string]$Prefix) {
    $stream = [IO.File]::Open($Path,[IO.FileMode]::Create,[IO.FileAccess]::Write)
    $archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($rel in $Files) {
            $source = Join-Path $Base $rel
            if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing source file: $rel" }
            $entry = $archive.CreateEntry($Prefix + $rel.Replace('\','/'), [IO.Compression.CompressionLevel]::Optimal)
            $input = [IO.File]::OpenRead($source)
            $output = $entry.Open()
            try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
}
$playerFiles = @(Get-ChildItem -LiteralPath $build -Recurse -File | Where-Object {
    $_.FullName -notmatch 'DoNotShip|BackUpThisFolder_ButDontShipItWithYourGame'
} | ForEach-Object { [IO.Path]::GetRelativePath($build,$_.FullName) })
$windowsZip = Join-Path $release 'Unity-vr-pipeline-v1-Windows-x64.zip'
Write-Zip $windowsZip $build $playerFiles 'Unity-vr-pipeline-v1/'
$projectFiles = @(& git -C $repo -c core.quotepath=false ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate source files with Git.' }
$projectZip = Join-Path $release 'Unity-vr-pipeline-v1-project.zip'
Write-Zip $projectZip $repo $projectFiles 'Unity-vr-pipeline-v1/'
$hashes = foreach ($path in @($windowsZip,$projectZip)) {
    $hash=Get-FileHash -LiteralPath $path -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($path)
}
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'),$hashes,[Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $release -File | Select-Object Name,Length
