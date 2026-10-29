using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ROVVR
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ROV/Quest Camera Tilt")]
    public sealed class RovHeadTiltController : MonoBehaviour
    {
        [Header("BlueOS")]
        [SerializeField] private string blueOsUrl = "http://192.168.2.2:6040";
        [SerializeField, Range(5f, 30f)] private float sendRate = 20f;

        [Header("Head movement (degrees, positive = up)")]
        [SerializeField, Range(-35f, 0f)] private float minimumPitch = -35f;
        [SerializeField, Range(0f, 30f)] private float maximumPitch = 30f;
        [SerializeField, Range(0.1f, 2f)] private float gain = 1f;
        [Tooltip("The servo is already reversed in ArduSub. Only enable if looking up physically moves the camera down.")]
        [SerializeField] private bool invertPitch;
        [SerializeField, Range(0f, 3f)] private float deadZone = 0.5f;
        [SerializeField, Range(0f, 0.5f)] private float smoothingSeconds = 0.12f;
        [SerializeField, Range(5f, 90f)] private float maximumSpeed = 45f;
        [SerializeField] private bool showDesktopStatus = true;
        [Tooltip("Counteract the ROV's IMU pitch to hold the calibrated headset elevation in the world frame.")]
        [SerializeField] private bool compensateRovPitch = true;
        [SerializeField, Range(0f, 1.5f)] private float pitchCompensationStrength = 1f;

        public string Status { get; private set; } = "Venter på VR-headsett";
        public float HeadPitch { get; private set; }
        public float TargetPitch { get; private set; }
        public float ReportedPitch { get; private set; }
        public int ServoPwm { get; private set; }
        public bool CommandAccepted { get; private set; }
        public bool ControlFocused => focused;
        public bool HeadTracked => tracked;
        public bool BlueOsLive => heartbeat.IsFresh(Time.realtimeSinceStartup);
        public RovHudTelemetry HudTelemetry { get; } = new RovHudTelemetry();
        public int CommandsSent { get; private set; }
        public bool RcControlSelected => paused;
        public bool StartupSelectionPending { get; set; }
        public string LastStopReason { get; private set; } = "";
        public string RcGateStatus { get; private set; } = "VR-prioritet ikke klargjort";
        public float AcknowledgementAge => lastAcceptedAt < 0f ? float.PositiveInfinity : Time.realtimeSinceStartup - lastAcceptedAt;
        public float ServoSmoothingSeconds
        {
            get => smoothingSeconds;
            set => smoothingSeconds = RovCameraPoseMath.Finite(value) ? Mathf.Clamp(value, 0f, 0.5f) : 0.12f;
        }
        public float ServoMaximumSpeed
        {
            get => maximumSpeed;
            set => maximumSpeed = RovCameraPoseMath.Finite(value) ? Mathf.Clamp(value, 5f, 90f) : 45f;
        }
        public Quaternion VehicleRotation { get; private set; } = Quaternion.identity;
        public Vector3 VehicleRollPitchYaw { get; private set; }
        public bool AttitudeLive => BlueOsLive && attitudeFreshness.IsFresh(Time.realtimeSinceStartup);
        public bool MountTelemetryLive => BlueOsLive && mountFreshness.IsFresh(Time.realtimeSinceStartup);
        public bool HasHeadNeutral => calibrated;
        public bool HeadFollowingEnabled { get; private set; } = true;
        public float MinimumPitch => minimumPitch;
        public float MaximumPitch => maximumPitch;
        public bool AtTiltLimit => calibrated && (RequestedPitch <= minimumPitch || RequestedPitch >= maximumPitch);
        public float DesiredWorldPitch => RovHeadTiltMath.DesiredPitch(HeadPitch, neutralPitch, gain, invertPitch, deadZone);
        public float RequestedPitch => !HeadFollowingEnabled ? TargetPitch : compensateRovPitch
            ? RovCameraPoseMath.TrimmedMountPitch(DesiredWorldPitch, VehicleRotation, pitchCompensationStrength, -180f, 180f) : DesiredWorldPitch;
        public float PitchCompensationStrength
        {
            get => pitchCompensationStrength;
            set => pitchCompensationStrength = RovCameraPoseMath.Finite(value) ? Mathf.Clamp(value, 0f, 1.5f) : 1f;
        }
        public bool CompensateRovPitch
        {
            get => compensateRovPitch;
            // Live tuning only changes the next bounded target. It must not
            // release the mount, abort an HTTP request or change head neutral.
            set => compensateRovPitch = value;
        }

        private readonly RovHeartbeatFreshness heartbeat = new RovHeartbeatFreshness();
        private readonly RovHeartbeatFreshness attitudeFreshness = new RovHeartbeatFreshness(0.75f);
        private readonly RovHeartbeatFreshness mountFreshness = new RovHeartbeatFreshness(0.75f);
        private UnityWebRequest commandRequest, telemetryRequest, releaseRequest;
        private bool paused, focused, applicationPaused, tracked, calibrated, canSend, ownsMount;
        private bool commandRejected, holdTargetInitialized;
        private float stableSince = -1f, neutralPitch, retryAt, firstCommandAt = -1f;
        private float ackRetryAt = -1f;
        private int ackRetryCount;
        private long ackCounter = -1, ackBaseline = -1;
        private long processedAckCounter = -1;
        private float lastAcceptedAt = -1f;
        private bool resumeTarget, gateBusy, shuttingDown;
        private bool quitPending, quitReady;
        private RovRcCameraGate rcGate;
        private string connectionError;

        private void OnEnable()
        {
            shuttingDown = false;
            quitReady = quitPending = false;
            Application.wantsToQuit += AllowQuit;
            RefreshControlFocus();
            heartbeat.Reset();
            HudTelemetry.Reset();
            attitudeFreshness.Reset();
            mountFreshness.Reset();
            ResetCalibration();
            StartCoroutine(PollTelemetry());
            StartCoroutine(SendTargets());
        }

        private void OnValidate()
        {
            minimumPitch = Mathf.Clamp(minimumPitch, RovMountProtocol.MinimumPitch, 0f);
            maximumPitch = Mathf.Clamp(maximumPitch, 0f, RovMountProtocol.MaximumPitch);
            sendRate = Mathf.Clamp(sendRate, 5f, 30f);
            maximumSpeed = Mathf.Clamp(maximumSpeed, 5f, 90f);
            PitchCompensationStrength = pitchCompensationStrength;
        }

        private void Update()
        {
            RefreshControlFocus();
            ReadButtons();
            tracked = TryReadHeadPitch(out float pitch);
            if (tracked) HeadPitch = pitch;

            bool ready = !StartupSelectionPending && focused && !paused && tracked && !commandRejected
                && (!HeadFollowingEnabled || !compensateRovPitch || AttitudeLive)
                && heartbeat.IsFresh(Time.realtimeSinceStartup) && Time.realtimeSinceStartup >= retryAt;

            if (!ready)
            {
                string reason = StartupSelectionPending ? "Velg profil og trykk Start" : paused ? "RC/kontroller valgt" : !focused ? "Venter på VR-fokus"
                    : !tracked ? "Venter på VR-headsett" : commandRejected ? "ROV avviste tiltkommandoen"
                    : BlueOsLive && compensateRovPitch && !AttitudeLive ? "Venter på fersk ROV-IMU" : "Venter på BlueOS";
                SuspendForInterruption(reason);
                return;
            }

            if (gateBusy || rcGate == null || !rcGate.Active)
            {
                canSend = false;
                if (!gateBusy) AcquireRcGate();
                SetStatus(RcGateStatus);
                return;
            }

            if (!AcknowledgementRetryReady(Time.realtimeSinceStartup))
            {
                SetStatus("Mangler tilt-kvittering; prøver igjen snart");
                return;
            }

            if (!HeadFollowingEnabled && !holdTargetInitialized)
            {
                if (!MountTelemetryLive) { SetStatus("Venter på kameratilt før låsing"); return; }
                TargetPitch = Mathf.Clamp(ReportedPitch, minimumPitch, maximumPitch);
                holdTargetInitialized = true;
            }

            if (HeadFollowingEnabled && !calibrated)
            {
                if (stableSince < 0f) stableSince = Time.realtimeSinceStartup;
                SetStatus("Se rett frem - kalibrerer...");
                if (Time.realtimeSinceStartup - stableSince < 1f) return;
                neutralPitch = HeadPitch;
                TargetPitch = Mathf.Clamp(ReportedPitch, minimumPitch, maximumPitch);
                calibrated = true;
            }

            if (HeadFollowingEnabled)
            {
                if (resumeTarget)
                {
                    if (!MountTelemetryLive) { SetStatus("Venter på ferskt tiltmål før gjenopptak"); return; }
                    TargetPitch = Mathf.Clamp(ReportedPitch, minimumPitch, maximumPitch);
                    resumeTarget = false;
                }
                float target = compensateRovPitch
                    ? RovCameraPoseMath.TrimmedMountPitch(DesiredWorldPitch, VehicleRotation, pitchCompensationStrength, minimumPitch, maximumPitch)
                    : Mathf.Clamp(DesiredWorldPitch, minimumPitch, maximumPitch);
                TargetPitch = RovHeadTiltMath.Smooth(TargetPitch, target, smoothingSeconds,
                    maximumSpeed, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
            }
            canSend = true;
            SetStatus(CommandAccepted ? HeadFollowingEnabled ? "VR-tilt: fersk ACK (ikke fysisk feedback)" : "Hodefølging av; kameratilt låst" : "VR-tilt: venter på fersk ACK");

            CheckAcknowledgementTimeout(Time.realtimeSinceStartup);
        }

        private void CheckAcknowledgementTimeout(float now)
        {
            if (firstCommandAt >= 0f && now - (lastAcceptedAt >= firstCommandAt ? lastAcceptedAt : firstCommandAt) > 4f)
                ScheduleAcknowledgementRetry(now, "No fresh COMMAND_ACK from the autopilot.");
        }

        private void ScheduleAcknowledgementRetry(float now, string reason)
        {
            // Missing/delayed ACK is not an explicit refusal. Release this
            // attempt and retry with fresh input and capped backoff, keeping
            // the pilot's neutral. Focus/tracking/telemetry gates still apply.
            canSend = false;
            ackRetryCount = Mathf.Min(ackRetryCount + 1, 4);
            float delay = Mathf.Pow(2f, ackRetryCount - 1); // 1, 2, 4, 8 seconds.
            ackRetryAt = now + delay;
            ReleaseControl(false);
            resumeTarget = calibrated;
            if (isActiveAndEnabled) Debug.LogWarning($"[ROV tilt] {reason} Retrying in {delay:F0}s.", this);
        }

        private bool AcknowledgementRetryReady(float now)
        {
            if (ackRetryAt < 0f) return true;
            if (now < ackRetryAt) { canSend = false; return false; }
            ackRetryAt = -1f;
            return true;
        }

        private void ObserveCommandResult(string result, float now)
        {
            if (string.IsNullOrEmpty(result) || result == "MAV_RESULT_IN_PROGRESS") return;
            if (result == "MAV_RESULT_ACCEPTED")
            {
                CommandAccepted = true;
                lastAcceptedAt = now;
                ackRetryCount = 0;
            }
            else if (result == "MAV_RESULT_TEMPORARILY_REJECTED")
                ScheduleAcknowledgementRetry(now, "Autopilot temporarily rejected tilt.");
            else
            {
                // An explicit refusal is never bypassed by automatic retry.
                commandRejected = true;
                if (isActiveAndEnabled) Debug.LogWarning("[ROV tilt] Autopilot reply: " + result, this);
            }
        }

        private void RefreshControlFocus()
        {
            bool xrActive = XRSettings.isDeviceActive;
            // Query the native Meta runtime directly. OVRManager.hasVrFocus is
            // cached by an OVRManager component, which this XR Rig does not use.
            focused = RovHeadTiltFocus.AllowsControl(Application.isFocused, applicationPaused,
                xrActive, xrActive && OVRPlugin.hasVrFocus, xrActive && OVRPlugin.hasInputFocus);
        }

        private void SetStatus(string next)
        {
            if (Status == next) return;
            Status = next;
            Debug.Log("[ROV tilt] " + next, this);
        }

        private static bool TryReadHeadPitch(out float pitch)
        {
            pitch = 0f;
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked) || !isTracked)
                return false;
            if (head.TryGetFeatureValue(CommonUsages.userPresence, out bool present) && !present)
                return false;
            if (head.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState state)
                && (state & InputTrackingState.Rotation) == 0) return false;
            if (!head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation)
                && !head.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation)) return false;
            pitch = RovHeadTiltMath.Pitch(rotation);
            return !float.IsNaN(pitch) && !float.IsInfinity(pitch);
        }

        private void ReadButtons()
        {
            // A/B belong to the view's screen-lock and HUD controls. They must
            // never also trigger the old recenter / servo-release actions.
            bool recenter = false, toggle = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && RovHeadTiltFocus.DesktopShortcutsAllowed)
            {
                recenter |= keyboard.rKey.wasPressedThisFrame;
                toggle |= keyboard.pKey.wasPressedThisFrame;
                if (keyboard.f8Key.wasPressedThisFrame) showDesktopStatus = !showDesktopStatus;
            }
