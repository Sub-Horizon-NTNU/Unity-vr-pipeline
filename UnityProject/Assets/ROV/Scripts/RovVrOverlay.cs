using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace ROVVR
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1100)]
    public sealed class RovVrOverlay : MonoBehaviour
    {
        public RovVideoViewCalibration view;
        public float RenderFps { get; private set; }
        public float VideoFps { get; private set; }
        public float HeadsetHz { get; private set; }
        public string Summary { get; private set; }
        public bool Built => root != null;
        public bool AttachedToStream => root != null && view != null && root.parent == view.transform;
        public bool FloatingAnchored => root != null && root.parent == null;
        public RectTransform LayoutRoot => root;

        private RectTransform root;
        private CanvasGroup group;
        private RectTransform statusRoot, centerRoot;
        private Text attitudeCue;
        private readonly Text[] pitchLabels = new Text[3];
        private readonly int[] formattedPitchMarks = {int.MinValue, int.MinValue, int.MinValue};
        private RovFpvAttitudeGraphic attitudeGraphic;
        private Text connection, armed, alert, diagnostics, pilotGain, lights;
        private Text controlState;
        private Image linkDot;
        private Metric battery, depth, tiltIndicator, attitude;
        private RovVrCompass compass;
        private Material material;
        private Material vectorMaterial;
        private Font font;
        private StreamController stream;
        private float nextText, sampleStart, oldSize = -1f, oldOpacity = -1f;
        private int renderedFrames, previousVideoFrames;
        private readonly List<Text> textElements = new List<Text>();
        private readonly List<int> fontSizes = new List<int>();
        private readonly Dictionary<RovHudArt.Kind, Sprite> hudSprites = new Dictionary<RovHudArt.Kind, Sprite>();
        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private static readonly Color Ink = RovFpvAttitudeGraphic.Stroke;
        private static readonly Color Muted = new Color(0.52f, 0.94f, 0.37f, 1f);
        private static readonly Color Accent = RovFpvAttitudeGraphic.Stroke;
        private static readonly Color Amber = new Color(1f, 0.73f, 0.28f, 1f);
        private static readonly Color Red = new Color(1f, 0.38f, 0.32f, 1f);
        private static readonly string[] CompassPoints = {"N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV"};

        private sealed class Metric
        {
            public RectTransform root;
            public Text value, hint, unit, caption;
        }

        private void LateUpdate()
        {
            if (view == null) view = GetComponent<RovVideoViewCalibration>();
            if (view == null || view.eye == null) return;
            if (root == null) Build();
            if (root == null) return;
            group.gameObject.SetActive(view.hudEnabled);
            RefreshGeometry();
            compass.Update();

            float now = Time.realtimeSinceStartup;
            renderedFrames++;
            float elapsed = now - sampleStart;
            if (elapsed >= 1f)
            {
                RenderFps = renderedFrames / elapsed;
                int frames = stream == null ? 0 : stream.FramesReceived;
                VideoFps = Mathf.Max(0, frames - previousVideoFrames) / elapsed;
                previousVideoFrames = frames; renderedFrames = 0; sampleStart = now;
                if (view.hudShowDiagnostics)
                {
                    displays.Clear(); SubsystemManager.GetSubsystems(displays);
                    HeadsetHz = 0f;
                    foreach (var display in displays)
                        if (display.running && display.TryGetDisplayRefreshRate(out float hz)) { HeadsetHz = hz; break; }
                }
            }
            // Rounded values at 5 Hz keep the pilot's scan stable. Static UI
            // geometry stays cached when neither values nor screen shape change.
            if (now >= nextText) { RefreshText(now); nextText = now + 0.2f; }
        }

        private void Build()
        {
            Shader shader = Resources.Load<Shader>("RovHudOverlay");
            if (shader == null) { Debug.LogError("[ROV HUD] Missing HUD shader.", this); enabled = false; return; }
            material = new Material(shader) { name = "ROV pilot HUD (runtime)", renderQueue = 4010 };
            vectorMaterial = new Material(shader) { name = "ROV vector instruments (runtime)", renderQueue = 4010, mainTexture = Texture2D.whiteTexture };
            font = Resources.Load<Font>("RovHudFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("ROV floating pilot instruments (runtime)", typeof(RectTransform), typeof(Canvas));
            root = (RectTransform)go.transform;
            root.localScale = Vector3.one * 0.001f;
            root.sizeDelta = new Vector2(1920f, 1080f);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = view.eye.GetComponent<Camera>();
            canvas.sortingOrder = 100;
            group = Node("Pilot instruments", root, 0, 0, 1920, 1080).gameObject.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;

            statusRoot = Node("Status box", group.transform, 0, 0, 1060, 66);
            // Outlined glyphs keep the camera image visible between instruments.
            linkDot = Symbol("Link light", statusRoot, -500, 0, RovHudArt.Kind.Dot);
            connection = Label("Connection", statusRoot, -315, 0, 325, 36, 20, TextAnchor.MiddleLeft, Ink);
            Rule(statusRoot, -132, 0, 1, 24);
            controlState = Label("Head control", statusRoot, 70, 0, 360, 36, 19, TextAnchor.MiddleCenter, Muted);
            controlState.horizontalOverflow = HorizontalWrapMode.Wrap;
            controlState.verticalOverflow = VerticalWrapMode.Truncate;
            controlState.rectTransform.sizeDelta = new Vector2(360f, 68f);
            Rule(statusRoot, 284, 0, 1, 24);
            armed = Label("Arming state", statusRoot, 406, 0, 196, 36, 19, TextAnchor.MiddleRight, Muted);
            alert = Label("Pilot alert", statusRoot, 0, -58, 950, 40, 22, TextAnchor.MiddleCenter, Amber);
            battery = Instrument("BAT", "V");
            depth = Instrument("DEPTH EST", "m");
            pilotGain = Label("Vehicle pilot gain", battery.root, 0, -88, 300, 30, 20, TextAnchor.MiddleLeft, Ink);
            lights = Label("Commanded lights", depth.root, 0, -88, 340, 30, 20, TextAnchor.MiddleLeft, Ink);
            tiltIndicator = Instrument("CAM TARGET", "°");
            attitude = Instrument("ROV PITCH", "°");
            diagnostics = Label("Diagnostics", group.transform, 0, -460, 960, 50, 20, TextAnchor.MiddleCenter, Muted);

            centerRoot = Node("FPV center reference", group.transform, 0, 0, 1920, 1080);
            var centerCanvas = centerRoot.gameObject.AddComponent<Canvas>();
            centerCanvas.renderMode = RenderMode.WorldSpace;
            centerCanvas.overrideSorting = true; centerCanvas.sortingOrder = 102;
            centerCanvas.worldCamera = canvas.worldCamera;
            attitudeGraphic = CreateAttitudeGraphic(centerRoot, vectorMaterial);
            attitudeCue = Label("Attitude availability", centerRoot, 0, 0, 600, 30, 19, TextAnchor.MiddleCenter, Muted);
            for (int i = 0; i < pitchLabels.Length; i++)
            {
                pitchLabels[i] = Label("Pitch reference " + i, centerRoot, 0, 0, 64, 30, 14, TextAnchor.MiddleCenter, Ink);
            }

            compass = new RovVrCompass(view, material, font);
            stream = GetComponent<StreamController>();
            sampleStart = Time.realtimeSinceStartup;
            previousVideoFrames = stream == null ? 0 : stream.FramesReceived;
            RefreshGeometry();
        }

        private void RefreshGeometry()
        {
            UpdateAnchorPose();
            var layout = view.hudLayout;
            Place(statusRoot, layout.status);
            Place(battery.root, layout.battery);
            Place(depth.root, layout.depth);
            Place(tiltIndicator.root, layout.cameraTilt);
            Place(attitude.root, layout.attitude);
            Place(diagnostics.rectTransform, layout.diagnostics);
            float size = Mathf.Clamp(view.hudScale, 0.7f, 1.5f);
            float opacity = Mathf.Clamp(view.hudOpacity, 0.2f, 1f);
            if (oldOpacity != opacity) { group.alpha = opacity; oldOpacity = opacity; }
            if (oldSize != size)
            {
                for (int i = 0; i < textElements.Count; i++) textElements[i].fontSize = Mathf.RoundToInt(fontSizes[i] * size);
                oldSize = size;
            }
        }

        // Runs after the stream's order-100 pose update, including the final
        // before-render update. HUD and video must use the same frame's pose.
        [BeforeRenderOrder(200)]
        private void UpdateAnchorPose()
        {
            if (!isActiveAndEnabled || root == null || view == null || view.eye == null) return;
            root.SetPositionAndRotation(view.PilotHudPosition, view.PilotHudRotation);
            // The new center reference follows the video, independently of calibrated edge boxes.
            if (centerRoot != null)
                centerRoot.SetPositionAndRotation(view.transform.position, view.transform.rotation);
            if (attitudeGraphic != null) UpdateAttitudeCue();
        }

        private void UpdateAttitudeCue()
        {
            bool valid = view.TryGetHudCameraRotation(out Quaternion camera);
            attitudeGraphic.SetProjection(view.horizontalFov, view.curveDegrees, view.cylindricalSurface);
            attitudeGraphic.SetMainZoom(view.pilot.mainZoom);
            attitudeGraphic.SetCameraAttitude(camera, valid && view.hudShowAttitude);
            float sourceScale = Mathf.Abs(attitudeGraphic.ProjectPoint(Vector2.right).x);
            attitudeCue.rectTransform.localPosition = attitudeGraphic.ProjectPoint(new Vector2(0, -440));
            attitudeCue.rectTransform.localScale = Vector3.one * sourceScale;
            attitudeCue.gameObject.SetActive(view.hudShowAttitude);
            SetText(attitudeCue, !attitudeGraphic.HorizonZoomAllowed ? "HORIZON AT 1× ONLY"
                : !valid ? "HORIZON UNAVAILABLE / NO FRESH IMU + TILT"
                : !attitudeGraphic.HorizonVisible ? "HORIZON OUT OF VIEW / CAM ATT EST" : "CAM ATT EST / TARGET + IMU");
            attitudeCue.color = valid ? Muted : Amber;
            for (int i = 0; i < pitchLabels.Length; i++)
            {
                int mark = attitudeGraphic.PitchMark(i);
                float x = (mark == 0 ? RovFpvAttitudeGraphic.HorizonHalfWidth : RovFpvAttitudeGraphic.TickHalfWidth) + 24f;
                bool projected = attitudeGraphic.TryMarkPoint(x, mark, out Vector2 point);
                bool inside = projected && Mathf.Abs(point.x) < 920 && Mathf.Abs(point.y) < 420;
                pitchLabels[i].gameObject.SetActive(attitudeGraphic.HorizonAvailable && inside);
                if (formattedPitchMarks[i] != mark)
                {
                    SetText(pitchLabels[i], mark.ToString("+0;-0;0"));
                    formattedPitchMarks[i] = mark;
                }
                pitchLabels[i].rectTransform.localPosition = attitudeGraphic.ProjectPoint(point);
                float angle = attitudeGraphic.Roll;
                if (attitudeGraphic.TryMarkPoint(x + 1f, mark, out var next))
                    angle = Mathf.Atan2(next.y - point.y, next.x - point.x) * Mathf.Rad2Deg;
                pitchLabels[i].rectTransform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                pitchLabels[i].rectTransform.localScale = Vector3.one * sourceScale;
                Color color = Ink; color.a *= 0.62f * attitudeGraphic.MarkOpacity(i);
                pitchLabels[i].color = color;
            }
        }

        private static void Place(RectTransform box, RovHudBoxLayout layout)
        {
            box.localPosition = layout.position * 1000f;
            box.localRotation = layout.Rotation;
            box.localScale = Vector3.one * layout.scale;
        }

        private void RefreshText(float now)
        {
            var tilt = view.tilt;
            bool live = tilt != null && tilt.BlueOsLive;
            bool pose = live && tilt.AttitudeLive;
            bool videoLive = stream != null && now - stream.LastFrameReceivedAt < 1f;
            SetText(connection, live ? "ROV   /   TILKOBLET" : "ROV   /   FRAKOBLET");
            linkDot.color = live ? Accent : Red;
            SetText(controlState, ControlLabel(tilt));
            controlState.color = tilt != null && tilt.CommandAccepted ? Accent : Muted;
            SetText(armed, !live ? "ARMSTATUS  ---" : tilt.HudTelemetry.Armed ? "ARMERT" : "AVARMERT");
            SetText(alert, !live ? "TELEMETRI MANGLER" : !videoLive ? "VIDEO IKKE OPPDATERT" : !pose ? "IMU IKKE OPPDATERT" : "");
            alert.color = !live || !videoLive ? Red : Amber;

            float volts = live ? tilt.HudTelemetry.Voltage(now) : float.NaN;
            float percent = live ? tilt.HudTelemetry.Remaining(now) : float.NaN;
            SetText(battery.value, Value(volts, "F1", ""));
            SetText(battery.hint, RovCameraPoseMath.Finite(percent) ? percent.ToString("F0") + " %  GJENSTÅR" : "GJENSTÅR   — %");
            battery.value.color = RovCameraPoseMath.Finite(percent) && percent <= 20f ? Amber : Ink;
            // Pilot gain, battery and light commands stay visible whenever the
            // pilot HUD is enabled. B still toggles the entire HUD.
            battery.root.gameObject.SetActive(true);
            float meters = live ? tilt.HudTelemetry.DepthEstimate(now) : float.NaN;
            SetText(depth.value, Value(meters, "F1", ""));
            SetText(depth.hint, "AHRS2  /  ESTIMAT");
            depth.root.gameObject.SetActive(true);
            depth.caption.gameObject.SetActive(view.hudShowDepth);
            depth.value.gameObject.SetActive(view.hudShowDepth);
            depth.unit.gameObject.SetActive(view.hudShowDepth);
            depth.hint.gameObject.SetActive(view.hudShowDepth);
            float gain = live ? tilt.HudTelemetry.PilotGain(now) : float.NaN;
            float light1 = live ? tilt.HudTelemetry.Light1Level(now) : float.NaN;
            float light2 = live ? tilt.HudTelemetry.Light2Level(now) : float.NaN;
            SetText(pilotGain, "PILOT GAIN  " + Percent(gain));
            SetText(lights, "LIGHT CMD  L1 " + LightLevel(light1) + " / L2 " + LightLevel(light2));

            bool mount = tilt != null && tilt.MountTelemetryLive;
            SetText(tiltIndicator.value, mount ? tilt.ReportedPitch.ToString("+0.0;-0.0;0.0") : "—");
            SetText(tiltIndicator.hint, tilt != null && tilt.AtTiltLimit ? "TILT LIMIT" : "REPORTED TARGET");
            tiltIndicator.value.color = tilt != null && tilt.AtTiltLimit ? Amber : Ink;
            tiltIndicator.root.gameObject.SetActive(view.hudShowAttitude);
            SetText(attitude.value, pose ? $"{tilt.VehicleRollPitchYaw.y:+0.0;-0.0;0.0}" : "—");
            SetText(attitude.hint, pose ? $"ROLL   {tilt.VehicleRollPitchYaw.x:+0.0;-0.0;0.0}°" : "ROLL   —");
            attitude.root.gameObject.SetActive(view.hudShowAttitude);
            SetText(diagnostics, $"APP {RenderFps:F0} FPS     VIDEO {VideoFps:F0} FPS     XR " + (HeadsetHz > 0f ? HeadsetHz.ToString("F0") + " Hz" : "---"));
            diagnostics.gameObject.SetActive(view.hudShowDiagnostics);
            Summary = (pose ? $"{Compass(tilt.VehicleRollPitchYaw.z)} {Mathf.Repeat(tilt.VehicleRollPitchYaw.z, 360f):000}°" : "KURS ---")
                + " | BATTERI " + battery.value.text + " V | DYBDE EST. " + depth.value.text + " m | " + connection.text;
        }

        public static string Compass(float yaw) => CompassPoints[Mathf.FloorToInt((Mathf.Repeat(yaw, 360f) + 22.5f) / 45f) % 8];
        private static string ControlLabel(RovHeadTiltController tilt)
        {
            if (tilt == null) return "HEAD CONTROL —";
            return CompactControlStatus(tilt.Status, tilt.RcGateStatus, tilt.CommandAccepted, tilt.HeadFollowingEnabled);
        }

        // Only known normal states are abbreviated. Refusals, RC gate errors,
        // calibration and retry/recovery instructions retain their owning text.
        public static string CompactControlStatus(string status, string rcGateStatus, bool accepted, bool following)
        {
            if (!string.IsNullOrEmpty(rcGateStatus)
                && (rcGateStatus.Contains("feilet") || rcGateStatus.StartsWith("Kunne ikke"))) return rcGateStatus;
            if (accepted && (status == "VR-tilt: fersk ACK (ikke fysisk feedback)" || status == "Hodefølging av; kameratilt låst"))
                return following ? "CAM ACK / HEAD FOLLOW" : "CAM ACK / HOLD";
            return string.IsNullOrEmpty(status) ? "HEAD CONTROL —" : status;
        }
        private static string Value(float value, string format, string unit) => RovCameraPoseMath.Finite(value) ? value.ToString(format) + unit : "---";
        private static string Percent(float value) => RovCameraPoseMath.Finite(value) ? (value * 100f).ToString("F0") + "%" : "—";
        private static string LightLevel(float value) => RovCameraPoseMath.Finite(value) && value == 0f ? "OFF" : Percent(value);
        private static void SetText(Text label, string text) { if (label.text != text) label.text = text; }

        // Shared with the editor composition regression so it exercises the
        // same node/components that the pilot actually renders.
        public static RovFpvAttitudeGraphic CreateAttitudeGraphic(Transform parent, Material vectorMaterial)
        {
            var node = Node("Reticle and estimated camera attitude", parent, 0, 0, 8000, 8000);
            var graphic = node.gameObject.AddComponent<RovFpvAttitudeGraphic>();
            graphic.material = vectorMaterial; graphic.raycastTarget = false;
            return graphic;
        }

        private static RectTransform Node(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private Text Label(string name, Transform parent, float x, float y, float width, float height, int size, TextAnchor alignment, Color color)
        {
            var label = Node(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = color; label.material = material;
            label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            textElements.Add(label); fontSizes.Add(size);
            return label;
        }

        private void Rule(Transform parent, float x, float y, float width, float height)
        {
            var line = Node("Divider", parent, x, y, width, height).gameObject.AddComponent<Image>();
            line.material = vectorMaterial; line.color = new Color(0.43f, 0.61f, 0.72f, 0.28f); line.raycastTarget = false;
        }

        private Sprite SpriteFor(RovHudArt.Kind kind)
        {
            if (!hudSprites.TryGetValue(kind, out var sprite)) { sprite = RovHudArt.Create(kind); hudSprites.Add(kind, sprite); }
            return sprite;
        }

        private Image Symbol(string name, Transform parent, float x, float y, RovHudArt.Kind kind)
        {
            var symbol = Node(name, parent, x, y, 32, 32).gameObject.AddComponent<Image>();
            symbol.sprite = SpriteFor(kind); symbol.color = Accent; symbol.material = vectorMaterial; symbol.raycastTarget = false;
            return symbol;
        }

        private Metric Instrument(string title, string unit)
        {
            var rect = Node(title, group.transform, 0, 0, 300, 112);
            var metric = new Metric { root = rect };
            metric.caption = Label("Caption", rect, 0, 45, 280, 28, 21, TextAnchor.MiddleLeft, Muted);
            metric.caption.text = title;
            metric.value = Label("Value", rect, -24, 0, 230, 58, 46, TextAnchor.MiddleLeft, Ink);
            metric.value.fontStyle = FontStyle.Bold;
            metric.unit = Label("Unit", rect, 121, -5, 40, 32, 26, TextAnchor.MiddleRight, Ink);
            metric.unit.text = unit;
            metric.hint = Label("Detail", rect, 0, -42, 280, 25, 19, TextAnchor.MiddleLeft, Muted);
            return metric;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdateAnchorPose;
            if (root != null) root.gameObject.SetActive(false);
            compass?.SetVisible(false);
        }
        private void OnEnable()
        {
            Application.onBeforeRender += UpdateAnchorPose;
            if (root != null) root.gameObject.SetActive(true);
        }
        private void OnDestroy()
        {
            Application.onBeforeRender -= UpdateAnchorPose;
            compass?.Dispose();
            if (root != null) Destroy(root.gameObject);
            if (material != null) Destroy(material);
            if (vectorMaterial != null) Destroy(vectorMaterial);
            foreach (var sprite in hudSprites.Values) { Destroy(sprite.texture); Destroy(sprite); }
        }
    }
}
