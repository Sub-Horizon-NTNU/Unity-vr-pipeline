using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ROVVR.Editor
{
    public static class RovVideoViewSetup
    {
        private const string Request = "Temp/rov-video-view.request";
        private const string Result = "Temp/rov-video-view-result.json";
        private const string Backup = "Assets/ROV/Backups/BeforeVideoViewCalibration.unity";

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
            if (action == "install" && EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try
            {
                if (action == "install") Install();
                else if (action == "panel") RovVideoCalibrationWindow.Open();
                else if (action == "trim") ApplyCompensationTrim();
                else if (action == "save") UnityEngine.Object.FindObjectsByType<RovVideoViewCalibration>(FindObjectsSortMode.None).Single().SaveCalibration();
                else if (action != "verify" && action != "runtime") throw new InvalidOperationException("Unknown view request.");
                int checks = RovVideoViewChecks.Run() + RovCameraPoseChecks.Run() + RovManualViewChecks.Run() + RovHudChecks.Run() + RovPilotControlsChecks.Run() + RovLiveSettingsChecks.Run();
                WriteResult(true, checks, action == "install" ? "Installed; original scene saved in " + Backup : "Checks passed.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                WriteResult(false, 0, error.ToString());
            }
        }

        private static void ApplyCompensationTrim()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before saving trim.");
            var view = UnityEngine.Object.FindObjectsByType<RovVideoViewCalibration>(FindObjectsSortMode.None).Single();
            var profile = File.Exists(view.ConfigurationPath)
                ? RovVideoViewProfile.Parse(File.ReadAllText(view.ConfigurationPath)) : view.GetProfile();
            profile.pitchCompensationStrength = 0.9f;
            profile.Save(view.ConfigurationPath);
            view.ApplyProfile(profile);
            EditorUtility.SetDirty(view);
            if (view.tilt != null) EditorUtility.SetDirty(view.tilt);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            if (!EditorSceneManager.SaveScene(view.gameObject.scene)) throw new IOException("Could not save compensation trim.");
            Debug.Log("[ROV tilt] Trial compensation strength 0.90 saved; other configuration values preserved.");
        }

        [MenuItem("ROV/View/Install immersive IMU anchored view")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before view setup.");
            var scene = SceneManager.GetSceneByPath(RovHeadTiltSetup.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the working ROV scene first.");
            var roots = scene.GetRootGameObjects();
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault(item => item.CompareTag("MainCamera"));
            var surface = roots.SelectMany(root => root.GetComponentsInChildren<CurvedVideoScreen>(true)).Single();
            var rig = roots.Single(item => item.name == "XRRig").transform;
            if (camera == null) throw new InvalidOperationException("No Main Camera in the ROV scene.");

            if (!File.Exists(Backup))
            {
                if (!AssetDatabase.IsValidFolder("Assets/ROV/Backups")) AssetDatabase.CreateFolder("Assets/ROV", "Backups");
                if (!EditorSceneManager.SaveScene(scene, Backup, true)) throw new IOException("Could not back up the current scene.");
            }
            const string imuBackup = "Assets/ROV/Backups/BeforeImuView.unity";
            if (!File.Exists(imuBackup) && !EditorSceneManager.SaveScene(scene, imuBackup, true))
                throw new IOException("Could not back up the current view before IMU changes.");

            Undo.RecordObjects(new UnityEngine.Object[] { rig, camera.transform, camera, surface.transform, surface }, "Calibrate ROV view");
            rig.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.localScale = Vector3.one;
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.005f, 0.008f, 0.012f, 1f);
            // Let the XR runtime supply stereo projection and the user's IPD.
            camera.ResetStereoProjectionMatrices();
            camera.ResetStereoViewMatrices();

            surface.transform.localScale = Vector3.one;
            surface.flipHorizontal = false;
            surface.flipVertical = true;
            // The old scene's 180-degree Z rotation plus mirrored U amounted
            // to a vertical texture flip. Preserve that net image orientation.
            var view = surface.GetComponent<RovVideoViewCalibration>();
            if (view == null) view = Undo.AddComponent<RovVideoViewCalibration>(surface.gameObject);
            view.eye = camera.transform;
            view.horizontalFov = 110f;
            view.curveDegrees = 45f;
            view.distance = 2f;
            view.cylindricalSurface = true;
            view.verticalOffsetDegrees = 0f;
            view.followHeadRoll = false;
            view.imageRollDegrees = 0f;
            view.followHeadPitch = false;
            view.followHeadTranslation = true;
            view.tilt = camera.GetComponent<RovHeadTiltController>();
            view.anchorToRovImu = true;
            view.ApplyGeometry();
            view.CenterView();

            var stream = surface.GetComponent<PipelineComponentStream>();
            if (stream != null)
            {
                var properties = new SerializedObject(stream);
                properties.FindProperty("_filterMode").intValue = (int)FilterMode.Bilinear;
                properties.FindProperty("_wrapMode").intValue = (int)TextureWrapMode.Clamp;
                // The native plugin updates level 0, so don't enable stale mip levels.
                properties.FindProperty("_generateMips").boolValue = false;
                properties.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save calibrated ROV scene.");
            AssetDatabase.SaveAssets();
            if (!File.Exists(view.ConfigurationPath)) view.SaveCalibration();
            Debug.Log("[ROV view] Installed manual 110° coverage / 45° curvature at 2 m. JSON: " + view.ConfigurationPath);
        }

        [MenuItem("ROV/View/Verify projection (no movement)")]
        public static void Verify()
        {
            Debug.Log("[ROV view] " + (RovVideoViewChecks.Run() + RovCameraPoseChecks.Run() + RovManualViewChecks.Run() + RovHudChecks.Run() + RovPilotControlsChecks.Run() + RovLiveSettingsChecks.Run()) + " projection, IMU, HUD and configuration checks passed.");
        }

        private static void WriteResult(bool success, int checks, string message)
        {
            var view = UnityEngine.Object.FindObjectsByType<RovVideoViewCalibration>(FindObjectsSortMode.None).FirstOrDefault();
            var camera = Camera.main;
            var report = new ViewResult { success = success, checks = checks, message = message, playing = Application.isPlaying };
            if (view != null)
            {
                report.horizontalFov = view.horizontalFov;
                report.apparentHorizontalFov = view.ApparentHorizontalFov;
                report.meshReferenceDistance = view.GetComponent<CurvedVideoScreen>().viewingDistance;
                report.curveDegrees = view.curveDegrees;
                report.configurationPath = view.ConfigurationPath;
                report.verticalFov = view.VerticalFov;
                report.radius = view.distance;
                report.cylindrical = view.cylindricalSurface;
                report.verticalOffsetDegrees = view.verticalOffsetDegrees;
                report.verticalPositionMeters = view.verticalPositionMeters;
                report.centerPixelsPerDegree = view.CenterPixelsPerDegree;
                report.averagePixelsPerDegree = view.AveragePixelsPerDegree;
                report.centered = view.IsCentered;
                report.imuAnchored = view.anchorToRovImu;
                report.poseLive = view.PoseLive;
                report.poseStatus = view.PoseStatus;
                report.reportedMountPitch = view.DisplayMountPitch;
                report.surfaceRotation = view.transform.rotation;
                report.hudEnabled = view.hudEnabled;
                report.hudAnchorMode = view.hudAnchorMode.ToString();
                report.headFollowingEnabled = view.headFollowingEnabled;
                report.screenLocked = view.ScreenLocked;
                var overlay = view.GetComponent<RovVrOverlay>();
                if (overlay != null)
                {
                    report.hudBuilt = overlay.Built;
                    report.hudAttachedToStream = overlay.AttachedToStream;
                    report.hudFloatingAnchored = overlay.FloatingAnchored;
                    report.hudSummary = overlay.Summary;
                    report.renderFps = overlay.RenderFps; report.videoFps = overlay.VideoFps; report.headsetHz = overlay.HeadsetHz;
                }
                if (view.tilt != null)
                {
                    report.vehicleRollPitchYaw = view.tilt.VehicleRollPitchYaw;
                    report.atTiltLimit = view.tilt.AtTiltLimit;
                    report.requestedPitch = view.tilt.RequestedPitch;
                    report.compensateRovPitch = view.tilt.CompensateRovPitch;
                    report.desiredWorldPitch = view.tilt.DesiredWorldPitch;
                    report.pitchCompensationStrength = view.tilt.PitchCompensationStrength;
                }
                report.surfacePosition = view.transform.position;
                report.surfaceScale = view.transform.lossyScale;
                if (view.eye != null) report.eyePosition = view.eye.position;
                var mesh = view.GetComponent<MeshFilter>().sharedMesh;
                report.vertices = mesh == null ? 0 : mesh.vertexCount;
                if (camera != null)
                {
                    Vector3 localEye = view.transform.InverseTransformPoint(camera.transform.position);
                    Vector3 expectedEye = new Vector3(0f, 0f, -view.distance)
                        - view.transform.InverseTransformVector(Vector3.up * view.verticalPositionMeters);
                    report.eyeErrorFromProjectionOrigin = Vector3.Distance(localEye, expectedEye);
                    report.headRotation = camera.transform.rotation;
                }
            }
            File.WriteAllText(Result, JsonUtility.ToJson(report, true));
        }

        [Serializable] private sealed class ViewResult
        {
            public bool success, playing, centered, cylindrical, imuAnchored, poseLive, atTiltLimit, compensateRovPitch;
            public int checks, vertices;
            public string message, poseStatus, configurationPath;
            public float horizontalFov, verticalFov, radius, verticalOffsetDegrees, centerPixelsPerDegree, averagePixelsPerDegree, eyeErrorFromProjectionOrigin;
            public float verticalPositionMeters;
            public bool hudEnabled, hudBuilt, hudAttachedToStream, hudFloatingAnchored, headFollowingEnabled, screenLocked;
            public string hudSummary, hudAnchorMode;
            public float renderFps, videoFps, headsetHz;
            public Vector3 surfacePosition, surfaceScale, eyePosition;
            public Vector3 vehicleRollPitchYaw;
            public Quaternion surfaceRotation, headRotation;
            public float reportedMountPitch, requestedPitch, curveDegrees, desiredWorldPitch, apparentHorizontalFov, meshReferenceDistance, pitchCompensationStrength;
        }
    }

    [CustomEditor(typeof(RovVideoViewCalibration))]
    public sealed class RovVideoViewCalibrationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var view = (RovVideoViewCalibration)target;
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) { view.ApplyGeometry(); EditorUtility.SetDirty(view); }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Video", "1920 x 1080, mono");
            EditorGUILayout.LabelField("Vertical field of view", view.VerticalFov.ToString("F1") + "°");
            EditorGUILayout.LabelField("Source pixels / degree (center)", view.CenterPixelsPerDegree.ToString("F1"));
            EditorGUILayout.LabelField("Orientation", view.PoseStatus);
            if (GUILayout.Button("Apply / preview view")) { view.ApplyGeometry(); view.CenterView(); }
            if (GUILayout.Button("Save JSON configuration")) view.SaveCalibration();
            if (GUILayout.Button("Load JSON configuration")) view.LoadCalibration();
            EditorGUILayout.SelectableLabel(view.ConfigurationPath, EditorStyles.wordWrappedLabel, GUILayout.Height(38));
        }
    }

    public sealed class RovVideoCalibrationWindow : EditorWindow
    {
        private Vector2 scroll;
        private readonly bool[] expandedBoxes = new bool[6];
        [MenuItem("ROV/View/Manual calibration")]
        public static void Open()
        {
            var window = GetWindow<RovVideoCalibrationWindow>("ROV bilde");
            window.minSize = new Vector2(440f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            var view = UnityEngine.Object.FindObjectsByType<RovVideoViewCalibration>(FindObjectsSortMode.None).FirstOrDefault();
            if (view == null) { EditorGUILayout.HelpBox("Åpne ROV-scenen for å justere bildet.", MessageType.Info); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Manuell kalibrering av videoflaten", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Størrelse og krumming justeres separat. Endringer vises med en gang. Lagre JSON for å beholde dem etter Play.", MessageType.Info);
            Undo.RecordObject(view, "Adjust ROV video view");
            EditorGUI.BeginChangeCheck();
            view.SetHeadFollowingEnabled(EditorGUILayout.Toggle(new GUIContent("Hodefølging på (A)", "Av låser skjermens posisjon/vinkel og holder siste kameratilt. A slår på igjen."), view.headFollowingEnabled));
            view.horizontalFov = EditorGUILayout.Slider("Bildestørrelse (grader)", view.horizontalFov, 50f, 150f);
            view.curveDegrees = EditorGUILayout.Slider("Krumming (0 = flat)", view.curveDegrees, 0f, 150f);
            view.distance = EditorGUILayout.Slider("Avstand (meter)", view.distance, 1f, 4f);
            view.verticalPositionMeters = EditorGUILayout.Slider(new GUIContent("Skjermplassering (m)", "Opp/ned fra øyehøyde. Positivt = opp, negativt = ned."), view.verticalPositionMeters, -2f, 2f);
            view.verticalOffsetDegrees = EditorGUILayout.Slider("Vinkel opp/ned (grader)", view.verticalOffsetDegrees, -25f, 25f);
            view.imageRollDegrees = EditorGUILayout.Slider("Helning (grader)", view.imageRollDegrees, -15f, 15f);
            bool anchored = view.anchorToRovImu;
            view.anchorToRovImu = EditorGUILayout.Toggle(new GUIContent("ROV pitch/roll", "Følger tipping og kameratilt. ROV-ens sving til høyre/venstre flytter ikke videoflaten."), view.anchorToRovImu);
            if (view.tilt != null)
            {
                view.SetPitchCompensation(EditorGUILayout.Toggle("Kompenser ROV-pitch", view.tilt.CompensateRovPitch));
                view.tilt.PitchCompensationStrength = EditorGUILayout.Slider("Kompensasjonsstyrke", view.tilt.PitchCompensationStrength, 0f, 1.5f);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Utjevning (0 sekunder = av)", EditorStyles.boldLabel);
            view.telemetrySmoothingSeconds = EditorGUILayout.Slider("Bilde: IMU (s)", view.telemetrySmoothingSeconds, 0f, 0.2f);
            if (view.tilt != null)
            {
                view.tilt.ServoSmoothingSeconds = EditorGUILayout.Slider("Kameratilt: utjevning (s)", view.tilt.ServoSmoothingSeconds, 0f, 0.5f);
                view.tilt.ServoMaximumSpeed = EditorGUILayout.Slider("Kameratilt: fart (°/s)", view.tilt.ServoMaximumSpeed, 5f, 90f);
            }
            EditorGUILayout.HelpBox("Mer utjevning gir mer etterslep. Fartsgrensen på fysisk kameratilt gjelder også ved 0 s. Utgangspunkt: bilde 0,06 s, servo 0,12 s og 45°/s.", MessageType.None);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VR-HUD", EditorStyles.boldLabel);
            view.hudEnabled = EditorGUILayout.Toggle("Vis HUD i VR (B)", view.hudEnabled);
            view.hudAnchorMode = (RovHudAnchorMode)EditorGUILayout.Popup("HUD følger", (int)view.hudAnchorMode, new[] {"Videoflaten", "Hodet", "Fast X-retning"});
            view.hudShowAttitude = EditorGUILayout.Toggle("Pitch/roll og kameratilt", view.hudShowAttitude);
            view.hudShowBattery = EditorGUILayout.Toggle("Batteri", view.hudShowBattery);
            view.hudShowDepth = EditorGUILayout.Toggle(new GUIContent("Estimert dybde", "AHRS2-estimat, samme kilde som Cockpits tradisjonelle dybdevisning."), view.hudShowDepth);
            view.hudShowDiagnostics = EditorGUILayout.Toggle("FPS og headset-Hz", view.hudShowDiagnostics);
            view.hudShowCompass = EditorGUILayout.Toggle("Kompassring", view.hudShowCompass);
            view.compassRadius = EditorGUILayout.Slider("Kompassradius (m)", view.compassRadius, 1f, 3f);
            view.compassDrop = EditorGUILayout.Slider("Kompass ned fra øyne", view.compassDrop, 0.4f, 1.8f);
            view.hudScale = EditorGUILayout.Slider("HUD-tekststørrelse", view.hudScale, 0.7f, 1.5f);
            view.hudOpacity = EditorGUILayout.Slider("HUD-synlighet", view.hudOpacity, 0.2f, 1f);
            EditorGUILayout.HelpBox("Videoflaten: boksene følger bildet. Hodet: boksene er alltid foran øynene. Fast X-retning: tidligere uavhengig plassering. A låser boksene i rommet. Kompassringen beholdes separat nederst.", MessageType.None);
            if (view.hudLayout == null) view.hudLayout = new RovHudLayout();
            DrawBox("Status", view.hudLayout.status, 0);
            DrawBox("Batteri", view.hudLayout.battery, 1);
            DrawBox("Dybde", view.hudLayout.depth, 2);
            DrawBox("Kameratilt", view.hudLayout.cameraTilt, 3);
            DrawBox("ROV-vinkel", view.hudLayout.attitude, 4);
            DrawBox("Diagnostikk", view.hudLayout.diagnostics, 5);
            if (GUILayout.Button("Tilbakestill bare HUD-plassering")) view.hudLayout = new RovHudLayout();
            if (EditorGUI.EndChangeCheck())
            {
                view.ApplyGeometry();
                if (anchored != view.anchorToRovImu) view.CenterView();
                EditorUtility.SetDirty(view);
                if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Synlig bredde", view.ApparentHorizontalFov.ToString("F1") + "°");
            EditorGUILayout.HelpBox("Avstand flytter skjermen uten å skalere den: nærmere gir større bilde. Bildestørrelsen i grader er angitt ved 2 m.", MessageType.None);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Lagre JSON")) view.SaveCalibration();
            if (GUILayout.Button("Last JSON")) view.LoadCalibration();
            if (GUILayout.Button("Standard")) view.RestoreManualDefaults();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Sentrer bilde og kameratilt")) view.CenterViewAndTilt();
            if (GUILayout.Button("Vis konfigurasjonsfil i Explorer"))
            {
                if (!File.Exists(view.ConfigurationPath)) view.SaveCalibration();
                EditorUtility.RevealInFinder(view.ConfigurationPath);
            }
            EditorGUILayout.SelectableLabel(view.ConfigurationPath, EditorStyles.wordWrappedLabel, GUILayout.Height(42));
            EditorGUILayout.HelpBox("A: hodefølging på/av; av låser skjerm og kameratilt.\nB: vis/skjul HUD og kompass.\nVenstre spak: størrelse / avstand\nAvtrekker + spak sideveis: krumming\nGrip + spak: helning / høyde\nX: sentrer bilde og kameratilt. Y: lagre JSON.", MessageType.None);
            EditorGUILayout.LabelField(view.PoseStatus);
            EditorGUILayout.EndScrollView();
        }

        private void OnInspectorUpdate() { Repaint(); }

        private void DrawBox(string title, RovHudBoxLayout box, int index)
        {
            expandedBoxes[index] = EditorGUILayout.Foldout(expandedBoxes[index], title + " — plassering", true);
            if (!expandedBoxes[index]) return;
            EditorGUI.indentLevel++;
            box.position.x = EditorGUILayout.Slider("Høyre/venstre (m)", box.position.x, -2f, 2f);
            box.position.y = EditorGUILayout.Slider("Høyde (m)", box.position.y, -2f, 2f);
            box.position.z = EditorGUILayout.Slider("Avstand fremover (m)", box.position.z, 0.6f, 4f);
            box.scale = EditorGUILayout.Slider("Boksstørrelse", box.scale, 0.5f, 2f);
            box.faceViewer = EditorGUILayout.Toggle("Vinkle mot øyeposisjon", box.faceViewer);
            box.angleOffset.x = EditorGUILayout.Slider("Ekstra pitch (°)", box.angleOffset.x, -60f, 60f);
            box.angleOffset.y = EditorGUILayout.Slider("Ekstra yaw (°)", box.angleOffset.y, -60f, 60f);
            box.angleOffset.z = EditorGUILayout.Slider("Ekstra roll (°)", box.angleOffset.z, -60f, 60f);
            EditorGUI.indentLevel--;
        }
    }
}
