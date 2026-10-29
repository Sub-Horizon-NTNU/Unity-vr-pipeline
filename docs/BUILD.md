# Bygging og utgivelse

## Build and package Windows v3

Use Unity **6000.1.7f1** with Windows Build Support and stop Play. In the Editor,
choose **ROV → Release → Build Windows v3**. For batch builds, invoke
`ROVVR.Editor.RovReleaseBuilder.BuildV3` with `-batchmode -quit -projectPath`
pointing to `UnityProject`. The build uses Mono, Direct3D 11, version 3.0.0 and
only the saved main scene. Check `Builds/v3/unity-build-report.json`: success
requires both a successful build result and zero errors.

The Unity process must inherit the complete GStreamer MSVC x64 runtime before
starting, **including for batch builds**. For example, in the launching
PowerShell process:

```powershell
$v3GStreamerRoot = 'C:\path\to\gstreamer\1.0\msvc_x86_64'
$env:PATH = "$v3GStreamerRoot\bin;$env:PATH"
$env:GST_PLUGIN_SYSTEM_PATH_1_0 = "$v3GStreamerRoot\lib\gstreamer-1.0"
$env:GST_PLUGIN_PATH_1_0 = $env:GST_PLUGIN_SYSTEM_PATH_1_0
$env:GST_PLUGIN_SCANNER_1_0 = "$v3GStreamerRoot\libexec\gstreamer-1.0\gst-plugin-scanner.exe"
```

These are process-local settings. A build started without them produced native
plugin-load errors even though Unity's summary said `Succeeded`; the stricter
zero-error gate correctly rejected it. Use the JSON report, not that summary
alone. After committing the reviewed source, package with:

```powershell
.\scripts\Package-V3.ps1 -GStreamerRoot $v3GStreamerRoot
```

`Releases/v3` receives Windows/project ZIPs and `SHA256SUMS.txt`. Packaging
requires a clean working tree, successful zero-error v3 build, complete runtime
plugins and actual LFS binaries. Older v1/v2 packages remain unchanged.

## Legacy Windows v2 build target

1. Åpne hovedscenen i Unity 6000.1.7f1 og stopp Play.
2. Oppdater eventuelt `Assets/StreamingAssets/rov-video-view.defaults.json`.
3. Velg **ROV → Release → Build Windows v2**.

Rebuilding the v2 target from current source runs 271 shared checks and builds
the saved main scene to `Builds/v2/Unity-vr-pipeline-v2.exe`. Den bruker Windows x64,
Mono, Direct3D 11, ordinær build uten development-flagg og versjon 2.0.0.
Alle scener beholdes i kildeprosjektet; kun hovedscenen inngår i spilleren.
Resultatet skrives til `Builds/v2/unity-build-report.json` og midlertidig til
`UnityProject/Temp/rov-release-result.json` mens Editor er åpen.

Windows Build Support må være installert i Unity Hub. Native GStreamer-DLL
følger Unity-builden, men runtime distribueres ved pakkingen under.

## Bygg native Windows-plugin på nytt

Den vedlagte DLL-en er en Release/x64-build. Den gamle upstream-debug-DLL-en
krever Visual Studio-debug-runtime og skal ikke brukes i en distribuert build.
Med Visual Studio 2022 C++-verktøy, Windows SDK, CMake 3.29+ og GStreamer
MSVC x64-utviklingsfiler (inkludert pkg-config i PATH):

```powershell
$env:PKG_CONFIG_PATH = 'C:\path\to\gstreamer\1.0\msvc_x86_64\lib\pkgconfig'
cmake -S . -B Builds/native-release -G 'Visual Studio 17 2022' -A x64
cmake --build Builds/native-release --config Release
```

Kopier DLL/lib/exp fra `Builds/native-release/Release` til
`UnityProject/Assets/Plugins/GstUnityPlugin/Windows` med Play stoppet.
Pluginimporten må ha Windows x86_64 aktivert; buildverktøyet sjekker dette.

## Legacy v2 packaging

Fra repoets toppmappe etter en vellykket Unity-build:

```powershell
.\scripts\Package-V2.ps1 -GStreamerRoot 'C:\path\to\gstreamer\1.0\msvc_x86_64'
```

Skriptet kopierer runtime-DLL-er, plugins, scanner, lisenser og
versjonsinformasjon. Utviklingsarkiver (`.a`) følger ikke med.
Startfilen setter PATH/pluginbaner bare for programmet.
Runtimeversjonen brukt for første v1 er 1.28.7.

`Releases/v2` får en Windows-ZIP, en prosjekt-ZIP og `SHA256SUMS.txt`.
Prosjekt-ZIP-en bruker arbeidskopiens faktiske filer, også LFS-binærer.
Den inkluderer Assets, Packages, ProjectSettings, native kildekode og docs;
cacher og build-output tas ikke med. Commit endringene før den endelige
pakkingen slik at kildepakken matcher Git-taggen. `git archive` alene kan
gi LFS-pekere i stedet for de store binærfilene.

## Publish v3

From the repository root, review and stage only the intended release changes.
Build v3 successfully first, then commit before packaging: `Package-V3.ps1`
requires a clean working tree and records that source commit in the package.
Set `$v3GStreamerRoot` to the complete runtime as shown above. Confirm `origin`
points to `https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline.git`.

```powershell
git lfs install
git diff --cached --stat
git commit -m 'Prepare Unity ROV VR v3 release'
.\scripts\Package-V3.ps1 -GStreamerRoot $v3GStreamerRoot
git push origin HEAD:main
git tag v3
git push origin v3
gh release create v3 --repo Sub-Horizon-NTNU/Unity-vr-pipeline `
  --title v3 --notes-file docs/RELEASE_v3.md `
  Releases/v3/Unity-vr-pipeline-v3-Windows-x64.zip `
  Releases/v3/Unity-vr-pipeline-v3-project.zip `
  Releases/v3/SHA256SUMS.txt
```

Check that v3 is a new tag before running the publication commands. Do not
overwrite an existing release tag or force-push shared history; preserve v1/v2.

