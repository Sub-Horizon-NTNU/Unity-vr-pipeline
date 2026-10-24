# v2 — Sub Horizon ROV VR

Windows x64 for Quest 2 med Link / Air Link. Unity-versjon 2.0.0.

Nytt siden v1:

- Digital zoom, valg av punkt på videobildet og et separat zoomvindu.
- VR-meny for video, HUD, layout, tilt, profiler, kontrollbindinger og opptak.
- Flytt meny/zoomvindu med venstre avtrekker + venstre spak. Grip + venstre spak endrer avstand; høyre spak justerer pitch/yaw. Tydelig peker og ramme viser valgt vindu.
- Egne menypitch-/yaw-slidere, justerbare pekere og valgfrie kontrollermodeller.
- Navngitte JSON-profiler og ombinding av pilotknapper/taster.
- MP4-opptak av ren kameravideo eller PC-speilbildet med HUD. 720p eller 1080p, 30 fps, uten lyd. Videoer lagres i Videoer/SubHorizon ROV.
- Medfølgende brannmurskript for UDP-video fra ROV-IP-en til denne EXE-en.

## Nedlasting og oppstart

- **Unity-vr-pipeline-v2-Windows-x64.zip**: pakk ut hele mappen og kjør **Start-v2.cmd**. GStreamer-runtime, opptaksenkoder, dokumentasjon og lisenser er inkludert.
- **Unity-vr-pipeline-v2-project.zip**: komplett Unity-prosjekt med faktiske LFS-filer. Åpne UnityProject i Unity **6000.1.7f1**.
- **SHA256SUMS.txt**: kontrollsummer for ZIP-filene.

Meta Horizon Link og eventuelt Microsoft VC++ x64-runtime må være installert. Dette er et Windows-program, ikke en Quest-APK.

Høyre spakklikk / F3 åpner VR-menyen. A låser/følger, B viser/skjuler HUD, X sentrerer og Y lagrer. F4 åpner PC-innstillingene. Eksisterende lokale innstillinger beholdes; lagre gjerne en navngitt profil før dere eksperimenterer.

Hvis video blokkeres: åpne PowerShell som administrator i programmappen og kjør `./Setup-Firewall.ps1 -WhatIf` for forhåndsvisning, deretter `./Setup-Firewall.ps1` for å opprette regelen. Brannmuren forblir aktiv.

ROV-oppsett: BlueOS 192.168.2.2, MAVLink2REST TCP 6040 og RTP H.264-video inn på UDP 5601. Standardoppsettet må tilpasses ved bruk med en annen ROV.

Byggeren kjører 222 kodekontroller. Begge opptaksveiene er tidligere testet i Editor; headsetkomfort, kontrollerfølelse, fysisk kamerafølging og opptaksytelse må fortsatt prøves på mottakerens PC/ROV. En oppstartskontroll av v2 bekrefter ikke en full piloteringstest.

v1-utgivelsen beholdes som tidligere referanse. Full bruksveiledning finnes i LES-MEG.md og PILOT_TOOLS.md i Windows-pakken, og i repoets docs-mappe.