#endif
            if (!focused) return;
            if (recenter) Recenter();
            if (toggle) TogglePause();
        }

        [ContextMenu("Recenter head (Play mode)")]
        public void Recenter()
        {
            if (tracked) neutralPitch = HeadPitch;
        }

        public void SetHeadFollowingEnabled(bool enabled)
        {
            if (HeadFollowingEnabled == enabled) return;
            HeadFollowingEnabled = enabled;
            // Hold the last commanded body tilt, including when the head or
            // ROV pitches. Keep mount ownership and the pilot's calibration.
            // Existing focus/tracking/connection stop conditions still apply.
            if (!enabled) holdTargetInitialized = calibrated || ownsMount;
        }

        [ContextMenu("Pause / resume head control (Play mode)")]
        public void TogglePause()
        {
            SelectRcControl(!paused);
        }

        public void SelectRcControl(bool rc)
        {
            paused = rc;
            commandRejected = false;
            ackRetryAt = -1f;
            ackRetryCount = 0;
            if (rc) { SuspendForInterruption("RC/kontroller valgt"); RestoreRcGate(); }
            else { resumeTarget = calibrated; retryAt = 0f; }
        }

        private void SuspendForInterruption(string reason)
        {
            if (canSend || stableSince >= 0f) resumeTarget |= calibrated;
            canSend = false;
            stableSince = -1f;
            AbortRequest(commandRequest);
            firstCommandAt = -1f; lastAcceptedAt = -1f; CommandAccepted = false;
            // Hold the autopilot's last target. Do not silently hand control to RC
            // or treat the pilot's current head angle as a new neutral on recovery.
            LastStopReason = reason;
            SetStatus(reason);
        }

        private async void AcquireRcGate()
        {
            gateBusy = true; RcGateStatus = "Sperrer RC-kamerainnganger for VR...";
            try
            {
                if (rcGate == null) rcGate = new RovRcCameraGate(blueOsUrl);
                await rcGate.AcquireAsync();
                RcGateStatus = "VR-prioritet: RC-kamerainnganger sperret";
                if (shuttingDown || paused || !isActiveAndEnabled) { await rcGate.RestoreAsync(); ReleaseControl(); }
            }
            catch (Exception e) { RcGateStatus = "RC-sperre/gjenoppretting feilet: " + e.Message; retryAt = Time.realtimeSinceStartup + 5f; }
            finally { gateBusy = false; }
        }

        private async void RestoreRcGate()
        {
            if (gateBusy) return; // Acquire's continuation restores if RC/exit was selected meanwhile.
            gateBusy = true;
            try
            {
                if (rcGate == null) rcGate = new RovRcCameraGate(blueOsUrl);
                await rcGate.RestoreAsync();
                RcGateStatus = "RC-kamerainnganger gjenopprettet";
                ReleaseControl();
            }
            catch (Exception e) { RcGateStatus = "RC-gjenoppretting feilet: " + e.Message; Debug.LogError("[ROV tilt] " + RcGateStatus); }
            finally { gateBusy = false; }
        }

        private bool AllowQuit()
        {
            if (quitReady || (rcGate == null && !gateBusy)) return true;
            if (!quitPending) FinishQuit();
            return false;
        }

        private async void FinishQuit()
        {
            quitPending = true; shuttingDown = true; paused = true;
            SuspendForInterruption("Avslutter; gjenoppretter RC-kamerainnganger...");
            try
            {
                while (gateBusy) await System.Threading.Tasks.Task.Delay(100);
                if (rcGate != null) await rcGate.RestoreAsync();
                ReleaseControl();
                // Give the best-effort RC mode request time to complete as well.
                while (releaseRequest != null) await System.Threading.Tasks.Task.Delay(50);
                quitReady = true; Application.Quit();
            }
            catch (Exception e) { RcGateStatus = "Kunne ikke gjenopprette RC før avslutning: " + e.Message; Debug.LogError("[ROV tilt] " + RcGateStatus); }
            finally { quitPending = false; }
        }

        private void ResetCalibration()
        {
            calibrated = false;
            stableSince = -1f;
        }

        private IEnumerator PollTelemetry()
        {
            while (true)
            {
                using (var request = UnityWebRequest.Get(RovMountProtocol.TelemetryUrl(blueOsUrl)))
                {
                    telemetryRequest = request;
                    request.timeout = 2;
                    var operation = BeginRequest(request);
                    if (operation != null) yield return operation;
                    telemetryRequest = null;
                    if (operation != null && request.result == UnityWebRequest.Result.Success)
                    {
                        ReadTelemetry(request.downloadHandler.text);
                    }
                    else if (operation != null) connectionError = request.error;
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        private void ReadTelemetry(string json)
        {
            try
            {
                var messages = JsonUtility.FromJson<RovMountProtocol.VehicleTelemetry>(json)?.messages;
                var hb = messages?.HEARTBEAT;
                if (hb?.message?.mavtype?.type != "MAV_TYPE_SUBMARINE" || hb.status?.time == null) return;
                heartbeat.Observe(hb.status.time.counter, Time.realtimeSinceStartup);
                HudTelemetry.Observe(messages, Time.realtimeSinceStartup);
                connectionError = null;
                if (messages.SERVO_OUTPUT_RAW?.message != null)
                    ServoPwm = messages.SERVO_OUTPUT_RAW.message.servo11_raw;
                var mount = messages.GIMBAL_DEVICE_ATTITUDE_STATUS;
                if (mount?.status?.time != null && RovCameraPoseMath.TryMountPitch(mount.message?.q, out float mountPitch))
                {
                    ReportedPitch = Mathf.Clamp(mountPitch, minimumPitch, maximumPitch);
                    mountFreshness.Observe(mount.status.time.counter, Time.realtimeSinceStartup);
                }
                var attitude = messages.ATTITUDE;
                if (attitude?.message != null && attitude.status?.time != null
                    && RovCameraPoseMath.Finite(attitude.message.roll) && RovCameraPoseMath.Finite(attitude.message.pitch)
                    && RovCameraPoseMath.Finite(attitude.message.yaw))
                {
                    VehicleRollPitchYaw = new Vector3(attitude.message.roll, attitude.message.pitch, attitude.message.yaw) * Mathf.Rad2Deg;
                    VehicleRotation = RovCameraPoseMath.VehicleRotation(attitude.message.roll, attitude.message.pitch, attitude.message.yaw);
                    attitudeFreshness.Observe(attitude.status.time.counter, Time.realtimeSinceStartup);
                }

                var ack = messages.COMMAND_ACK;
                if (ack?.status?.time == null) return;
                ackCounter = ack.status.time.counter;
                var reply = ack.message;
                if (firstCommandAt < 0f || releaseRequest != null || !canSend || ackCounter <= ackBaseline || ackCounter == processedAckCounter || reply?.command?.type != RovMountProtocol.MountCommand
                    || reply.target_system != RovMountProtocol.SourceSystem
                    || reply.target_component != RovMountProtocol.SourceComponent) return;
                processedAckCounter = ackCounter;
                ObserveCommandResult(reply.result?.type, Time.realtimeSinceStartup);
            }
            catch (Exception error) { connectionError = error.Message; }
        }

        private IEnumerator SendTargets()
        {
            while (true)
            {
                float started = Time.realtimeSinceStartup;
                if (canSend && releaseRequest == null && (!HeadFollowingEnabled || !compensateRovPitch || AttitudeLive))
                {
                    if (firstCommandAt < 0f)
                    {
                        ackBaseline = ackCounter;
                        firstCommandAt = started;
                        lastAcceptedAt = -1f;
                        CommandAccepted = false;
                    }
                    using (var request = RovMountProtocol.Post(blueOsUrl, TargetPitch))
                    {
                        // A timed-out HTTP request may still have reached the mount.
                        ownsMount = true;
                        commandRequest = request;
                        var operation = BeginRequest(request);
                        if (operation != null) yield return operation;
                        commandRequest = null;
                        if (operation != null && request.result == UnityWebRequest.Result.Success) CommandsSent++;
                        if (canSend && (operation == null || request.result != UnityWebRequest.Result.Success))
                        {
                            if (operation != null) connectionError = request.error;
                            heartbeat.Reset();
                            retryAt = Time.realtimeSinceStartup + 1f;
                        }
                    }
                }
                float delay = Mathf.Max(0f, 1f / sendRate - (Time.realtimeSinceStartup - started));
                yield return new WaitForSecondsRealtime(delay);
            }
        }

        private void ReleaseControl(bool sendRcMode = true)
        {
            AbortRequest(commandRequest);
            firstCommandAt = -1f;
            CommandAccepted = false;
            lastAcceptedAt = -1f;
            if (!sendRcMode) return;
            if (!ownsMount || releaseRequest != null) return;
            ownsMount = false;
            // One best-effort mode change restores joystick tilt without changing
            // any persistent parameters. No target queue survives tracking loss.
            var request = RovMountProtocol.Post(blueOsUrl, 0f, true);
            releaseRequest = request;
            var operation = BeginRequest(request);
            if (operation == null)
            {
                releaseRequest = null;
                request.Dispose();
                return;
            }
            operation.completed += _ =>
            {
                if (this != null) releaseRequest = null;
                request.Dispose();
            };
        }

        private UnityWebRequestAsyncOperation BeginRequest(UnityWebRequest request)
        {
            try { return request.SendWebRequest(); }
            catch (InvalidOperationException error)
            {
                // For example, HTTP was disabled again in Player Settings.
                connectionError = error.Message;
                heartbeat.Reset();
                return null;
            }
        }

        private static void AbortRequest(UnityWebRequest request)
        {
            if (request == null) return;
            try { request.Abort(); }
            // Unity may already have released the native request on leaving Play
            // mode, before the component's OnDisable callback runs.
            catch (ObjectDisposedException) { }
            catch (NullReferenceException) { }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            RefreshControlFocus();
            if (!focused)
            {
                canSend = false;
                SuspendForInterruption("Venter på VR-fokus");
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            applicationPaused = isPaused;
            RefreshControlFocus();
            if (!focused)
            {
                canSend = false;
                SuspendForInterruption("VR-økt pauset");
            }
        }

        private void OnDisable()
        {
            Application.wantsToQuit -= AllowQuit;
            canSend = false;
            shuttingDown = true;
            AbortRequest(commandRequest);
            if (!quitReady) { if (rcGate != null) RestoreRcGate(); else if (ownsMount) ReleaseControl(); }
            AbortRequest(telemetryRequest);
            StopAllCoroutines();
            commandRequest = null;
            telemetryRequest = null;
        }

        private void OnGUI()
        {
            if (!showDesktopStatus) return;
            GUILayout.BeginArea(new Rect(12, 12, 500, 210), GUI.skin.box);
            GUILayout.Label("ROV-kamera: " + Status);
            GUILayout.Label($"Målvinkel: {TargetPitch:F1}°   Rapportert: {ReportedPitch:F1}°");
            if (compensateRovPitch) GUILayout.Label($"Hodeønske: {DesiredWorldPitch:F1}°   ROV pitch: {VehicleRollPitchYaw.y:F1}°");
            GUILayout.Label("A: lås/følg   B: HUD   X: sentrer   R: nullstill   P: servopause");
            GUILayout.Label(RcGateStatus);
            GUILayout.Label($"HTTP sendt: {CommandsSent}  PWM11: {ServoPwm}  ACK-alder: {AcknowledgementAge:F1}s");
            GUILayout.Label("Sist stopp: " + LastStopReason);
            if (GUILayout.Button(paused ? "Ta VR-kontroll" : "Gi kamerakontroll til RC")) SelectRcControl(!paused);
            if (paused && GUILayout.Button("Prøv RC-gjenoppretting igjen")) SelectRcControl(true);
            if (!string.IsNullOrEmpty(connectionError)) GUILayout.Label("BlueOS: " + connectionError);
            GUILayout.EndArea();
        }
    }
}
