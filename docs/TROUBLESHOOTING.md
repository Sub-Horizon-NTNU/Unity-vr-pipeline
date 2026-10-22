# Feilsøking

| Symptom | Sjekk |
| --- | --- |
| Svart video | ROV-strøm til riktig PC-IP, UDP 5601, H.264/RTP payload96. Bare én app bør eie mottaksporten. |
| `DllNotFoundException: GstUnityPlugin` | Start release med `Start-v1.cmd`. I Editor må GStreamer MSVC x64 `bin` være i PATH før Unity starter. Sjekk VC++ x64-runtime. |
| Missing scripts/materialer etter flytting | Klon hele prosjektet med `.meta`, Packages og ProjectSettings. Bruk riktig Unity-versjon og `git lfs pull`. |
| Ingen VR-visning | Aktiver Quest Link/Air Link og Meta/OpenXR på PC-en. Bygget er ikke en Quest-APK. |
| «Venter på VR-fokus» | Gå tilbake til applikasjonen inne i Quest. En Quest-meny kan ta ekte VR-fokus selv om PC-en viser video. |
| «Venter på fersk ROV-IMU» | Pitch-kompensasjon krever fersk ATTITUDE, ikke bare video/heartbeat. |
| Ingen fysisk tilt | Bekreft fysisk bevegelse. ACK/PWM er ikke bevegelsesbevis. Test vanlig Cockpit-tilt, korrekt mount-utgang og strøm ved behov. |
| Servo står etter A | A er skjerm-/tiltlås. Trykk A igjen. B er bare HUD. P er separat servopause. |
| Ulik visning på ny PC | Eksisterende lokal JSON prioriteres over medfølgende defaults. Juster i F4 eller erstatt egen profil med programmet stoppet. |
| Feil kompass også i Cockpit | Kursen kommer fra autopilotens IMU. VR legger ikke på en skjult GPS-offset. |

Etter kodeinnlasting midt i Play kan OpenXR/GStreamer få ugyldig økt.
Stopp/start Play rent før videre testing. Bygging krever stoppet Play.

## Logger

Spillerlogg: `%USERPROFILE%\AppData\LocalLow\infiniteMonkey\GstUnityQuest3\Player.log`.
Editor-menyene under ROV kan kontrollere fokus, tracking, BlueOS og tilt.
Kodekontrollene bruker testobjekter uten nettverk.

## Begrensninger

Videoen er monokulær. Kamerafelt, skjermstørrelse, avstand, krumming og HUD
bør tilpasses piloten. Video og telemetri er ikke tidsjustert mot hverandre
i renderer. Mer utjevning gir mer etterslep; 0 slår av filteret, men ikke
servoens fartsgrense. Mount-status/PWM er ikke fysisk encoderfeedback.
Ukjent batteriprosent vises ukjent og AHRS2-dybde er merket estimert.

Kodesjekk og oppstartstest på utviklings-PC-en bekrefter ikke Quest-opplevelse
eller installasjon på en ren Windows-PC. Gjør en praktisk kontroll før bruk.
