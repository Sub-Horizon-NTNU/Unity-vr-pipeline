using System;
using System.Reflection;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovLiveSettingsChecks
    {
        // Inactive test objects never start networking or issue mount commands.
        // Seed an acquired control state, then use the same setters and apply
        // path as the calibration window, Inspector and JSON loader.
        public static int Run()
        {
            int checks = 0;
            var go = new GameObject("ROV live-settings regression (no networking)");
            go.SetActive(false);
            try
            {
                var tilt = go.AddComponent<RovHeadTiltController>();
                var view = go.AddComponent<RovVideoViewCalibration>();
                view.tilt = tilt;
                view.ApplyGeometry();
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Set(tilt, "calibrated", true); Set(tilt, "canSend", true);
                Set(tilt, "neutralPitch", 12f); Set(tilt, "firstCommandAt", 42f);
                view.hudScale = 1.5f;
                view.ApplyGeometry();
                Expect(go.GetComponent<MeshFilter>().sharedMesh == mesh, "HUD font size must not recreate the video mesh");
                Preserved(tilt); checks++;
                view.hudLayout.battery.position.y += 0.15f;
                view.hudOpacity = 0.6f; view.hudEnabled = false;
                view.ApplyGeometry();
                Expect(go.GetComponent<MeshFilter>().sharedMesh == mesh, "HUD visibility/layout must not recreate the video mesh");
                Preserved(tilt); checks++;
                view.distance += 0.2f; view.verticalPositionMeters = -0.2f;
                view.ApplyGeometry();
                Expect(go.GetComponent<MeshFilter>().sharedMesh == mesh, "View placement must not recreate video vertices");
                Preserved(tilt); checks++;
                tilt.CompensateRovPitch = false; Preserved(tilt);
                tilt.CompensateRovPitch = true; Preserved(tilt); checks++;
                tilt.PitchCompensationStrength = 0.5f;
                tilt.ServoSmoothingSeconds = 0f; tilt.ServoMaximumSpeed = 30f;
                view.telemetrySmoothingSeconds = 0f;
                Preserved(tilt); checks++;
                view.ApplyProfile(RovVideoViewProfile.Parse(JsonUtility.ToJson(view.GetProfile())));
                Preserved(tilt);
                Expect(go.GetComponent<MeshFilter>().sharedMesh == mesh, "Reloading unchanged geometry must keep the live mesh"); checks++;
                view.curveDegrees += 5f; view.ApplyGeometry();
                Expect(go.GetComponent<MeshFilter>().sharedMesh != mesh, "Actual curvature changes must rebuild the video mesh");
                Preserved(tilt); checks++;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            return checks;
        }

        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(RovHeadTiltController tilt, string field, object value) => typeof(RovHeadTiltController).GetField(field, Fields).SetValue(tilt, value);
        private static T Get<T>(RovHeadTiltController tilt, string field) => (T)typeof(RovHeadTiltController).GetField(field, Fields).GetValue(tilt);
        private static void Preserved(RovHeadTiltController tilt)
        {
            Expect(tilt.HasHeadNeutral && Get<bool>(tilt, "canSend"), "Settings must preserve active head control");
            Expect(Get<float>(tilt, "neutralPitch") == 12f, "Settings must preserve calibrated head neutral");
            Expect(Get<float>(tilt, "firstCommandAt") == 42f, "Settings must not release the mount session");
        }
        private static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException("ROV live settings: " + message); }
    }
}
