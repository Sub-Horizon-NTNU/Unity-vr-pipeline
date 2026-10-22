using System;
using System.Reflection;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovPilotControlsChecks
    {
        // The controller stays disabled throughout: no telemetry coroutine,
        // mount requests or physical camera movement can start from this test.
        public static int Run()
        {
            int checks = 0;
            var screen = new GameObject("ROV pilot controls check");
            screen.SetActive(false);
            var eye = new GameObject("ROV test eye");
            try
            {
                var tilt = screen.AddComponent<RovHeadTiltController>();
                tilt.enabled = false;
                var view = screen.AddComponent<RovVideoViewCalibration>();
                view.eye = eye.transform; view.tilt = tilt; view.loadSavedCalibration = false;
                view.anchorToRovImu = false; view.followHeadPitch = true;
                eye.transform.position = new Vector3(1f, 1.5f, 2f);
                eye.transform.rotation = Quaternion.Euler(-10f, 20f, 0f);
                screen.SetActive(true);
                view.CenterView();
                Set(tilt, "calibrated", true); Set(tilt, "canSend", true);
                Set(tilt, "neutralPitch", 8f); Set(tilt, "firstCommandAt", 42f);
                Set(tilt, "<TargetPitch>k__BackingField", 12f);
                Vector3 position = screen.transform.position;
                Quaternion rotation = screen.transform.rotation;
                Vector3 hudOrigin = view.PilotHudPosition;
                Quaternion hudRotation = view.PilotHudRotation;

                Buttons(view, true, false, true);
                Expect(!view.headFollowingEnabled && view.ScreenLocked && !tilt.HeadFollowingEnabled, "A must lock both screen and tilt");
                Preserved(tilt); checks++;
                Buttons(view, true, false, true);
                Expect(!view.headFollowingEnabled, "Holding A must not repeatedly toggle"); checks++;

                eye.transform.SetPositionAndRotation(new Vector3(-2f, 3f, -1f), Quaternion.Euler(35f, 120f, 30f));
                screen.transform.position = Vector3.zero; // Also resist movement inherited from a parent.
                Invoke(view, "UpdateViewPose");
                Expect(Vector3.Distance(screen.transform.position, position) < 0.0001f
                    && Quaternion.Angle(screen.transform.rotation, rotation) < 0.001f
                    && Vector3.Distance(view.PilotHudPosition, hudOrigin) < 0.0001f
                    && Quaternion.Angle(view.PilotHudRotation, hudRotation) < 0.001f, "Locked screen/HUD must ignore head movement");
                Near(tilt.RequestedPitch, 12f); Preserved(tilt); checks++;

                Buttons(view, false, true, true);
                Expect(!view.hudEnabled && view.ScreenLocked, "B must hide HUD without unlocking or releasing tilt");
                Preserved(tilt); checks++;
                Buttons(view, false, true, true);
                Expect(!view.hudEnabled, "Holding B must not repeatedly toggle"); checks++;

                var saved = RovVideoViewProfile.Parse(JsonUtility.ToJson(view.GetProfile()));
                Expect(!saved.headFollowingEnabled && !saved.hudEnabled, "Lock and HUD state must round-trip in JSON");
                view.ApplyProfile(saved);
                Expect(view.ScreenLocked && !tilt.HeadFollowingEnabled, "Loading a locked profile must keep screen/servo locked");
                Preserved(tilt); checks++;
                Expect(RovVideoViewProfile.Parse("{\"schemaVersion\":1}").headFollowingEnabled, "Older profiles must keep head-following enabled"); checks++;

                Buttons(view, false, false, true);
                Buttons(view, true, true, true);
                Expect(view.headFollowingEnabled && tilt.HeadFollowingEnabled && view.hudEnabled && !view.ScreenLocked, "A/B must restore following and HUD independently");
                Preserved(tilt);
                Invoke(view, "UpdateViewPose");
                Expect(Vector3.Distance(screen.transform.position, position) > 0.1f, "Unlock must resume view updates"); checks++;

                Buttons(view, false, false, false);
                Buttons(view, true, true, false);
                Buttons(view, true, true, true);
                Expect(view.headFollowingEnabled && view.hudEnabled, "Focus recovery must not replay buttons held in a system menu"); checks++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(screen);
                UnityEngine.Object.DestroyImmediate(eye);
            }
            return checks;
        }

        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Fields).Invoke(target, null);
        private static void Buttons(RovVideoViewCalibration view, bool a, bool b, bool ready) => typeof(RovVideoViewCalibration)
            .GetMethod("ProcessRightButtons", Fields).Invoke(view, new object[] {a, b, ready});
        private static void Preserved(RovHeadTiltController tilt)
        {
            Expect(tilt.HasHeadNeutral && Get<bool>(tilt, "canSend") && Get<float>(tilt, "neutralPitch") == 8f
                && Get<float>(tilt, "firstCommandAt") == 42f && tilt.CommandsSent == 0, "Buttons must preserve control/calibration and issue no test commands");
            Near(tilt.TargetPitch, 12f);
        }
        private static void Near(float actual, float expected) => Expect(Mathf.Abs(actual - expected) < 0.001f, "Held camera target changed");
        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ROV pilot controls: " + message);
        }
    }
}
