# Sub Horizon — Unity VR Pipeline

ROV-video i Quest 2 via en Windows-PC med Quest Link / Air Link. Prosjektet dekoder H.264 med GStreamer, viser video på en justerbar buet flate og styrer kameratilt fra hodebevegelse via BlueOS/MAVLink. Et flytende pilot-HUD viser telemetri, med en kompassring nederst.

Basert på [sealfoss/GstUnityPlugin](https://github.com/sealfoss/GstUnityPlugin). Hele Unity-prosjektet, egne ROV-komponenter og den native pluginens kildekode er med. Original lisens og kreditering er bevart.

## Last ned v1

[Gå til v1-utgivelsen](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/releases/tag/v1).

| Fil | Bruk |
| --- | --- |
| `Unity-vr-pipeline-v1-Windows-x64.zip` | Ferdig Windows-program med GStreamer-runtime. Pakk ut hele mappen og kjør `Start-v1.cmd`. |
| `Unity-vr-pipeline-v1-project.zip` | Komplett kildeprosjekt med faktiske LFS-filer. Pakk ut og åpne `UnityProject` i Unity Hub. |
| `SHA256SUMS.txt` | SHA-256 for nedlastingene. |

Dette er en **Windows x64-build for Quest Link / Air Link**, ikke en APK for direkte installasjon på Quest. Meta Horizon Link og en fungerende VR-PC er fortsatt nødvendig. Ved manglende MSVC-runtime, installer [Microsoft Visual C++ x64 Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe).

Ved blokkert videostrøm: bruk [Setup-Firewall.ps1](scripts/Setup-Firewall.ps1) og følg [brannmuroppsettet](docs/SETUP.md#brannmur-for-videostrøm). Skriptet tillater UDP 5601 fra ROV-en for valgt EXE, og kan fjerne sin egen regel igjen. Brannmuren forblir på.

## Åpne prosjektet på en annen PC

Installer **Unity 6000.1.7f1** med **Windows Build Support**, Git og Git LFS.

```powershell
git lfs install
git clone https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline.git
cd Unity-vr-pipeline
git lfs pull
```

I Unity Hub: **Add → Add project from disk → velg `UnityProject`**. La Package Manager hente pakkene første gang. Åpne scenen:

```text
Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity
```

Start Quest Link / Air Link, gå tilbake til Unity-visningen i headsettet og trykk Play. Se rett frem og trykk **venstre X** for å sentrere. For kjøring i Editor trenger PC-en [GStreamer MSVC x64](https://gstreamer.freedesktop.org/download/) i PATH; releasepakken setter miljøet lokalt gjennom startfilen.

**Behold `.meta`-filene.** Assets, Packages og ProjectSettings ligger i Git. `Library`, `Temp`, `Logs`, IDE-filer og lokale builds er utelatt med vilje; Unity lager dem på nytt. Ikke kopier bare C#-filene til et tomt prosjekt. Prosjekt-ZIP-en i v1-utgivelsen er et alternativ til Git/LFS.

## Oppsett som v1 bruker

| Del | Verdi |
| --- | --- |
| Unity | 6000.1.7f1 |
| Plattform | Windows x64, Direct3D 11, Mono |
| Headsett | Quest 2 med Link / Air Link og OpenXR |
| Pakker | Meta XR 77.0.0, OpenXR 1.14.3, URP 17.1.0, Input System 1.14.0 |
| ROV | Raspberry Pi 4, Navigator, BlueOS, ArduSub 4.5.7 |
| MAVLink2REST | `http://192.168.2.2:6040` |
| Video inn | UDP 5601, RTP H.264 payload 96, 1920×1080 RGBA |
| Kameratilt | Mount Pitch på SERVO11, −35° til +30° |

IP, servoutgang og grenser beskriver den eksisterende ROV-en. Kontroller oppsettet før bruk med en annen ROV. Programmet sender kameratiltkommandoer; det armerer ikke ROV-en og styrer ikke thrustere.

```text
udpsrc port=5601 ! application/x-rtp,media=video,encoding-name=H264,clock-rate=90000,payload=96 ! rtph264depay ! h264parse ! avdec_h264 ! videoconvert ! video/x-raw,format=RGBA ! appsink name=videoSink
```

## Kontroller og innstillinger

| Kontroll | Handling |
| --- | --- |
| Høyre A | Hodefølging på/av. Av låser skjerm/HUD og holder siste kameratilt. |
| Høyre B | Skjul/vis HUD og kompass. |
| Venstre X | Sentrer bilde og hodenøytral. Lås opp med A først. |
| Venstre Y | Lagre innstillinger som JSON. |
| Venstre spak | Bildestørrelse sideveis, avstand opp/ned. |
| Venstre avtrekker + spak sideveis | Krumming. |
| Venstre grip + spak | Helning sideveis, vertikal plassering opp/ned. |
| F4 | Innstillingspanel på PC. |
| F5 / F2 | Lagre / last JSON. |
| R / P | Nullstill hodenøytral / separat servopause. I Editor må Game-visningen ha fokus. |

I Editor: **ROV → View → Manual calibration**. HUD følger videoflaten som standard; boksene kan flyttes hver for seg. Kompassringen har separat radius og høyde. Utjevningsslider på 0 slår av det aktuelle tidsfilteret; servoens hastighetsgrense gjelder fortsatt.

Lokal profil:

```text
%USERPROFILE%\AppData\LocalLow\infiniteMonkey\GstUnityQuest3\rov-video-view.json
```

En ny PC starter med `Assets/StreamingAssets/rov-video-view.defaults.json`. En eksisterende lokal profil beholdes. Y/Lagre JSON endrer din lokale fil, ikke teamets startprofil.

## Dokumentasjon

- [Oppsett og flytting til en ny PC](docs/SETUP.md)
- [Bygging, pakking og publisering](docs/BUILD.md)
- [Arkitektur og kildekodekart](docs/ARCHITECTURE.md)
- [Feilsøking og testbegrensninger](docs/TROUBLESHOOTING.md)
- [Detaljert bilde-/HUD-kalibrering](UnityProject/Assets/ROV/VIDEO_VIEW.md)
- [Kameratilt og MAVLink](UnityProject/Assets/ROV/README.md)
- [Endringslogg](CHANGELOG.md), [tredjepartsprogramvare](THIRD_PARTY_NOTICES.md)

v1 er første delbare prosjektpakke. Byggeren kjører 158 kodekontroller. Kodesjekker erstatter ikke Quest-test eller måling av fysisk kamerabevegelse. Videoen er monokulær, og PWM/mount-vinkel er rapportert mål fra autopiloten, ikke ekstern servovinkelmåling.

## Lisens

Prosjektet viderefører upstreams **CC BY-NC-SA 4.0**, se [LICENSE.md](LICENSE.md). Tredjepartspakker og runtimekomponenter har egne lisenser, se [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Upstreams README er bevart i [docs/UPSTREAM_GSTUNITYPLUGIN.md](docs/UPSTREAM_GSTUNITYPLUGIN.md).
