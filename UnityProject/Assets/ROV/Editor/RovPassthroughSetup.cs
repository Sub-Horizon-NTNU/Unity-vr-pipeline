using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ROVVR.Editor
{
    public static class RovPassthroughSetup
    {
        private const string RequestPath = "Temp/rov-passthrough.request";
        private const string ResultPath = "Temp/rov-passthrough-result.json";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.update -= ProcessRequest;
            EditorApplication.update += ProcessRequest;
        }

        private static void ProcessRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText(RequestPath).Trim();
            if (action == "install" && EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            try
            {
                if (action == "install") Install();
                else if (action == "verify") Verify();
                else if (action != "runtime") throw new InvalidOperationException("Unknown passthrough request: " + action);
                WriteState(true, action == "install" ? "Passthrough installed and saved." : "Passthrough state inspected.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                WriteState(false, error.ToString());
            }
        }

        [MenuItem("ROV/View/Enable room passthrough")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before installing passthrough.");
            Camera camera = MainCamera();
            // Editor quality and Windows Play/build quality can select different assets.
            foreach (var pipelineAsset in RelevantPipelines())
            {
                if (pipelineAsset is not UniversalRenderPipelineAsset pipeline)
                    throw new InvalidOperationException("The editor and Standalone quality levels must use Universal Render Pipeline.");
                var serializedPipeline = new SerializedObject(pipeline);
                var alphaOutput = serializedPipeline.FindProperty("m_AllowPostProcessAlphaOutput");
                if (alphaOutput == null) throw new InvalidOperationException("This URP version has no postprocessing alpha output setting.");
                alphaOutput.boolValue = true;
                serializedPipeline.ApplyModifiedProperties();
                EditorUtility.SetDirty(pipeline);
            }
            PlayerSettings.preserveFramebufferAlpha = true;

            var background = camera.GetComponent<RovPassthroughBackground>();
            if (background == null) background = Undo.AddComponent<RovPassthroughBackground>(camera.gameObject);
            background.enabled = true;
            EditorUtility.SetDirty(background);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            if (!EditorSceneManager.SaveScene(camera.gameObject.scene)) throw new IOException("Could not save the Atlas passthrough scene.");
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("[ROV passthrough] Installed. Existing camera, XR rig, video, HUD and controls are retained. Enable passthrough in Meta Link before Play.");
        }

        [MenuItem("ROV/View/Inspect room passthrough (no movement)")]
        public static void Inspect() => WriteState(true, "Passthrough state inspected.");

        public static void Verify()
        {
            Camera camera = MainCamera();
            var background = camera.GetComponent<RovPassthroughBackground>();
            if (background == null || !background.enabled) throw new InvalidOperationException("Room passthrough is not enabled on the main camera.");
            if (camera.GetComponent<RovHeadTiltController>() == null) throw new InvalidOperationException("The existing Atlas tilt controller is missing.");
            if (camera.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>() == null) throw new InvalidOperationException("The existing tracked pose driver is missing.");
            if (!PlayerSettings.preserveFramebufferAlpha) throw new InvalidOperationException("Framebuffer alpha must be preserved.");
            foreach (var pipelineAsset in RelevantPipelines())
            {
                if (pipelineAsset is not UniversalRenderPipelineAsset pipeline || !pipeline.allowPostProcessAlphaOutput)
                    throw new InvalidOperationException("URP must preserve postprocessing alpha output for the editor and Standalone quality levels: "
                        + (pipelineAsset == null ? "Built-in pipeline" : AssetDatabase.GetAssetPath(pipelineAsset)));
            }
            if (Application.isPlaying && !Graphics.preserveFramebufferAlpha)
                throw new InvalidOperationException("The runtime framebuffer has no preserved alpha. Restart Unity after enabling framebuffer alpha.");
        }

        private static RenderPipelineAsset[] RelevantPipelines()
        {
            return new[] { GraphicsSettings.currentRenderPipeline }
                .Concat(QualitySettings.GetActiveQualityLevelsForPlatform("Standalone")
                    .Select(index => QualitySettings.GetRenderPipelineAssetAt(index) ?? GraphicsSettings.defaultRenderPipeline))
                .Distinct().ToArray();
        }

        private static Camera MainCamera()
        {
            Scene scene = SceneManager.GetSceneByPath(RovHeadTiltSetup.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open " + RovHeadTiltSetup.ScenePath + " first.");
            var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Where(camera => camera.CompareTag("MainCamera")).ToArray();
            if (cameras.Length != 1) throw new InvalidOperationException("The Atlas scene must contain exactly one MainCamera.");
            return cameras[0];
        }

        private static void WriteState(bool success, string message)
        {
            var state = new State { success = success, message = message, playing = Application.isPlaying };
            var scene = SceneManager.GetSceneByPath(RovHeadTiltSetup.ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .FirstOrDefault(item => item.CompareTag("MainCamera"));
                if (camera != null)
                {
                    state.scene = camera.gameObject.scene.path;
                    state.camera = camera.name;
                    state.backgroundAlpha = camera.backgroundColor.a;
                    var background = camera.GetComponent<RovPassthroughBackground>();
                    if (background != null)
                    {
                        state.componentEnabled = background.enabled;
                        state.status = background.Status;
                        state.capabilityChecked = background.CapabilityChecked;
                        state.supported = background.Supported;
                        state.initialized = background.Initialized;
                        state.managerLinked = background.ManagerLinked;
                        state.layerResumed = background.LayerResumed;
                        state.backgroundTransparent = background.BackgroundTransparent;
                    }
                }
            }
            state.preserveFramebufferAlpha = PlayerSettings.preserveFramebufferAlpha;
            state.graphicsFramebufferAlpha = Graphics.preserveFramebufferAlpha;
            state.qualityLevel = QualitySettings.GetQualityLevel();
            state.qualityName = QualitySettings.names[state.qualityLevel];
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            state.activePipelineName = pipeline == null ? "Built-in pipeline" : pipeline.name;
            state.activePipelinePath = pipeline == null ? "" : AssetDatabase.GetAssetPath(pipeline);
            state.postprocessAlphaOutput = pipeline != null && pipeline.allowPostProcessAlphaOutput;
            state.relevantPipelines = RelevantPipelines().Select(asset => new PipelineState
            {
                name = asset == null ? "Built-in pipeline" : asset.name,
                path = asset == null ? "" : AssetDatabase.GetAssetPath(asset),
                postprocessAlphaOutput = asset is UniversalRenderPipelineAsset urp && urp.allowPostProcessAlphaOutput
            }).ToArray();
            File.WriteAllText(ResultPath, JsonUtility.ToJson(state, true));
        }

        [Serializable]
        private sealed class State
        {
            public bool success, playing, componentEnabled, capabilityChecked, supported, initialized;
            public bool managerLinked, layerResumed, backgroundTransparent, preserveFramebufferAlpha, graphicsFramebufferAlpha, postprocessAlphaOutput;
            public string message, scene, camera, status, activePipelineName, activePipelinePath, qualityName;
            public int qualityLevel;
            public float backgroundAlpha;
            public PipelineState[] relevantPipelines;
        }

        [Serializable]
        private sealed class PipelineState
        {
            public string name, path;
            public bool postprocessAlphaOutput;
        }
    }
}
