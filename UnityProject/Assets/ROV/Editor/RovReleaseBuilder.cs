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
            if (action != "build-v1") { Write(false, "Unknown release request.", null, 0, 0); return; }
            EditorApplication.delayCall += BuildV1;
        }

        [MenuItem("ROV/Release/Build Windows v1")]
        public static void BuildV1()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before building v1.");
                int checks = RovHeadTiltChecks.Run() + RovCommandRecoveryChecks.Run()
                    + RovVideoViewChecks.Run() + RovCameraPoseChecks.Run() + RovManualViewChecks.Run()
                    + RovHudChecks.Run() + RovPilotControlsChecks.Run() + RovLiveSettingsChecks.Run();
                if (!EditorSceneManager.SaveOpenScenes()) throw new IOException("Could not save open scenes.");
                string root = Directory.GetParent(Application.dataPath).Parent.FullName;
                string output = Path.Combine(root, "Builds", "v1", "Unity-vr-pipeline-v1.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
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
                bool success = report.summary.result == BuildResult.Succeeded;
                Write(success, report.summary.result.ToString(), output, checks, report.summary.totalErrors);
                if (!success) throw new InvalidOperationException("Unity build failed. See Temp/rov-release-result.json and Editor.log.");
                Debug.Log($"[ROV release] v1 built: {output}; {checks} checks passed; {report.summary.totalSize} bytes.");
            }
            catch (Exception error)
            {
                Write(false, error.ToString(), null, 0, 1);
                Debug.LogException(error);
            }
        }

        private static void Write(bool success, string message, string output, int checks, int errors)
        {
            File.WriteAllText(Result, JsonUtility.ToJson(new ReleaseResult
            {
                success = success, message = message, output = output, checks = checks,
                errors = errors, unityVersion = Application.unityVersion, version = "v1"
            }, true));
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
