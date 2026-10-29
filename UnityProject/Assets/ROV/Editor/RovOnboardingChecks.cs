using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
namespace ROVVR.Editor
{
    public static class RovOnboardingChecks
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        [InitializeOnLoadMethod] private static void Register() { EditorApplication.update -= Request; EditorApplication.update += Request; }
        private static void Request()
        {
            const string path = "Temp/rov-pilot-ux.request";
            if (!File.Exists(path) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string page = File.ReadAllText(path).Trim(); File.Delete(path);
            bool preview = page.StartsWith("preview-"); if (preview) page = page.Substring(8);
            if (!Application.isPlaying) return;
            var view = UnityEngine.Object.FindFirstObjectByType<RovVideoViewCalibration>();
            int target = page == "help" ? 13 : page == "profiles" ? 6 : page == "home" ? 12 : 14;
            Invoke(view, "GoPilotPage", target);
            if (page == "keyboard") Invoke(view, "BeginProfileName", new object[] {null});
            if (preview)
            {
                Set(view, "flatMenuPreview", true); view.showSettings = false;
                if (view.tilt != null) Set(view.tilt, "showDesktopStatus", false);
                var root = (RectTransform)typeof(RovVideoViewCalibration).GetField("pilotMenuRoot", Fields).GetValue(view);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>() ?? root.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(900, 1000); scaler.matchWidthOrHeight = 1;
                root.localScale = Vector3.one;
            }
        }
        public static int Run()
        {
            int checks = 0;
            Expect(RovProfileLibrary.NextName("Pilotprofil", new[] {"pilotprofil", "Pilotprofil 2"}) == "Pilotprofil 3", "New names must be readable and avoid case-insensitive collisions"); checks++;
            Expect(RovProfileLibrary.FriendlyName("VR-20261022-192400-000").StartsWith("Lagret profil")
                && RovProfileLibrary.FriendlyName("Inspeksjon") == "Inspeksjon", "Legacy timestamp names must be readable without changing files"); checks++;
            string original = "ux-check-" + Guid.NewGuid().ToString("N"), renamed = original + "-ny";
            string from = RovProfileLibrary.ProfilePath(original), to = RovProfileLibrary.ProfilePath(renamed);
            Directory.CreateDirectory(RovProfileLibrary.DirectoryPath);
            var go = new GameObject("UX checks (no network)"); go.SetActive(false);
            var eye = new GameObject("UX test eye"); var window = new GameObject("UX test menu", typeof(RectTransform), typeof(Canvas));
            RovVideoViewCalibration view = null; Material material = null;
            try
            {
                File.WriteAllText(from, "original"); File.WriteAllText(to, "existing");
                bool refused = false; try { RovProfileLibrary.Rename(original, renamed); } catch (IOException) { refused = true; }
                Expect(refused && File.ReadAllText(from) == "original" && File.ReadAllText(to) == "existing", "Rename must never overwrite another profile"); checks++;
                File.Delete(to); RovProfileLibrary.Rename(original, renamed);
                Expect(!File.Exists(from) && File.ReadAllText(to) == "original", "Rename must preserve profile content"); checks++;
                File.Delete(to);
                view = go.AddComponent<RovVideoViewCalibration>();
                var tilt = go.AddComponent<RovHeadTiltController>(); view.tilt = tilt;
                Set(view, "eye", eye.transform); Set(view, "pilotMenuRoot", (RectTransform)window.transform);
                material = new Material(Resources.Load<Shader>("RovHudOverlay")); Set(view, "pilotUiMaterial", material);
                Invoke(view, "BeginPilotStartup");
                Expect(tilt.StartupSelectionPending && tilt.CommandsSent == 0, "Startup selection must block tilt without movement commands"); checks++;
                Set(view, "pilotMenuOpen", true);
                foreach (int page in new[] {12, 13, 14, 6, 15, 16})
                {
                    Set(view, "pilotPage", page); Invoke(view, "BuildMenuPage");
                    Expect(window.GetComponentsInChildren<Text>().Length > 4, "Every onboarding page must have visible labels"); checks++;
                }
                Invoke(view, "CompletePilotStartup", original + "-missing");
                Expect(tilt.StartupSelectionPending, "Failed profile load must never start head control"); checks++;
                Invoke(view, "CompletePilotStartup", "");
                Expect(!tilt.StartupSelectionPending && tilt.CommandsSent == 0, "Explicit start must remove only startup gate"); checks++;
                view.pilot.bindings[0].button = RovPilotButton.LeftTrigger;
                string binding = (string)Invoke(view, "BindingText", RovPilotAction.Follow);
                Expect(binding.Contains("V. avtrekker"), "Controller help must reflect rebound buttons"); checks++;
            }
            finally
            {
                if (view != null) { Set(view, "pilotMenuRoot", null); Set(view, "pilotMenuPage", null); Set(view, "pilotUiMaterial", null); }
                UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(eye);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                foreach (string p in new[] {from, to}) if (File.Exists(p)) File.Delete(p);
            }
            return checks;
        }
        private static void Set(object o, string name, object value)
        { var field = o.GetType().GetField(name, Fields | BindingFlags.Public); field.SetValue(o, value); }
        private static object Invoke(object o, string method, params object[] args) => o.GetType().GetMethod(method, Fields).Invoke(o, args);
        private static void Expect(bool ok, string message) { if (!ok) throw new InvalidOperationException("Pilot onboarding: " + message); }
    }
}
