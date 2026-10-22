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
        private RectTransform statusRoot;
        private Text connection, armed, alert, diagnostics;
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
        private static readonly Color Ink = new Color(0.94f, 0.97f, 1f, 1f);
        private static readonly Color Muted = new Color(0.57f, 0.67f, 0.77f, 1f);
        private static readonly Color Accent = new Color(0.31f, 0.84f, 0.95f, 1f);
        private static readonly Color Amber = new Color(1f, 0.73f, 0.28f, 1f);
        private static readonly Color Red = new Color(1f, 0.38f, 0.32f, 1f);
        private static readonly string[] CompassPoints = {"N", "NØ", "Ø", "SØ", "S", "SV", "V", "NV"};

        private sealed class Metric
        {
            public RectTransform root;
            public Text value, hint, unit;
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
            Card("Status capsule", statusRoot, 1060, 66);
            linkDot = Symbol("Link light", statusRoot, -500, 0, RovHudArt.Kind.Dot);
            connection = Label("Connection", statusRoot, -315, 0, 325, 36, 20, TextAnchor.MiddleLeft, Ink);
            Rule(statusRoot, -132, 0, 1, 24);
            controlState = Label("Head control", statusRoot, 70, 0, 360, 36, 19, TextAnchor.MiddleCenter, Muted);
            Rule(statusRoot, 284, 0, 1, 24);
            armed = Label("Arming state", statusRoot, 406, 0, 196, 36, 19, TextAnchor.MiddleRight, Muted);
            alert = Label("Pilot alert", statusRoot, 0, -58, 950, 40, 22, TextAnchor.MiddleCenter, Amber);
            battery = Instrument("B A T T E R I", "V", RovHudArt.Kind.Battery);
            depth = Instrument("D Y B D E  ·  E S T.", "m", RovHudArt.Kind.Depth);
            tiltIndicator = Instrument("K A M E R A", "°", RovHudArt.Kind.Camera);
            attitude = Instrument("R O V  ·  P I T C H", "°", RovHudArt.Kind.Attitude);
            diagnostics = Label("Diagnostics", group.transform, 0, -460, 960, 50, 20, TextAnchor.MiddleCenter, Muted);

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
            SetText(controlState, tilt == null ? "HODESTYRING  ---" : tilt.Status.ToUpperInvariant());
            controlState.color = tilt != null && tilt.CommandAccepted ? Accent : Muted;
            SetText(armed, !live ? "ARMSTATUS  ---" : tilt.HudTelemetry.Armed ? "ARMERT" : "AVARMERT");
            SetText(alert, !live ? "TELEMETRI MANGLER" : !videoLive ? "VIDEO IKKE OPPDATERT" : !pose ? "IMU IKKE OPPDATERT" : "");
            alert.color = !live || !videoLive ? Red : Amber;

            float volts = live ? tilt.HudTelemetry.Voltage(now) : float.NaN;
            float percent = live ? tilt.HudTelemetry.Remaining(now) : float.NaN;
            SetText(battery.value, Value(volts, "F1", ""));
            SetText(battery.hint, RovCameraPoseMath.Finite(percent) ? percent.ToString("F0") + " %  GJENSTÅR" : "GJENSTÅR   — %");
            battery.value.color = RovCameraPoseMath.Finite(percent) && percent <= 20f ? Amber : Ink;
            battery.root.gameObject.SetActive(view.hudShowBattery);
            float meters = live ? tilt.HudTelemetry.DepthEstimate(now) : float.NaN;
            SetText(depth.value, Value(meters, "F1", ""));
            SetText(depth.hint, "AHRS2  /  ESTIMAT");
            depth.root.gameObject.SetActive(view.hudShowDepth);

            bool mount = tilt != null && tilt.MountTelemetryLive;
            SetText(tiltIndicator.value, mount ? tilt.ReportedPitch.ToString("+0.0;-0.0;0.0") : "—");
            SetText(tiltIndicator.hint, tilt != null && tilt.AtTiltLimit ? "TILTGRENSE"
                : tilt == null ? "—" : $"OMRÅDE   {tilt.MinimumPitch:F0}°  /  +{tilt.MaximumPitch:F0}°");
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
        private static string Value(float value, string format, string unit) => RovCameraPoseMath.Finite(value) ? value.ToString(format) + unit : "---";
        private static void SetText(Text label, string text) { if (label.text != text) label.text = text; }

        private RectTransform Node(string name, Transform parent, float x, float y, float width, float height)
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
            textElements.Add(label); fontSizes.Add(size);
            return label;
        }

        private void Card(string name, Transform parent, float width, float height)
        {
            var edge = Node(name + " edge", parent, 0, 0, width, height).gameObject.AddComponent<Image>();
            edge.sprite = SpriteFor(RovHudArt.Kind.Card); edge.type = Image.Type.Sliced;
            edge.material = vectorMaterial; edge.color = new Color(0.4f,0.6f,0.73f,0.40f); edge.raycastTarget = false;
            var card = Node(name, parent, 0, 0, width - 2f, height - 2f).gameObject.AddComponent<Image>();
            card.sprite = edge.sprite; card.type = Image.Type.Sliced;
            card.color = new Color(0.025f,0.045f,0.075f,0.94f); card.material = vectorMaterial; card.raycastTarget = false;
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

        private Metric Instrument(string title, string unit, RovHudArt.Kind kind)
        {
            var rect = Node(title, group.transform, 0, 0, 320, 182);
            Card("Instrument card", rect, 320, 182);
            var metric = new Metric { root = rect };
            Symbol("Instrument icon", rect, -125, 56, kind);
            Label("Caption", rect, 26, 56, 240, 30, 16, TextAnchor.MiddleLeft, Muted).text = title;
            Rule(rect, 0, 33, 268, 1);
            metric.value = Label("Value", rect, -27, -7, 222, 76, 56, TextAnchor.MiddleLeft, Ink);
            metric.value.fontStyle = FontStyle.Bold;
            metric.unit = Label("Unit", rect, 119, -17, 40, 40, 24, TextAnchor.MiddleRight, Accent);
            metric.unit.text = unit;
            metric.hint = Label("Detail", rect, 0, -62, 268, 28, 16, TextAnchor.MiddleLeft, Muted);
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
