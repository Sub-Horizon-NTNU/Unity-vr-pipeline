using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace ROVVR
{
    public sealed partial class RovVideoViewCalibration
    {
        private RectTransform pilotMenuRoot, pilotMenuPage;
        private Text pilotMenuTitle, pilotMenuStatus;
        private int pilotPage, layoutCard, bindingIndex;
        private float menuTextAt;
        private readonly bool[] menuTriggerHeld = new bool[2];
        private readonly List<PilotMenuItem> menuItems = new List<PilotMenuItem>();
        private readonly List<PilotMenuItem> pageItems = new List<PilotMenuItem>();
        private static readonly string[] PilotPages = { "ZOOM", "DETAIL PLACEMENT", "VIDEO", "HUD", "HUD PLACEMENT", "COMPASS / TILT", "PROFILES", "BINDINGS" };
        private static readonly string[] CardNames = { "Status", "Battery", "Depth", "Camera tilt", "Attitude", "Diagnostics" };

        private sealed class PilotMenuItem
        {
            public RectTransform rect;
            public Text label;
            public Func<string> caption;
            public Action<float> select;
            public bool drag;
            public Image background;
        }

        private RectTransform PilotNode(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(width, height); return rt;
        }
        private Text PilotLabel(Transform parent, string text, float x, float y, float width, float height, int size)
        {
            var label = PilotNode(text, parent, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = Resources.Load<Font>("RovHudFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = size; label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.88f, 0.95f, 1f); label.material = pilotUiMaterial; label.raycastTarget = false;
            return label;
        }

        public void TogglePilotMenu()
        {
            if (!Application.isPlaying || eye == null) return;
            EnsurePilotMaterials();
            pilotMenuOpen = !pilotMenuOpen;
            if (pilotMenuRoot == null)
            {
                var go = new GameObject("ROV pilot menu", typeof(RectTransform), typeof(Canvas));
                pilotMenuRoot = (RectTransform)go.transform; pilotMenuRoot.sizeDelta = new Vector2(900, 930);
                pilotMenuRoot.localScale = Vector3.one * 0.0011f;
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = eye.GetComponent<Camera>(); canvas.sortingOrder = 250;
                var bg = go.AddComponent<Image>(); bg.material = pilotUiMaterial; bg.color = new Color(0.018f, 0.032f, 0.052f, 0.98f);
                pilotMenuTitle = PilotLabel(pilotMenuRoot, "ROV / PILOT", 0, 415, 850, 60, 30);
                MenuButton(pilotMenuRoot, "<", -360, 345, 90, 55, () => { pilotPage = (pilotPage + PilotPages.Length - 1) % PilotPages.Length; BuildMenuPage(); }, false);
                MenuButton(pilotMenuRoot, ">", 360, 345, 90, 55, () => { pilotPage = (pilotPage + 1) % PilotPages.Length; BuildMenuPage(); }, false);
                MenuButton(pilotMenuRoot, "CLOSE", 0, -420, 280, 54, () => TogglePilotMenu(), false);
                pilotMenuStatus = PilotLabel(pilotMenuRoot, "", 0, -355, 840, 65, 20);
            }
            pilotMenuRoot.gameObject.SetActive(pilotMenuOpen);
            if (pilotMenuOpen) { RefreshProfiles(); PositionPilotMenu(); BuildMenuPage(); }
            // Consume the opening press; a held trigger cannot click through into the menu.
            for (int i = 0; i < 2; i++)
            {
                var device = InputDevices.GetDeviceAtXRNode(i == 0 ? XRNode.LeftHand : XRNode.RightHand);
                device.TryGetFeatureValue(CommonUsages.triggerButton, out menuTriggerHeld[i]);
            }
        }

        private void PositionPilotMenu()
        {
            if (eye == null || pilotMenuRoot == null) return;
            Vector3 forward = eye.forward;
            pilotMenuRoot.SetPositionAndRotation(eye.position + forward * pilot.menuDistance + Vector3.up * pilot.menuHeight,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        private PilotMenuItem MenuButton(Transform parent, string title, float x, float y, float width, float height, Action onClick, bool page = true)
        {
            var rt = PilotNode(title, parent, x, y, width, height);
            var bg = rt.gameObject.AddComponent<Image>(); bg.material = pilotUiMaterial; bg.color = new Color(0.045f, 0.09f, 0.13f, 1f);
            var item = new PilotMenuItem { rect = rt, label = PilotLabel(rt, title, 0, 0, width - 18, height, 24), select = _ => onClick(), background = bg };
            menuItems.Add(item); if (page) pageItems.Add(item); return item;
        }

        private float MenuRowY => 265f - pageItems.Count * 65f;
        private void MenuAction(Func<string> caption, Action action)
        {
            var item = MenuButton(pilotMenuPage, "", 0, MenuRowY, 820, 56, action);
            item.caption = caption;
        }
        private void MenuToggle(string title, Func<bool> get, Action<bool> set)
            => MenuAction(() => title + (get() ? "   ON" : "   OFF"), () => set(!get()));
        private void MenuSlider(string title, Func<float> get, Action<float> set, float min, float max, string unit = "", string format = "F2")
        {
            var item = MenuButton(pilotMenuPage, "", 0, MenuRowY, 820, 56, () => { });
            item.caption = () => title + "   " + get().ToString(format) + unit + "   [ -     + ]";
            item.drag = true; item.select = x => set(Mathf.Lerp(min, max, Mathf.Clamp01(x)));
        }

        private RovHudBoxLayout SelectedHudBox()
        {
            if (hudLayout == null) hudLayout = new RovHudLayout();
            switch (layoutCard)
            {
                case 0: return hudLayout.status; case 1: return hudLayout.battery; case 2: return hudLayout.depth;
                case 3: return hudLayout.cameraTilt; case 4: return hudLayout.attitude; default: return hudLayout.diagnostics;
            }
        }
        private string SelectedProfileName => profileNames.Length == 0 ? "(no saved profiles)" : profileNames[selectedProfile];
        private void BuildMenuPage()
        {
            foreach (var item in pageItems) menuItems.Remove(item);
            pageItems.Clear();
            if (pilotMenuPage != null) { pilotMenuPage.gameObject.SetActive(false); Destroy(pilotMenuPage.gameObject); }
            pilotMenuPage = PilotNode("Page", pilotMenuRoot, 0, 0, 900, 650);
            PilotLabel(pilotMenuPage, $"{pilotPage + 1} / {PilotPages.Length}   {PilotPages[pilotPage]}", 0, 345, 600, 55, 27);
            switch (pilotPage)
            {
                case 0:
                    MenuSlider("Main zoom", () => pilot.mainZoom, v => pilot.mainZoom = v, 1, 8, "x");
                    MenuToggle("Detail window", () => pilot.detailEnabled, v => pilot.detailEnabled = v);
                    MenuSlider("Detail zoom (total)", () => pilot.detailZoom, v => pilot.detailZoom = v, 1, 8, "x");
                    MenuSlider("Target left / right", () => pilot.zoomCenter.x, v => pilot.zoomCenter.x = v, 0, 1);
                    MenuSlider("Target bottom / top", () => pilot.zoomCenter.y, v => pilot.zoomCenter.y = v, 0, 1);
                    MenuAction(() => "Reset main zoom / center target", () => ExecutePilotAction(RovPilotAction.ResetZoom));
                    MenuToggle("Controller models", () => pilot.showControllers, v => pilot.showControllers = v);
                    MenuToggle("Pointers outside menu", () => pilot.showPointers, v => pilot.showPointers = v);
                    break;
                case 1:
                    MenuToggle("Detail window", () => pilot.detailEnabled, v => pilot.detailEnabled = v);
                    MenuSlider("Left / right", () => pilot.detailPosition.x, v => pilot.detailPosition.x = v, -3, 3, " m");
                    MenuSlider("Height", () => pilot.detailPosition.y, v => pilot.detailPosition.y = v, -2, 2, " m");
                    MenuSlider("Distance", () => pilot.detailPosition.z, v => pilot.detailPosition.z = v, 0.6f, 4, " m");
                    MenuSlider("Width", () => pilot.detailWidth, v => pilot.detailWidth = v, 0.3f, 2, " m");
                    MenuSlider("Pitch", () => pilot.detailAngles.x, v => pilot.detailAngles.x = v, -60, 60, "°");
                    MenuSlider("Yaw", () => pilot.detailAngles.y, v => pilot.detailAngles.y = v, -80, 80, "°");
                    MenuSlider("Roll", () => pilot.detailAngles.z, v => pilot.detailAngles.z = v, -45, 45, "°");
                    break;
                case 2:
                    MenuSlider("Image width", () => horizontalFov, v => { horizontalFov = v; ApplyGeometry(); }, 50, 150, "°");
                    MenuSlider("Curvature", () => curveDegrees, v => { curveDegrees = v; ApplyGeometry(); }, 0, 150, "°");
                    MenuSlider("Distance", () => distance, v => { distance = v; ApplyGeometry(); }, 1, 4, " m");
                    MenuSlider("Screen height", () => verticalPositionMeters, v => { verticalPositionMeters = v; ApplyGeometry(); }, -2, 2, " m");
                    MenuSlider("Pitch offset", () => verticalOffsetDegrees, v => { verticalOffsetDegrees = v; ApplyGeometry(); }, -25, 25, "°");
                    MenuSlider("Roll offset", () => imageRollDegrees, v => { imageRollDegrees = v; ApplyGeometry(); }, -15, 15, "°");
                    MenuToggle("Head following", () => headFollowingEnabled, SetHeadFollowingEnabled);
                    MenuAction(() => "Recenter video and tilt", CenterViewAndTilt);
                    MenuToggle("Left stick calibration", () => pilot.stickCalibrationEnabled, v => pilot.stickCalibrationEnabled = v);
                    break;
                case 3:
                    MenuToggle("HUD", () => hudEnabled, v => hudEnabled = v);
                    MenuToggle("Compass", () => hudShowCompass, v => hudShowCompass = v);
                    MenuToggle("Battery", () => hudShowBattery, v => hudShowBattery = v);
                    MenuToggle("Depth", () => hudShowDepth, v => hudShowDepth = v);
                    MenuToggle("Attitude", () => hudShowAttitude, v => hudShowAttitude = v);
                    MenuToggle("FPS diagnostics", () => hudShowDiagnostics, v => hudShowDiagnostics = v);
                    MenuSlider("Text scale", () => hudScale, v => hudScale = v, 0.7f, 1.5f, "x");
                    MenuSlider("Opacity", () => hudOpacity, v => hudOpacity = v, 0.2f, 1);
                    MenuAction(() => "Anchor: " + hudAnchorMode, () => hudAnchorMode = (RovHudAnchorMode)(((int)hudAnchorMode + 1) % 3));
                    break;
                case 4:
                    MenuAction(() => "Instrument: " + CardNames[layoutCard] + "  >", () => layoutCard = (layoutCard + 1) % CardNames.Length);
                    MenuSlider("Left / right", () => SelectedHudBox().position.x, v => SelectedHudBox().position.x = v, -2, 2, " m");
                    MenuSlider("Height", () => SelectedHudBox().position.y, v => SelectedHudBox().position.y = v, -2, 2, " m");
                    MenuSlider("Depth", () => SelectedHudBox().position.z, v => SelectedHudBox().position.z = v, 0.6f, 4, " m");
                    MenuSlider("Scale", () => SelectedHudBox().scale, v => SelectedHudBox().scale = v, 0.5f, 2, "x");
                    MenuSlider("Pitch", () => SelectedHudBox().angleOffset.x, v => SelectedHudBox().angleOffset.x = v, -60, 60, "°");
                    MenuSlider("Yaw", () => SelectedHudBox().angleOffset.y, v => SelectedHudBox().angleOffset.y = v, -60, 60, "°");
                    MenuSlider("Roll", () => SelectedHudBox().angleOffset.z, v => SelectedHudBox().angleOffset.z = v, -60, 60, "°");
                    MenuToggle("Face viewing origin", () => SelectedHudBox().faceViewer, v => SelectedHudBox().faceViewer = v);
                    break;
                case 5:
                    MenuSlider("Compass radius", () => compassRadius, v => compassRadius = v, 1, 3, " m");
                    MenuSlider("Compass drop", () => compassDrop, v => compassDrop = v, 0.4f, 1.8f, " m");
                    MenuSlider("IMU smoothing (0=off)", () => telemetrySmoothingSeconds, v => telemetrySmoothingSeconds = v, 0, 0.2f, " s", "F3");
                    if (tilt != null)
                    {
                        MenuSlider("Servo smoothing (0=off)", () => tilt.ServoSmoothingSeconds, v => tilt.ServoSmoothingSeconds = v, 0, 0.5f, " s", "F3");
                        MenuSlider("Servo speed", () => tilt.ServoMaximumSpeed, v => tilt.ServoMaximumSpeed = v, 5, 90, "°/s");
                        MenuToggle("Pitch compensation", () => tilt.CompensateRovPitch, SetPitchCompensation);
                        MenuSlider("Pitch compensation strength", () => tilt.PitchCompensationStrength, v => tilt.PitchCompensationStrength = v, 0, 1.5f, "x");
                    }
                    break;
                case 6:
                    MenuAction(() => "Profile: " + SelectedProfileName + "  >", () => { if (profileNames.Length > 0) selectedProfile = (selectedProfile + 1) % profileNames.Length; });
                    MenuAction(() => "Load selected profile", () => { if (profileNames.Length > 0) LoadNamedProfile(SelectedProfileName); });
                    MenuAction(() => "Save over selected profile (.bak kept)", () => { if (profileNames.Length > 0) SaveNamedProfile(SelectedProfileName); });
                    MenuAction(() => "Save as new profile", () => SaveNamedProfile("VR-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));
                    MenuAction(() => "Save startup JSON", SaveCalibration);
                    MenuAction(() => "Load startup JSON", () => { LoadCalibration(); BuildMenuPage(); });
                    MenuSlider("Menu distance", () => pilot.menuDistance, v => pilot.menuDistance = v, 0.8f, 2, " m");
                    MenuSlider("Menu height", () => pilot.menuHeight, v => pilot.menuHeight = v, -0.6f, 0.6f, " m");
                    MenuAction(() => "Place menu in front of me", PositionPilotMenu);
                    break;
                case 7:
                    MenuAction(() => "Action: " + pilot.bindings[bindingIndex].action + "  >", () => bindingIndex = (bindingIndex + 1) % pilot.bindings.Length);
                    MenuAction(() => "Controller: " + pilot.bindings[bindingIndex].button + "  >", () => CycleBinding(pilot.bindings[bindingIndex], false));
                    MenuAction(() => "Keyboard: " + pilot.bindings[bindingIndex].key + "  >", () => CycleBinding(pilot.bindings[bindingIndex], true));
                    MenuAction(() => "Restore default bindings", () => { pilot.bindings = RovPilotSettings.Defaults(); ResetPilotInputEdges(); });
                    break;
            }
            RefreshMenuText();
        }

        private void RefreshMenuText()
        {
            foreach (var item in menuItems)
                if (item.caption != null) { string caption = item.caption(); if (item.label.text != caption) item.label.text = caption; }
        }

        private void UpdatePilotMenu()
        {
            if (!pilotMenuOpen || pilotMenuRoot == null) return;
            if (Time.unscaledTime >= menuTextAt)
            {
                RefreshMenuText();
                string status = string.IsNullOrEmpty(pilotMessage) ? "Point + trigger / drag a value left-right.\nClose menu to aim at video. Changes are live; save to keep." : pilotMessage;
                if (pilotMenuStatus.text != status) pilotMenuStatus.text = status;
                menuTextAt = Time.unscaledTime + 0.1f;
            }
            foreach (var item in menuItems) item.background.color = new Color(0.045f, 0.09f, 0.13f, 1f);
            for (int i = 0; i < 2; i++)
            {
                var device = InputDevices.GetDeviceAtXRNode(i == 0 ? XRNode.LeftHand : XRNode.RightHand);
                device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed);
                bool edge = pressed && !menuTriggerHeld[i]; menuTriggerHeld[i] = pressed;
                if (!handTracked[i] || !IsHeadReady()) continue;
                var plane = new Plane(pilotMenuRoot.forward, pilotMenuRoot.position);
                if (!plane.Raycast(handRays[i], out float enter) || enter < 0f || enter > 5f) continue;
                Vector3 hit = handRays[i].GetPoint(enter);
                Vector3 menuPoint = pilotMenuRoot.InverseTransformPoint(hit);
                if (!pilotMenuRoot.rect.Contains((Vector2)menuPoint)) continue;
                controllerRays[i].SetPosition(1, hit);
                PilotMenuItem target = null;
                foreach (var item in menuItems)
                {
                    Vector2 p = item.rect.InverseTransformPoint(hit);
                    if (item.rect.rect.Contains(p)) { target = item; break; }
                }
                if (target == null) continue;
                target.background.color = new Color(0.07f, 0.27f, 0.33f, 1f);
                if (pressed && (target.drag || edge))
                {
                    var local = target.rect.InverseTransformPoint(hit);
                    target.select(Mathf.InverseLerp(target.rect.rect.xMin, target.rect.rect.xMax, local.x));
                    RefreshMenuText();
                    // A click can rebuild the page or hide the menu.
                    break;
                }
            }
        }
    }
}
