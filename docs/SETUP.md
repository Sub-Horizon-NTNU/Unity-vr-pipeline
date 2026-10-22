# Oppsett på en ny PC

## Ferdig Windows-program

1. Installer Meta Horizon Link og aktiver Quest Link / Air Link.
2. Last ned Windows-ZIP-en fra [v1](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/releases/tag/v1).
3. Pakk ut hele ZIP-en. Behold EXE, `_Data`, `UnityPlayer.dll`, `MonoBleedingEdge` og `GStreamer` sammen.
4. Koble PC-en til ROV-nettverket. Denne ROV-en bruker BlueOS på `192.168.2.2` og videomottak på PC-ens UDP-port 5601.
5. Start `Start-v1.cmd`. Den setter PATH og pluginbaner bare for programmet; systemets PATH endres ikke.
6. Åpne programvisningen i Quest. Se rett frem og trykk venstre X.

Ferdigbuilden trenger ingen Unity Editor og inkluderer GStreamer-runtime.
Microsoft Visual C++ x64 Redistributable kan være nødvendig på en ny PC.
Quest/Link-driver og OpenXR-runtime må være satt opp på vertsmaskinen.
Programmet endrer ikke nettverkskort, brannmur eller autopilotparametere.

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
hovedscenen legges i Windows v1-spilleren.

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
