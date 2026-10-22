# Arkitektur

```text
ROV-kamera → RTP/H.264 UDP:5601 → GStreamer → RGBA Texture2D → buet videoflate
Quest hodepose → tiltberegning + ROV-pitch → HTTP/MAVLink2REST → mount pitch
BlueOS heartbeat/IMU/mount/batteri/AHRS2 → delt polling → visning og HUD
```

## Unity-kode

| Fil/område | Ansvar |
| --- | --- |
| `Assets/GstUnityPlugin/Scripts/GstUnityController.cs` | Native plugin, strømmer og teksturoppdatering. |
| `Assets/GstUnityPlugin/Scripts/CurvedVideoScreen.cs` | Mesh og UV-er for videoflaten. |
| `Assets/ROV/Scripts/RovHeadTiltController.cs` | Sporing, fokus, kalibrering, tiltgrenser, rate og MAVLink-sending. |
| `RovMountProtocol.cs` | MAVLink2REST JSON og heartbeat-ferskhet. |
| `RovCameraPoseMath.cs` | NED/FRD-konvertering og pitch-kompensasjon. |
| `RovVideoViewCalibration.cs` | Skjermpose, knapper, kalibrering og innstillingspanel. |
| `RovVideoViewProfile.cs` | JSON-validering og atomisk lagring med `.bak`. |
| `RovVideoProjection.cs` | Bildegeometri, avstand og uavhengig krumming. |
| `RovHudTelemetry.cs` | Sensorverdier, sentinelverdier og separat ferskhet. |
| `RovVrOverlay.cs`, `RovHudArt.cs`, `RovHudLayout.cs` | HUD-kort, grafikk, plassering og forankring. |
| `RovVrCompass.cs` | Horisontal kompassring, uavhengig av videoflatens tilt. |
| `Assets/ROV/Editor` | Innstillingsvindu, tester, diagnostikk og v1-bygging. |
| `Assets/ROV/Resources` | Stereo-HUD-shader og Liberation Sans med lisens. |
| `Assets/StreamingAssets` | Delte standardverdier for en ny installasjon. |

Skjermens sideretning settes med X. ROV-yaw dreier ikke selve videoflaten;
bildemotivet endres når fartøyet svinger. Pitch/roll og kameratilt kan følge
telemetrien. HUD følger videoflaten som standard og oppdateres etter
videoposen i samme frame. Kompasset bruker autopilotens yaw.

A fryser skjerm/HUD i rommet og holder siste body-tiltmål. B skjuler HUD.
Live innstillinger slipper ikke servokontrollen eller nullstiller hodenøytral.
Bare faktisk geometriendring bygger nytt mesh.

Manglende tracking, VR-fokus eller fersk nødvendig telemetri stopper styringen.
Native Meta-fokus brukes for Quest Link, uavhengig av desktop-fokus.
Manglende ACK får nytt forsøk med 1–8 s backoff. DENIED/UNSUPPORTED/FAILED
blir stående som feil. P er separat pause som frigir mount til RC.
Det sendes ingen thruster-, armerings- eller parameterendringskommandoer.

## Native kode

`source`, `include` og `CMakeLists.txt` er Windows-pluginens kildekode.
`Android` inneholder upstreams Android/NDK-oppsett. Ferdige `.dll`/`.so`
ligger under `Assets/Plugins/GstUnityPlugin`. Android er beholdt i prosjektet,
men v1-pakken er en Windows x64-utgivelse.
