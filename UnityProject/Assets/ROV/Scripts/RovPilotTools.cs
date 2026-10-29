using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace ROVVR
{
    public sealed partial class RovVideoViewCalibration
    {
        public RovPilotSettings pilot = new RovPilotSettings();
        private bool pilotMenuOpen, resetPilotEdges = true, pilotHadFocus;
        private readonly bool[] pilotHeld = new bool[10];
        private float nextZoomStep;
        private Mesh zoomMesh;
        private Vector2[] originalUv, croppedUv;
        private Vector3[] pickVertices;
        private int[] pickTriangles;
        private float appliedZoom = -1f;
        private Vector2 appliedCenter;
        private Canvas detailCanvas;
        private RawImage detailImage;
        private Text detailCaption;
        private Material pilotUiMaterial, controllerMaterial;
        private readonly Transform[] controllerModels = new Transform[2];
        private readonly Transform[] stickModels = new Transform[2];
        private readonly LineRenderer[] controllerRays = new LineRenderer[2];
        private readonly Ray[] handRays = new Ray[2];
        private readonly bool[] handTracked = new bool[2];
        private string profileName = "Pilot 1", pilotMessage = "";
        private string[] profileNames = Array.Empty<string>();
        private bool profileListLoaded, desktopToolsExpanded;
        private int selectedProfile;
#if ENABLE_INPUT_SYSTEM
        private static readonly Key[] PilotKeys = BuildPilotKeys();
        private static Key[] BuildPilotKeys()
        {
            var values = (RovPilotKey[])Enum.GetValues(typeof(RovPilotKey));
            var keys = new Key[values.Length];
            for (int i = 1; i < values.Length; i++) Enum.TryParse(values[i].ToString(), out keys[i]);
            return keys;
        }
#endif

        private void ResetPilotInputEdges() { resetPilotEdges = true; }

        private bool ReadPilotButton(RovPilotButton button)
        {
            if (button == RovPilotButton.None) return false;
            bool left = button == RovPilotButton.X || button == RovPilotButton.Y || button == RovPilotButton.LeftStick
                || button == RovPilotButton.LeftMenu || button == RovPilotButton.LeftTrigger || button == RovPilotButton.LeftGrip;
            var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
            if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked) return false;
            if (button == RovPilotButton.RightUp || button == RovPilotButton.RightDown)
            {
                device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis);
                return button == RovPilotButton.RightUp ? axis.y > 0.7f : axis.y < -0.7f;
            }
            var usage = CommonUsages.primaryButton;
            switch (button)
            {
                case RovPilotButton.Y: case RovPilotButton.B: usage = CommonUsages.secondaryButton; break;
                case RovPilotButton.LeftStick: case RovPilotButton.RightStick: usage = CommonUsages.primary2DAxisClick; break;
                case RovPilotButton.LeftMenu: usage = CommonUsages.menuButton; break;
                case RovPilotButton.LeftTrigger: case RovPilotButton.RightTrigger: usage = CommonUsages.triggerButton; break;
                case RovPilotButton.LeftGrip: case RovPilotButton.RightGrip: usage = CommonUsages.gripButton; break;
            }
            return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
        }

        private void ReadPilotButtons(bool ready)
        {
            if (pilot == null) pilot = new RovPilotSettings();
            // Keep this frame's binding snapshot if a profile replaces it during dispatch.
            var bindings = pilot.bindings;
            bool controlsLive = ready && pilotHadFocus && !resetPilotEdges;
            bool menuWasOpen = pilotMenuOpen;
            bool desktopAllowed = RovHeadTiltFocus.DesktopShortcutsAllowed;
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                bool held = ReadPilotButton(binding.button);
                bool key = false;
#if ENABLE_INPUT_SYSTEM
                if (desktopAllowed && binding.key != RovPilotKey.None && Keyboard.current != null)
                    key = Keyboard.current[PilotKeys[(int)binding.key]].wasPressedThisFrame;
#endif
                bool repeat = controlsLive && held && (binding.action == RovPilotAction.ZoomIn || binding.action == RovPilotAction.ZoomOut)
                    && Time.unscaledTime >= nextZoomStep;
                bool suppress = (startupPending && binding.action != RovPilotAction.Menu) || WindowDragActive || ((pilotMenuOpen || menuWasOpen) && binding.action != RovPilotAction.Menu)
                    || (GrabConsumesPilotInput(binding.button) && binding.action != RovPilotAction.Menu)
                    || (binding.action == RovPilotAction.AimZoom && TryPointedWindow(1, out _, out _, out _));
                if (ProcessPilotInput(binding.action, held, controlsLive, key, repeat, suppress))
                {
                    if (binding.action == RovPilotAction.Load) { pilotHadFocus = ready; return; }
                    if (binding.action == RovPilotAction.ZoomIn || binding.action == RovPilotAction.ZoomOut) nextZoomStep = Time.unscaledTime + 0.12f;
                }
            }
            resetPilotEdges = false;
            pilotHadFocus = ready;
        }

        private bool ProcessPilotInput(RovPilotAction action, bool held, bool ready, bool key, bool repeat, bool suppress)
        {
            bool edge = ready && held && !pilotHeld[(int)action];
            pilotHeld[(int)action] = held;
            if (suppress || !(edge || key || repeat)) return false;
            ExecutePilotAction(action); return true;
        }

        private void ExecutePilotAction(RovPilotAction action)
        {
            switch (action)
            {
                case RovPilotAction.Follow: SetHeadFollowingEnabled(!headFollowingEnabled); break;
                case RovPilotAction.Hud: ToggleHud(); break;
                case RovPilotAction.Center: CenterViewAndTilt(); break;
                case RovPilotAction.Save: SaveCalibration(); break;
                case RovPilotAction.Load: LoadCalibration(); break;
                case RovPilotAction.Menu: TogglePilotMenu(); break;
                case RovPilotAction.ZoomIn: pilot.mainZoom = Mathf.Min(8f, pilot.mainZoom + 0.25f); break;
                case RovPilotAction.ZoomOut: pilot.mainZoom = Mathf.Max(1f, pilot.mainZoom - 0.25f); break;
                case RovPilotAction.ResetZoom: pilot.mainZoom = 1f; pilot.zoomCenter = Vector2.one * 0.5f; break;
                case RovPilotAction.AimZoom:
                    if (handTracked[1]) AimZoom(handRays[1]);
                    else if (eye != null) AimZoom(new Ray(eye.position, eye.forward));
                    break;
            }
        }

        public bool Rebind(RovPilotAction action, RovPilotButton button, RovPilotKey key)
        {
            if (!Enum.IsDefined(typeof(RovPilotAction), action) || !Enum.IsDefined(typeof(RovPilotButton), button)
                || !Enum.IsDefined(typeof(RovPilotKey), key)) return false;
            if (key == RovPilotKey.F4 || (key >= RovPilotKey.F6 && key <= RovPilotKey.F10))
            { pilotMessage = "F4 and F6-F10 are reserved desktop calibration/status keys."; return false; }
            foreach (var b in pilot.bindings)
                if (b.action != action && ((button != RovPilotButton.None && b.button == button) || (key != RovPilotKey.None && b.key == key)))
                { pilotMessage = "Already in use: " + b.action + ". Set that binding to None first."; return false; }
            foreach (var b in pilot.bindings) if (b.action == action) { b.button = button; b.key = key; }
            ResetPilotInputEdges(); pilotMessage = "Binding updated. Save JSON/profile to keep it."; return true;
        }

        private void UpdatePilotTools()
        {
            if (eye == null) return;
            EnsurePilotMaterials();
            UpdatePilotStartup();
            UpdateZoomMesh();
            UpdateDetailView();
            if (pilotMenuOpen) ApplyMenuPlacement();
            UpdatePilotMenu();
            UpdatePointerFeedback();
            UpdateRecordingFeedback();
        }

        private void EnsurePilotMaterials()
        {
            if (pilotUiMaterial == null) pilotUiMaterial = new Material(Resources.Load<Shader>("RovHudOverlay")) { renderQueue = 4050 };
            if (controllerMaterial == null) controllerMaterial = new Material(Resources.Load<Shader>("RovPilotSolid"));
        }

        private void UpdateZoomMesh()
        {
            var mesh = GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null) return;
            if (mesh != zoomMesh)
            {
                zoomMesh = mesh; originalUv = mesh.uv; croppedUv = new Vector2[originalUv.Length];
                pickVertices = mesh.vertices; pickTriangles = mesh.triangles; appliedZoom = -1f;
            }
            if (Mathf.Approximately(appliedZoom, pilot.mainZoom) && appliedCenter == pilot.zoomCenter) return;
            var curved = GetComponent<CurvedVideoScreen>();
            var crop = RovPilotSettings.Crop(pilot.zoomCenter, pilot.mainZoom);
            for (int i = 0; i < croppedUv.Length; i++)
            {
                Vector2 displayed = new Vector2(curved.flipHorizontal ? 1f - originalUv[i].x : originalUv[i].x,
                    curved.flipVertical ? 1f - originalUv[i].y : originalUv[i].y);
                Vector2 source = crop.position + Vector2.Scale(displayed, crop.size);
                croppedUv[i] = new Vector2(curved.flipHorizontal ? 1f - source.x : source.x, curved.flipVertical ? 1f - source.y : source.y);
            }
            mesh.uv = croppedUv; appliedZoom = pilot.mainZoom; appliedCenter = pilot.zoomCenter;
        }

        // Pick against the actual curved triangles; no collider cooking or physics interaction.
        public bool AimZoom(Ray worldRay)
        {
            UpdateZoomMesh();
            if (zoomMesh == null) return false;
            Ray ray = new Ray(transform.InverseTransformPoint(worldRay.origin), transform.InverseTransformVector(worldRay.direction));
            float closest = float.PositiveInfinity; Vector2 uv = Vector2.zero;
            for (int i = 0; i < pickTriangles.Length; i += 3)
            {
                int a = pickTriangles[i], b = pickTriangles[i + 1], c = pickTriangles[i + 2];
                Vector3 edge1 = pickVertices[b] - pickVertices[a], edge2 = pickVertices[c] - pickVertices[a];
                Vector3 p = Vector3.Cross(ray.direction, edge2); float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < 0.000001f) continue;
                float inverse = 1f / determinant;
                Vector3 s = ray.origin - pickVertices[a]; float u = Vector3.Dot(s, p) * inverse;
                if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, edge1); float v = Vector3.Dot(ray.direction, q) * inverse;
                if (v < 0f || u + v > 1f) continue;
                float t = Vector3.Dot(edge2, q) * inverse;
                if (t < 0f || t >= closest) continue;
                closest = t; uv = croppedUv[a] * (1f - u - v) + croppedUv[b] * u + croppedUv[c] * v;
            }
            if (float.IsPositiveInfinity(closest)) { pilotMessage = "Point at the main video to choose a zoom target."; return false; }
            var curved = GetComponent<CurvedVideoScreen>();
            pilot.zoomCenter = new Vector2(curved.flipHorizontal ? 1f - uv.x : uv.x, curved.flipVertical ? 1f - uv.y : uv.y);
            pilotMessage = "Zoom target selected."; return true;
        }

        private void UpdateDetailView()
        {
            if (!pilot.detailEnabled) { if (detailCanvas != null) detailCanvas.gameObject.SetActive(false); return; }
            if (detailCanvas == null)
            {
                var go = new GameObject("ROV detail video (shared texture)", typeof(RectTransform), typeof(Canvas));
                detailCanvas = go.GetComponent<Canvas>(); detailCanvas.renderMode = RenderMode.WorldSpace;
                detailCanvas.worldCamera = eye.GetComponent<Camera>(); detailCanvas.sortingOrder = 90;
                var root = (RectTransform)go.transform; root.sizeDelta = new Vector2(800, 490);
                var bg = PilotNode("Frame", root, 0, 0, 820, 510).gameObject.AddComponent<Image>();
                bg.color = new Color(0.015f, 0.035f, 0.06f, 1f); bg.material = pilotUiMaterial;
                detailImage = PilotNode("Video", root, 0, -15, 800, 450).gameObject.AddComponent<RawImage>();
                detailImage.material = pilotUiMaterial;
                detailCaption = PilotLabel(root, "Detail", 0, 225, 780, 32, 22);
            }
            detailCanvas.gameObject.SetActive(true);
            UpdateDetailPose();
            var source = GetComponent<Renderer>().sharedMaterial;
            var tex = source != null && source.HasProperty("_rgb") ? source.GetTexture("_rgb") : null;
            detailImage.texture = tex; detailImage.enabled = tex != null;
            var crop = RovPilotSettings.Crop(pilot.zoomCenter, pilot.detailZoom);
            var curved = GetComponent<CurvedVideoScreen>();
            detailImage.uvRect = new Rect(curved.flipHorizontal ? 1f - crop.x : crop.x,
                curved.flipVertical ? 1f - crop.y : crop.y, curved.flipHorizontal ? -crop.width : crop.width,
                curved.flipVertical ? -crop.height : crop.height);
            string caption = tex == null ? "DETAIL / waiting for RGBA video" : $"DETAIL  {pilot.detailZoom:F1}x  /  {1920f / pilot.detailZoom:F0} x {1080f / pilot.detailZoom:F0} source px";
            if (detailCaption.text != caption) detailCaption.text = caption;
        }

        [BeforeRenderOrder(210)]
        private void UpdateDetailPose()
        {
            if (!isActiveAndEnabled || detailCanvas == null || !pilot.detailEnabled) return;
            var rt = (RectTransform)detailCanvas.transform;
            rt.SetPositionAndRotation(PilotHudPosition + PilotHudRotation * pilot.detailPosition, PilotHudRotation * Quaternion.Euler(pilot.detailAngles));
            rt.localScale = Vector3.one * (pilot.detailWidth / 800f);
        }

        private void HidePilotTools()
        {
            HidePointerInteraction();
            if (detailCanvas != null) detailCanvas.gameObject.SetActive(false);
            if (pilotMenuRoot != null) pilotMenuRoot.gameObject.SetActive(false);
            pilotMenuOpen = false;
            for (int i = 0; i < 2; i++)
            {
                if (controllerModels[i] != null) controllerModels[i].gameObject.SetActive(false);
                if (controllerRays[i] != null) controllerRays[i].gameObject.SetActive(false);
            }
        }

        private void UpdateController(int index)
        {
            var device = InputDevices.GetDeviceAtXRNode(index == 0 ? XRNode.LeftHand : XRNode.RightHand);
            handTracked[index] = device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked
                && device.TryGetFeatureValue(CommonUsages.devicePosition, out _) && device.TryGetFeatureValue(CommonUsages.deviceRotation, out _);
            if (controllerModels[index] == null)
            {
                BuildControllerModel(index);
                var line = new GameObject("ROV pointer").AddComponent<LineRenderer>();
                line.sharedMaterial = pointerMaterial; line.sortingOrder = 15000; line.positionCount = 2; line.startWidth = 0.0025f; line.endWidth = 0.0015f;
                line.startColor = line.endColor = new Color(0.3f, 0.9f, 1f, 0.85f); line.useWorldSpace = true;
                controllerRays[index] = line;
            }
            bool visible = handTracked[index] && IsHeadReady();
            controllerModels[index].gameObject.SetActive(visible && pilot.showControllers);
            controllerRays[index].gameObject.SetActive(visible && (pilot.showPointers || pilotMenuOpen));
            if (!visible) { pointerFilterReady[index] = false; return; }
            device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
            device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);
            Transform origin = eye.parent;
            Vector3 worldPos = origin == null ? pos : origin.TransformPoint(pos);
            Quaternion worldRot = origin == null ? rot : origin.rotation * rot;
            controllerModels[index].SetPositionAndRotation(worldPos, worldRot);
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stickAxis);
            stickModels[index].localPosition = new Vector3((index == 0 ? 0.012f : -0.012f) + stickAxis.x * 0.008f, 0.046f, 0.017f + stickAxis.y * 0.008f);
            // Prefer the runtime's aim pose; device pose is a fallback for older backends.
            if (TryRuntimeAim(index, out Vector3 aimPos, out Quaternion aimRot))
            { worldPos = origin == null ? aimPos : origin.TransformPoint(aimPos); worldRot = origin == null ? aimRot : origin.rotation * aimRot; }
            Vector3 direction = AdjustPointer(index, worldRot);
            handRays[index] = new Ray(worldPos, direction);
            controllerRays[index].SetPosition(0, worldPos);
            controllerRays[index].SetPosition(1, worldPos + direction * 3f);
        }

        public void SaveNamedProfile(string name)
        {
            try { GetProfile().Save(RovProfileLibrary.ProfilePath(name)); activeProfileName = name; RefreshProfiles(); pilotMessage = "Profil lagret: " + name; }
            catch (Exception error) { pilotMessage = error.Message; }
        }
        public void LoadNamedProfile(string name) => TryLoadNamedProfile(name);
        private bool TryLoadNamedProfile(string name)
        {
            try
            {
                var profile = RovVideoViewProfile.Parse(File.ReadAllText(RovProfileLibrary.ProfilePath(name)));
                ApplyProfile(profile); profileName = activeProfileName = name; pilotMessage = "Lastet: " + RovProfileLibrary.FriendlyName(name) + ". Lagre oppstartsoppsett for å beholde endringene.";
                if (pilotMenuOpen) BuildMenuPage();
                return true;
            }
            catch (Exception error) { pilotMessage = error.Message; return false; }
        }
        private void RefreshProfiles()
        {
            try { profileNames = RovProfileLibrary.Names(); selectedProfile = Mathf.Clamp(selectedProfile, 0, Mathf.Max(0, profileNames.Length - 1)); profileListLoaded = true; }
            catch (Exception error) { pilotMessage = error.Message; }
        }

        public void DrawPilotTools()
        {
            desktopToolsExpanded = GUILayout.Toggle(desktopToolsExpanded, (desktopToolsExpanded ? "▼ " : "▶ ") + "Zoom / VR menu / profiles / bindings");
            if (!desktopToolsExpanded) return;
            if (pilot == null) pilot = new RovPilotSettings();
            GUILayout.Label("Digital zoom / same source video, no extra detail");
            DrawStartupDesktop();
            DrawRecordingControls();
            Slider("Main video zoom", ref pilot.mainZoom, 1f, 8f, "x");
            Slider("Target left/right", ref pilot.zoomCenter.x, 0f, 1f, "", "F2");
            Slider("Target bottom/top", ref pilot.zoomCenter.y, 0f, 1f, "", "F2");
            pilot.detailEnabled = GUILayout.Toggle(pilot.detailEnabled, "Show detail video window");
            if (pilot.detailEnabled)
            {
                Slider("Detail zoom", ref pilot.detailZoom, 1f, 8f, "x");
                Slider("Detail left/right", ref pilot.detailPosition.x, -3f, 3f, " m");
                Slider("Detail height", ref pilot.detailPosition.y, -2f, 2f, " m");
                Slider("Detail distance", ref pilot.detailPosition.z, 0.6f, 4f, " m");
                Slider("Detail width", ref pilot.detailWidth, 0.3f, 2f, " m");
                Slider("Detail pitch", ref pilot.detailAngles.x, -60f, 60f, "°");
                Slider("Detail yaw", ref pilot.detailAngles.y, -80f, 80f, "°");
                Slider("Detail roll", ref pilot.detailAngles.z, -45f, 45f, "°");
            }
            pilot.showControllers = GUILayout.Toggle(pilot.showControllers, "Show VR controllers and joystick deflection");
            pilot.showPointers = GUILayout.Toggle(pilot.showPointers, "Show pointers outside menu");
            GUILayout.Label("Pointer comfort (only pointers; does not filter headset, video or servo)");
            Slider("Pointer horizontal sensitivity", ref pilot.pointerYawSensitivity, 0.4f, 2.5f, "x", "F2");
            Slider("Pointer vertical sensitivity", ref pilot.pointerPitchSensitivity, 0.4f, 2.5f, "x", "F2");
            Slider("Aim pitch offset (+ up)", ref pilot.pointerPitchOffset, -45f, 45f, "°");
            Slider("Aim yaw offset", ref pilot.pointerYawOffset, -30f, 30f, "°");
            Slider("Pointer smoothing (0=off)", ref pilot.pointerSmoothingSeconds, 0f, 0.15f, " s", "F3");
            if (Application.isPlaying)
            {
                if (GUILayout.Button("Center left pointer to gaze")) CenterPointer(0);
                if (GUILayout.Button("Center right pointer to gaze")) CenterPointer(1);
            }
            pilot.windowDraggingEnabled = GUILayout.Toggle(pilot.windowDraggingEnabled, "Grab and move windows with left controller");
            if (GUILayout.Button("Window grab: " + pilot.windowGrabButton)) pilot.windowGrabButton = (RovWindowGrabButton)(((int)pilot.windowGrabButton + 1) % 3);
            Slider("Window movement speed", ref pilot.windowMoveSpeed, 0.1f, 2f, " m/s", "F2");
            Slider("Window rotation speed", ref pilot.windowRotateSpeed, 10f, 120f, " deg/s", "F1");
            pilot.windowFollowHand = GUILayout.Toggle(pilot.windowFollowHand, "Also follow hand ray while holding (off = stable sticks only)");
            Slider("VR menu pitch", ref pilot.menuAngles.x, -60f, 60f, " deg");
            Slider("VR menu yaw", ref pilot.menuAngles.y, -80f, 80f, " deg");
            pilot.stickCalibrationEnabled = GUILayout.Toggle(pilot.stickCalibrationEnabled, "Left stick adjusts screen geometry (trigger/grip modifiers)");
            Slider("VR menu distance", ref pilot.menuDistance, 0.8f, 3f, " m");
            Slider("VR menu height", ref pilot.menuHeight, -1.5f, 1.5f, " m");
            Slider("VR menu left/right", ref pilot.menuSide, -2f, 2f, " m");
            if (Application.isPlaying && GUILayout.Button("Open / close VR menu (F3 default)")) TogglePilotMenu();
            GUILayout.Space(8);
            GUILayout.Label("Named profiles (complete configuration)");
            GUI.SetNextControlName("RovPilotProfileName");
            profileName = GUILayout.TextField(profileName, 64);
            if (GUI.GetNameOfFocusedControl() == "RovPilotProfileName") RovPilotTextEntry.MarkActive();
            if (GUILayout.Button("Save named profile (replaces same name; .bak kept)")) SaveNamedProfile(profileName);
            if (!string.IsNullOrEmpty(activeProfileName) && GUILayout.Button("Gi aktiv profil navnet i feltet over"))
            {
                try { RovProfileLibrary.Rename(activeProfileName, profileName); activeProfileName = profileName; RefreshProfiles(); pilotMessage = "Nytt profilnavn: " + profileName; }
                catch (Exception error) { pilotMessage = error.Message; }
            }
            if (Application.isPlaying && GUILayout.Button("Kontroller og hjelp i VR")) GoPilotPage(13);
            if (!profileListLoaded) RefreshProfiles();
            if (GUILayout.Button("Refresh profile list")) RefreshProfiles();
            foreach (string name in profileNames) if (GUILayout.Button("Load: " + name)) LoadNamedProfile(name);
            GUILayout.Label(RovProfileLibrary.DirectoryPath);
            GUILayout.Label("Bindings: controller / keyboard. Duplicates are rejected; choose None to free a button.");
            foreach (var b in pilot.bindings)
            {
                GUILayout.Label(b.action.ToString()); GUILayout.BeginHorizontal();
                if (GUILayout.Button(b.button.ToString())) CycleBinding(b, false);
                if (GUILayout.Button(b.key.ToString())) CycleBinding(b, true);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Restore default bindings")) { pilot.bindings = RovPilotSettings.Defaults(); ResetPilotInputEdges(); }
            GUILayout.Label(pilotMessage);
            GUILayout.Space(8);
        }

        private void CycleBinding(RovPilotBinding binding, bool keyboard)
        {
            if (keyboard)
            {
                int count = Enum.GetValues(typeof(RovPilotKey)).Length;
                for (int n = 1; n <= count; n++)
                    if (Rebind(binding.action, binding.button, (RovPilotKey)(((int)binding.key + n) % count))) break;
            }
            else
            {
                int count = Enum.GetValues(typeof(RovPilotButton)).Length;
                for (int n = 1; n <= count; n++)
                    if (Rebind(binding.action, (RovPilotButton)(((int)binding.button + n) % count), binding.key)) break;
            }
        }

        private void DestroyPilotTools()
        {
            if (recorder != null) recorder.Stop();
            if (recordingBadge != null) Destroy(recordingBadge.transform.parent.gameObject);
            DestroyPointerInteraction();
            if (zoomMesh != null && originalUv != null) zoomMesh.uv = originalUv;
            if (detailCanvas != null) Destroy(detailCanvas.gameObject);
            if (pilotMenuRoot != null) Destroy(pilotMenuRoot.gameObject);
            for (int i = 0; i < 2; i++)
            {
                if (controllerModels[i] != null) Destroy(controllerModels[i].gameObject);
                if (controllerRays[i] != null) Destroy(controllerRays[i].gameObject);
            }
            if (pilotUiMaterial != null) Destroy(pilotUiMaterial);
            if (controllerMaterial != null) Destroy(controllerMaterial);
        }
    }
}
