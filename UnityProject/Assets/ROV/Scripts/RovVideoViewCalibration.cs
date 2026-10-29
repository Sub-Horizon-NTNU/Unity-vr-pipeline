using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ROVVR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CurvedVideoScreen))]
    [DefaultExecutionOrder(1000)]
    public sealed partial class RovVideoViewCalibration : MonoBehaviour
    {
        public Transform eye;
        public RovHeadTiltController tilt;
        [Header("Video and viewing geometry")]
        [Range(50f, 150f)] public float horizontalFov = 110f;
        [Range(0f, 150f)] public float curveDegrees = 45f;
        [Range(1f, 4f)] public float distance = 2f;
        [Tooltip("Vertical placement relative to eye level in meters; positive moves the screen up.")]
        [Range(-2f, 2f)] public float verticalPositionMeters;
        public bool cylindricalSurface = true;
        public bool showSettings;
        [Header("ROV pose (Navigator unstabilized tilt mount)")]
        [Tooltip("Follow ROV pitch/roll and mount tilt; screen heading stays fixed when the ROV turns left/right.")]
        public bool anchorToRovImu = true;
        [Tooltip("A toggles head-following. Off holds camera tilt and locks the screen's world position and rotation.")]
        public bool headFollowingEnabled = true;
        [Range(0f, 0.2f)] public float telemetrySmoothingSeconds = 0.06f;
        [Header("Floating pilot HUD")]
        public bool hudEnabled = true, hudShowAttitude = true, hudShowBattery = true, hudShowDepth = true;
        public bool hudShowDiagnostics;
        public RovHudAnchorMode hudAnchorMode = RovHudAnchorMode.VideoSurface;
        public bool hudShowCompass = true;
        [Range(1f, 3f)] public float compassRadius = 1.7f;
        [Range(0.4f, 1.8f)] public float compassDrop = 0.9f;
        [Range(0.7f, 1.5f)] public float hudScale = 1f;
        [Range(0.2f, 1f)] public float hudOpacity = 0.85f;
        public RovHudLayout hudLayout = new RovHudLayout();
        [Header("Screen alignment")]
        [Range(-25f, 25f)] public float verticalOffsetDegrees;
        public bool followHeadRoll;
        [Range(-15f, 15f)] public float imageRollDegrees;
        public bool followHeadPitch;
        public bool followHeadTranslation = true;
        public bool loadSavedCalibration = true;

        public float ApparentHorizontalFov => RovVideoProjection.ApparentHorizontalFov(horizontalFov, curveDegrees, distance, cylindricalSurface);
        public float VerticalFov => RovVideoProjection.ApparentVerticalFov(horizontalFov, curveDegrees, distance, cylindricalSurface);
        public float CenterPixelsPerDegree => cylindricalSurface ? 1920f * distance * Mathf.Deg2Rad
            / RovVideoProjection.CurvedArcWidth(RovVideoProjection.ReferenceDistance, horizontalFov, curveDegrees)
            : RovVideoProjection.CenterPixelsPerDegree(1920, horizontalFov) * distance / RovVideoProjection.ReferenceDistance;
        public float AveragePixelsPerDegree => 1920f / ApparentHorizontalFov;
        public bool IsCentered => centered;
        public bool ScreenLocked => !headFollowingEnabled && frozenPoseCaptured;
        public Vector3 HudAnchorPosition => ScreenLocked ? frozenEyePosition : eye == null ? Vector3.zero : eye.position;
        public Vector3 PilotHudPosition => ScreenLocked ? frozenHudPosition : CalculatePilotHudPosition();
        public Quaternion PilotHudRotation => ScreenLocked ? frozenHudRotation : CalculatePilotHudRotation();
        public Quaternion CenteredHeading => heading;
        public string ConfigurationPath => RovVideoViewProfile.FilePath;
        public bool PoseLive => tilt != null && tilt.AttitudeLive && tilt.MountTelemetryLive;
        public float DisplayMountPitch => tilt == null ? 0f : Mathf.Clamp(tilt.ReportedPitch, tilt.MinimumPitch, tilt.MaximumPitch);
        public string PoseStatus => !headFollowingEnabled ? "Hodefølging av; skjermen er låst (A for å følge)"
            : !anchorToRovImu ? "Manuell videoskjerm"
            : !PoseLive ? "Venter på fersk IMU/tilt; retningen holdes"
            : tilt.AtTiltLimit ? "Tiltgrense nådd" : "ROV pitch/roll + tilt; fast sideretning";

        private CurvedVideoScreen surface;
        private Quaternion heading = Quaternion.identity;
        private Vector3 centeredEye;
        private bool centered;
        private bool appliedHeadFollowing = true, frozenPoseCaptured;
        private Vector3 frozenPosition, frozenEyePosition;
        private Quaternion frozenRotation;
        private Vector3 frozenHudPosition;
        private Quaternion frozenHudRotation;
        private float readySince = -1f, nextAdjustment;
        private string notice;
        private float noticeUntil;
        private Quaternion rovAlignment = Quaternion.identity, lastPose = Quaternion.identity;
        private bool imuCentered, poseInitialized;
        private int poseFrame = -1;
        private Vector2 settingsScroll;

        private void Start()
        {
            surface = GetComponent<CurvedVideoScreen>();
            if (eye == null && Camera.main != null) eye = Camera.main.transform;
            if (tilt == null && eye != null) tilt = eye.GetComponent<RovHeadTiltController>();
            SetHeadFollowingEnabled(headFollowingEnabled);
            if (loadSavedCalibration && File.Exists(ConfigurationPath)) LoadCalibration();
            else
            {
                string bundled = Path.Combine(Application.streamingAssetsPath, "rov-video-view.defaults.json");
                if (loadSavedCalibration && File.Exists(bundled))
                {
                    try { ApplyProfile(RovVideoViewProfile.Parse(File.ReadAllText(bundled))); }
                    catch (Exception error)
                    {
                        Debug.LogWarning("[ROV view] Could not load bundled defaults: " + error.Message, this);
                        ApplyGeometry();
                    }
                }
                else ApplyGeometry();
                if (loadSavedCalibration) SaveCalibration();
            }
            var overlay = GetComponent<RovVrOverlay>();
            if (overlay == null) overlay = gameObject.AddComponent<RovVrOverlay>();
            overlay.view = this;
            BeginPilotStartup();
        }

        private void OnEnable() { Application.onBeforeRender += UpdateViewPose; Application.onBeforeRender += UpdateDetailPose; }
        private void OnDisable() { Application.onBeforeRender -= UpdateViewPose; Application.onBeforeRender -= UpdateDetailPose; HidePilotTools(); }

        private void Update()
        {
            if (eye == null) return;
            if (appliedHeadFollowing != headFollowingEnabled) SetHeadFollowingEnabled(headFollowingEnabled);
            bool ready = !XRSettings.isDeviceActive || IsHeadReady();
            if (!centered && ready)
            {
                if (readySince < 0f) readySince = Time.realtimeSinceStartup;
                if (!XRSettings.isDeviceActive || Time.realtimeSinceStartup - readySince > 0.75f) CenterView();
            }
            else if (!ready) readySince = -1f;
            if (anchorToRovImu && centered && ready && PoseLive && !imuCentered) CaptureImuAlignment();
            ReadControls();
        }

        private static bool IsHeadReady()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked
                && OVRPlugin.hasVrFocus && OVRPlugin.hasInputFocus;
        }

        private void LateUpdate() { UpdateViewPose(); UpdatePilotTools(); }

        [BeforeRenderOrder(100)]
        private void UpdateViewPose()
        {
            if (!isActiveAndEnabled || eye == null || !centered) return;
            if (ScreenLocked)
            {
                transform.SetPositionAndRotation(frozenPosition, frozenRotation);
                return;
            }
            // Translation of a single ROV camera cannot track the user's lean.
            // Keep the projection origin at the viewer instead of inventing
            // parallax. Only pitch/roll telemetry rotates an anchored surface;
            // turning the headset itself must not rotate the video frame.
            Vector3 origin = followHeadTranslation ? eye.position : centeredEye;
            Quaternion orientation;
            if (anchorToRovImu)
            {
                if (PoseLive && imuCentered && poseFrame != Time.frameCount)
                {
                    var target = rovAlignment * RovCameraPoseMath.CameraRotation(
                        RovCameraPoseMath.VehicleRotationWithoutYaw(tilt.VehicleRollPitchYaw),
                        DisplayMountPitch, tilt.MinimumPitch, tilt.MaximumPitch);
                    float blend = telemetrySmoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / telemetrySmoothingSeconds);
                    lastPose = poseInitialized ? Quaternion.Slerp(lastPose, target, blend) : target;
                    poseInitialized = true;
                    poseFrame = Time.frameCount;
                }
                orientation = lastPose * Quaternion.AngleAxis(imageRollDegrees, Vector3.forward)
                    * Quaternion.Euler(-verticalOffsetDegrees, 0f, 0f);
            }
            else orientation = RovVideoProjection.ViewOrientation(eye.rotation, heading,
                    followHeadPitch, followHeadRoll, verticalOffsetDegrees, imageRollDegrees);
            transform.SetPositionAndRotation(origin + orientation * Vector3.forward * distance
                + Vector3.up * verticalPositionMeters, orientation);
            // A profile can start locked. Place it once after tracking is ready,
            // then retain this pose rather than freezing an unplaced screen.
            if (!headFollowingEnabled) CaptureFrozenPose();
        }

        private void CaptureFrozenPose()
        {
            frozenPosition = transform.position;
            frozenRotation = transform.rotation;
            frozenEyePosition = eye.position;
            frozenHudPosition = CalculatePilotHudPosition();
            frozenHudRotation = CalculatePilotHudRotation();
            frozenPoseCaptured = true;
        }

        private Vector3 CalculatePilotHudPosition()
        {
            // Layout z=2m stays at the video centre, with each floating box's
            // depth/offset preserved when the entire screen moves or tilts.
            if (hudAnchorMode == RovHudAnchorMode.VideoSurface)
                return transform.position - transform.forward * RovVideoProjection.ReferenceDistance;
            return eye == null ? Vector3.zero : eye.position;
        }

        private Quaternion CalculatePilotHudRotation()
        {
            if (hudAnchorMode == RovHudAnchorMode.VideoSurface) return transform.rotation;
            if (hudAnchorMode == RovHudAnchorMode.Headset && eye != null) return eye.rotation;
            return heading;
        }

        public void SetHeadFollowingEnabled(bool enabled)
        {
            headFollowingEnabled = enabled;
            if (tilt != null) tilt.SetHeadFollowingEnabled(enabled);
            if (appliedHeadFollowing == enabled) return;
            appliedHeadFollowing = enabled;
            if (!enabled && centered && eye != null) CaptureFrozenPose();
            if (enabled) frozenPoseCaptured = false;
            ShowNotice(enabled ? "Hodefølging på" : "Hodefølging av — skjerm og kameratilt låst");
        }

        public void ToggleHud()
        {
            hudEnabled = !hudEnabled;
            ShowNotice(hudEnabled ? "VR-HUD på" : "VR-HUD av");
        }

        private void CaptureImuAlignment()
        {
            // Calibrate the user's straight-ahead pose against the vehicle,
            // excluding mount pitch: X also recenters the mount toward zero.
            Quaternion straightAhead = Quaternion.LookRotation(eye.forward, Vector3.up);
            // Remove yaw both here and in the live pose. With compensation,
            // retain world pitch instead of canceling the initial ROV pitch.
            Quaternion reference = tilt.CompensateRovPitch ? Quaternion.identity
                : RovCameraPoseMath.VehicleRotationWithoutYaw(tilt.VehicleRollPitchYaw);
            rovAlignment = straightAhead * Quaternion.Inverse(reference);
            imuCentered = true;
            poseInitialized = false;
        }

        [ContextMenu("Center view (Play mode)")]
        public void CenterView()
        {
            if (eye == null) return;
            if (ScreenLocked) { ShowNotice("Skjermen er låst. Trykk A for hodefølging før sentrering."); return; }
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) return;
            heading = Quaternion.LookRotation(forward.normalized, Vector3.up);
            centeredEye = eye.position;
            centered = true;
            lastPose = Quaternion.LookRotation(eye.forward, Vector3.up);
            imuCentered = false;
            poseInitialized = false;
            if (anchorToRovImu && PoseLive) CaptureImuAlignment();
            UpdateViewPose();
            ShowNotice("Bildet er sentrert");
        }

        public void ApplyGeometry()
        {
            if (hudLayout == null) hudLayout = new RovHudLayout();
            hudLayout.Validate();
            telemetrySmoothingSeconds = Mathf.Clamp(telemetrySmoothingSeconds, 0f, 0.2f);
            horizontalFov = Mathf.Clamp(horizontalFov, 50f, cylindricalSurface ? 150f : 140f);
            curveDegrees = Mathf.Clamp(curveDegrees, 0f, 150f);
            distance = Mathf.Clamp(distance, 1f, 4f);
            verticalPositionMeters = Mathf.Clamp(verticalPositionMeters, -2f, 2f);
            verticalOffsetDegrees = Mathf.Clamp(verticalOffsetDegrees, -25f, 25f);
            imageRollDegrees = Mathf.Clamp(imageRollDegrees, -15f, 15f);
            if (surface == null) surface = GetComponent<CurvedVideoScreen>();
            bool rebuild = GetComponent<MeshFilter>().sharedMesh == null || !surface.calibratedProjection
                || surface.cylindricalProjection != cylindricalSurface
                || surface.independentCurveDegrees != curveDegrees
                || surface.viewingDistance != RovVideoProjection.ReferenceDistance
                || surface.horizontalViewDegrees != horizontalFov
                || surface.sourceWidth != 1920 || surface.sourceHeight != 1080
                || surface.segments != 96 || surface.verticalSegments != 54;
            surface.calibratedProjection = true;
            surface.cylindricalProjection = cylindricalSurface;
            surface.independentCurveDegrees = curveDegrees;
            // Keep physical dimensions fixed when moving the screen closer or
            // farther; the distance slider must visibly change angular size.
            surface.viewingDistance = RovVideoProjection.ReferenceDistance;
            surface.horizontalViewDegrees = horizontalFov;
            surface.sourceWidth = 1920;
            surface.sourceHeight = 1080;
            surface.segments = 96;
            surface.verticalSegments = 54;
            // HUD, servo settings and placement do not change video vertices.
            if (rebuild) surface.RebuildMesh();
            UpdateViewPose();
        }

        public void Adjust(float fovChange, float distanceChange, float heightChange = 0f, float rollChange = 0f, float curveChange = 0f, float verticalPositionChange = 0f)
        {
            horizontalFov += fovChange;
            distance += distanceChange;
            verticalPositionMeters += verticalPositionChange;
            verticalOffsetDegrees += heightChange;
            imageRollDegrees += rollChange;
            curveDegrees += curveChange;
            ApplyGeometry();
            ShowNotice($"Bilde {horizontalFov:F0}°   Bue {curveDegrees:F0}°   Avstand {distance:F2} m   Plassering {verticalPositionMeters:F2} m   Vinkel {verticalOffsetDegrees:F1}°   Helning {imageRollDegrees:F1}°");
        }

        [ContextMenu("Save viewing calibration (Play mode)")]
        public void SaveCalibration()
        {
            try
            {
                GetProfile().Save(ConfigurationPath);
                ShowNotice("Lagret: " + ConfigurationPath);
                Debug.Log("[ROV view] Configuration saved: " + ConfigurationPath, this);
            }
            catch (Exception error) { ShowNotice("Kunne ikke lagre: " + error.Message); Debug.LogWarning("[ROV view] " + notice, this); }
        }

        public RovVideoViewProfile GetProfile() => new RovVideoViewProfile
        {
            horizontalFov = horizontalFov, curveDegrees = curveDegrees, distance = distance,
            verticalPositionMeters = verticalPositionMeters,
            verticalOffsetDegrees = verticalOffsetDegrees, imageRollDegrees = imageRollDegrees,
            anchorToRovImu = anchorToRovImu, cylindricalSurface = cylindricalSurface,
            headFollowingEnabled = headFollowingEnabled,
            compensateRovPitch = tilt == null || tilt.CompensateRovPitch,
            pitchCompensationStrength = tilt == null ? 1f : tilt.PitchCompensationStrength,
            hudEnabled = hudEnabled, hudShowAttitude = hudShowAttitude, hudShowBattery = hudShowBattery,
            hudShowDepth = hudShowDepth, hudShowDiagnostics = hudShowDiagnostics,
            hudAnchorMode = hudAnchorMode,
            hudShowCompass = hudShowCompass, compassRadius = compassRadius, compassDrop = compassDrop,
            hudScale = hudScale, hudOpacity = hudOpacity, hudLayout = (hudLayout ?? new RovHudLayout()).Copy(),
            telemetrySmoothingSeconds = telemetrySmoothingSeconds,
            servoSmoothingSeconds = tilt == null ? 0.12f : tilt.ServoSmoothingSeconds,
            servoMaximumSpeed = tilt == null ? 45f : tilt.ServoMaximumSpeed,
            pilot = pilot.Copy()
        };

        public void ApplyProfile(RovVideoViewProfile profile)
        {
            profile.Validate();
            pilot = profile.pilot.Copy();
            ResetPilotInputEdges();
            horizontalFov = profile.horizontalFov;
            curveDegrees = profile.curveDegrees;
            distance = profile.distance;
            verticalPositionMeters = profile.verticalPositionMeters;
            verticalOffsetDegrees = profile.verticalOffsetDegrees;
            imageRollDegrees = profile.imageRollDegrees;
            anchorToRovImu = profile.anchorToRovImu;
            cylindricalSurface = profile.cylindricalSurface;
            hudEnabled = profile.hudEnabled; hudShowAttitude = profile.hudShowAttitude;
            hudShowBattery = profile.hudShowBattery; hudShowDepth = profile.hudShowDepth;
            hudShowDiagnostics = profile.hudShowDiagnostics;
            hudAnchorMode = profile.hudAnchorMode;
            hudShowCompass = profile.hudShowCompass; compassRadius = profile.compassRadius; compassDrop = profile.compassDrop;
            hudScale = profile.hudScale; hudOpacity = profile.hudOpacity;
            hudLayout = profile.hudLayout.Copy();
            telemetrySmoothingSeconds = profile.telemetrySmoothingSeconds;
            if (tilt != null)
            {
                bool wasCompensating = tilt.CompensateRovPitch;
                tilt.CompensateRovPitch = profile.compensateRovPitch;
                tilt.PitchCompensationStrength = profile.pitchCompensationStrength;
                tilt.ServoSmoothingSeconds = profile.servoSmoothingSeconds;
                tilt.ServoMaximumSpeed = profile.servoMaximumSpeed;
                if (wasCompensating != profile.compensateRovPitch) imuCentered = false;
            }
            ApplyGeometry();
            SetHeadFollowingEnabled(profile.headFollowingEnabled);
        }

        public void LoadCalibration()
        {
            try
            {
                ApplyProfile(RovVideoViewProfile.Parse(File.ReadAllText(ConfigurationPath)));
                ShowNotice("Konfigurasjonen er lastet");
            }
            catch (Exception error)
            {
                ApplyGeometry();
                ShowNotice("Kunne ikke laste: " + error.Message);
                Debug.LogWarning("[ROV view] " + notice, this);
            }
        }

        public void RestoreManualDefaults() { ApplyProfile(new RovVideoViewProfile()); ShowNotice("Standard: 110° bilde / 45° bue / 2 m. Y lagrer."); }

        public void SetPitchCompensation(bool enabled)
        {
            if (tilt == null || tilt.CompensateRovPitch == enabled) return;
            tilt.CompensateRovPitch = enabled;
            imuCentered = false;
        }

        public void CenterViewAndTilt()
        {
            if (ScreenLocked) { ShowNotice("Skjermen er låst. Trykk A for hodefølging før sentrering."); return; }
            if (tilt != null) tilt.Recenter();
            CenterView();
        }

        [ContextMenu("Restore camera baseline 80 degrees / 2 meters")]
        public void RestoreBaseline()
        {
            horizontalFov = 80f;
            distance = 2f;
            cylindricalSurface = false;
            verticalPositionMeters = 0f;
            verticalOffsetDegrees = 0f;
            ApplyGeometry();
            SaveCalibration();
        }

        private void ReadControls()
        {
            PreparePointerInput();
            ReadPilotButtons(IsHeadReady());
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (IsHeadReady())
            {
                if (!startupPending && !pilotMenuOpen && !WindowDragActive && !pointerRecoveryHeld && pilot.stickCalibrationEnabled && Time.realtimeSinceStartup >= nextAdjustment
                    && left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick))
                {
                    left.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
                    left.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger);
                    if (Mathf.Abs(stick.x) > 0.7f && Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
                    {
                        if (trigger) Adjust(0f, 0f, 0f, 0f, Mathf.Sign(stick.x) * 5f);
                        else if (grip) Adjust(0f, 0f, 0f, Mathf.Sign(stick.x) * 0.5f);
                        else Adjust(Mathf.Sign(stick.x) * 2f, 0f);
                    }
                    else if (Mathf.Abs(stick.y) > 0.7f)
                    {
                        if (grip) Adjust(0f, 0f, verticalPositionChange: Mathf.Sign(stick.y) * 0.05f);
                        else Adjust(0f, Mathf.Sign(stick.y) * 0.1f);
                    }
                    if (stick.magnitude > 0.7f) nextAdjustment = Time.realtimeSinceStartup + 0.25f;
                }
            }
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (!RovHeadTiltFocus.DesktopShortcutsAllowed || keyboard == null) return;
            if (keyboard.f4Key.wasPressedThisFrame) showSettings = !showSettings;
            if (keyboard.f6Key.wasPressedThisFrame) Adjust(-2f, 0f);
            if (keyboard.f7Key.wasPressedThisFrame) Adjust(2f, 0f);
            if (keyboard.f9Key.wasPressedThisFrame) Adjust(0f, -0.1f);
            if (keyboard.f10Key.wasPressedThisFrame) Adjust(0f, 0.1f);
            if (keyboard.pageUpKey.wasPressedThisFrame) Adjust(0f, 0f, verticalPositionChange: 0.05f);
            if (keyboard.pageDownKey.wasPressedThisFrame) Adjust(0f, 0f, verticalPositionChange: -0.05f);
