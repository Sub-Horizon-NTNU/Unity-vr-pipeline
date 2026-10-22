using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ROVVR.Editor
{
    public static class RovPilotFeatureChecks
    {
        [MenuItem("ROV/View/Verify pilot tools (no movement)")]
        public static void Verify()
        {
            try
            {
                int checks = Run() + RovHeadTiltChecks.Run() + RovCommandRecoveryChecks.Run()
                    + RovVideoViewChecks.Run() + RovCameraPoseChecks.Run() + RovManualViewChecks.Run()
                    + RovHudChecks.Run() + RovPilotControlsChecks.Run() + RovLiveSettingsChecks.Run();
                Debug.Log("[ROV pilot tools] PASS: " + checks + " checks; no control traffic sent.");
            }
            catch (Exception error) { Debug.LogException(error); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }

        public static int Run()
        {
            int checks = 0;
            var old = RovVideoViewProfile.Parse("{\"schemaVersion\":1}");
            Expect(old.pilot.mainZoom == 1f && !old.pilot.detailEnabled, "Older JSON must preserve normal video"); checks++;
            foreach (float zoom in new[] { 1f, 2f, 3f, 8f })
                foreach (var target in new[] { Vector2.zero, Vector2.one, new Vector2(0.2f, 0.7f) })
                {
                    Rect crop = RovPilotSettings.Crop(target, zoom);
                    Expect(crop.xMin >= 0 && crop.yMin >= 0 && crop.xMax <= 1.00001f && crop.yMax <= 1.00001f, "Crop must remain inside video");
                    Near(crop.width, 1f / zoom); Near(crop.height, crop.width); checks++;
                }
            var profile = new RovVideoViewProfile(); profile.pilot.detailEnabled = true;
            profile.pilot.mainZoom = 2.5f; profile.pilot.zoomCenter = new Vector2(0.15f, 0.8f);
            profile.pilot.detailPosition = new Vector3(-1f, 0.3f, 2.4f);
            var copy = RovVideoViewProfile.Parse(JsonUtility.ToJson(profile));
            Near(copy.pilot.mainZoom, 2.5f); Near(copy.pilot.zoomCenter.y, 0.8f); Near(copy.pilot.detailPosition.z, 2.4f); checks++;
            copy.pilot.bindings[0].button = RovPilotButton.None;
            Expect(profile.pilot.bindings[0].button == RovPilotButton.A, "Profiles must not share mutable bindings"); checks++;
            bool duplicate = false;
            try { copy.pilot.bindings[0].button = RovPilotButton.B; copy.Validate(); } catch (ArgumentException) { duplicate = true; }
            Expect(duplicate, "Reject overlapping bindings"); checks++;
            bool badNumber = false;
            try { new RovPilotSettings { mainZoom = float.NaN }.Validate(); } catch (ArgumentException) { badNumber = true; }
            Expect(badNumber, "Reject non-finite zoom"); checks++;
            foreach (string name in new[] { "../escape", "..", "CON", "LPT1", "x/y", "trailing.", " space " })
            {
                bool rejected = false; try { RovProfileLibrary.ProfilePath(name); } catch (ArgumentException) { rejected = true; }
                Expect(rejected, "Reject invalid profile name " + name); checks++;
            }
            var go = new GameObject("Pilot feature check");
            string profileName = "pilot-check-" + Guid.NewGuid().ToString("N");
            string profilePath = RovProfileLibrary.ProfilePath(profileName);
            Mesh mesh = null;
            try
            {
                var curved = go.AddComponent<CurvedVideoScreen>();
                var view = go.AddComponent<RovVideoViewCalibration>();
                curved.flipHorizontal = false; curved.flipVertical = true;
                mesh = RovVideoProjection.Create(1920, 1080, 110f, 2f, 8, 4, false, true, true, 45f);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                view.pilot.mainZoom = 2f; view.pilot.zoomCenter = new Vector2(0.7f, 0.3f);
                Invoke(view, "UpdateZoomMesh");
                var uv = mesh.uv;
                Near(uv[0].x, 0.45f); Near(uv[0].y, 0.95f); checks++;
                Expect(view.AimZoom(new Ray(new Vector3(0, 0, -2f), Vector3.forward)), "Ray must pick curved image");
                Near(view.pilot.zoomCenter.x, 0.7f); Near(view.pilot.zoomCenter.y, 0.3f); checks++;
                Expect(!view.AimZoom(new Ray(new Vector3(0, 0, -2f), Vector3.back)), "Ray behind screen must not change target"); checks++;
                Expect(!view.Rebind(RovPilotAction.Hud, RovPilotButton.A, RovPilotKey.H), "Reject binding conflict live");
                Expect(view.Rebind(RovPilotAction.Hud, RovPilotButton.LeftStick, RovPilotKey.H), "Allow unused binding live"); checks++;
                view.pilot.mainZoom = 1f; Invoke(view, "UpdateZoomMesh");
                Near(mesh.uv[0].x, 0f); Near(mesh.uv[0].y, 1f); checks++;
                Expect(!view.Rebind(RovPilotAction.Hud, RovPilotButton.LeftStick, RovPilotKey.F8), "Do not overlap fixed status hotkey"); checks++;
                view.pilot.mainZoom = 2.75f;
                view.SaveNamedProfile(profileName);
                Expect(File.Exists(profilePath), "Named profile must be written"); checks++;
                view.pilot.mainZoom = 4f;
                view.LoadNamedProfile(profileName);
                Near(view.pilot.mainZoom, 2.75f); checks++;
                view.pilot.mainZoom = 3f; view.SaveNamedProfile(profileName);
                Near(RovVideoViewProfile.Parse(File.ReadAllText(profilePath + ".bak")).pilot.mainZoom, 2.75f); checks++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go); if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                foreach (string path in new[] {profilePath, profilePath + ".bak", profilePath + ".tmp"}) if (File.Exists(path)) File.Delete(path);
            }
            return checks + RovPointerChecks.Run();
        }
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Near(float a, float b) => Expect(Mathf.Abs(a - b) < 0.001f, $"Expected {b}, got {a}");
        private static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException("Pilot tools: " + message); }
    }
}
