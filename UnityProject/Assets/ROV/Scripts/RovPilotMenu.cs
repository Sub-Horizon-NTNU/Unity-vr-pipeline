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
        private PilotMenuItem closeMenuItem, backMenuItem;
        private int pilotPage = 12, layoutCard, bindingIndex;
        private float menuTextAt;
        // Pending-startup menu placement must not modify the profile being prepared.
        private Vector3 startupMenuPosition = new Vector3(0f, -0.05f, 1.25f);
        private Vector2 startupMenuAngles;
        private readonly bool[] menuTriggerHeld = new bool[2];
        private readonly PilotMenuItem[] capturedMenuSliders = new PilotMenuItem[2];
        private readonly List<PilotMenuItem> menuItems = new List<PilotMenuItem>();
        private readonly List<PilotMenuItem> pageItems = new List<PilotMenuItem>();
        // Preserve page IDs consumed by the existing Editor tools.
        private static readonly string[] PilotPages = { "ZOOM", "ZOOMVINDU", "BILDE", "HUD OG LESBARHET", "HUD-PLASSERING", "KAMERA OG KOMPASS", "PROFILER", "KNAPPEVALG", "PEKERE", "FLYTT VINDUER", "MENYPLASSERING", "OPPTAK", "PILOTINNSTILLINGER", "KONTROLLER OG HJELP", "PILOTINNSTILLINGER", "GI PROFILEN ET NAVN", "ALLE INNSTILLINGER" };
        private static readonly string[] CardNames = { "Status", "Batteri", "Dybde", "Kameratilt", "Horisont", "Diagnostikk" };

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
            SetPilotMenuOpen(!pilotMenuOpen);
        }

        private void SetPilotMenuOpen(bool open)
        {
            EnsurePilotMaterials();
            pilotMenuOpen = open;
            if (pilotMenuRoot == null)
            {
                var go = new GameObject("ROV pilot menu", typeof(RectTransform), typeof(Canvas));
                pilotMenuRoot = (RectTransform)go.transform; pilotMenuRoot.sizeDelta = new Vector2(900, 930);
                pilotMenuRoot.localScale = Vector3.one * 0.0011f;
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = eye.GetComponent<Camera>(); canvas.sortingOrder = 250;
                var bg = go.AddComponent<Image>(); bg.material = pilotUiMaterial; bg.color = new Color(0.018f, 0.032f, 0.052f, 0.98f);
                pilotMenuTitle = PilotLabel(pilotMenuRoot, "SUB HORIZON   /   PILOT", 0, 415, 850, 60, 26);
                MenuButton(pilotMenuRoot, "RETT OPP", 330, 415, 160, 48, RecoverPilotMenu, false);
                closeMenuItem = MenuButton(pilotMenuRoot, "LUKK / PILOTVISNING", 212, -420, 396, 54, () => TogglePilotMenu(), false);
                backMenuItem = MenuButton(pilotMenuRoot, "TILBAKE", -212, -420, 396, 54,
                    () => GoPilotPage(pilotPage == 16 ? 12 : pilotPage == 15 ? 6 : 16), false);
                pilotMenuStatus = PilotLabel(pilotMenuRoot, "", 0, -355, 840, 65, 20);
            }
            pilotMenuRoot.gameObject.SetActive(pilotMenuOpen);
            if (pilotMenuOpen) { RefreshProfiles(); pilotPage = 12; PositionPilotMenu(); BuildMenuPage(); }
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
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            menuAnchorPosition = eye.position;
            menuAnchorRotation = Quaternion.LookRotation(forward, Vector3.up);
            ApplyMenuPlacement();
        }

        private void ApplyMenuPlacement()
        {
            if (startupPending && pilotMenuRoot != null)
            { pilotMenuRoot.SetPositionAndRotation(menuAnchorPosition + menuAnchorRotation * startupMenuPosition,
                menuAnchorRotation * Quaternion.Euler(startupMenuAngles.x, startupMenuAngles.y, 0f)); return; }
            if (pilotMenuRoot != null) pilotMenuRoot.SetPositionAndRotation(menuAnchorPosition
                + menuAnchorRotation * new Vector3(pilot.menuSide, pilot.menuHeight, pilot.menuDistance),
                menuAnchorRotation * Quaternion.Euler(pilot.menuAngles.x, pilot.menuAngles.y, 0f));
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
            => MenuAction(() => title + (get() ? "   PÅ" : "   AV"), () => set(!get()));
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
            capturedMenuSliders[0] = capturedMenuSliders[1] = null;
            foreach (var item in pageItems) menuItems.Remove(item);
            pageItems.Clear();
            if (pilotMenuPage != null) { pilotMenuPage.gameObject.SetActive(false); if (Application.isPlaying) Destroy(pilotMenuPage.gameObject); else DestroyImmediate(pilotMenuPage.gameObject); }
            pilotMenuPage = PilotNode("Page", pilotMenuRoot, 0, 0, 900, 650);
            PilotLabel(pilotMenuPage, PilotPages[pilotPage], 0, 345, 600, 55, 27);
            if (closeMenuItem != null) closeMenuItem.rect.gameObject.SetActive(true);
            if (backMenuItem != null) backMenuItem.rect.gameObject.SetActive(pilotPage != 12 && pilotPage != 14);
            switch (pilotPage)
            {
                case 0:
                    MenuSlider("Hovedzoom", () => pilot.mainZoom, v => pilot.mainZoom = v, 1, 8, "x");
                    MenuToggle("Detaljvindu", () => pilot.detailEnabled, v => pilot.detailEnabled = v);
                    MenuSlider("Detaljzoom (totalt)", () => pilot.detailZoom, v => pilot.detailZoom = v, 1, 8, "x");
                    MenuSlider("Zoompunkt venstre / høyre", () => pilot.zoomCenter.x, v => pilot.zoomCenter.x = v, 0, 1);
                    MenuSlider("Zoompunkt ned / opp", () => pilot.zoomCenter.y, v => pilot.zoomCenter.y = v, 0, 1);
                    MenuAction(() => "Nullstill zoom og sentrer zoompunkt", () => ExecutePilotAction(RovPilotAction.ResetZoom));
                    MenuToggle("Vis kontrollere", () => pilot.showControllers, v => pilot.showControllers = v);
                    MenuToggle("Vis pekere utenfor menyen", () => pilot.showPointers, v => pilot.showPointers = v);
                    break;
                case 1:
                    MenuToggle("Detaljvindu", () => pilot.detailEnabled, v => pilot.detailEnabled = v);
                    MenuSlider("Venstre / høyre", () => pilot.detailPosition.x, v => pilot.detailPosition.x = v, -3, 3, " m");
                    MenuSlider("Høyde", () => pilot.detailPosition.y, v => pilot.detailPosition.y = v, -2, 2, " m");
                    MenuSlider("Avstand", () => pilot.detailPosition.z, v => pilot.detailPosition.z = v, 0.6f, 4, " m");
                    MenuSlider("Bredde", () => pilot.detailWidth, v => pilot.detailWidth = v, 0.3f, 2, " m");
                    MenuSlider("Vinkel opp / ned", () => pilot.detailAngles.x, v => pilot.detailAngles.x = v, -60, 60, "°");
                    MenuSlider("Vinkel venstre / høyre", () => pilot.detailAngles.y, v => pilot.detailAngles.y = v, -80, 80, "°");
                    MenuSlider("Helning", () => pilot.detailAngles.z, v => pilot.detailAngles.z = v, -45, 45, "°");
                    break;
                case 2:
                    MenuSlider("Bildebredde", () => horizontalFov, v => { horizontalFov = v; ApplyGeometry(); }, 50, 150, "°");
                    MenuSlider("Krumming", () => curveDegrees, v => { curveDegrees = v; ApplyGeometry(); }, 0, 150, "°");
                    MenuSlider("Avstand", () => distance, v => { distance = v; ApplyGeometry(); }, 1, 4, " m");
                    MenuSlider("Bildehøyde", () => verticalPositionMeters, v => { verticalPositionMeters = v; ApplyGeometry(); }, -2, 2, " m");
                    MenuSlider("Bilde vinkel opp / ned", () => verticalOffsetDegrees, v => { verticalOffsetDegrees = v; ApplyGeometry(); }, -25, 25, "°");
                    MenuSlider("Bilde helning", () => imageRollDegrees, v => { imageRollDegrees = v; ApplyGeometry(); }, -15, 15, "°");
                    MenuToggle("Hodefølging", () => headFollowingEnabled, SetHeadFollowingEnabled);
                    MenuAction(() => "Sentrer bilde og kameratilt", CenterViewAndTilt);
                    MenuToggle("Kalibrering med venstre spak", () => pilot.stickCalibrationEnabled, v => pilot.stickCalibrationEnabled = v);
                    break;
                case 3:
                    MenuToggle("HUD", () => hudEnabled, v => hudEnabled = v);
                    MenuToggle("Kompassring", () => hudShowCompass, v => hudShowCompass = v);
                    MenuToggle("Dybde", () => hudShowDepth, v => hudShowDepth = v);
                    MenuToggle("Kunstig horisont", () => hudShowAttitude, v => hudShowAttitude = v);
                    MenuToggle("FPS-diagnostikk", () => hudShowDiagnostics, v => hudShowDiagnostics = v);
                    MenuSlider("Tekststørrelse", () => hudScale, v => hudScale = v, 0.7f, 1.5f, "x");
                    MenuSlider("Synlighet", () => hudOpacity, v => hudOpacity = v, 0.2f, 1);
                    MenuAction(() => "Forankring: " + hudAnchorMode, () => hudAnchorMode = (RovHudAnchorMode)(((int)hudAnchorMode + 1) % 3));
                    break;
                case 4:
                    MenuAction(() => "Instrument: " + CardNames[layoutCard] + "  >", () => layoutCard = (layoutCard + 1) % CardNames.Length);
                    MenuSlider("Venstre / høyre", () => SelectedHudBox().position.x, v => SelectedHudBox().position.x = v, -2, 2, " m");
                    MenuSlider("Høyde", () => SelectedHudBox().position.y, v => SelectedHudBox().position.y = v, -2, 2, " m");
                    MenuSlider("Dybde", () => SelectedHudBox().position.z, v => SelectedHudBox().position.z = v, 0.6f, 4, " m");
                    MenuSlider("Størrelse", () => SelectedHudBox().scale, v => SelectedHudBox().scale = v, 0.5f, 2, "x");
                    MenuSlider("Vinkel opp / ned", () => SelectedHudBox().angleOffset.x, v => SelectedHudBox().angleOffset.x = v, -60, 60, "°");
                    MenuSlider("Vinkel venstre / høyre", () => SelectedHudBox().angleOffset.y, v => SelectedHudBox().angleOffset.y = v, -60, 60, "°");
                    MenuSlider("Helning", () => SelectedHudBox().angleOffset.z, v => SelectedHudBox().angleOffset.z = v, -60, 60, "°");
                    MenuToggle("Vend mot visningspunktet", () => SelectedHudBox().faceViewer, v => SelectedHudBox().faceViewer = v);
                    break;
                case 5:
                    MenuSlider("Kompassradius", () => compassRadius, v => compassRadius = v, 1, 3, " m");
                    MenuSlider("Kompass under øyehøyde", () => compassDrop, v => compassDrop = v, 0.4f, 1.8f, " m");
                    MenuSlider("IMU-utjevning (0 = av)", () => telemetrySmoothingSeconds, v => telemetrySmoothingSeconds = v, 0, 0.2f, " s", "F3");
                    if (tilt != null)
                    {
                        MenuSlider("Tiltutjevning (0 = av)", () => tilt.ServoSmoothingSeconds, v => tilt.ServoSmoothingSeconds = v, 0, 0.5f, " s", "F3");
                        MenuSlider("Maksimal tiltfart", () => tilt.ServoMaximumSpeed, v => tilt.ServoMaximumSpeed = v, 5, 90, "°/s");
                        MenuToggle("Kompenser ROV pitch", () => tilt.CompensateRovPitch, SetPitchCompensation);
                        MenuSlider("Kompensasjonsstyrke", () => tilt.PitchCompensationStrength, v => tilt.PitchCompensationStrength = v, 0, 1.5f, "x");
                        MenuAction(() => tilt.RcControlSelected ? "Kamera: RC / velg VR" : "Kamera: VR / velg RC", () => tilt.SelectRcControl(!tilt.RcControlSelected));
                        MenuAction(() => "Gjenopprett RC-kamerastyring", () => tilt.SelectRcControl(true));
                    }
                    break;
                case 6:
                    BuildProfilePicker();
                    break;
                case 12: BuildPilotHome(); break;
                case 13: BuildControllerHelp(); break;
                case 14: BuildPilotHome(); break;
                case 15: BuildProfileKeyboard(); break;
                case 16: BuildAdvancedHome(); break;
                case 10:
                    MenuSlider("Menyvinkel opp / ned", () => pilot.menuAngles.x, v => { pilot.menuAngles.x = v; ApplyMenuPlacement(); }, -60, 60, "°");
                    MenuSlider("Menyvinkel venstre / høyre", () => pilot.menuAngles.y, v => { pilot.menuAngles.y = v; ApplyMenuPlacement(); }, -80, 80, "°");
                    MenuSlider("Meny venstre / høyre", () => pilot.menuSide, v => { pilot.menuSide = v; ApplyMenuPlacement(); }, -2, 2, " m");
                    MenuSlider("Menyhøyde", () => pilot.menuHeight, v => { pilot.menuHeight = v; ApplyMenuPlacement(); }, -1.5f, 1.5f, " m");
                    MenuSlider("Menyavstand", () => pilot.menuDistance, v => { pilot.menuDistance = v; ApplyMenuPlacement(); }, 0.8f, 3, " m");
                    MenuAction(() => "Nullstill menyvinkler", () => { pilot.menuAngles = Vector2.zero; ApplyMenuPlacement(); });
                    MenuAction(() => "Flytt menyen foran meg", PositionPilotMenu);
                    break;
                case 11:
                    MenuAction(() => recorder != null && recorder.Recording ? "STOPP opptak" : "START opptak", ToggleRecording);
                    MenuAction(() => RecordingStatus, () => {});
                    MenuToggle("PC-visning + HUD (av = ren video)", () => pilot.recordPresentation, v => pilot.recordPresentation = v);
                    MenuToggle("Full HD (av = 720p) / 30 fps", () => pilot.recordFullHd, v => pilot.recordFullHd = v);
                    MenuToggle("Vend opptaket vertikalt", () => pilot.recordFlipVertical, v => pilot.recordFlipVertical = v);
                    MenuAction(() => "Lagre til neste oppstart", SaveCalibration);
                    break;
                case 7:
                    MenuAction(() => "Handling: " + pilot.bindings[bindingIndex].action + "  >", () => bindingIndex = (bindingIndex + 1) % pilot.bindings.Length);
                    MenuAction(() => "Kontroller: " + pilot.bindings[bindingIndex].button + "  >", () => CycleBinding(pilot.bindings[bindingIndex], false));
                    MenuAction(() => "Tastatur: " + pilot.bindings[bindingIndex].key + "  >", () => CycleBinding(pilot.bindings[bindingIndex], true));
                    MenuAction(() => "Gjenopprett standard knappevalg", () => { pilot.bindings = RovPilotSettings.Defaults(); ResetPilotInputEdges(); });
                    break;
                case 8:
                    MenuSlider("Vannrett følsomhet", () => pilot.pointerYawSensitivity, v => pilot.pointerYawSensitivity = v, 0.4f, 2.5f, "x");
                    MenuSlider("Loddrett følsomhet", () => pilot.pointerPitchSensitivity, v => pilot.pointerPitchSensitivity = v, 0.4f, 2.5f, "x");
                    MenuSlider("Pekervinkel (+ opp)", () => pilot.pointerPitchOffset, v => pilot.pointerPitchOffset = v, -45, 45, "°");
                    MenuSlider("Pekervinkel venstre / høyre", () => pilot.pointerYawOffset, v => pilot.pointerYawOffset = v, -30, 30, "°");
                    MenuSlider("Pekerutjevning (0 = av)", () => pilot.pointerSmoothingSeconds, v => pilot.pointerSmoothingSeconds = v, 0, 0.15f, " s", "F3");
                    MenuAction(() => "Sentrer VENSTRE peker mot menyen", () => CenterPointer(0));
                    MenuAction(() => "Sentrer HØYRE peker mot menyen", () => CenterPointer(1));
                    MenuToggle("Vis kontrollere", () => pilot.showControllers, v => pilot.showControllers = v);
                    MenuToggle("Vis pekere utenfor menyen", () => pilot.showPointers, v => pilot.showPointers = v);
                    break;
                case 9:
                    MenuToggle("Tillat flytting av vinduer", () => pilot.windowDraggingEnabled, v => pilot.windowDraggingEnabled = v);
                    MenuAction(() => "Gripeknapp: " + pilot.windowGrabButton, () => pilot.windowGrabButton = (RovWindowGrabButton)(((int)pilot.windowGrabButton + 1) % 3));
                    MenuSlider("Flyttefart med spak", () => pilot.windowMoveSpeed, v => pilot.windowMoveSpeed = v, 0.1f, 2f, " m/s");
                    MenuSlider("Vinkelfart med spak", () => pilot.windowRotateSpeed, v => pilot.windowRotateSpeed = v, 10f, 120f, " deg/s");
                    MenuToggle("Følg også håndens peker", () => pilot.windowFollowHand, v => pilot.windowFollowHand = v);
                    MenuSlider("Meny venstre / høyre", () => pilot.menuSide, v => { pilot.menuSide = v; ApplyMenuPlacement(); }, -2, 2, " m");
                    MenuSlider("Menyhøyde", () => pilot.menuHeight, v => { pilot.menuHeight = v; ApplyMenuPlacement(); }, -1.5f, 1.5f, " m");
                    MenuSlider("Menyavstand", () => pilot.menuDistance, v => { pilot.menuDistance = v; ApplyMenuPlacement(); }, 0.8f, 3, " m");
                    MenuAction(() => "Flytt menyen foran meg", PositionPilotMenu);
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
                string status = XRSettings.isDeviceActive && IsHeadReady() && !handTracked[1]
                    ? "Høyre peker venter på full posisjonssporing.\nHold kontrolleren synlig foran headsettet; sjekk lysforholdene."
                    : WindowDragActive ? "V. spak: flytt / grip + spak: avstand\nH. spak: vinkle / H. spakklikk: nullstill vinkler"
                    : string.IsNullOrEmpty(pilotMessage) ? PageHint() : pilotMessage;
                if (pilotMenuStatus.text != status) pilotMenuStatus.text = status;
                menuTextAt = Time.unscaledTime + 0.1f;
            }
            foreach (var item in menuItems) item.background.color = new Color(0.045f, 0.09f, 0.13f, 1f);
            for (int i = 0; i < 2; i++)
            {
                var device = InputDevices.GetDeviceAtXRNode(i == 0 ? XRNode.LeftHand : XRNode.RightHand);
                device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed);
                bool edge = pressed && !menuTriggerHeld[i]; menuTriggerHeld[i] = pressed;
                if (!pressed) capturedMenuSliders[i] = null;
                if (!handTracked[i] || !IsHeadReady() || pointerRecoveryHeld) { capturedMenuSliders[i] = null; continue; }
                if (WindowDragActive || (i == 0 && (startupPending || pilot.windowDraggingEnabled) && pilot.windowGrabButton == RovWindowGrabButton.LeftTrigger))
                { capturedMenuSliders[i] = null; continue; }
                var plane = new Plane(pilotMenuRoot.forward, pilotMenuRoot.position);
                if (!plane.Raycast(handRays[i], out float enter) || enter < 0f || enter > 5f) continue;
                Vector3 hit = handRays[i].GetPoint(enter);
                var captured = capturedMenuSliders[i];
                if (pressed && captured != null)
                {
                    var local = captured.rect.InverseTransformPoint(hit);
                    captured.select(Mathf.InverseLerp(captured.rect.rect.xMin, captured.rect.rect.xMax, local.x));
                    captured.background.color = new Color(0.07f, 0.27f, 0.33f, 1f);
                    RefreshMenuText(); break;
                }
                Vector3 menuPoint = pilotMenuRoot.InverseTransformPoint(hit);
                if (!pilotMenuRoot.rect.Contains((Vector2)menuPoint)) continue;
                PilotMenuItem target = null;
                foreach (var item in menuItems)
                {
                    if (!item.rect.gameObject.activeInHierarchy) continue;
                    Vector2 p = item.rect.InverseTransformPoint(hit);
                    if (item.rect.rect.Contains(p)) { target = item; break; }
                }
                if (target == null) continue;
                target.background.color = new Color(0.07f, 0.27f, 0.33f, 1f);
                if (pressed && edge)
                {
                    if (target.drag) capturedMenuSliders[i] = target;
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