#endif
        }

        private void ProcessRightButtons(bool a, bool b, bool ready)
        {
            // Legacy regression entry point shares the live binding dispatcher.
            ProcessPilotInput(RovPilotAction.Follow, a, ready, false, false, false);
            ProcessPilotInput(RovPilotAction.Hud, b, ready, false, false, false);
        }

        private void ShowNotice(string text)
        {
            notice = text;
            noticeUntil = Time.realtimeSinceStartup + 3f;
        }

        private void OnGUI()
        {
            DrawStartupOverlay();
            if (showSettings)
            {
                GUILayout.BeginArea(new Rect(12, 200, 440, Mathf.Max(220f, Mathf.Min(620f, Screen.height - 212f))), GUI.skin.box);
                settingsScroll = GUILayout.BeginScrollView(settingsScroll);
                GUILayout.Label("ROV bildeinnstillinger — F4 viser/skjuler");
                DrawPilotTools();
                SetHeadFollowingEnabled(GUILayout.Toggle(headFollowingEnabled, "Hodefølging på (A) — av låser skjerm og kameratilt"));
                bool wasAnchored = anchorToRovImu;
                bool changed = false;
                changed |= Slider("Bildestørrelse", ref horizontalFov, 50f, 150f, "°");
                changed |= Slider("Krumming (0 = flat)", ref curveDegrees, 0f, 150f, "°");
                changed |= Slider("Avstand", ref distance, 1f, 4f, "m");
                changed |= Slider("Skjermplassering opp/ned", ref verticalPositionMeters, -2f, 2f, " m", "F2");
                changed |= Slider("Vinkel opp/ned", ref verticalOffsetDegrees, -25f, 25f, "°");
                changed |= Slider("Helning", ref imageRollDegrees, -15f, 15f, "°");
                anchorToRovImu = GUILayout.Toggle(anchorToRovImu, "Følg ROV pitch/roll og kameratilt");
                if (tilt != null) SetPitchCompensation(GUILayout.Toggle(tilt.CompensateRovPitch, "Kompenser når ROV-en tipper (pitch)"));
                if (tilt != null)
                {
                    float strength = tilt.PitchCompensationStrength;
                    if (Slider("Kompensasjonsstyrke", ref strength, 0f, 1.5f, "×", "F2")) tilt.PitchCompensationStrength = strength;
                }
                GUILayout.Space(10f);
                GUILayout.Label("Utjevning — 0 sekunder = av");
                Slider("Bilde: IMU-utjevning", ref telemetrySmoothingSeconds, 0f, 0.2f, " s", "F3");
                if (tilt != null)
                {
                    float smoothing = tilt.ServoSmoothingSeconds;
                    if (Slider("Fysisk kameratilt: utjevning", ref smoothing, 0f, 0.5f, " s", "F3")) tilt.ServoSmoothingSeconds = smoothing;
                    float speed = tilt.ServoMaximumSpeed;
                    if (Slider("Kameratilt: maksimal fart", ref speed, 5f, 90f, " °/s")) tilt.ServoMaximumSpeed = speed;
                }
                GUILayout.Label("Mer utjevning gir mer etterslep. Fartsgrensen gjelder også ved 0 s.");
                GUILayout.Space(10f);
                GUILayout.Label("VR-HUD");
                hudEnabled = GUILayout.Toggle(hudEnabled, "Vis HUD i headsettet (B)");
                GUILayout.Label("HUD følger");
                hudAnchorMode = (RovHudAnchorMode)GUILayout.SelectionGrid((int)hudAnchorMode, new[] {"Videoflaten", "Hodet", "Fast X-retning"}, 1);
                hudShowAttitude = GUILayout.Toggle(hudShowAttitude, "Vis pitch/roll og kameratilt");
                hudShowBattery = GUILayout.Toggle(hudShowBattery, "Vis batteri");
                hudShowDepth = GUILayout.Toggle(hudShowDepth, "Vis estimert dybde (AHRS2)");
                hudShowDiagnostics = GUILayout.Toggle(hudShowDiagnostics, "Vis render-/video-FPS og headset-Hz");
                hudShowCompass = GUILayout.Toggle(hudShowCompass, "Kompassring rundt deg nederst");
                Slider("Kompassradius", ref compassRadius, 1f, 3f, " m");
                Slider("Kompass under øyehøyde", ref compassDrop, 0.4f, 1.8f, " m");
                Slider("HUD-tekststørrelse", ref hudScale, 0.7f, 1.5f, "×");
                Slider("HUD-synlighet", ref hudOpacity, 0.2f, 1f, "");
                GUILayout.Label("Flytende bokser med valgfri forankring. A låser også boksene; kompasset ligger fortsatt nederst.");
                DrawHudBox("Status", hudLayout.status, 0);
                DrawHudBox("Batteri", hudLayout.battery, 1);
                DrawHudBox("Dybde", hudLayout.depth, 2);
                DrawHudBox("Kameratilt", hudLayout.cameraTilt, 3);
                DrawHudBox("ROV-vinkel", hudLayout.attitude, 4);
                DrawHudBox("Diagnostikk", hudLayout.diagnostics, 5);
                if (GUILayout.Button("Tilbakestill bare HUD-plassering")) hudLayout = new RovHudLayout();
                if (changed) ApplyGeometry();
                if (wasAnchored != anchorToRovImu) CenterView();
                GUILayout.Label($"Synlig bredde: {ApparentHorizontalFov:F1}° — kortere avstand gir større bilde");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Lagre JSON")) SaveCalibration();
                if (GUILayout.Button("Last JSON")) LoadCalibration();
                if (GUILayout.Button("Standard")) RestoreManualDefaults();
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Sentrer bilde og kameratilt")) CenterViewAndTilt();
                GUILayout.Label(ConfigurationPath);
                GUILayout.Label("A: lås/følg skjerm og kameratilt. B: vis/skjul HUD.\nSpak: størrelse / avstand\nAvtrekker + spak: bue\nGrip + spak: helning / høyde. Y: lagre JSON.");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            if (Time.realtimeSinceStartup <= noticeUntil && !string.IsNullOrEmpty(notice))
                GUI.Label(new Rect(12, 148, 900, 44), notice);
        }

        private readonly bool[] hudBoxExpanded = new bool[6];

        private void DrawHudBox(string title, RovHudBoxLayout box, int index)
        {
            hudBoxExpanded[index] = GUILayout.Toggle(hudBoxExpanded[index], (hudBoxExpanded[index] ? "▼ " : "▶ ") + title + " — plassering");
            if (!hudBoxExpanded[index]) return;
            Slider("Høyre/venstre (+ = høyre)", ref box.position.x, -2f, 2f, " m", "F2");
            Slider("Høyde (+ = opp)", ref box.position.y, -2f, 2f, " m", "F2");
            Slider("Avstand fremover", ref box.position.z, 0.6f, 4f, " m", "F2");
            Slider("Boksstørrelse", ref box.scale, 0.5f, 2f, "×", "F2");
            box.faceViewer = GUILayout.Toggle(box.faceViewer, "Vinkle mot sentrert øyeposisjon");
            Slider("Ekstra pitch", ref box.angleOffset.x, -60f, 60f, "°");
            Slider("Ekstra yaw", ref box.angleOffset.y, -60f, 60f, "°");
            Slider("Ekstra roll", ref box.angleOffset.z, -60f, 60f, "°");
        }

        private static bool Slider(string label, ref float value, float min, float max, string unit, string format = "F1")
        {
            GUILayout.Label(label + ": " + value.ToString(format) + unit);
            float next = GUILayout.HorizontalSlider(value, min, max);
            if (Mathf.Abs(next - value) < 0.001f) return false;
            value = next;
            return true;
        }

        private void OnDestroy() { Application.onBeforeRender -= UpdateViewPose; Application.onBeforeRender -= UpdateDetailPose; DestroyPilotTools(); }
    }
}
