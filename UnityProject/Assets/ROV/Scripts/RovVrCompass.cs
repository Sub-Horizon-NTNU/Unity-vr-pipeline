using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ROVVR
{
    // A horizontal ring below eye level, independent of head rotation.
    // It follows translation only. Turning the ROV turns the compass markings,
    // not the video surface, with a fixed forward index at the X reference.
    public sealed class RovVrCompass
    {
        private readonly RovVideoViewCalibration view;
        private readonly Transform anchor, ring;
        private readonly Mesh mesh;
        private readonly Text readout;
        private readonly List<Transform> labels = new List<Transform>();
        private readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        private float oldOpacity = -1f;
        private float oldTilt = float.NaN, nextReadout;

        public RovVrCompass(RovVideoViewCalibration view, Material material, Font font)
        {
            this.view = view;
            anchor = new GameObject("ROV compass around viewer (runtime)").transform;
            ring = new GameObject("Heading ring", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            ring.SetParent(anchor, false);
            mesh = new Mesh { name = "360 degree compass ticks" };
            ring.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ring.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            for (int degrees = 0; degrees < 360; degrees += 30)
            {
                string text = degrees % 90 == 0 ? RovVrOverlay.Compass(degrees) : degrees.ToString("000");
                var label = MakeLabel(text, ring, material, font, degrees % 90 == 0 ? 44 : 32);
                label.transform.parent.localPosition = BearingDirection(degrees, 0f) * 1.12f + Vector3.up * 0.055f;
            }
            readout = MakeLabel("KURS ---", anchor, material, font, 38);
            readout.transform.parent.localPosition = new Vector3(0f, 0.10f, 0.86f);
        }

        public static Vector3 BearingDirection(float bearing, float vehicleYaw) => Quaternion.AngleAxis(bearing - vehicleYaw, Vector3.up) * Vector3.forward;

        public void Update()
        {
            bool fresh = view.tilt != null && view.tilt.AttitudeLive;
            SetVisible(view.hudEnabled && view.hudShowCompass && view.IsCentered);
            if (!anchor.gameObject.activeSelf) return;
            anchor.SetPositionAndRotation(view.HudAnchorPosition - Vector3.up * Mathf.Clamp(view.compassDrop, 0.4f, 1.8f), view.CenteredHeading);
            anchor.localScale = Vector3.one * Mathf.Clamp(view.compassRadius, 1f, 3f);
            // Hide stale cardinal markings rather than showing an old heading
            // as if it were live. The forward readout remains a visible warning.
            ring.gameObject.SetActive(fresh);
            if (fresh)
            {
                float yaw = view.tilt.VehicleRollPitchYaw.z;
                ring.localRotation = Quaternion.AngleAxis(-yaw, Vector3.up);
                if (Time.realtimeSinceStartup >= nextReadout)
                {
                    string text = $"▼  {Mathf.Repeat(yaw, 360f):000}°  {RovVrOverlay.Compass(yaw)}";
                    if (readout.text != text) readout.text = text;
                    nextReadout = Time.realtimeSinceStartup + 0.2f;
                }
            }
            else readout.text = "KURS --- / IMU";
            float localEyeHeight = view.compassDrop / view.compassRadius;
            if (oldTilt != localEyeHeight)
            {
                // The viewer stays over the ring centre. Billboard orientation
                // is constant in ring space; head turns must not dirty 13 canvases.
                foreach (var label in labels)
                    label.localRotation = Quaternion.LookRotation(label.localPosition - Vector3.up * localEyeHeight, Vector3.up);
                oldTilt = localEyeHeight;
            }
            float opacity = Mathf.Clamp(view.hudOpacity, 0.2f, 1f);
            if (oldOpacity != opacity)
            {
                foreach (var group in groups) group.alpha = opacity;
                BuildMesh(opacity); oldOpacity = opacity;
            }
        }

        private Text MakeLabel(string text, Transform parent, Material material, Font font, int size)
        {
            var canvasGo = new GameObject("Compass " + text, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            var rect = (RectTransform)canvasGo.transform; rect.sizeDelta = new Vector2(360f, 85f);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = view.eye.GetComponent<Camera>(); canvas.sortingOrder = 101;
            var group = canvasGo.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false; groups.Add(group);
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(rect, false);
            var textRect = (RectTransform)textGo.transform; textRect.sizeDelta = rect.sizeDelta;
            var label = textGo.GetComponent<Text>(); label.text = text; label.font = font; label.fontSize = size;
            label.material = material; label.alignment = TextAnchor.MiddleCenter; label.color = new Color(0.8f, 1f, 0.96f);
            label.raycastTarget = false;
            var shadow = textGo.AddComponent<Shadow>(); shadow.effectColor = new Color(0f, 0f, 0f, 0.9f); shadow.effectDistance = new Vector2(1.5f, -1.5f);
            labels.Add(rect); return label;
        }

        private void BuildMesh(float opacity)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < 180; i++)
            {
                float a = i * Mathf.PI * 2f / 180f, b = (i + 1) * Mathf.PI * 2f / 180f;
                Quad(vertices, triangles, Point(a, 0.995f), Point(a, 1.005f), Point(b, 1.005f), Point(b, 0.995f));
            }
            for (int degrees = 0; degrees < 360; degrees += 5)
            {
                float a = degrees * Mathf.Deg2Rad;
                float length = degrees % 90 == 0 ? 0.12f : degrees % 30 == 0 ? 0.085f : 0.04f;
                Quad(vertices, triangles, Point(a - 0.0025f, 1f - length), Point(a - 0.0025f, 1f), Point(a + 0.0025f, 1f), Point(a + 0.0025f, 1f - length));
            }
            var colors = new Color[vertices.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.7f, 1f, 0.93f, opacity * 0.6f);
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.colors = colors; mesh.RecalculateBounds();
        }

        private static Vector3 Point(float angle, float radius) => new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        private static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }
        public void SetVisible(bool visible) { if (anchor != null) anchor.gameObject.SetActive(visible); }
        public void Dispose() { if (anchor != null) Object.Destroy(anchor.gameObject); if (mesh != null) Object.Destroy(mesh); }
    }
}
