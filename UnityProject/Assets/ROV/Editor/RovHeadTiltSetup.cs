using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.XR;

namespace ROVVR.Editor
{
    public static class RovHeadTiltSetup
    {
        public const string ScenePath = "Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity";
        private const string RequestPath = "Temp/rov-head-tilt-setup.request";
        private const string ResultPath = "Temp/rov-head-tilt-setup-result.json";

        // A one-shot request allows installation in the already open Editor,
        // using its in-memory scene so existing unsaved edits are preserved.
        [InitializeOnLoadMethod]
        private static void RegisterSetupRequest()
        {
            EditorApplication.update -= ProcessSetupRequest;
            EditorApplication.update += ProcessSetupRequest;
        }

        private static void ProcessSetupRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string action = File.ReadAllText(RequestPath).Trim();
            if (EditorApplication.isPlayingOrWillChangePlaymode && (action == "install" || action == "stabilize")) return;
            File.Delete(RequestPath);
            try
            {
                if (action == "runtime") { WriteRuntimeDiagnostics(); return; }
                if (action == "connection") { VerifyBlueOsConnection(); return; }
                int checks = RovHeadTiltChecks.Run() + RovCommandRecoveryChecks.Run();
                if (action == "install") InstallInOpenScene();
                else if (action == "stabilize") EnablePitchCompensation();
                else if (action != "verify") throw new InvalidOperationException("Unknown setup request.");
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new SetupResult
                {
                    success = true, checks = checks, scene = ScenePath,
                    message = action == "install" ? "Installed and saved. Build starts with the working ROV scene." : "Checks passed."
                }, true));
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText(ResultPath, JsonUtility.ToJson(new SetupResult { message = error.ToString() }, true));
            }
        }

        [MenuItem("ROV/Enable IMU pitch compensation")]
        public static void EnablePitchCompensation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RovHeadTiltController>(true)).Single();
            Undo.RecordObject(controller, "Enable ROV pitch compensation");
            controller.CompensateRovPitch = true;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save pitch compensation.");
            Debug.Log("[ROV tilt] IMU pitch compensation enabled; video configuration preserved.");
        }

        [MenuItem("ROV/Install Quest camera tilt in working scene")]
        public static void InstallInOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before installing the component.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open " + ScenePath + " before running setup.");
            Camera camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(item => item.CompareTag("MainCamera"));
            if (camera == null) throw new InvalidOperationException("The working scene has no MainCamera.");
            var controller = camera.GetComponent<RovHeadTiltController>();
            if (controller == null) controller = Undo.AddComponent<RovHeadTiltController>(camera.gameObject);
            controller.enabled = true;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the ROV scene.");

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            scenes.AddRange(EditorBuildSettings.scenes.Where(item => item.path != ScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();
            // BlueOS exposes MAVLink2REST over HTTP on the tether network.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            AssetDatabase.SaveAssets();
            Debug.Log("[ROV tilt] Installed on " + camera.name + ". A/R = recenter, B/P = pause. No commands are sent until the headset is tracked.");
        }

        [MenuItem("ROV/Verify head tracking and MAVLink format (no movement)")]
        public static void Verify()
        {
            Debug.Log("[ROV tilt] " + (RovHeadTiltChecks.Run() + RovCommandRecoveryChecks.Run()) + " checks passed. No ROV commands sent.");
        }

        [MenuItem("ROV/Verify BlueOS connection (no movement)")]
        public static void VerifyBlueOsConnection()
        {
            var request = UnityWebRequest.Get(RovMountProtocol.TelemetryUrl("http://192.168.2.2:6040"));
            request.timeout = 3;
            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success) throw new IOException(request.error);
                    var data = JsonUtility.FromJson<RovMountProtocol.VehicleTelemetry>(request.downloadHandler.text);
                    if (data?.messages?.HEARTBEAT?.message?.mavtype?.type != "MAV_TYPE_SUBMARINE")
                        throw new InvalidDataException("BlueOS did not return an ArduSub heartbeat.");
                    int pwm = data.messages.SERVO_OUTPUT_RAW.message.servo11_raw;
                    File.WriteAllText(ResultPath, JsonUtility.ToJson(new SetupResult
                    {
                        success = true, checks = RovHeadTiltChecks.Run(), scene = ScenePath,
                        message = "UnityWebRequest connected to BlueOS; parsed ROV heartbeat and SERVO11=" + pwm + ". No movement commands sent."
                    }, true));
                    Debug.Log("[ROV tilt] BlueOS connection verified from Unity. SERVO11=" + pwm);
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    File.WriteAllText(ResultPath, JsonUtility.ToJson(new SetupResult { message = error.ToString() }, true));
                }
                finally { request.Dispose(); }
            };
        }

        [Serializable] private sealed class SetupResult
        {
            public bool success;
            public int checks;
            public string scene, message;
        }

        [MenuItem("ROV/Inspect live headset and tilt status (no movement)")]
        public static void WriteRuntimeDiagnostics()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool hasTracked = head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
            bool hasPresence = head.TryGetFeatureValue(CommonUsages.userPresence, out bool present);
            bool hasState = head.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState trackingState);
            bool hasEye = head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion eye);
            bool hasRotation = head.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation);
            var controllers = UnityEngine.Object.FindObjectsByType<RovHeadTiltController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var result = new LiveDiagnostics
            {
                playing = Application.isPlaying, desktopFocused = Application.isFocused,
                xrActive = XRSettings.isDeviceActive, vrFocused = OVRPlugin.hasVrFocus, vrInputFocused = OVRPlugin.hasInputFocus,
                metaInitialized = OVRPlugin.initialized,
                headName = head.name, headValid = head.isValid, hasTracked = hasTracked, tracked = tracked,
                hasPresence = hasPresence, present = present, hasState = hasState, trackingState = (int)trackingState,
                hasEye = hasEye, eye = eye, hasRotation = hasRotation, rotation = rotation,
                controllers = controllers.Select(item => new LiveController
                {
                    scene = item.gameObject.scene.path, active = item.isActiveAndEnabled,
                    status = item.Status, headPitch = item.HeadPitch, targetPitch = item.TargetPitch,
                    reportedPitch = item.ReportedPitch, pwm = item.ServoPwm, accepted = item.CommandAccepted,
                    controlFocused = item.ControlFocused, headTracked = item.HeadTracked,
                    blueOsLive = item.BlueOsLive, commandsSent = item.CommandsSent,
                    compensateRovPitch = item.CompensateRovPitch, attitudeLive = item.AttitudeLive,
                    desiredWorldPitch = item.DesiredWorldPitch, requestedMountPitch = item.RequestedPitch,
                    vehiclePitch = item.VehicleRollPitchYaw.y,
                    pitchCompensationStrength = item.PitchCompensationStrength,
                    estimatedWorldPitch = RovHeadTiltMath.Pitch(RovCameraPoseMath.CameraRotation(item.VehicleRotation,
                        item.ReportedPitch, item.MinimumPitch, item.MaximumPitch))
                }).ToArray()
            };
            File.WriteAllText("Temp/rov-head-tilt-live.json", JsonUtility.ToJson(result, true));
        }

        [Serializable] private sealed class LiveDiagnostics
        {
            public bool playing, desktopFocused, headValid, hasTracked, tracked, hasPresence, present, hasState, hasEye, hasRotation;
            public bool xrActive, vrFocused, vrInputFocused, metaInitialized;
            public string headName;
            public int trackingState;
            public Quaternion eye, rotation;
            public LiveController[] controllers;
        }
        [Serializable] private sealed class LiveController
        {
            public string scene, status;
            public bool active, accepted, controlFocused, headTracked, blueOsLive, compensateRovPitch, attitudeLive;
            public float headPitch, targetPitch, reportedPitch;
            public float desiredWorldPitch, requestedMountPitch, vehiclePitch, estimatedWorldPitch, pitchCompensationStrength;
            public int pwm, commandsSent;
        }
    }
}
