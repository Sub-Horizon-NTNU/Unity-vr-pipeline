using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ROVVR.Editor
{
    public static class RovReleaseBuilder
    {
        private const string Request = "Temp/rov-release.request";
        private const string Result = "Temp/rov-release-result.json";
        public const string MainScene = "Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.update -= ProcessRequest;
            EditorApplication.update += ProcessRequest;
        }

        private static void ProcessRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            if (action == "build-pilot-preview") { EditorApplication.delayCall += BuildPilotPreview; return; }
            if (action != "build-v1") { Write(false, "Unknown release request.", null, 0, 0); return; }
            EditorApplication.delayCall += BuildV1;
        }

        [MenuItem("ROV/Release/Build Windows pilot tools preview")]
        public static void BuildPilotPreview()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/pilot-tools-preview"));
            Directory.CreateDirectory(folder);
            var result = new PreviewResult();
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before building.");
                result.checks = RovPilotFeatureChecks.Run() + RovHeadTiltChecks.Run() + RovCommandRecoveryChecks.Run()
                    + RovVideoViewChecks.Run() + RovCameraPoseChecks.Run() + RovManualViewChecks.Run()
                    + RovHudChecks.Run() + RovPilotControlsChecks.Run() + RovLiveSettingsChecks.Run();
                // Meta XR 77's postprocessor copies this file without overwrite.
                string bindings = Path.Combine(folder, "RuntimeActionBindings.json");
                if (File.Exists(bindings)) File.Delete(bindings);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] {MainScene},
                    locationPathName = Path.Combine(folder, "Unity-vr-pipeline-preview.exe"),
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                result.success = report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0;
                result.errors = (int)report.summary.totalErrors;
                result.message = report.summary.result.ToString();
                if (!result.success) Debug.LogError("[ROV pilot preview] " + result.message);
                else Debug.Log("[ROV pilot preview] Built successfully; " + result.checks + " checks passed.");
            }
            catch (Exception error) { result.message = error.ToString(); Debug.LogException(error); }
            finally { File.WriteAllText(Path.Combine(folder, "build-report.json"), JsonUtility.ToJson(result, true)); }
        }

        [Serializable] private sealed class PreviewResult
        {
            public bool success;
            public int checks, errors;
            public string message;
        }

        [MenuItem("ROV/Release/Build Windows v1")]
        public static void BuildV1()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before building v1.");
                var plugin = AssetImporter.GetAtPath("Assets/Plugins/GstUnityPlugin/Windows/GstUnityPlugin.dll") as PluginImporter;
                if (plugin == null || !plugin.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64)
                    || plugin.GetPlatformData(BuildTarget.StandaloneWindows64, "CPU") != "x86_64")
                    throw new InvalidOperationException("GstUnityPlugin.dll must be enabled for Windows x86_64.");
                if (Application.isBatchMode) EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
                int checks = RovHeadTiltChecks.Run() + RovCommandRecoveryChecks.Run()
                    + RovVideoViewChecks.Run() + RovCameraPoseChecks.Run() + RovManualViewChecks.Run()
                    + RovHudChecks.Run() + RovPilotControlsChecks.Run() + RovLiveSettingsChecks.Run() + RovPilotFeatureChecks.Run();
                if (!EditorSceneManager.SaveOpenScenes()) throw new IOException("Could not save open scenes.");
                string root = Directory.GetParent(Application.dataPath).Parent.FullName;
                string output = Path.Combine(root, "Builds", "v1", "Unity-vr-pipeline-v1.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                // Meta XR 77 copies this generated file without overwrite.
                // Remove only its old build output before a repeat build.
                string bindings = Path.Combine(Path.GetDirectoryName(output), "RuntimeActionBindings.json");
                if (File.Exists(bindings)) File.Delete(bindings);
                PlayerSettings.bundleVersion = "1.0.0";
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] {GraphicsDeviceType.Direct3D11});
                AssetDatabase.SaveAssets();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] {MainScene},
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                bool success = report.summary.result == BuildResult.Succeeded && report.summary.totalErrors == 0;
                Write(success, report.summary.result.ToString(), output, checks, report.summary.totalErrors);
                if (!success) throw new InvalidOperationException("Unity build failed. See Temp/rov-release-result.json and Editor.log.");
                Debug.Log($"[ROV release] v1 built: {output}; {checks} checks passed; {report.summary.totalSize} bytes.");
            }
            catch (Exception error)
            {
                Write(false, error.ToString(), null, 0, 1);
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void Write(bool success, string message, string output, int checks, int errors)
        {
            string json = JsonUtility.ToJson(new ReleaseResult
            {
                success = success, message = message, output = output == null ? null : Path.GetFileName(output), checks = checks,
                errors = errors, unityVersion = Application.unityVersion, version = "v1"
            }, true);
            string persistent = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/v1/unity-build-report.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(persistent));
            File.WriteAllText(persistent, json);
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Result, json);
        }

        [Serializable] private sealed class ReleaseResult
        {
            public bool success;
            public string message, output, unityVersion, version;
            public int checks;
            public int errors;
        }
    }
}
