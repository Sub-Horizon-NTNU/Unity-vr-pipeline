using UnityEngine;
using UnityEngine.UI;

namespace ROVVR
{
    // Source-image coordinates are projected onto the actual video surface.
    // The optical ray model uses the configured display FOV as an estimate;
    // physical camera intrinsics and mount feedback are not available.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RovFpvAttitudeGraphic : MaskableGraphic
    {
        private float roll, pitch;
        private Vector3 gravity = Vector3.up;
        private bool poseAvailable;
        private float fov = 110f, curve = 45f;
        private bool cylindrical = true;
        private float mainZoom = 1f;
        public const float HorizonHalfWidth = 235f, TickHalfWidth = 95f, ReticleHalfWidth = 28f;
        private const float DecorationOpacity = 0.62f;
        private int firstMark = -10, secondMark = 10;
        public static readonly Color Stroke = new Color(0.36f, 1f, 0.16f, 1f);
        private static readonly Color Outline = new Color(0f, 0f, 0f, 0.9f);
        public override Texture mainTexture => Texture2D.whiteTexture;
        public bool PoseAvailable => poseAvailable;
        public bool HorizonZoomAllowed => RovCameraPoseMath.Finite(mainZoom) && Mathf.Abs(mainZoom - 1f) <= 0.001f;
        public bool HorizonAvailable => poseAvailable && HorizonZoomAllowed;
        public float Roll => roll;
        public float Pitch => pitch;
        public int GeneratedVertices { get; private set; }
        public int MarkCount => 3;
        public int PitchMark(int index) => index == 0 ? 0 : index == 1 ? firstMark : secondMark;
        public float MarkOpacity(int index) => index == 0 ? 1f
            : 0.25f + 0.75f * Mathf.Clamp01((10f - Mathf.Abs(PitchMark(index) - pitch)) / 10f);
        public bool HorizonVisible
        {
            get
            {
                return HorizonAvailable && TryMarkPoint(-HorizonHalfWidth, 0, out var a)
                    && TryMarkPoint(HorizonHalfWidth, 0, out var b) && ClipToVideo(ref a, ref b);
            }
        }

        // Until the estimated optical cue shares the video's crop mapping,
        // zoomed views retain the reticle but must not show unregistered bars.
        public void SetMainZoom(float zoom)
        {
            if (mainZoom == zoom) return;
            mainZoom = zoom; SetVerticesDirty();
        }

        public void SetProjection(float nextFov, float nextCurve, bool nextCylindrical)
        {
            if (fov == nextFov && curve == nextCurve && cylindrical == nextCylindrical) return;
            fov = nextFov; curve = nextCurve; cylindrical = nextCylindrical;
            SetVerticesDirty();
        }

        // A pinhole ray r=(x/f,y/f,1) has elevation mark when
        // dot(normalize(r), camera-local gravity)=sin(mark). Solve in the
        // vertical plane of each lateral sample, including roll and perspective.
        public static bool TryPitchPoint(Vector3 up, float x, float mark, float horizontalFov, out Vector2 point)
        {
            point = Vector2.zero;
            up.Normalize();
            float h = new Vector2(up.x, up.y).magnitude;
            if (h < 0.0001f) return false;
            float focal = 960f / Mathf.Tan(horizontalFov * Mathf.Deg2Rad * 0.5f);
            float baseLength = Mathf.Sqrt(x * x + focal * focal);
            float forwardUp = up.z * focal / baseLength;
            float amplitude = Mathf.Sqrt(h * h + forwardUp * forwardUp);
            float target = Mathf.Sin(mark * Mathf.Deg2Rad) / amplitude;
            if (Mathf.Abs(target) > 1f) return false;
            float phase = Mathf.Atan2(forwardUp, h);
            float principal = Mathf.Asin(target);
            float first = Mathf.DeltaAngle(0, (principal - phase) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float second = Mathf.DeltaAngle(0, (Mathf.PI - principal - phase) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float angle = Mathf.Abs(first) <= Mathf.Abs(second) ? first : second;
            if (Mathf.Abs(angle) >= Mathf.PI * 0.5f - 0.0001f) return false;
            Vector2 vertical = new Vector2(up.x, up.y) / h;
            Vector2 right = new Vector2(vertical.y, -vertical.x);
            point = right * x + vertical * (baseLength * Mathf.Tan(angle));
            return RovCameraPoseMath.Finite(point.x) && RovCameraPoseMath.Finite(point.y);
        }

        public bool TryMarkPoint(float x, float mark, out Vector2 point) => TryPitchPoint(gravity, x, mark, fov, out point);

        public Vector3 ProjectPoint(Vector2 source) => RovHudSurfaceGraphic.SurfacePoint(
            source.x, source.y, fov, curve, cylindrical) * 1000f;

        public void SetAttitude(float nextRoll, float nextPitch, bool available)
        {
            SetCameraAttitude(Quaternion.AngleAxis(-nextPitch, Vector3.right)
                * Quaternion.AngleAxis(-nextRoll, Vector3.forward), available);
        }

        public void SetCameraAttitude(Quaternion camera, bool available)
        {
            Vector3 nextGravity = Quaternion.Inverse(camera) * Vector3.up;
            if (Vector3.Angle(gravity, nextGravity) < 0.05f && available == poseAvailable) return;
            gravity = nextGravity.normalized; poseAvailable = available;
            roll = Mathf.Atan2(-gravity.x, gravity.y) * Mathf.Rad2Deg;
            pitch = RovHeadTiltMath.Pitch(camera);
            // A one-degree selection margin prevents a pair of equally near
            // reference marks flickering. Their geometry still follows every
            // current pose sample; this does not filter or delay the pose.
            for (int pass = 0; pass < 2; pass++)
            {
                bool replaceFirst = Mathf.Abs(firstMark - pitch) > Mathf.Abs(secondMark - pitch);
                int worst = replaceFirst ? firstMark : secondMark;
                int best = worst;
                for (int mark = -80; mark <= 80; mark += 10)
                    if (mark != 0 && mark != firstMark && mark != secondMark
                        && Mathf.Abs(mark - pitch) + 1f < Mathf.Abs(best - pitch)) best = mark;
                if (replaceFirst) firstMark = best; else secondMark = best;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // A fixed, open center mark leaves the actual target visible.
            StrokeLine(mesh, new Vector2(-ReticleHalfWidth, 0), new Vector2(-9, 0), 2f);
            StrokeLine(mesh, new Vector2(9, 0), new Vector2(ReticleHalfWidth, 0), 2f);
            StrokeLine(mesh, new Vector2(-9, 0), new Vector2(0, -4.5f), 2f);
            StrokeLine(mesh, new Vector2(0, -4.5f), new Vector2(9, 0), 2f);
            if (HorizonAvailable) for (int index = 0; index < MarkCount; index++)
            {
                int mark = PitchMark(index);
                float outer = mark == 0 ? HorizonHalfWidth : TickHalfWidth;
                float weight = MarkOpacity(index);
                MarkLine(mesh, -outer, -32f, mark, weight);
                MarkLine(mesh, 32f, outer, mark, weight);
            }
            GeneratedVertices = mesh.currentVertCount;
        }

        private void MarkLine(VertexHelper mesh, float from, float to, int mark, float weight)
        {
            // Sample the pinhole curve as well as the curved display surface.
            int pieces = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / 24f));
            for (int i = 0; i < pieces; i++)
                if (TryMarkPoint(Mathf.Lerp(from, to, (float)i / pieces), mark, out var a)
                    && TryMarkPoint(Mathf.Lerp(from, to, (float)(i + 1) / pieces), mark, out var b))
                    StrokeLine(mesh, a, b, 1.7f, weight);
        }

        private void StrokeLine(VertexHelper mesh, Vector2 a, Vector2 b, float width, float weight = 1f)
        {
            Color outline = Outline, stroke = Stroke;
            outline.a *= DecorationOpacity * weight; stroke.a *= DecorationOpacity * weight;
            Line(mesh, a, b, width + 1.5f, outline);
            Line(mesh, a, b, width, stroke);
        }

        private void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            if (!ClipToVideo(ref a, ref b)) return;
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
            // A long straight quad cuts through a curved video. Subdivision
            // keeps every part at its corresponding source-image coordinate.
            int segments = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 32f));
            for (int i = 0; i < segments; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)i / segments);
                Vector2 q = Vector2.Lerp(a, b, (float)(i + 1) / segments);
                int start = mesh.currentVertCount;
                mesh.AddVert(ProjectPoint(p - normal), color, new Vector2(0.5f, 0.5f));
                mesh.AddVert(ProjectPoint(p + normal), color, new Vector2(0.5f, 0.5f));
                mesh.AddVert(ProjectPoint(q + normal), color, new Vector2(0.5f, 0.5f));
                mesh.AddVert(ProjectPoint(q - normal), color, new Vector2(0.5f, 0.5f));
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static bool ClipToVideo(ref Vector2 a, ref Vector2 b)
        {
            Vector2 d = b - a;
            float start = 0f, end = 1f;
            if (!Clip(-d.x, a.x + 930f, ref start, ref end)
                || !Clip(d.x, 930f - a.x, ref start, ref end)
                || !Clip(-d.y, a.y + 475f, ref start, ref end)
                || !Clip(d.y, 475f - a.y, ref start, ref end)) return false;
            b = a + d * end; a += d * start;
            return true;
        }

        private static bool Clip(float direction, float distance, ref float start, ref float end)
        {
            if (Mathf.Abs(direction) < 0.00001f) return distance >= 0f;
            float t = distance / direction;
            if (direction < 0f) { if (t > end) return false; start = Mathf.Max(start, t); }
            else { if (t < start) return false; end = Mathf.Min(end, t); }
            return true;
        }
    }
}
