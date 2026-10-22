# Bygging og utgivelse

## Bygg Windows v1

1. Åpne hovedscenen i Unity 6000.1.7f1 og stopp Play.
2. Oppdater eventuelt `Assets/StreamingAssets/rov-video-view.defaults.json`.
3. Velg **ROV → Release → Build Windows v1**.

Byggeren lagrer åpne scener, kjører prosjektets 158 kodekontroller og bygger
hovedscenen til `Builds/v1/Unity-vr-pipeline-v1.exe`. Den bruker Windows x64,
Mono, Direct3D 11, ordinær build uten development-flagg og versjon 1.0.0.
Alle scener beholdes i kildeprosjektet; kun hovedscenen inngår i spilleren.
Resultatet skrives til `UnityProject/Temp/rov-release-result.json`.

Windows Build Support må være installert i Unity Hub. Native GStreamer-DLL
følger Unity-builden, men runtime distribueres ved pakkingen under.

## Pakk build og komplett kildeprosjekt

Fra repoets toppmappe etter en vellykket Unity-build:

```powershell
.\scripts\Package-V1.ps1 -GStreamerRoot 'C:\path\to\gstreamer\1.0\msvc_x86_64'
```

Skriptet kopierer runtime-DLL-er, plugins, scanner, lisenser og
versjonsinformasjon. Utviklingsarkiver (`.a`) følger ikke med.
Startfilen setter PATH/pluginbaner bare for programmet.
Runtimeversjonen brukt for første v1 er 1.28.7.

`Releases/v1` får en Windows-ZIP, en prosjekt-ZIP og `SHA256SUMS.txt`.
Prosjekt-ZIP-en bruker arbeidskopiens faktiske filer, også LFS-binærer.
Den inkluderer Assets, Packages, ProjectSettings, native kildekode og docs;
cacher og build-output tas ikke med. Commit endringene før den endelige
pakkingen slik at kildepakken matcher Git-taggen. `git archive` alene kan
gi LFS-pekere i stedet for de store binærfilene.

## Publiser

```powershell
git lfs install
git add .
git commit -m 'Prepare Unity ROV VR v1 release'
git push subhorizon HEAD:main
git tag v1
git push subhorizon v1
gh release create v1 --repo Sub-Horizon-NTNU/Unity-vr-pipeline `
  --title v1 --notes-file docs/RELEASE_v1.md `
  Releases/v1/Unity-vr-pipeline-v1-Windows-x64.zip `
  Releases/v1/Unity-vr-pipeline-v1-project.zip `
  Releases/v1/SHA256SUMS.txt
```

`origin` kan fortsatt peke på upstream. `subhorizon` skal peke på teamrepoet.
Ikke force-push over andres arbeid eller gjenbruk v1-taggen for en ny build.
