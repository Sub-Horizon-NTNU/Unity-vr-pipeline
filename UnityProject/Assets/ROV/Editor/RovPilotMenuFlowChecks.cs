using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ROVVR.Editor
{
    // Inactive fixtures: no Awake/OnEnable, transports, recording, settings writes or vehicle commands.
    public static class RovPilotMenuFlowChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string RequestPath = "Temp/rov-pilot-menu-flow.request";
        private const string ResultPath = "Temp/rov-pilot-menu-flow-result.json";

        [InitializeOnLoadMethod]
        private static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }

        private static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
            try
            {
                File.ReadAllText(RequestPath); // A partially written/shared request is retried next update.
                File.Delete(RequestPath);
                var report = new Report { checks = Run(), success = true };
                File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
            }
            catch (IOException error)
            {
                if (File.Exists(RequestPath)) return;
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new Report { error = error.ToString() }, true));
            }
            catch (Exception error)
            {
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new Report { error = error.ToString() }, true));
                Debug.LogException(error);
            }
        }

        [MenuItem("ROV/Verify/Pilot automatic startup and settings (no network)")]
        public static void Verify() => Debug.Log("[ROV menu] " + Run() + " checks passed. No network or vehicle commands.");

        public static int Run()
        {
            int checks = 0;
            var fixture = new GameObject("Pilot menu checks (inactive)"); fixture.SetActive(false);
            var eye = new GameObject("Pilot menu check eye"); eye.SetActive(false); eye.AddComponent<Camera>();
            RovVideoViewCalibration view = null;
            RectTransform root = null;
            Material ui = null, solid = null;
            try
            {
                view = fixture.AddComponent<RovVideoViewCalibration>();
                var tilt = fixture.AddComponent<RovHeadTiltController>();
                view.eye = eye.transform; view.tilt = tilt;
                view.pilot.mainZoom = 2.25f; view.hudOpacity = 0.73f;
                string bindings = JsonUtility.ToJson(view.pilot);
                Invoke(view, "BeginPilotStartup");
                Expect(tilt.StartupSelectionPending && !MenuOpen(view) && Field(view, "pilotMenuRoot") == null,
                    "Startup must wait without opening any welcome/profile panel"); checks++;
                Expect(view.pilot.mainZoom == 2.25f && view.hudOpacity == 0.73f,
                    "Startup must preserve the already-loaded last/default profile"); checks++;

                // Each missing live prerequisite independently keeps the actual startup gate closed.
                for (int missing = 0; missing < 6; missing++)
                {
                    object[] ready = { true, true, true, true, true, true }; ready[missing] = false;
                    Expect(!(bool)Invoke(view, "TryCompleteAutomaticStartup", ready) && tilt.StartupSelectionPending,
                        "Missing XR/focus/tracking/ROV/IMU readiness must not start (gate " + missing + ")"); checks++;
                }
                // An explicit RC choice is not overridden by automatic startup.
                Set(tilt, "paused", true);
                Expect((bool)Invoke(view, "TryCompleteAutomaticStartup", true, true, true, true, true, true)
                    && !tilt.StartupSelectionPending && tilt.RcControlSelected && tilt.CommandsSent == 0,
                    "Ready startup must release only its gate and preserve RC selection"); checks++;
                Expect(!(bool)Invoke(view, "TryCompleteAutomaticStartup", true, true, true, true, true, true),
                    "Automatic startup must happen once per session"); checks++;
                Expect(!(bool)Invoke(view, "TryCompleteAutomaticStartup", false, false, false, false, false, false)
                    && !tilt.StartupSelectionPending && !MenuOpen(view),
                    "A later disconnect must use controller recovery without restoring startup menus"); checks++;

                Invoke(view, "BeginPilotStartup");
                Invoke(view, "SetPilotMenuOpen", true);
                root = (RectTransform)Field(view, "pilotMenuRoot");
                ui = (Material)Field(view, "pilotUiMaterial"); solid = (Material)Field(view, "controllerMaterial");
                Expect(MenuOpen(view) && (int)Field(view, "pilotPage") == 12 && root.gameObject.activeSelf,
                    "Settings must open the quick panel even while startup is waiting"); checks++;
                Expect(!Labels(root).Any(t => t.Contains("VELKOMMEN") || t.Contains("START MED VALGT")),
                    "The quick panel must never ask for a profile or Start"); checks++;
                Expect(Captions(view).Any(t => t.Contains("OPPTAK")) && Captions(view).Any(t => t.Contains("KAMERA:"))
                    && Captions(view).Any(t => t.Contains("HODEFØLGING")),
                    "Recording and distinct head-follow/camera ownership controls must be directly accessible"); checks++;
                Invoke(view, "SetPilotMenuOpen", false);
                Invoke(view, "TryCompleteAutomaticStartup", true, true, true, true, false, false);
                Expect(!MenuOpen(view) && !root.gameObject.activeSelf && tilt.StartupSelectionPending,
                    "Settings must close while disconnected and must not be forced open again"); checks++;
                Invoke(view, "SetPilotMenuOpen", true);
                bool hud = view.hudEnabled;
                Click(view, "HUD:");
                Expect(view.hudEnabled != hud && tilt.CommandsSent == 0,
                    "The quick HUD action must affect presentation without sending vehicle commands"); checks++;
                Expect((bool)Invoke(view, "TryCompleteAutomaticStartup", true, true, true, true, true, true)
                    && MenuOpen(view) && root.gameObject.activeSelf,
                    "Readiness must preserve settings deliberately opened by the pilot"); checks++;

                Click(view, "ALLE INNSTILLINGER");
                Expect((int)Field(view, "pilotPage") == 16, "Advanced controls must have one direct entry"); checks++;
                // Exercise each catalog action and the actual Back callback, not just its list of IDs.
                foreach (int page in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13 })
                {
                    string[] pages = (string[])typeof(RovVideoViewCalibration).GetField("PilotPages", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    Click(view, pages[page]);
                    Expect((int)Field(view, "pilotPage") == page && Labels(root).Any(),
                        "Every advanced feature must be reachable directly: " + page); checks++;
                    Select(Field(view, "backMenuItem"));
                    Expect((int)Field(view, "pilotPage") == 16,
                        "Advanced Back must consistently return to the catalog: " + page); checks++;
                }
                Click(view, "PROFILER");
                Click(view, "NY PROFIL");
                Expect((int)Field(view, "pilotPage") == 15, "Named-profile creation must remain available from profiles"); checks++;
                Select(Field(view, "backMenuItem"));
                Expect((int)Field(view, "pilotPage") == 6, "Naming Back must return to profiles without saving"); checks++;
                Select(Field(view, "backMenuItem"));
                Select(Field(view, "backMenuItem"));
                Expect((int)Field(view, "pilotPage") == 12, "Catalog Back must return to quick settings"); checks++;
                Invoke(view, "SetPilotMenuOpen", false);
                Expect(!MenuOpen(view) && !root.gameObject.activeSelf,
                    "Closing settings must hide the complete panel"); checks++;
                Invoke(view, "SetPilotMenuOpen", true);
                Expect(MenuOpen(view) && (int)Field(view, "pilotPage") == 12,
                    "Every reopen must return to quick settings rather than a previous submenu"); checks++;
                Expect(JsonUtility.ToJson(view.pilot) == bindings && tilt.CommandsSent == 0 && tilt.RcControlSelected,
                    "Menu navigation must preserve bindings, saved settings and vehicle control ownership"); checks++;
                var menu = view.pilot.bindings.Single(b => b.action == RovPilotAction.Menu);
                Expect(menu.button == RovPilotButton.RightStick && menu.key == RovPilotKey.F3,
                    "Default right-stick/F3 settings toggle must remain available"); checks++;
            }
            finally
            {
                if (view != null)
                {
                    root = (RectTransform)Field(view, "pilotMenuRoot");
                    ui = (Material)Field(view, "pilotUiMaterial"); solid = (Material)Field(view, "controllerMaterial");
                    Set(view, "pilotMenuRoot", null); Set(view, "pilotMenuPage", null);
                    Set(view, "pilotUiMaterial", null); Set(view, "controllerMaterial", null);
                }
                if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
                UnityEngine.Object.DestroyImmediate(fixture); UnityEngine.Object.DestroyImmediate(eye);
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui);
                if (solid != null) UnityEngine.Object.DestroyImmediate(solid);
            }
            return checks;
        }

        private static string[] Labels(RectTransform root) => root.GetComponentsInChildren<Text>().Select(t => t.text).ToArray();
        private static bool MenuOpen(RovVideoViewCalibration view) => (bool)Field(view, "pilotMenuOpen");
        private static object Field(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static object Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
        private static string[] Captions(RovVideoViewCalibration view) => ((IEnumerable)Field(view, "pageItems")).Cast<object>()
            .Select(item => ((Text)item.GetType().GetField("label").GetValue(item)).text).ToArray();
        private static void Click(RovVideoViewCalibration view, string caption)
        {
            var item = ((IEnumerable)Field(view, "pageItems")).Cast<object>()
                .First(i => ((Text)i.GetType().GetField("label").GetValue(i)).text.StartsWith(caption, StringComparison.Ordinal));
            Select(item);
        }
        private static void Select(object item) => ((Action<float>)item.GetType().GetField("select").GetValue(item))(0.5f);
        private static void Expect(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Pilot menu flow: " + message); }

        [Serializable] private sealed class Report { public bool success; public int checks; public string error; }
    }
}
