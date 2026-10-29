#Requires -Version 5.1
param(
    [Parameter(Mandatory=$true)][string]$GStreamerRoot
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$build = Join-Path $repo 'Builds\v3'
$release = Join-Path $repo 'Releases\v3'
$gst = [IO.Path]::GetFullPath($GStreamerRoot)
$runtime = Join-Path $build 'GStreamer'
if ($gst.TrimEnd('\') -eq $runtime.TrimEnd('\')) { throw 'GStreamerRoot must be outside the v3 build folder.' }
if (!(Test-Path -LiteralPath (Join-Path $build 'Unity-vr-pipeline-v3.exe'))) { throw 'Build Windows v3 in Unity first.' }
if (!(Test-Path -LiteralPath (Join-Path $build 'Unity-vr-pipeline-v3_Data\Plugins\x86_64\GstUnityPlugin.dll'))) { throw 'The player is missing the Windows x64 native plugin.' }
foreach ($relative in @('bin\gstreamer-1.0-0.dll', 'bin\gst-launch-1.0.exe', 'bin\gst-inspect-1.0.exe',
    'libexec\gstreamer-1.0\gst-plugin-scanner.exe', 'lib\gstreamer-1.0\gstlibav.dll',
    'lib\gstreamer-1.0\gstudp.dll', 'lib\gstreamer-1.0\gstrtp.dll',
    'lib\gstreamer-1.0\gstvideoparsersbad.dll', 'lib\gstreamer-1.0\gstvideoconvertscale.dll',
    'lib\gstreamer-1.0\gstapp.dll', 'lib\gstreamer-1.0\gstx264.dll', 'lib\gstreamer-1.0\gstisomp4.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $gst $relative))) { throw "GStreamerRoot is incomplete: $relative" }
}
$result = Get-Content -Raw -LiteralPath (Join-Path $build 'unity-build-report.json') | ConvertFrom-Json
if (!$result.success -or $result.errors -ne 0) { throw 'The latest Unity release build did not succeed without errors.' }
if ($result.version -ne 'v3') { throw 'The build report must identify v3.' }
$changes = @(& git -C $repo status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Git working tree state.' }
if ($changes.Count -gt 0) { throw 'Commit or resolve working tree changes before packaging v3.' }
# A GitHub source archive can contain LFS pointers; this package must contain the actual binaries.
$lfsFiles = @(& git -C $repo -c core.quotepath=false lfs ls-files --name-only)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect Git LFS files.' }
foreach ($relative in $lfsFiles) {
    $path = Join-Path $repo $relative
    $input = [IO.File]::OpenRead($path)
    try {
        $prefix = [byte[]]::new(128)
        $length = $input.Read($prefix, 0, $prefix.Length)
        if ([Text.Encoding]::ASCII.GetString($prefix, 0, $length).StartsWith('version https://git-lfs.github.com/spec/')) {
            throw "Download actual LFS content before packaging: $relative"
        }
    } finally { $input.Dispose() }
}
New-Item -ItemType Directory -Path $release -Force | Out-Null
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
if not exist "%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\v3" mkdir "%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\v3"
set "GST_REGISTRY_1_0=%LOCALAPPDATA%\SubHorizon\Unity-vr-pipeline\v3\registry.bin"
"%~dp0Unity-vr-pipeline-v3.exe" %*
exit /b %ERRORLEVEL%
'@
[IO.File]::WriteAllText((Join-Path $build 'Start-v3.cmd'), $launcher.Replace("`r`n","`n").Replace("`n","`r`n"), [Text.Encoding]::ASCII)
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE.md'),(Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'UnityProject\Assets\ROV\Resources\RovHudFont-LICENSE.txt') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'docs\SETUP.md') -Destination (Join-Path $build 'LES-MEG.md') -Force
Copy-Item -LiteralPath (Join-Path $repo 'scripts\Setup-Firewall.ps1') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'docs\PILOT_TOOLS.md') -Destination $build -Force
Copy-Item -LiteralPath (Join-Path $repo 'docs\RELEASE_v3.md') -Destination $build -Force
$gstVersion = & (Join-Path $gst 'bin\gst-launch-1.0.exe') --version | Select-Object -First 1
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect GStreamer runtime version.' }
$commit = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not identify source commit.' }
[ordered]@{version='v3';unity=$result.unityVersion;platform='Windows x64';graphics='Direct3D11';backend='Mono';gstreamer=$gstVersion;sourceCommit=$commit;checks=$result.checks} |
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
} | ForEach-Object { $_.FullName.Substring($build.Length + 1) })
$windowsZip = Join-Path $release 'Unity-vr-pipeline-v3-Windows-x64.zip'
Write-Zip $windowsZip $build $playerFiles 'Unity-vr-pipeline-v3/'
$projectFiles = @(& git -C $repo -c core.quotepath=false ls-files --cached)
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate source files with Git.' }
$projectZip = Join-Path $release 'Unity-vr-pipeline-v3-project.zip'
Write-Zip $projectZip $repo $projectFiles 'Unity-vr-pipeline-v3/'
$hashes = foreach ($path in @($windowsZip,$projectZip)) {
    $hash=Get-FileHash -LiteralPath $path -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($path)
}
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'),$hashes,[Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $release -File | Select-Object Name,Length
