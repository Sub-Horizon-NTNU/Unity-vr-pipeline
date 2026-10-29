using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

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
            Expect(!RovPointerMath.FullyTracked(InputTrackingState.Rotation)
                && RovPointerMath.FullyTracked(InputTrackingState.Position | InputTrackingState.Rotation),
                "Rotation-only prediction must never count as a valid UI pointer"); checks++;
            Expect(!RovPointerMath.PoseValid(new Vector3(float.NaN, 0, 0), Quaternion.identity)
                && !RovPointerMath.PoseValid(Vector3.zero, new Quaternion(0, 0, 0, 0))
                && RovPointerMath.PoseValid(Vector3.zero, Quaternion.identity), "Reject corrupt poses without rejecting a real origin pose"); checks++;
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
                Vector3 expected = new Vector3(0.8f, 0.1f, 2f) + Quaternion.Inverse(view.PilotHudRotation) * (Vector3.right * 0.03f);
                Expect(Vector3.Distance(view.pilot.detailPosition, expected) < 0.001f, "Movement axes must be viewer-level, independent of window yaw"); checks++;
                Set(view, "pilotMenuOpen", true); Drag(view, false, true, false, Vector2.zero, ray);
                Drag(view, true, true, false, Vector2.zero, ray);
                Vector3 beforeRotation = root.position;
                typeof(RovVideoViewCalibration).GetMethod("ProcessWindowRotation", Fields).Invoke(view, new object[] {Vector2.right, 0.05f});
                Near(view.pilot.menuAngles.y, 2.25f); Expect(Vector3.Distance(root.position, beforeRotation) < 0.001f, "Rotation must keep window center fixed"); checks++;
                typeof(RovVideoViewCalibration).GetMethod("ProcessWindowRotation", Fields).Invoke(view, new object[] {Vector2.up, 0.05f});
                Near(view.pilot.menuAngles.x, -2.25f); checks++;
                Drag(view, true, true, false, Vector2.zero, new Ray(eye.transform.position, Vector3.down));
                Expect(Vector3.Distance(root.position, beforeRotation) < 0.001f, "Hand pitch/yaw must not move a stick-controlled window"); checks++;
                Drag(view, true, true, false, Vector2.up, ray);
                Near(root.position.y - beforeRotation.y, 0.03f); Near(root.position.x, beforeRotation.x); checks++;
                var rotationProfile = RovVideoViewProfile.Parse(JsonUtility.ToJson(view.GetProfile()));
                Near(rotationProfile.pilot.menuAngles.x, -2.25f); Near(rotationProfile.pilot.menuAngles.y, 2.25f);
                Expect(!rotationProfile.pilot.windowFollowHand, "Stable sticks-only mode must persist"); checks++;

                // The startup chooser moves independently, even if the saved profile disables window dragging.
                Drag(view, false, true, false, Vector2.zero, ray);
                Set(view, "startupPending", true); tilt.StartupSelectionPending = true;
                view.pilot.windowDraggingEnabled = false;
                Vector2 savedMenuAngles = view.pilot.menuAngles;
                Vector3 savedMenuPosition = new Vector3(view.pilot.menuSide, view.pilot.menuHeight, view.pilot.menuDistance);
                typeof(RovVideoViewCalibration).GetMethod("PositionPilotMenu", Fields).Invoke(view, null);
                Ray startupRay = new Ray(eye.transform.position, (root.position - eye.transform.position).normalized);
                initial = root.position;
                Drag(view, true, true, false, Vector2.right, startupRay);
                Expect(root.position.x > initial.x + 0.02f, "Startup profile chooser must be movable before Start"); checks++;
                typeof(RovVideoViewCalibration).GetMethod("ProcessWindowRotation", Fields).Invoke(view, new object[] {Vector2.one, 0.05f});
                Expect(Quaternion.Angle(root.rotation, Quaternion.identity) > 1f && tilt.StartupSelectionPending
                    && savedMenuAngles == view.pilot.menuAngles && savedMenuPosition == new Vector3(view.pilot.menuSide, view.pilot.menuHeight, view.pilot.menuDistance),
                    "Moving/rotating startup UI must preserve saved placement and keep servo gated"); checks++;
                moved = root.position;
                typeof(RovVideoViewCalibration).GetMethod("StraightenDraggedWindow", Fields).Invoke(view, null);
                Expect(Quaternion.Angle(root.rotation, Quaternion.identity) < 0.01f && Vector3.Distance(root.position, moved) < 0.0001f
                    && view.pilot.menuAngles == savedMenuAngles, "Straightening a grabbed chooser must keep its position and saved angles"); checks++;
                Drag(view, false, true, false, Vector2.zero, startupRay);
                eye.transform.rotation = Quaternion.Euler(20f, 37f, 12f);
                view.RecoverPilotMenu();
                Expect(Mathf.Abs(root.forward.y) < 0.0001f && Mathf.Abs(root.right.y) < 0.0001f
                    && Mathf.Abs(Vector3.Distance(root.position, eye.transform.position) - new Vector3(0, -0.05f, 1.25f).magnitude) < 0.0001f,
                    "Recovery must place menu upright at a reachable distance regardless of head pitch/roll"); checks++;
                Expect(tilt.StartupSelectionPending && (bool)Get(tilt, "calibrated") && (float)Get(tilt, "neutralPitch") == 8f
                    && tilt.CommandsSent == 0, "Menu recovery must not reset head calibration, start tilt or send control traffic"); checks++;
                var hands = (bool[])Get(view, "handTracked"); hands[1] = true;
                var rays = (Ray[])Get(view, "handRays"); rays[1] = new Ray(eye.transform.position + new Vector3(0.3f, -0.25f, 0.1f), Vector3.forward);
                var rawAims = (Quaternion[])Get(view, "rawAimRotations"); rawAims[1] = Quaternion.Euler(15f, 23f, 0);
                view.pilot.pointerSmoothingSeconds = 0;
                view.CenterPointer(1);
                Vector3 aimed = (Vector3)typeof(RovVideoViewCalibration).GetMethod("AdjustPointer", Fields).Invoke(view, new object[] {1, rawAims[1]});
                Expect(RovPointerMath.Hit(root, new Ray(rays[1].origin, aimed), out Vector3 centerHit, out _)
                    && Vector3.Distance(centerHit, root.position) < 0.0001f,
                    "Pointer recenter must hit panel center from offset hand origin, including on yawed panels"); checks++;
                typeof(RovVideoViewCalibration).GetMethod("PointerTrackingOriginChanged", Fields).Invoke(view, new object[] {null});
                Expect(!((bool[])Get(view, "pointerInitialized"))[1] && !hands[1], "XR recenter must discard old pointer calibration and tracking"); checks++;
                Set(view, "startupPending", false); tilt.StartupSelectionPending = false;
                view.pilot.menuAngles = new Vector2(33f, -42f); view.pilot.menuDistance = 3;
                view.RecoverPilotMenu();
                Expect(view.pilot.menuAngles == Vector2.zero && view.pilot.menuSide == 0f && view.pilot.menuDistance == 1.25f,
                    "Ordinary menu recovery must clear troublesome angles and placement"); checks++;
                eye.transform.rotation = Quaternion.Euler(0, 179f, 0);
                aimed = (Vector3)typeof(RovVideoViewCalibration).GetMethod("AdjustPointer", Fields).Invoke(view, new object[] {1, eye.transform.rotation});
                Expect(Vector3.Angle(aimed, eye.transform.forward) < 0.01f,
                    "Turning around must not amplify absolute world yaw into an inaccurate pointer"); checks++;
                view.pilot.pointerYawOffset = 10;
                aimed = (Vector3)typeof(RovVideoViewCalibration).GetMethod("AdjustPointer", Fields).Invoke(view, new object[] {1, eye.transform.rotation});
                Expect(Vector3.Angle(aimed, Quaternion.Euler(0, 191f, 0) * Vector3.forward) < 0.01f,
                    "Changed pointer settings must replace stale calibration from the old profile"); checks++;
                RecoveryAt(view, true, 10f);
                Expect(view.pilot.menuAngles == Vector2.zero && !(bool)Get(view, "pointerRecoveryFired")
                    && (float)Get(view, "pointerRecoverySince") == 10f,
                    "Recovery hold starts without changing menu placement"); checks++;
                view.pilot.menuAngles = new Vector2(20, 30);
                RecoveryAt(view, true, 10.69f);
                Expect(view.pilot.menuAngles == new Vector2(20, 30) && !(bool)Get(view, "pointerRecoveryFired"),
                    "Recovery must not fire before the deliberate 0.7-second hold"); checks++;
                RecoveryAt(view, true, 10.71f);
                Expect(view.pilot.menuAngles == Vector2.zero && (bool)Get(view, "pointerRecoveryFired"),
                    "Holding both grips must recover without requiring a pointer hit"); checks++;
                view.pilot.menuAngles = new Vector2(20, 30);
                RecoveryAt(view, true, 11.5f);
                Expect(view.pilot.menuAngles == new Vector2(20, 30) && (bool)Get(view, "pointerRecoveryFired"),
                    "A continuing hold must recover only once"); checks++;
                RecoveryAt(view, false, 11.6f);
                Expect(!(bool)Get(view, "pointerRecoveryHeld") && !(bool)Get(view, "pointerRecoveryFired")
                    && (float)Get(view, "pointerRecoverySince") < 0f,
                    "Releasing both grips must reset the recovery timer and fired state"); checks++;
                RecoveryAt(view, true, 12f);
                Expect(view.pilot.menuAngles == new Vector2(20, 30) && !(bool)Get(view, "pointerRecoveryFired")
                    && (float)Get(view, "pointerRecoverySince") == 12f,
                    "A new hold must start a new recovery interval"); checks++;
                RecoveryAt(view, true, 12.69f);
                Expect(view.pilot.menuAngles == new Vector2(20, 30) && !(bool)Get(view, "pointerRecoveryFired"),
                    "The previous hold must not shorten a new deliberate hold"); checks++;
                RecoveryAt(view, true, 12.71f);
                Expect(view.pilot.menuAngles == Vector2.zero && (bool)Get(view, "pointerRecoveryFired"),
                    "A second deliberate hold must recover again after release"); checks++;
                RecoveryAt(view, false, 12.8f);

                // The visible endpoint must be UI geometry above the window,
                // rather than a 3D line that can disappear in its canvas.
                var beam = new GameObject("Cursor test beam").AddComponent<LineRenderer>(); beam.positionCount = 2;
                ((LineRenderer[])Get(view, "controllerRays"))[0] = beam;
                typeof(RovVideoViewCalibration).GetMethod("DrawPointerCursor", Fields).Invoke(view, new object[] {0, root, root.position, true});
                var cursor = ((RectTransform[])Get(view, "pointerReticles"))[0];
                var cursorCanvas = cursor.GetComponent<Canvas>();
                Expect((cursorCanvas.isRootCanvas || cursorCanvas.overrideSorting) && cursorCanvas.sortingOrder > 250,
                    "Cursor must draw above menu canvas"); checks++;
                Expect(Vector3.Distance(beam.GetPosition(1), cursor.position) < 0.0001f, "Beam must terminate at visible cursor"); checks++;
                var cursorGraphic = cursor.GetComponent<RovPointerVisualGraphic>(); cursorGraphic.Rebuild(CanvasUpdate.PreRender);
                var cursorMesh = cursorGraphic.canvasRenderer.GetMesh();
                Expect(cursorMesh.vertexCount > 0, "Cursor must contain visible UI geometry"); checks++;
                typeof(RovVideoViewCalibration).GetMethod("DrawWindowOutline", Fields).Invoke(view, new object[] {0, root, true, true});
                var frame = ((RectTransform[])Get(view, "windowOutlines"))[0];
                Expect(frame.gameObject.activeSelf && frame.GetComponentInChildren<Text>().text.Contains("MENU / HELD"), "Held window must be visibly identified"); checks++;
            }
            finally
            {
                if (view != null)
                {
                    Set(view, "pilotMenuRoot", null); Set(view, "detailCanvas", null);
                    foreach (string field in new[] {"pointerReticles", "windowOutlines"})
                    {
                        var roots = (RectTransform[])Get(view, field);
                        for (int i = 0; i < roots.Length; i++) { if (roots[i] != null) UnityEngine.Object.DestroyImmediate(roots[i].gameObject); roots[i] = null; }
                    }
                    var beams = (LineRenderer[])Get(view, "controllerRays");
                    for (int i = 0; i < beams.Length; i++) { if (beams[i] != null) UnityEngine.Object.DestroyImmediate(beams[i].gameObject); beams[i] = null; }
                    var material = Get(view, "pointerMaterial") as Material; if (material != null) UnityEngine.Object.DestroyImmediate(material); Set(view, "pointerMaterial", null);
                }
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(detail); UnityEngine.Object.DestroyImmediate(eye);
            }
            return checks;
        }
        private static void RecoveryAt(RovVideoViewCalibration view, bool held, float now)
            => typeof(RovVideoViewCalibration).GetMethod("UpdatePointerRecoveryAt", Fields).Invoke(view, new object[] {held, now});

        private static void Drag(RovVideoViewCalibration view, bool held, bool ready, bool depth, Vector2 stick, Ray ray)
            => typeof(RovVideoViewCalibration).GetMethod("ProcessWindowDrag", Fields).Invoke(view, new object[] {held, ready, depth, stick, ray, 0.05f});
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        private static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
        private static void Near(float actual, float expected) => Expect(Mathf.Abs(actual - expected) < 0.001f, $"Expected {expected}, got {actual}");
        private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException("Pointer interaction: " + message); }
    }
}
