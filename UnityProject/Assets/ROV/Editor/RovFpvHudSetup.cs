using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ROVVR.Editor
{
    public static class RovFpvHudSetup
    {
        private const string MainScene = "Assets/GstUnityPlugin/Scenes/GstUnityPluginDemo01.unity";
        private const string Request = "Temp/rov-fpv-hud.request";
        private const string Result = "Temp/rov-fpv-hud-result.json";

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
            if (EditorApplication.isPlayingOrWillChangePlaymode && action != "runtime" && action != "snapshot") return;
            File.Delete(Request);
            try
            {
                if (action == "install") Install();
                else if (action == "migrate-profile") MigrateSavedLayout();
                else if (action == "verify") Write(true, RovHudChecks.Run() + RunFpvChecks(), "HUD data, control-state and projected instrument checks passed.");
                else if (action == "runtime") WriteRuntime();
                else if (action == "snapshot")
                {
                    if (!Application.isPlaying) throw new InvalidOperationException("Start Play before requesting a HUD frame.");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/fpv-hud-preview.png"));
                    Write(true, 0, "Game frame capture scheduled: Temp/fpv-hud-preview.png. Wait for the file to finish writing before opening it.");
                }
                else throw new InvalidOperationException("Unknown FPV HUD setup request.");
            }
            catch (Exception error) { Debug.LogException(error); Write(false, 0, error.ToString()); }
        }

        [MenuItem("ROV/View/Apply FPV HUD layout to scene")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before applying the FPV layout.");
            var view = FindView();
            Undo.RecordObject(view, "Apply FPV HUD layout");
            view.hudLayout = RovHudLayout.CreateFpv();
            EditorUtility.SetDirty(view);
            if (view.tilt != null)
            {
                var controller = new SerializedObject(view.tilt);
                var status = controller.FindProperty("showDesktopStatus");
                if (status == null) throw new InvalidOperationException("Controller diagnostic visibility setting was not found.");
                status.boolValue = false;
                controller.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            if (!EditorSceneManager.SaveScene(view.gameObject.scene)) throw new IOException("Could not save FPV HUD scene settings.");
            Write(true, RovHudChecks.Run(), "FPV scene layout saved; existing local profiles are unchanged. Use the separate profile action to migrate one.");
        }

        [MenuItem("ROV/View/Apply FPV layout to local profile (backs up first)")]
        public static void MigrateSavedLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before updating the local profile.");
            string path = RovVideoViewProfile.FilePath;
            if (!File.Exists(path)) throw new FileNotFoundException("No saved local profile exists. Start once, then apply this preset.", path);
            string original = File.ReadAllText(path);
            RovVideoViewProfile.Parse(original);
            var before = JObject.Parse(original);
            var after = (JObject)before.DeepClone();
            after["hudLayout"] = JObject.Parse(JsonUtility.ToJson(RovHudLayout.CreateFpv()));
            foreach (var property in before.Properties())
                if (property.Name != "hudLayout" && !JToken.DeepEquals(property.Value, after[property.Name]))
                    throw new InvalidOperationException("A non-layout profile value changed: " + property.Name);
            string updated = after.ToString(Formatting.Indented);
            RovVideoViewProfile.Parse(updated);
            Directory.CreateDirectory("Temp");
            string backup = Path.GetFullPath("Temp/rov-fpv-profile-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
            File.Copy(path, backup, false);
            string temporary = path + ".fpv.tmp";
            File.WriteAllText(temporary, updated, new UTF8Encoding(false));
            File.Replace(temporary, path, null);
            Write(true, 1, "Only the local profile's hudLayout was replaced. Backup: " + backup);
        }

        private static RovVideoViewCalibration FindView()
        {
            var views = UnityEngine.Object.FindObjectsByType<RovVideoViewCalibration>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.gameObject.scene.path == MainScene).ToArray();
            if (views.Length != 1) throw new InvalidOperationException("Open the main ROV scene with exactly one video view.");
            return views[0];
        }

        private static void WriteRuntime()
        {
            var hud = UnityEngine.Object.FindObjectsByType<RovVrOverlay>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.gameObject.scene.path == MainScene);
            bool built = hud != null && hud.Built;
            var graphic = built ? hud.LayoutRoot.GetComponentInChildren<RovFpvAttitudeGraphic>(true) : null;
            bool cards = built && hud.LayoutRoot.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Instrument card");
            if (graphic == null || hud.view == null || hud.view.eye == null)
            { Write(false, 0, "HUD/graphic/view camera unavailable."); return; }
            Canvas.ForceUpdateCanvases();
            var camera = hud.view.eye.GetComponent<Camera>();
            if (camera == null) throw new InvalidOperationException("View eye has no Camera.");
            var mesh = graphic.canvasRenderer.GetMesh();
            if (mesh == null) { Write(false, 0, "CanvasRenderer has no instrument mesh; component presence is insufficient."); return; }
            var tilt = hud.view.tilt;
            Vector3 center = graphic.transform.TransformPoint(graphic.ProjectPoint(Vector2.zero));
            Vector3 videoCenter = hud.view.transform.TransformPoint(RovHudSurfaceGraphic.SurfacePoint(0, 0,
                hud.view.horizontalFov, hud.view.curveDegrees, hud.view.cylindricalSurface));
            var mono = ProjectMesh(camera, graphic, mesh, Camera.MonoOrStereoscopicEye.Mono, center);
            var report = new RuntimeReport
            {
                success = Application.isPlaying && graphic.isActiveAndEnabled && !graphic.canvasRenderer.cull
                    && mesh.vertexCount > 0 && mono.boundsIntersectViewport && !cards,
                message = "Geometry/projection evidence only; inspect the captured game frame and headset for actual pixels. " + hud.Summary,
                playing = Application.isPlaying, frame = Time.frameCount, hudEnabled = hud.view.hudEnabled,
                attitudeEnabled = hud.view.hudShowAttitude, graphicActive = graphic.isActiveAndEnabled,
                canvasCulled = graphic.canvasRenderer.cull, generatedVertices = graphic.GeneratedVertices,
                rendererVertices = mesh.vertexCount, rendererTriangles = mesh.triangles.Length / 3,
                texture = graphic.mainTexture == null ? "null" : graphic.mainTexture.name,
                shader = graphic.material.shader.name, shaderSupported = graphic.material.shader.isSupported,
                material = graphic.material.name, renderQueue = graphic.material.renderQueue,
                canvasAlpha = graphic.canvasRenderer.GetInheritedAlpha(), stereo = camera.stereoEnabled,
                videoCenterErrorMeters = Vector3.Distance(center, videoCenter), centerWorld = center,
                videoScale = hud.view.transform.lossyScale, horizontalFov = hud.view.horizontalFov,
                curve = hud.view.curveDegrees, nearClip = camera.nearClipPlane, farClip = camera.farClipPlane,
                heartbeatFresh = tilt != null && tilt.BlueOsLive,
                attitudeFresh = tilt != null && tilt.AttitudeLive, mountFresh = tilt != null && tilt.MountTelemetryLive,
                horizonAvailable = graphic.HorizonAvailable, estimatedCameraRoll = graphic.Roll, estimatedCameraPitch = graphic.Pitch,
                controlStatus = tilt == null ? "unavailable" : tilt.Status,
                displayedControlStatus = tilt == null ? "unavailable" : RovVrOverlay.CompactControlStatus(tilt.Status, tilt.RcGateStatus, tilt.CommandAccepted, tilt.HeadFollowingEnabled),
                pilotGain = NormalizedSample(tilt != null && tilt.BlueOsLive ? tilt.HudTelemetry.PilotGain(Time.realtimeSinceStartup) : float.NaN),
                light1 = NormalizedSample(tilt != null && tilt.BlueOsLive ? tilt.HudTelemetry.Light1Level(Time.realtimeSinceStartup) : float.NaN),
                light2 = NormalizedSample(tilt != null && tilt.BlueOsLive ? tilt.HudTelemetry.Light2Level(Time.realtimeSinceStartup) : float.NaN),
                mono = mono,
                left = camera.stereoEnabled ? ProjectMesh(camera, graphic, mesh, Camera.MonoOrStereoscopicEye.Left, center) : null,
                right = camera.stereoEnabled ? ProjectMesh(camera, graphic, mesh, Camera.MonoOrStereoscopicEye.Right, center) : null
            };
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Result, JsonUtility.ToJson(report, true));
        }

        private static EyeProjection ProjectMesh(Camera camera, RovFpvAttitudeGraphic graphic, Mesh mesh, Camera.MonoOrStereoscopicEye eye, Vector3 center)
        {
            var result = new EyeProjection { centerViewport = camera.WorldToViewportPoint(center, eye),
                minViewport = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                maxViewport = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity) };
            foreach (var vertex in mesh.vertices)
            {
                Vector3 p = camera.WorldToViewportPoint(graphic.transform.TransformPoint(vertex), eye);
                result.minViewport = Vector3.Min(result.minViewport, p); result.maxViewport = Vector3.Max(result.maxViewport, p);
                if (p.z > camera.nearClipPlane && p.z < camera.farClipPlane && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1)
                    result.verticesInsideViewport++;
            }
            result.boundsIntersectViewport = mesh.vertexCount > 0 && result.maxViewport.z > camera.nearClipPlane
                && result.minViewport.z < camera.farClipPlane && result.maxViewport.x >= 0 && result.minViewport.x <= 1
                && result.maxViewport.y >= 0 && result.minViewport.y <= 1;
            if (graphic.TryMarkPoint(-RovFpvAttitudeGraphic.HorizonHalfWidth, 0, out var left)
                && graphic.TryMarkPoint(RovFpvAttitudeGraphic.HorizonHalfWidth, 0, out var right))
            {
                result.horizonLeftViewport = camera.WorldToViewportPoint(graphic.transform.TransformPoint(graphic.ProjectPoint(left)), eye);
                result.horizonRightViewport = camera.WorldToViewportPoint(graphic.transform.TransformPoint(graphic.ProjectPoint(right)), eye);
            }
            if (mesh.vertexCount == 0) result.minViewport = result.maxViewport = Vector3.zero;
            return result;
        }

        public static int RunFpvChecks()
        {
            int checks = 0;
            Expect(RovVrOverlay.CompactControlStatus("ROV avviste tiltkommandoen", "VR-prioritet: RC-kamerainnganger sperret", false, true)
                == "ROV avviste tiltkommandoen", "Explicit refusal must remain visible"); checks++;
            Expect(RovVrOverlay.CompactControlStatus("Venter på BlueOS", "RC-sperre/gjenoppretting feilet: fixture", false, true)
                == "RC-sperre/gjenoppretting feilet: fixture", "RC gate failure must remain visible"); checks++;
            Expect(RovVrOverlay.CompactControlStatus("Mangler tilt-kvittering; prøver igjen snart", "", false, true)
                == "Mangler tilt-kvittering; prøver igjen snart", "A retry must not imply an autopilot refusal"); checks++;
            Expect(RovVrOverlay.CompactControlStatus("VR-tilt: fersk ACK (ikke fysisk feedback)", "", true, true)
                == "CAM ACK / HEAD FOLLOW", "Only a known accepted state may be shortened"); checks++;
            Vector3 gravity = Quaternion.Inverse(Quaternion.Euler(-10, 0, 0)) * Vector3.up;
            Expect(RovFpvAttitudeGraphic.TryPitchPoint(gravity, 0, 0, 110, out var zero)
                && Mathf.Abs(zero.y + 960f / Mathf.Tan(55f * Mathf.Deg2Rad) * Mathf.Tan(10f * Mathf.Deg2Rad)) < 0.01f,
                "Positive pitch uses pinhole tangent projection, not fixed pixels per degree"); checks++;
            gravity = Quaternion.Inverse(Quaternion.Euler(0, 0, -20)) * Vector3.up;
            Expect(RovFpvAttitudeGraphic.TryPitchPoint(gravity, 100, 0, 110, out var bank) && bank.y > 0,
                "Horizon bank sign"); checks++;
            gravity = Quaternion.Inverse(Quaternion.Euler(-60, 0, 0)) * Vector3.up;
            Expect(RovFpvAttitudeGraphic.TryPitchPoint(gravity, 0, 0, 110, out var beyond) && beyond.y < -900,
                "Off-screen horizon must not saturate at a fake 45-degree level"); checks++;
            checks += RunCameraFrameChecks();
            var go = new GameObject("FPV geometry check (temporary)", typeof(RectTransform), typeof(Canvas));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(1920f, 1080f);
            go.transform.localScale = Vector3.one * 0.001f;
            go.transform.position = Vector3.forward * 2f;
            var mesh = new Mesh();
            var material = new Material(Resources.Load<Shader>("RovHudOverlay"));
            try
            {
                var graphic = RovVrOverlay.CreateAttitudeGraphic(go.transform, material);
                // Do not add a renderer in this fixture: the concrete graphic
                // must supply the production dependency itself.
                Expect(graphic.GetComponent<CanvasRenderer>() != null,
                    "The production graphic composition must include CanvasRenderer before accessing graphic.canvasRenderer"); checks++;
                graphic.SetAttitude(0, 0, true);
                graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                graphic.Rebuild(CanvasUpdate.PreRender);
                var renderedMesh = graphic.canvasRenderer.GetMesh();
                Expect(renderedMesh != null && renderedMesh.vertexCount > 0 && renderedMesh.triangles.Length > 0,
                    "The production UGUI rebuild must submit an instrument mesh to CanvasRenderer"); checks++;
                int fullMeshCount = renderedMesh.vertexCount;
                int reticleMeshCount = 0;
                foreach (float zoom in new[] {2f, 8f})
                {
                    graphic.SetMainZoom(zoom);
                    graphic.Rebuild(CanvasUpdate.PreRender);
                    int count = graphic.canvasRenderer.GetMesh().vertexCount;
                    Expect(graphic.PoseAvailable && !graphic.HorizonAvailable && !graphic.HorizonVisible
                        && count > 0 && count < fullMeshCount, "Zoom must suppress bars and their shared label gate while retaining the native reticle mesh");
                    if (reticleMeshCount > 0) Expect(count == reticleMeshCount, "Both zoom levels retain exactly the fixed reticle");
                    reticleMeshCount = count; checks++;
                }
                graphic.SetMainZoom(1f); graphic.SetAttitude(0, 0, false);
                graphic.Rebuild(CanvasUpdate.PreRender);
                Expect(!graphic.HorizonAvailable && !graphic.HorizonVisible
                    && graphic.canvasRenderer.GetMesh().vertexCount == reticleMeshCount,
                    "Returning to 1x with stale pose must not restore a fake horizon"); checks++;
                graphic.SetAttitude(0, 0, true); graphic.Rebuild(CanvasUpdate.PreRender);
                Expect(graphic.HorizonAvailable && graphic.HorizonVisible
                    && graphic.canvasRenderer.GetMesh().vertexCount == fullMeshCount,
                    "Returning to 1x with fresh pose restores the actual production horizon mesh"); checks++;
                graphic.SetMainZoom(1.0005f);
                Expect(graphic.HorizonZoomAllowed, "Small 1x serialization noise must be tolerated"); checks++;
                graphic.SetMainZoom(1.01f);
                Expect(!graphic.HorizonAvailable, "A real zoom change must suppress the horizon"); checks++;
                graphic.SetMainZoom(1f);
                var populate = typeof(RovFpvAttitudeGraphic).GetMethod("OnPopulateMesh",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null, new[] {typeof(VertexHelper)}, null);
                int staleCount;
                using (var vertices = new VertexHelper())
                {
                    graphic.SetAttitude(0, 0, false); populate.Invoke(graphic, new object[] { vertices });
                    staleCount = vertices.currentVertCount;
                    Expect(staleCount > 0 && !graphic.PoseAvailable, "A missing pose retains only the fixed reticle"); checks++;
                }
                foreach (bool cylindrical in new[] {true, false})
                foreach (float curve in new[] {0f, 45f, 150f})
                using (var vertices = new VertexHelper())
                {
                    graphic.SetProjection(110f, curve, cylindrical); graphic.SetAttitude(20, 10, true);
                    populate.Invoke(graphic, new object[] { vertices }); vertices.FillMesh(mesh);
                    Expect(mesh.vertexCount > staleCount && graphic.MarkCount == 3 && graphic.PitchMark(0) == 0
                        && graphic.PitchMark(1) != graphic.PitchMark(2), "Only horizon and two shared pitch references may be emitted");
                    float horizonSpan = ProjectionSpan(graphic, RovFpvAttitudeGraphic.HorizonHalfWidth);
                    float tickSpan = ProjectionSpan(graphic, RovFpvAttitudeGraphic.TickHalfWidth);
                    Expect(horizonSpan > 15 && horizonSpan < 45 && tickSpan > 5 && tickSpan < 20,
                        "Decorations must occupy a bounded angular region, not the full 107-degree view");
                    foreach (var vertex in mesh.vertices)
                        Expect(RovCameraPoseMath.Finite(vertex.x) && RovCameraPoseMath.Finite(vertex.y) && RovCameraPoseMath.Finite(vertex.z), "Projected vertices must be finite");
                    Expect(graphic.mainTexture == Texture2D.whiteTexture, "Vector mesh must sample solid white independently of the font atlas");
                    checks++;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(material);
            }
            return checks;
        }

        private static float ProjectionSpan(RovFpvAttitudeGraphic graphic, float halfWidth)
        {
            Vector3 origin = Vector3.forward * 1.8f;
            return Vector3.Angle(origin + graphic.ProjectPoint(new Vector2(-halfWidth, 0)) * 0.001f,
                origin + graphic.ProjectPoint(new Vector2(halfWidth, 0)) * 0.001f);
        }

        private static int RunCameraFrameChecks()
        {
            var go = new GameObject("HUD camera frame fixture (inactive)");
            go.SetActive(false); // No controller lifecycle, requests or tracking.
            try
            {
                var view = go.AddComponent<RovVideoViewCalibration>();
                var tilt = go.AddComponent<RovHeadTiltController>();
                view.tilt = tilt;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                void Set(object owner, string field, object value) => owner.GetType().GetField(field, flags).SetValue(owner, value);
                float now = Time.realtimeSinceStartup;
                foreach (string field in new[] {"heartbeat", "attitudeFreshness", "mountFreshness"})
                {
                    var age = (RovHeartbeatFreshness)typeof(RovHeadTiltController).GetField(field, flags).GetValue(tilt);
                    age.Observe(10, now - 0.1f); age.Observe(11, now);
                }
                Set(tilt, "<VehicleRollPitchYaw>k__BackingField", new Vector3(12, 8, 20));
                Set(tilt, "<ReportedPitch>k__BackingField", 5f);
                Quaternion alignment = Quaternion.Euler(-25, 42, 3);
                Quaternion filtered = Quaternion.Euler(-3, 0, -6);
                Set(view, "rovAlignment", alignment); Set(view, "lastPose", alignment * filtered);
                Set(view, "imuCentered", true); Set(view, "poseInitialized", true);
                view.anchorToRovImu = true; view.headFollowingEnabled = true;
                Expect(view.TryGetHudCameraRotation(out var camera) && Quaternion.Angle(camera, filtered) < 0.01f,
                    "HUD must read the same filtered physical camera step and remove nonzero recenter alignment");
                view.imageRollDegrees = 13; view.verticalOffsetDegrees = -7;
                Expect(view.TryGetHudCameraRotation(out camera) && Quaternion.Angle(camera, filtered) < 0.01f,
                    "User presentation trims must not become physical camera attitude");
                Quaternion raw = RovCameraPoseMath.CameraRotation(RovCameraPoseMath.VehicleRotationWithoutYaw(tilt.VehicleRollPitchYaw),
                    tilt.ReportedPitch, tilt.MinimumPitch, tilt.MaximumPitch);
                view.headFollowingEnabled = false; Set(view, "frozenPoseCaptured", true);
                Expect(view.TryGetHudCameraRotation(out camera) && Quaternion.Angle(camera, raw) < 0.01f,
                    "Holding screen placement must retain live camera gravity");
                view.anchorToRovImu = false; view.headFollowingEnabled = true;
                Expect(view.TryGetHudCameraRotation(out camera) && Quaternion.Angle(camera, raw) < 0.01f,
                    "Manual presentation must use fresh physical camera gravity");
                ((RovHeartbeatFreshness)typeof(RovHeadTiltController).GetField("mountFreshness", flags).GetValue(tilt)).Reset();
                Expect(!view.TryGetHudCameraRotation(out camera), "Missing mount freshness must make the estimate unavailable");
                return 5;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void Expect(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        private static string NormalizedSample(float value) => RovCameraPoseMath.Finite(value) ? value.ToString("F3") : "unavailable";

        private static void Write(bool success, int checks, string message)
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Result, JsonUtility.ToJson(new Report { success = success, checks = checks, message = message }, true));
        }

        [Serializable] private sealed class Report { public bool success; public int checks; public string message; }
        [Serializable] private sealed class EyeProjection
        {
            public Vector3 centerViewport, minViewport, maxViewport, horizonLeftViewport, horizonRightViewport;
            public int verticesInsideViewport;
            public bool boundsIntersectViewport;
        }
        [Serializable] private sealed class RuntimeReport
        {
            public bool success, playing, hudEnabled, attitudeEnabled, graphicActive, canvasCulled, stereo, shaderSupported;
            public bool heartbeatFresh, attitudeFresh, mountFresh, horizonAvailable;
            public int frame, generatedVertices, rendererVertices, rendererTriangles, renderQueue;
            public float canvasAlpha, videoCenterErrorMeters, horizontalFov, curve, nearClip, farClip, estimatedCameraRoll, estimatedCameraPitch;
            public Vector3 centerWorld, videoScale;
            public string message, texture, shader, material, controlStatus, displayedControlStatus, pilotGain, light1, light2;
            public EyeProjection mono, left, right;
        }
    }
}
