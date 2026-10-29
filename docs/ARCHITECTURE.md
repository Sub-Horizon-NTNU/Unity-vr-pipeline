# Arkitektur

```text
ROV-kamera → RTP/H.264 UDP:5601 → GStreamer → RGBA Texture2D → buet videoflate
Quest hodepose → tiltberegning + ROV-pitch → HTTP/MAVLink2REST → mount pitch
BlueOS heartbeat/IMU/mount/batteri/AHRS2 → delt polling → visning og HUD
BlueOS NAMED_VALUE_FLOAT → receive-only WebSocket → gain / commanded lights
Quest room cameras → Meta passthrough underlay → transparent VR background
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
| `RovHudTelemetry.cs`, `RovPilotTelemetryStream.cs` | Independently expiring sensor values; receive-only named gain/light telemetry. |
| `RovVrOverlay.cs`, `RovFpvAttitudeGraphic.cs`, `RovHudLayout.cs` | Green FPV instruments, projected horizon/reticle, edge placement and anchoring. |
| `RovPilotOnboarding.cs`, `RovPilotMenu.cs` | Automatic readiness gate, quick settings and advanced catalog. |
| `RovPassthroughBackground.cs` | Meta room underlay lifecycle and opaque-background fallback. |
| `RovVrCompass.cs` | Horisontal kompassring, uavhengig av videoflatens tilt. |
| `Assets/ROV/Editor` | Settings, checks, diagnostics and versioned Windows release builds. |
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
Det sendes ingen thruster- eller armeringskommandoer. Etter-v2-styringen bruker
PARAM_REQUEST_READ/PARAM_SET bare for å sperre og gjenopprette RC-kamerafunksjoner
(RCn_OPTION 212/213/214). Originalverdier journalføres før endring og verifiseres
ved tilbakeføring. Se VR-prioritet i PILOT_TOOLS.md.

## Pilot display contracts

Automatic startup loads the local profile or, on first use, bundled defaults.
It clears only `StartupSelectionPending` once XR, headset readiness, native VR
focus/tracking and required live ROV data are available. It preserves explicit
RC selection and the controller's calibration, acknowledgement and recovery
gates. Settings stay optional and a deliberately opened panel remains open.

At 1×, the estimated horizon and at most two nearby pitch marks use camera-local
gravity with a pinhole ray model, projected onto the same curved video surface.
The model uses the configured **display** FOV, not measured camera intrinsics.
At the tested geometry the horizon spans about 30° and the short references
about 12°. A 0.62 decoration-opacity multiplier and fading reference marks reduce
clutter; critical text and compass opacity are unchanged. A one-degree mark
selection margin prevents equally near references flickering without delaying
pose updates. The horizon may move out of view; it is not clamped to a fake
level reading. Stale heartbeat/IMU/mount data hides the horizon cues while the
fixed reticle remains.

`TryGetHudCameraRotation` reads the existing filtered physical-camera estimate
before presentation trims: `inverse(rovAlignment) * lastPose` while following
the IMU-anchored video. Held/manual presentation uses the fresh raw camera
estimate because holding the screen does not freeze the decoded live video.
No additional headset, camera-control or actuator filter is introduced.

For main zoom outside 1× (with a 0.001 floating-point tolerance), horizon bars
and labels are suppressed and `HORIZON AT 1× ONLY` is shown. Reticle, critical
telemetry and the independent compass circle remain. Returning to 1× restores
the horizon when pose data is fresh. Optical crop/zoom registration is not
implemented, so the estimate is intentionally limited to the uncropped view.

The connected ArduSub 4.5.7 (`b09fafe2`) bench stream supplied ATTITUDE/mount
messages at about 16.6 Hz; the existing GET plus 0.1 s wait limits cached app
polling to at most about 10 Hz. Attitude/mount data expires after 0.75 s. The
firmware's [PWM-mount implementation](https://github.com/ArduPilot/ardupilot/blob/b09fafe2/libraries/AP_Mount/AP_Mount_Servo.cpp#L113-L123)
reports the latest angle targets because it has no feedback. Physical mount
delay and video/telemetry timing are not synchronized or measured by this HUD.

Battery, arm state, pilot gain and commanded light levels remain present while
the HUD is enabled. `PilotGain`, `Lights1` and `Lights2` are normalized 0–1 values
from system 1/component 1 `NAMED_VALUE_FLOAT` frames. Each expires three seconds
after receipt; a different field or heartbeat cannot refresh it. Zero means a
valid zero/OFF command, while unavailable values remain unknown. Light output
commands are not measured lamp brightness.

The receive-only endpoint is `/v1/ws/mavlink?filter=^NAMED_VALUE_FLOAT$`. The
REST cache retains only the latest named value, so polling it loses fields
published together. `RovPilotTelemetryStream` bounds frames and queued work,
retains monotonic receipt age, parses on Unity's main thread and disposes its
socket on disable. The existing Newtonsoft package preserves null-padded
MAVLink names; Unity `JsonUtility` truncated U+0000 during the actual Editor
check. See [ArduSub's published fields](https://github.com/ArduPilot/ardupilot/blob/master/ArduSub/GCS_MAVLink_Sub.cpp#L162-L170)
and [mavlink2rest WebSockets](https://github.com/mavlink/mavlink2rest#websocket).

Passthrough retains the existing XR rig. It owns or borrows an `OVRManager`
and creates an underlay, enabling transparent camera clear only after a
resumed, initialized layer. Disable removes listeners, destroys owned objects,
and restores borrowed manager and camera state. Both relevant URP quality
assets preserve postprocessing alpha. User setup is in [SETUP](SETUP.md#room-passthrough-over-meta-horizon-link-after-v2).

## V3 verification (29 October 2026)

Unity 6000.1.7f1 compiled the compact Editor candidate; Quest 3/Link showed the
rendered horizon and the pilot accepted the smaller presentation. Passthrough
reported a resumed layer and transparent background, and the pilot confirmed
the room was visible. Automatic startup reached a fresh camera-command ACK.
The final v3 batch build passed **372 checks**: 271 existing view/control,
51 startup/menu, 23 named-telemetry and 27 FPV checks. Its JSON report records
`success: true`, `Succeeded` and zero build errors in Unity 6000.1.7f1. The
standalone executable has not had a full VR session test. The HUD regression
uses the production graphic factory and actual
`CanvasRenderer` mesh, covering the missing-renderer defect found during
integration. These are bench/session checks, not proof of physical servo
motion, operational vehicle performance or another pilot's readability.
Pointer-recovery fixtures use an explicit positive test clock so a fresh batch
process cannot confuse negative elapsed time with the unset-time sentinel.

Useful Editor requests under `UnityProject/Temp`: `rov-fpv-hud.request` accepts
`verify` when stopped and `runtime`/`snapshot` in Play; the latter writes
`fpv-hud-preview.png`. `rov-pilot-menu-flow.request` and
`rov-pilot-telemetry.request` run the separate no-network checks. Results use
the corresponding `*-result.json` names. `rov-passthrough.request` with
`runtime` reports capability, layer resume and transparency. Stop Play before
source changes or profile migration. Rendered pixels and headset observation
remain necessary; the stereo viewport diagnostic's depth sign was inconclusive.

## Native kode

`source`, `include` og `CMakeLists.txt` er Windows-pluginens kildekode.
`Android` inneholder upstreams Android/NDK-oppsett. Ferdige `.dll`/`.so`
ligger under `Assets/Plugins/GstUnityPlugin`. Android er beholdt i prosjektet,
men v1-pakken er en Windows x64-utgivelse.
