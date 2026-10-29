# v3 — Atlas Quest pilot view

Windows x64 for Quest 3 through Meta Horizon Link / Air Link, using Unity
6000.1.7f1, Direct3D 11 and Mono. This is a PC application, not a Quest APK.

## Changes since v2

- Room passthrough behind the floating Atlas video/HUD, with an opaque VR
  fallback when Meta passthrough is unavailable.
- Compact green estimated horizon, short pitch references and an open reticle.
  The existing compass circle is retained. Horizon lines and labels are shown
  at main-video zoom **1× only**; zoom retains the reticle and pilot information, and
  returning to 1× restores the horizon when pose data is fresh.
- Always-present battery, arm state, live vehicle pilot gain and commanded
  L1/L2 light levels while the HUD is enabled. Missing values remain unknown;
  light commands are not measured lamp brightness.
- Automatic startup with the saved setup after headset, VR focus/tracking and
  required ROV data are ready. One right-stick click/F3 opens quick settings;
  advanced pages, profiles and control bindings remain available.
- Updated shared HUD/UI defaults with main zoom 1×, detail window off and
  standard bindings. Existing bundled vehicle/view calibration and personal
  local profiles are preserved.
- Camera RC-input gating and journaled recovery, explicit RC/VR handover,
  improved interruption/ACK recovery, and menu/pointer recovery developed after
  v2. No arming or thruster commands are added. See the
  [camera ownership rules](PILOT_TOOLS.md#vr-prioritet-og-rc-håndover-etter-v2).

## Download and start

- **Unity-vr-pipeline-v3-Windows-x64.zip:** extract the whole folder and run
  **Start-v3.cmd**. Keep the executable, data, Mono and GStreamer folders together.
- **Unity-vr-pipeline-v3-project.zip:** complete Unity source with actual LFS
  assets; open **UnityProject** with Unity **6000.1.7f1**.
- **SHA256SUMS.txt:** SHA-256 checksums for the ZIP files.

Install Meta Horizon Link and, if needed, Microsoft Visual C++ x64 Runtime.
Connect the PC to the ROV network and enter Link in the headset. Look forward
and press left **X** to recenter. A toggles follow/hold, B toggles the HUD,
Y/F5 saves the local startup setup, and right-stick click/F3 opens settings.
Existing local startup JSON takes precedence over bundled defaults; named
profiles are preserved.

Passthrough requires developer access for the same Meta account on PC and
Quest, **Developer Runtime Features**, **Passthrough over Meta Horizon Link**
and any required consent before launching. Restart Unity if it was already open.
Follow [setup](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/blob/v3/docs/SETUP.md#room-passthrough-over-meta-horizon-link-after-v2)
or the included **LES-MEG.md**.

## Validation and limits

The compact 1× horizon and room passthrough were exercised in Unity Editor on
Quest 3/Link, and the pilot confirmed the smaller presentation was better.
Automatic startup, live gain/light reads and existing control recovery were
checked. The final Windows v3 batch build succeeded with **372 checks and zero
build errors**: 271 existing view/control, 51 startup/menu, 23 named-telemetry
and 27 FPV checks, including the compact projection and 1× zoom guard.
The new standalone executable has not had a full VR session test.

The horizon uses IMU plus reported mount targets and a display-FOV-based
projection estimate. ArduSub's PWM mount does not measure actual camera angle;
video and telemetry are not synchronized. These bench checks do not establish
physical servo motion, full vehicle-operation validation, or performance on
another PC/ROV. See [display contracts](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/blob/v3/docs/ARCHITECTURE.md#pilot-display-contracts).

Earlier [v2](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/releases/tag/v2)
and [v1](https://github.com/Sub-Horizon-NTNU/Unity-vr-pipeline/releases/tag/v1)
remain available.
