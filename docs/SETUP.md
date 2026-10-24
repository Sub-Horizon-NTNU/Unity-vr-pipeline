# Oppsett på en ny PC

## Ferdig Windows-program

1. Installer Meta Horizon Link og aktiver Quest Link / Air Link.
2. Last ned Windows-ZIP-en fra [v2](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/releases/tag/v2).
3. Pakk ut hele ZIP-en. Behold EXE, `_Data`, `UnityPlayer.dll`, `MonoBleedingEdge` og `GStreamer` sammen.
4. Koble PC-en til ROV-nettverket. Denne ROV-en bruker BlueOS på `192.168.2.2` og videomottak på PC-ens UDP-port 5601.
5. Start `Start-v2.cmd`. Den setter PATH og pluginbaner bare for programmet; systemets PATH endres ikke.
6. Åpne programvisningen i Quest. Se rett frem og trykk venstre X.

Ferdigbuilden trenger ingen Unity Editor og inkluderer GStreamer-runtime.
Microsoft Visual C++ x64 Redistributable kan være nødvendig på en ny PC.
Quest/Link-driver og OpenXR-runtime må være satt opp på vertsmaskinen.
Programmet endrer ikke nettverkskort, brannmur eller autopilotparametere.

## Brannmur for videostrøm

Last ned [Setup-Firewall.ps1](../scripts/Setup-Firewall.ps1) og legg den ved siden av
`Unity-vr-pipeline-v2.exe` i den utpakkede programmappen. Den opprinnelige v1-ZIP-en
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
.\Setup-Firewall.ps1 -ProgramPath 'C:\ROV\Unity-vr-pipeline-v2.exe' -RovAddress 192.168.2.2 -VideoPort 5601
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

Hovedscenen er `Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity`.
Alle øvrige scener og sikkerhetskopiscener følger kildeprosjektet, men bare
hovedscenen legges i Windows v2-spilleren.

## Kalibrering og profiler

`Assets/StreamingAssets/rov-video-view.defaults.json` er et utgangspunkt fra
utviklingsoppsettet, ikke en universell ergonomisk kalibrering. På første
oppstart brukes verdiene til den lokale profilen. En eksisterende profil
prioriteres. Hver bruker kan justere bilde og HUD uten å endre teamets profil.

For å dele et nytt teamutgangspunkt: lagre med Y, kopier den lokale JSON-filen
over `rov-video-view.defaults.json`, og commit endringen. Ikke commit lokale
logger eller `AppData`. Company/Product beholdes som
`infiniteMonkey/GstUnityQuest3` for å beholde eksisterende kalibrering.

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

