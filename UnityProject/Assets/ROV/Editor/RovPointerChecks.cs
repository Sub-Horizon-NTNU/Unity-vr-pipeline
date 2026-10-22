using System;
using System.Reflection;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovPointerChecks
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        public static int Run()
        {
            int checks = 0;
            Vector3 raw = Quaternion.Euler(-20f, 30f, 0f) * Vector3.forward;
            Vector3 normal = RovPointerMath.Direction(Quaternion.Euler(-20f, 30f, 0f), Quaternion.identity, Quaternion.identity, 1, 1, 0, 0);
            Expect(Vector3.Angle(raw, normal) < 0.01f, "Unity aim direction must be preserved at 1x"); checks++;
            Vector3 calm = RovPointerMath.Direction(Quaternion.Euler(-20f, 30f, 0f), Quaternion.identity, Quaternion.identity, 1.2f, 0.5f, 0, 0);
            Near(Mathf.Atan2(calm.y, new Vector2(calm.x, calm.z).magnitude) * Mathf.Rad2Deg, 10);
            Near(Mathf.Atan2(calm.x, calm.z) * Mathf.Rad2Deg, 36); checks++;
            var neutral = Quaternion.Euler(25f, 45f, 0f); var desired = Quaternion.Euler(-5f, 60f, 0f);
            Vector3 centered = RovPointerMath.Direction(neutral, neutral, desired, 1.2f, 0.8f, 0, 0);
            Expect(Vector3.Angle(centered, desired * Vector3.forward) < 0.01f, "Comfortable hand pose must recenter to gaze"); checks++;
            var correctedReference = Quaternion.LookRotation(neutral * Quaternion.Euler(-12f, 4f, 0f) * Vector3.forward, Vector3.up);
            Vector3 withOffset = RovPointerMath.Direction(neutral, correctedReference, desired, 1.2f, 0.8f, 12f, 4f);
            Expect(Vector3.Angle(withOffset, desired * Vector3.forward) < 0.01f, "Recentering must account for configured aim offsets"); checks++;
            Expect(RovPointerMath.Stick(new Vector2(0.1f, 0.1f)) == Vector2.zero, "Stick drift must not move window"); checks++;
            Near(RovPointerMath.Stick(Vector2.up).y, 1); checks++;
            var profile = new RovVideoViewProfile();
            profile.pilot.pointerPitchSensitivity = 0.6f; profile.pilot.pointerPitchOffset = 12f;
            profile.pilot.menuSide = -0.4f; profile.pilot.windowMoveSpeed = 0.8f;
            var copy = RovVideoViewProfile.Parse(JsonUtility.ToJson(profile));
            Near(copy.pilot.pointerPitchSensitivity, 0.6f); Near(copy.pilot.pointerPitchOffset, 12);
            Near(copy.pilot.menuSide, -0.4f); Near(copy.pilot.windowMoveSpeed, 0.8f); checks++;
            var legacy = RovVideoViewProfile.Parse("{\"pilot\":{\"mainZoom\":2}}");
            Near(legacy.pilot.pointerPitchSensitivity, 0.8f); Expect(legacy.pilot.windowDraggingEnabled, "Old profiles must enable window moving"); checks++;
            bool invalid = false;
            try { new RovPilotSettings { pointerPitchSensitivity = float.NaN }.Validate(); } catch (ArgumentException) { invalid = true; }
            Expect(invalid, "Non-finite pointing settings must be rejected"); checks++;
            var go = new GameObject("Pointer drag regression (no control traffic)"); go.SetActive(false);
            var window = new GameObject("Pointer test window", typeof(RectTransform));
            var detail = new GameObject("Pointer test detail", typeof(RectTransform), typeof(Canvas));
            var eye = new GameObject("Pointer test eye");
            RovVideoViewCalibration view = null;
            try
            {
                view = go.AddComponent<RovVideoViewCalibration>(); view.eye = eye.transform;
                var tilt = go.AddComponent<RovHeadTiltController>(); view.tilt = tilt;
                Set(tilt, "calibrated", true); Set(tilt, "canSend", true); Set(tilt, "neutralPitch", 8f); Set(tilt, "firstCommandAt", 42f);
                var root = (RectTransform)window.transform; root.sizeDelta = new Vector2(900, 930); root.localScale = Vector3.one * 0.0011f;
                root.position = Vector3.forward * 1.25f;
                Set(view, "pilotMenuRoot", root); Set(view, "pilotMenuOpen", true);
                Ray ray = new Ray(Vector3.zero, new Vector3(0.2f, 0.1f, 1.25f).normalized);
                Expect(RovPointerMath.Hit(root, ray, out _, out _), "Pointer must hit scaled menu"); checks++;
                Expect(!RovPointerMath.Hit(root, new Ray(Vector3.zero, Vector3.back), out _, out _), "Do not interact behind pointer"); checks++;
                Drag(view, false, true, false, Vector2.zero, ray);
                Vector3 initial = root.position;
                Drag(view, true, true, false, Vector2.zero, ray);
                Expect(Vector3.Distance(initial, root.position) < 0.0001f, "Grabbing away from center must not snap window"); checks++;
                Drag(view, true, true, false, Vector2.right, ray);
                Near(root.position.x, 0.03f); checks++;
                Drag(view, true, true, true, Vector2.up, ray);
                Near(root.position.z, 1.28f); Near(root.position.y, 0); checks++;
                Vector3 moved = root.position;
                Drag(view, false, true, false, Vector2.right, ray);
                Expect(Vector3.Distance(moved, root.position) < 0.0001f, "Release must stop window movement"); checks++;
                Drag(view, true, false, false, Vector2.right, ray);
                Drag(view, true, true, false, Vector2.right, ray);
                Expect(Vector3.Distance(moved, root.position) < 0.0001f, "Tracking recovery must not replay held grab"); checks++;
                Expect((bool)Get(tilt, "calibrated") && (bool)Get(tilt, "canSend") && (float)Get(tilt, "neutralPitch") == 8f
                    && (float)Get(tilt, "firstCommandAt") == 42f && tilt.CommandsSent == 0,
                    "Dragging must preserve head calibration and control session"); checks++;
                var saved = RovVideoViewProfile.Parse(JsonUtility.ToJson(view.GetProfile()));
                Near(saved.pilot.menuSide, root.position.x); Near(saved.pilot.menuDistance, root.position.z); checks++;
                // Detail placement must be converted into the moving HUD frame, not world axes.
                Set(view, "pilotMenuOpen", false); Set(view, "detailCanvas", detail.GetComponent<Canvas>());
                view.pilot.detailEnabled = true; view.hudAnchorMode = RovHudAnchorMode.CenteredHeading;
                eye.transform.position = new Vector3(1f, 1.5f, 2f);
                Set(view, "heading", Quaternion.Euler(0, 65, 0));
                var dr = (RectTransform)detail.transform; dr.sizeDelta = new Vector2(800, 450); dr.localScale = Vector3.one * 0.001f;
                dr.SetPositionAndRotation(view.PilotHudPosition + view.PilotHudRotation * new Vector3(0.8f, 0.1f, 2f), view.PilotHudRotation);
                Ray detailRay = new Ray(eye.transform.position, (dr.position - eye.transform.position).normalized);
                Drag(view, false, true, false, Vector2.zero, detailRay);
                Drag(view, true, true, false, Vector2.zero, detailRay);
                Near(view.pilot.detailPosition.x, 0.8f); Near(view.pilot.detailPosition.y, 0.1f); Near(view.pilot.detailPosition.z, 2f); checks++;
                Drag(view, true, true, false, Vector2.right, detailRay);
                Near(view.pilot.detailPosition.x, 0.83f); Near(view.pilot.detailPosition.z, 2f); checks++;
            }
            finally
            {
                if (view != null) { Set(view, "pilotMenuRoot", null); Set(view, "detailCanvas", null); }
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(detail); UnityEngine.Object.DestroyImmediate(eye);
            }
            return checks;
        }
        private static void Drag(RovVideoViewCalibration view, bool held, bool ready, bool depth, Vector2 stick, Ray ray)
            => typeof(RovVideoViewCalibration).GetMethod("ProcessWindowDrag", Fields).Invoke(view, new object[] {held, ready, depth, stick, ray, 0.05f});
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        private static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
        private static void Near(float actual, float expected) => Expect(Mathf.Abs(actual - expected) < 0.001f, $"Expected {expected}, got {actual}");
        private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException("Pointer interaction: " + message); }
    }
}
