# Bygging og utgivelse

## Bygg Windows v2

1. Åpne hovedscenen i Unity 6000.1.7f1 og stopp Play.
2. Oppdater eventuelt `Assets/StreamingAssets/rov-video-view.defaults.json`.
3. Velg **ROV → Release → Build Windows v2**.

Byggeren kjører prosjektets 222 kodekontroller og bygger den lagrede
hovedscenen til `Builds/v2/Unity-vr-pipeline-v2.exe`. Den bruker Windows x64,
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

## Pakk build og komplett kildeprosjekt

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

## Publiser

```powershell
git lfs install
git add .
git commit -m 'Prepare Unity ROV VR v2 release'
git push origin HEAD:main
git tag v2
git push origin v2
gh release create v2 --repo Sub-Horizon-NTNU/Unity-vr-pipeline `
  --title v2 --notes-file docs/RELEASE_v2.md `
  Releases/v2/Unity-vr-pipeline-v2-Windows-x64.zip `
  Releases/v2/Unity-vr-pipeline-v2-project.zip `
  Releases/v2/SHA256SUMS.txt
```

`origin` skal peke på https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline.git.
Ikke force-push over andres arbeid eller gjenbruk v1-taggen for en ny build.

