# Oppsett på en ny PC

## Ferdig Windows-program

1. Installer Meta Horizon Link og aktiver Quest Link / Air Link.
2. Download the Windows ZIP from [v3](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/releases/tag/v3).
3. Pakk ut hele ZIP-en. Behold EXE, `_Data`, `UnityPlayer.dll`, `MonoBleedingEdge` og `GStreamer` sammen.
4. Koble PC-en til ROV-nettverket. Denne ROV-en bruker BlueOS på `192.168.2.2` og videomottak på PC-ens UDP-port 5601.
5. Run `Start-v3.cmd`. It sets PATH and plugin paths for this process; system PATH is unchanged.
6. Åpne programvisningen i Quest. Se rett frem og trykk venstre X.

Ferdigbuilden trenger ingen Unity Editor og inkluderer GStreamer-runtime.
Microsoft Visual C++ x64 Redistributable kan være nødvendig på en ny PC.
Quest/Link-driver og OpenXR-runtime må være satt opp på vertsmaskinen.
The application does not change network adapters or firewall automatically.
Unlike published v2, v3 gates and restores RC camera functions through persistent
autopilot parameter changes and a local recovery journal.
Les [VR-prioritet og RC-håndover](PILOT_TOOLS.md#vr-prioritet-og-rc-håndover-etter-v2).

## Room passthrough over Meta Horizon Link (after v2)

Use v3 or the current Unity project. On Quest 3, enable
developer access for the same Meta account used in the headset and PC app.
In the PC app's **Settings → Developer**, enable **Developer Runtime Features**
and **Passthrough over Meta Horizon Link**, and complete any permission or
camera-consent prompt yourself before launching. Restart Unity if the Editor
was already open when these features changed.
Follow [Meta's passthrough-over-Link setup](https://developers.meta.com/vr/documentation/unity/unity-passthrough-use-over-link/)
if the Developer section is missing.

The main scene already contains the passthrough background component. It
automatically places the physical room behind the Atlas video and HUD once
Meta reports a supported, initialized and resumed compositor layer. Unsupported
or failed sessions retain the normal opaque VR background. Putting the headset
back on may be needed before the layer resumes. Check the room in the headset;
a Unity mirror or recording does not establish whether passthrough is visible.

For source maintenance, **ROV → View → Enable room passthrough** configures
framebuffer alpha and postprocessing alpha for the Editor and Windows quality
pipelines. Restart Unity after configuration changes before testing Play.

## Brannmur for videostrøm

Last ned [Setup-Firewall.ps1](../scripts/Setup-Firewall.ps1) og legg den ved siden av
`Unity-vr-pipeline-v3.exe` i den utpakkede programmappen. Den opprinnelige v1-ZIP-en
inneholder ikke skriptet; senere pakking legger det automatisk ved.
Åpne **PowerShell som administrator**, gå til programmappen og kjør:

```powershell
.\Setup-Firewall.ps1 -WhatIf  # Vis hva som vil gjøres, uten endringer
.\Setup-Firewall.ps1          # Opprett eller oppdater regelen
```

Regelen tillater bare **innkommende UDP til lokal port 5601 fra 192.168.2.2**
for den valgte EXE-filen. Den gjelder alle nettverksprofiler, også når
ROV-nettverket er klassifisert som offentlig. Brannmuren forblir aktiv.
Kildeporten er fri fordi videosenderen kan velge en dynamisk port.
MAVLink2REST bruker utgående HTTP til ROV-ens TCP-port 6040;
Unity trenger ingen ekstra innkommende regel for denne forbindelsen.

Egen programbane, IP eller videoport:

```powershell
.\Setup-Firewall.ps1 -ProgramPath 'C:\ROV\Unity-vr-pipeline-v3.exe' -RovAddress 192.168.2.2 -VideoPort 5601
```

Fjern regelen med `.\Setup-Firewall.ps1 -Remove` fra samme mappe, eller oppgi
samme `-ProgramPath` som ved opprettelse. Kjør skriptet på nytt etter flytting
av programmet; fjern gjerne regelen for gammel programbane først.
For Unity Editor kan samme skript brukes med `-ProgramPath` satt til den
installerte `Unity.exe`, siden Editor er en egen prosess.

Hvis Windows blokkerer et nedlastet skript, kontroller innholdet og kjør
`Unblock-File .\Setup-Firewall.ps1`. Skriptet endrer ikke kjøringspolicyen.
En eksplisitt **Block**-regel for samme program har forrang fremfor en
Allow-regel og må gjennomgås i Windows Defender-brannmur med avansert sikkerhet.
Skriptet endrer ikke andre regler eller organisasjonens sikkerhetspolicy.
Se [Microsofts dokumentasjon om regelprioritet](https://learn.microsoft.com/en-us/windows/security/operating-system-security/network-security/windows-firewall/rules).

PC-en må fortsatt ha forbindelse til ROV-nettverket, og senderen må sende til
riktig PC-adresse og port. Dette skriptet setter ikke IP på nettverkskortet.

## Hele Unity-prosjektet

Bruk Git + Git LFS som i hoved-README, eller den vedlagte prosjekt-ZIP-en.
Den automatiske «Source code»-ZIP-en fra GitHub kan inneholde LFS-pekere;
bruk den eksplisitte `*-project.zip` for en selvstendig kopi.
`libgstreamer_android.so` er omtrent 159 MB og skal være en virkelig binærfil,
ikke en liten tekstfil som begynner med `version https://git-lfs.github.com`.

Åpne `UnityProject`, ikke repoets toppmappe, i Unity 6000.1.7f1.
`Packages/manifest.json` og `packages-lock.json` låser pakkeversjonene;
Package Manager laster dem ned. `Library/PackageCache` skal ikke lastes opp.
Det kan ta tid å importere prosjektet første gang.

Installer GStreamer **MSVC x64** på Editor-PC-en og ha installasjonens `bin`
i prosessens PATH før Unity starter. Runtimeversjonen på utviklingsmaskinen
for v1 er **1.28.7**. Installer de nødvendige runtimepluginene og start
Unity Hub på nytt etter en endring i PATH.

The main scene is `Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity`.
Other scenes and backup scenes remain in the source project; only the main
scene is included in the Windows v3 player.

## Kalibrering og profiler

`Assets/StreamingAssets/rov-video-view.defaults.json` er et utgangspunkt fra
utviklingsoppsettet, ikke en universell ergonomisk kalibrering. På første
oppstart brukes verdiene til den lokale profilen. En eksisterende profil
prioriteres. Hver bruker kan justere bilde og HUD uten å endre teamets profil.

To share a new team starting point, save with Y/F5, back up both the local JSON
and the existing bundled defaults, and test the saved settings in Quest before
copying the JSON to `rov-video-view.defaults.json`. Review the changed values
before committing. Existing local profiles still take precedence; updating the
bundle does not reset another pilot's calibration. Do not commit local logs or
`AppData`. Company/Product remains `infiniteMonkey/GstUnityQuest3` to preserve
existing calibration. See [automatic startup and settings](PILOT_TOOLS.md#automatic-startup-and-control-help-after-v2).

V3 updates the shared HUD layout and pilot UI defaults: main zoom 1×, detail
window off, centered zoom target and standard right-stick/F3 menu binding.
Existing bundled video geometry, compass geometry, 0.9 pitch compensation and
45°/s camera target limit are retained. This is a shared HUD/UI preset, not a
copy of every personal calibration value. Existing user profiles are untouched.

## Bruk med en annen ROV

BlueOS-adressen ligger på `RovHeadTiltController` på hovedkameraet. Videoport
og pipeline ligger på streamkomponenten i hovedscenen. Mount-protokollen
bruker target system 1 / component 1 og source 245 / 191. Denne ROV-ens
SERVO11 er konfigurert for Mount Pitch. Programmet skriver ingen SERVO- eller
MNT-parametere; tilkobling og mekaniske grenser må samsvare med fartøyet.

Kursen kommer fra autopilotens ATTITUDE/IMU. Oppsettet hadde ikke GPS-fiks
under utviklingen. Dybde er merket EST. og kommer fra AHRS2. Ukjent
batteriprosent vises ukjent selv når spenningen er tilgjengelig.

## Hvorfor disse mappene er med

| Mappe | Innhold |
| --- | --- |
| Assets + alle `.meta` | Scener, prefabs, materialer, scripts, shaders, fonter og native plugins med stabile GUID-er. |
| Packages | Pakkehenvisninger og låste versjoner, inkludert Meta/OpenXR. |
| ProjectSettings | XR, input, spiller-, grafikk- og byggeinnstillinger. |
| Library / Temp / Logs | Generert per maskin og derfor ignorert. |

Referanser: [Unity og eksterne versjonskontrollsystemer](https://docs.unity.cn/2021.1/Documentation/Manual/ExternalVersionControlSystemSupport.html),
[Unity-metadata](https://docs.unity.com/en-us/engine/6000.7/manual/assets-and-media/import-assets/asset-metadata),
[GStreamer på Windows](https://gstreamer.freedesktop.org/documentation/installing/on-windows.html).

