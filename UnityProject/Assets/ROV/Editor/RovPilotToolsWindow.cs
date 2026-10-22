using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ROVVR.Editor
{
    // In-memory tooling for visual QA; these actions never save or change the scene on disk.
    public static class RovPilotToolsWindow
    {
        private const string Request = "Temp/rov-pilot-tools.request";
        private const string Result = "Temp/rov-pilot-tools-result.json";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [InitializeOnLoadMethod]
        private static void Register() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try { Execute(action); }
            catch (Exception error) { File.WriteAllText(Result, JsonUtility.ToJson(new Report { error = error.ToString() }, true)); Debug.LogException(error); }
        }
        [MenuItem("ROV/View/Toggle VR menu (Play)")]
        public static void ToggleMenu() => Execute("menu");
        private static object Field(RovVideoViewCalibration view, string name) => typeof(RovVideoViewCalibration).GetField(name, Private).GetValue(view);
        private static void Execute(string action)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start Play first.");
            var view = UnityEngine.Object.FindObjectsByType<RovVideoViewCalibration>(FindObjectsSortMode.None).Single();
            if (action == "menu") view.TogglePilotMenu();
            else if (action == "detail") view.pilot.detailEnabled = !view.pilot.detailEnabled;
            else if (action == "next")
            {
                var field = typeof(RovVideoViewCalibration).GetField("pilotPage", Private);
                field.SetValue(view, ((int)field.GetValue(view) + 1) % 8);
                typeof(RovVideoViewCalibration).GetMethod("BuildMenuPage", Private).Invoke(view, null);
            }
            else if (action != "status") throw new ArgumentException("Unknown pilot tools request.");
            var image = Field(view, "detailImage") as RawImage;
            var source = view.GetComponent<Renderer>().sharedMaterial;
            File.WriteAllText(Result, JsonUtility.ToJson(new Report
            {
                success = true, menuOpen = (bool)Field(view, "pilotMenuOpen"), page = (int)Field(view, "pilotPage"),
                detailEnabled = view.pilot.detailEnabled,
                sharedTexture = image != null && source.HasProperty("_rgb") && image.texture == source.GetTexture("_rgb"),
                desktopAllowed = RovHeadTiltFocus.DesktopShortcutsAllowed, keyboardControl = GUIUtility.keyboardControl,
                pilot = view.pilot
            }, true));
        }
        [Serializable] private sealed class Report
        {
            public bool success, menuOpen, detailEnabled, sharedTexture, desktopAllowed;
            public int page, keyboardControl;
            public string error;
            public RovPilotSettings pilot;
        }
    }
}
