using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace ROVVR
{
    public static class RovPointerMath
    {
        public static bool PoseValid(Vector3 position, Quaternion rotation)
        {
            float norm = Quaternion.Dot(rotation, rotation);
            return RovCameraPoseMath.Finite(position.x) && RovCameraPoseMath.Finite(position.y) && RovCameraPoseMath.Finite(position.z)
                && RovCameraPoseMath.Finite(norm) && norm > 0.5f && norm < 1.5f;
        }
        public static bool FullyTracked(InputTrackingState state)
            => (state & (InputTrackingState.Position | InputTrackingState.Rotation)) == (InputTrackingState.Position | InputTrackingState.Rotation);

        public static Vector3 Direction(Quaternion rawAim, Quaternion reference, Quaternion target,
            float yawGain, float pitchGain, float pitchOffset, float yawOffset)
        {
            Vector3 v = Quaternion.Inverse(reference) * (rawAim * Quaternion.Euler(-pitchOffset, yawOffset, 0f) * Vector3.forward);
            float yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
            return target * (Quaternion.Euler(-Mathf.Clamp(pitch * pitchGain, -85f, 85f), yaw * yawGain, 0f) * Vector3.forward);
        }

        public static bool Hit(RectTransform window, Ray ray, out Vector3 point, out float distance)
        {
            point = Vector3.zero; distance = 0f;
            if (window == null || !window.gameObject.activeInHierarchy) return false;
            var plane = new Plane(window.forward, window.position);
            if (!plane.Raycast(ray, out distance) || distance < 0f || distance > 8f) return false;
            point = ray.GetPoint(distance);
            return window.rect.Contains((Vector2)window.InverseTransformPoint(point));
        }

        public static Vector2 Stick(Vector2 input)
        {
            const float deadzone = 0.18f;
            float magnitude = input.magnitude;
            return magnitude <= deadzone ? Vector2.zero : input.normalized * Mathf.InverseLerp(deadzone, 1f, magnitude);
        }
    }

    public sealed partial class RovVideoViewCalibration
    {
        private enum DragWindow { None, Menu, Detail }
        private DragWindow draggedWindow;
        private bool grabWasHeld, pointerFocusLastFrame, windowAlignHeld;
        private Plane dragPlane;
        private Vector3 previousDragPoint;
        private Vector2 grabbedPoint;
        private Quaternion dragAxes = Quaternion.identity;
        private Vector3 menuAnchorPosition;
        private Quaternion menuAnchorRotation = Quaternion.identity;
        private readonly Quaternion[] pointerReferences = new Quaternion[2], pointerTargets = new Quaternion[2];
        private readonly bool[] pointerInitialized = new bool[2];
        private readonly bool[] pointerFilterReady = new bool[2];
        private readonly Quaternion[] rawAimRotations = new Quaternion[2];
        private readonly Vector3[] smoothAimDirections = new Vector3[2];
        private readonly int[] pointerPoseSources = new int[2];
        private readonly Vector4[] pointerSettingsUsed = new Vector4[2];
        private Vector4 PointerSettings => new Vector4(pilot.pointerYawSensitivity, pilot.pointerPitchSensitivity, pilot.pointerPitchOffset, pilot.pointerYawOffset);
        private readonly List<XRInputSubsystem> pointerSubsystems = new List<XRInputSubsystem>();
        private readonly List<XRInputSubsystem> discoveredPointerSubsystems = new List<XRInputSubsystem>();
        private float nextPointerSubsystemCheck;
        private bool pointerRecoveryHeld, pointerRecoveryFired;
        private float pointerRecoverySince = -1f;
        private readonly RectTransform[] pointerReticles = new RectTransform[2], windowOutlines = new RectTransform[2];
        private readonly RovPointerVisualGraphic[] cursorGraphics = new RovPointerVisualGraphic[2], outlineGraphics = new RovPointerVisualGraphic[2];
        private Material pointerMaterial;
        private bool WindowDragActive => draggedWindow != DragWindow.None;

        private void PreparePointerInput()
        {
            if (eye == null) return;
            WatchPointerTrackingOrigin();
            EnsurePilotMaterials();
            UpdateController(0); UpdateController(1);
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var grabUsage = pilot.windowGrabButton == RovWindowGrabButton.LeftTrigger ? CommonUsages.triggerButton
                : pilot.windowGrabButton == RovWindowGrabButton.LeftGrip ? CommonUsages.gripButton : CommonUsages.primary2DAxisClick;
            left.TryGetFeatureValue(grabUsage, out bool held);
            left.TryGetFeatureValue(CommonUsages.gripButton, out bool depthModifier);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            right.TryGetFeatureValue(CommonUsages.gripButton, out bool rightGrip);
            UpdatePointerRecovery(depthModifier && rightGrip && handTracked[0] && handTracked[1] && IsHeadReady());
            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            ProcessWindowDrag(held, handTracked[0] && IsHeadReady(),
                (depthModifier && pilot.windowGrabButton != RovWindowGrabButton.LeftGrip) || (handTracked[1] && rightGrip), stick, handRays[0], delta);
            if (handTracked[1] && right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rotation)) ProcessWindowRotation(rotation, delta);
            right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool align);
            if (WindowDragActive && handTracked[1] && align && !windowAlignHeld) StraightenDraggedWindow();
            windowAlignHeld = align;
        }

        private bool TryPointedWindow(int hand, out DragWindow window, out Vector3 hit, out float distance)
        {
            window = DragWindow.None; hit = Vector3.zero; distance = 0f;
            if (!handTracked[hand]) return false;
            // Menu draws above detail video, so it also owns overlapping pointer hits.
            if (pilotMenuOpen && RovPointerMath.Hit(pilotMenuRoot, handRays[hand], out hit, out distance)) window = DragWindow.Menu;
            else if (pilot.detailEnabled && detailCanvas != null
                && RovPointerMath.Hit((RectTransform)detailCanvas.transform, handRays[hand], out hit, out distance)) window = DragWindow.Detail;
            return window != DragWindow.None;
        }

        private void ProcessWindowDrag(bool held, bool ready, bool depthModifier, Vector2 stick, Ray ray, float delta)
        {
            bool edge = held && !grabWasHeld && pointerFocusLastFrame;
            grabWasHeld = held; pointerFocusLastFrame = ready;
            if (!ready || pointerRecoveryHeld || (!startupPending && !pilot.windowDraggingEnabled) || !held) { draggedWindow = DragWindow.None; return; }
            if (!WindowDragActive && edge)
            {
                RectTransform target = null;
                if (pilotMenuOpen && RovPointerMath.Hit(pilotMenuRoot, ray, out _, out _))
                { target = pilotMenuRoot; draggedWindow = DragWindow.Menu; }
                else if (!startupPending && pilot.detailEnabled && detailCanvas != null && RovPointerMath.Hit((RectTransform)detailCanvas.transform, ray, out _, out _))
                { target = (RectTransform)detailCanvas.transform; draggedWindow = DragWindow.Detail; }
                if (target == null) return;
                RovPointerMath.Hit(target, ray, out Vector3 hit, out _);
                Vector3 local = target.InverseTransformPoint(hit);
                grabbedPoint = new Vector2(Mathf.InverseLerp(target.rect.xMin, target.rect.xMax, local.x), Mathf.InverseLerp(target.rect.yMin, target.rect.yMax, local.y));
                Vector3 levelForward = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
                dragAxes = levelForward.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(levelForward, Vector3.up);
                dragPlane = new Plane(dragAxes * Vector3.forward, hit);
                previousDragPoint = hit;
            }
            if (!WindowDragActive) return;
            RectTransform active = draggedWindow == DragWindow.Menu ? pilotMenuRoot : detailCanvas == null ? null : (RectTransform)detailCanvas.transform;
            if (active == null || !active.gameObject.activeInHierarchy) { draggedWindow = DragWindow.None; return; }
            Vector3 handDelta = Vector3.zero;
            if (dragPlane.Raycast(ray, out float enter) && enter >= 0f && enter <= 8f)
            {
                Vector3 point = ray.GetPoint(enter);
                if (pilot.windowFollowHand) handDelta = point - previousDragPoint;
                previousDragPoint = point;
            }
            Vector2 axis = RovPointerMath.Stick(stick);
            Vector3 stickDelta = (dragAxes * Vector3.right * axis.x + (depthModifier ? dragAxes * Vector3.forward : Vector3.up) * axis.y)
                * pilot.windowMoveSpeed * delta;
            Vector3 desired = active.position + handDelta + stickDelta;
            if (draggedWindow == DragWindow.Menu)
            {
                Vector3 p = Quaternion.Inverse(menuAnchorRotation) * (desired - menuAnchorPosition);
                p = new Vector3(Mathf.Clamp(p.x, -2f, 2f), Mathf.Clamp(p.y, -1.5f, 1.5f), Mathf.Clamp(p.z, 0.8f, 3f));
                if (startupPending) startupMenuPosition = p;
                else { pilot.menuSide = p.x; pilot.menuHeight = p.y; pilot.menuDistance = p.z; }
                ApplyMenuPlacement();
            }
            else
            {
                Vector3 p = Quaternion.Inverse(PilotHudRotation) * (desired - PilotHudPosition);
                pilot.detailPosition = new Vector3(Mathf.Clamp(p.x, -3f, 3f), Mathf.Clamp(p.y, -2f, 2f), Mathf.Clamp(p.z, 0.6f, 4f));
                UpdateDetailPose();
            }
        }

        private void ProcessWindowRotation(Vector2 stick, float delta)
        {
            if (!WindowDragActive) return;
            Vector2 axis = RovPointerMath.Stick(stick) * pilot.windowRotateSpeed * delta;
            if (axis == Vector2.zero) return;
            if (draggedWindow == DragWindow.Menu)
            {
                Vector2 angles = startupPending ? startupMenuAngles : pilot.menuAngles;
                angles = new Vector2(Mathf.Clamp(angles.x - axis.y, -60f, 60f), Mathf.Clamp(angles.y + axis.x, -80f, 80f));
                if (startupPending) startupMenuAngles = angles; else pilot.menuAngles = angles;
                ApplyMenuPlacement();
            }
            else
            {
                pilot.detailAngles.x = Mathf.Clamp(pilot.detailAngles.x - axis.y, -60f, 60f);
                pilot.detailAngles.y = Mathf.Clamp(pilot.detailAngles.y + axis.x, -80f, 80f);
                UpdateDetailPose();
            }
        }

        private bool GrabConsumesPilotInput(RovPilotButton button)
        {
            if (WindowDragActive) return true;
            RovPilotButton grab = pilot.windowGrabButton == RovWindowGrabButton.LeftTrigger ? RovPilotButton.LeftTrigger
                : pilot.windowGrabButton == RovWindowGrabButton.LeftGrip ? RovPilotButton.LeftGrip : RovPilotButton.LeftStick;
            return (startupPending || pilot.windowDraggingEnabled) && button == grab && TryPointedWindow(0, out _, out _, out _);
        }

        private void StraightenDraggedWindow()
        {
            if (draggedWindow == DragWindow.Menu)
            {
                if (startupPending) startupMenuAngles = Vector2.zero; else pilot.menuAngles = Vector2.zero;
                ApplyMenuPlacement();
            }
            else if (draggedWindow == DragWindow.Detail)
            { pilot.detailAngles = Vector3.zero; UpdateDetailPose(); }
        }

        private bool TryRuntimeAim(int index, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            // Prefer pose data from the same XR device used for tracking and buttons.
            var device = InputDevices.GetDeviceAtXRNode(index == 0 ? XRNode.LeftHand : XRNode.RightHand);
            if (device.TryGetFeatureValue(new InputFeatureUsage<Vector3>("PointerPosition"), out position)
                && device.TryGetFeatureValue(new InputFeatureUsage<Quaternion>("PointerRotation"), out rotation)
                && RovPointerMath.PoseValid(position, rotation)) return true;
#if ENABLE_INPUT_SYSTEM
            var controller = index == 0 ? XRController.leftHand : XRController.rightHand;
            if (controller != null && controller.added && controller.enabled && controller.isTracked.isPressed
                && RovPointerMath.FullyTracked((InputTrackingState)controller.trackingState.ReadValue()))
            {
                var p = controller.TryGetChildControl<Vector3Control>("pointerPosition");
                var r = controller.TryGetChildControl<QuaternionControl>("pointerRotation");
                if (p != null && r != null)
                {
                    var aim = controller.TryGetChildControl<PoseControl>("pointer");
                    if (aim != null)
                    {
                        var state = aim.ReadValue();
                        if (!state.isTracked || !RovPointerMath.FullyTracked((InputTrackingState)state.trackingState)) return false;
                    }
                    position = p.ReadValue(); rotation = r.ReadValue();
                    if (RovPointerMath.PoseValid(position, rotation)) return true;
                }
            }
#endif
            return false;
        }

        public void CenterPointer(int hand)
        {
            if (hand < 0 || hand > 1 || !handTracked[hand] || eye == null) return;
            Vector3 target = pilotMenuOpen && pilotMenuRoot != null ? pilotMenuRoot.position : eye.position + eye.forward * 2f;
            CenterPointerAt(hand, target);
        }

        private void CenterPointerAt(int hand, Vector3 point)
        {
            if (!handTracked[hand] || eye == null) return;
            Quaternion origin = eye.parent == null ? Quaternion.identity : eye.parent.rotation;
            Vector3 corrected = Quaternion.Inverse(origin) * (rawAimRotations[hand] * Quaternion.Euler(-pilot.pointerPitchOffset, pilot.pointerYawOffset, 0f) * Vector3.forward);
            Vector3 direction = (point - handRays[hand].origin).normalized;
            Quaternion pivot = PointerSensitivityPivot(origin);
            Vector3 gained = RovPointerMath.Direction(Quaternion.LookRotation(corrected, Vector3.up), pivot, pivot,
                pilot.pointerYawSensitivity, pilot.pointerPitchSensitivity, 0f, 0f);
            pointerReferences[hand] = Quaternion.LookRotation(gained, Vector3.up);
            pointerTargets[hand] = Quaternion.LookRotation(Quaternion.Inverse(origin) * direction, Vector3.up);
            pointerSettingsUsed[hand] = PointerSettings;
            pointerInitialized[hand] = true; smoothAimDirections[hand] = direction;
            pointerFilterReady[hand] = true;
        }

        private Vector3 AdjustPointer(int hand, Quaternion raw)
        {
            rawAimRotations[hand] = raw;
            Quaternion origin = eye.parent == null ? Quaternion.identity : eye.parent.rotation;
            if (pointerSettingsUsed[hand] != PointerSettings)
            { pointerInitialized[hand] = pointerFilterReady[hand] = false; pointerSettingsUsed[hand] = PointerSettings; }
            bool initialize = !pointerInitialized[hand];
            if (initialize)
            {
                pointerReferences[hand] = pointerTargets[hand] = Quaternion.identity;
                pointerInitialized[hand] = true;
            }
            Quaternion corrected = Quaternion.Inverse(origin) * raw * Quaternion.Euler(-pilot.pointerPitchOffset, pilot.pointerYawOffset, 0f);
            Quaternion pivot = PointerSensitivityPivot(origin);
            // Scale around the current heading, not the heading at app boot. A 180-degree
            // turn must not multiply the entire world yaw and throw the pointer behind its target.
            Vector3 gained = RovPointerMath.Direction(corrected, pivot, pivot, pilot.pointerYawSensitivity, pilot.pointerPitchSensitivity, 0f, 0f);
            Vector3 direction = origin * (pointerTargets[hand] * Quaternion.Inverse(pointerReferences[hand]) * gained);
            bool resetFilter = !pointerFilterReady[hand];
            float blend = pilot.pointerSmoothingSeconds <= 0f || resetFilter ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / pilot.pointerSmoothingSeconds);
            smoothAimDirections[hand] = resetFilter ? direction : Vector3.Slerp(smoothAimDirections[hand], direction, blend).normalized;
            pointerFilterReady[hand] = true;
            return smoothAimDirections[hand];
        }

        private Quaternion PointerSensitivityPivot(Quaternion origin)
        {
            Vector3 forward = Vector3.ProjectOnPlane(Quaternion.Inverse(origin) * eye.forward, Vector3.up);
            return forward.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(forward, Vector3.up);
        }

        // Deliberate, aim-independent recovery. It does not recenter the video or send servo commands.
        private void UpdatePointerRecovery(bool held)
        {
            pointerRecoveryHeld = held && !WindowDragActive;
            if (!pointerRecoveryHeld) { pointerRecoverySince = -1f; pointerRecoveryFired = false; return; }
            if (pointerRecoverySince < 0f) pointerRecoverySince = Time.realtimeSinceStartup;
            if (pointerRecoveryFired || Time.realtimeSinceStartup - pointerRecoverySince < 0.7f) return;
            pointerRecoveryFired = true; RecoverPilotMenu();
        }

        public void RecoverPilotMenu()
        {
            if (eye == null) return;
            if (!pilotMenuOpen && Application.isPlaying) TogglePilotMenu();
            if (startupPending) { startupMenuPosition = new Vector3(0f, -0.05f, 1.25f); startupMenuAngles = Vector2.zero; }
            else { pilot.menuSide = 0f; pilot.menuHeight = -0.05f; pilot.menuDistance = 1.25f; pilot.menuAngles = Vector2.zero; }
            draggedWindow = DragWindow.None;
            PositionPilotMenu();
            if (pilotMenuRoot != null) for (int hand = 0; hand < 2; hand++) CenterPointerAt(hand, pilotMenuRoot.position);
            capturedMenuSliders[0] = capturedMenuSliders[1] = null;
            ResetPilotInputEdges();
            pilotMessage = "Meny rettet opp. Pekere sentrert mot menyen.\nHold begge grip-knappene i 0,7 s for å gjøre dette igjen.";
        }

        private void WatchPointerTrackingOrigin()
        {
            if (Time.unscaledTime < nextPointerSubsystemCheck) return;
            nextPointerSubsystemCheck = Time.unscaledTime + 1f;
            SubsystemManager.GetSubsystems(discoveredPointerSubsystems);
            foreach (var subsystem in discoveredPointerSubsystems)
                if (!pointerSubsystems.Contains(subsystem)) { subsystem.trackingOriginUpdated += PointerTrackingOriginChanged; pointerSubsystems.Add(subsystem); }
        }
        private void PointerTrackingOriginChanged(XRInputSubsystem subsystem)
        {
            for (int i = 0; i < 2; i++) { pointerInitialized[i] = pointerFilterReady[i] = handTracked[i] = false; }
            HidePointerInteraction(); ResetPilotInputEdges();
            if (pilotMenuOpen) PositionPilotMenu();
        }

        private LineRenderer PointerLine(string name, Transform parent, int count, float width, Color color)
        {
            if (pointerMaterial == null) pointerMaterial = new Material(Resources.Load<Shader>("RovHudOverlay")) { renderQueue = 4990 };
            var line = new GameObject(name).AddComponent<LineRenderer>();
            if (parent != null) line.transform.SetParent(parent, false);
            line.sharedMaterial = pointerMaterial; line.useWorldSpace = false; line.positionCount = count;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
            return line;
        }

        private void ControllerPart(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Quaternion rotation, Color color)
        {
            var part = GameObject.CreatePrimitive(shape); part.name = name; Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false); part.transform.localPosition = position;
            part.transform.localScale = scale; part.transform.localRotation = rotation;
            var renderer = part.GetComponent<Renderer>(); renderer.sharedMaterial = controllerMaterial;
            var properties = new MaterialPropertyBlock(); properties.SetColor("_Color", color); renderer.SetPropertyBlock(properties);
        }

        private void BuildControllerModel(int index)
        {
            var root = new GameObject(index == 0 ? "ROV left Touch marker" : "ROV right Touch marker").transform;
            controllerModels[index] = root;
            float side = index == 0 ? 1f : -1f;
            Color dark = new Color(0.07f, 0.1f, 0.14f), light = new Color(0.7f, 0.83f, 0.9f);
            ControllerPart(root, "Grip", PrimitiveType.Capsule, new Vector3(0f, -0.025f, -0.008f), new Vector3(0.035f, 0.045f, 0.04f), Quaternion.Euler(-15f, 0f, 0f), dark);
            ControllerPart(root, "Top plate", PrimitiveType.Sphere, new Vector3(0f, 0.026f, 0.015f), new Vector3(0.065f, 0.025f, 0.076f), Quaternion.identity, light);
            ControllerPart(root, "Thumb pad", PrimitiveType.Cylinder, new Vector3(side * 0.012f, 0.04f, 0.017f), new Vector3(0.022f, 0.003f, 0.022f), Quaternion.identity, dark);
            ControllerPart(root, "Primary", PrimitiveType.Sphere, new Vector3(-side * 0.017f, 0.041f, 0.006f), new Vector3(0.013f, 0.005f, 0.013f), Quaternion.identity, dark);
            ControllerPart(root, "Secondary", PrimitiveType.Sphere, new Vector3(-side * 0.014f, 0.041f, 0.03f), new Vector3(0.013f, 0.005f, 0.013f), Quaternion.identity, dark);
            var stick = new GameObject("Thumbstick pivot").transform; stick.SetParent(root, false); stickModels[index] = stick;
            ControllerPart(stick, "Thumbstick", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.015f, 0.008f, 0.015f), Quaternion.identity, dark);
            var ring = PointerLine("Tracking ring", root, 41, 0.004f, light);
            ring.sharedMaterial = controllerMaterial;
            var ringColor = new MaterialPropertyBlock(); ringColor.SetColor("_Color", light); ring.SetPropertyBlock(ringColor);
            for (int j = 0; j <= 40; j++)
            {
                float a = j * Mathf.PI * 2f / 40f;
                ring.SetPosition(j, new Vector3(Mathf.Cos(a) * 0.045f, 0.057f + Mathf.Sin(a) * 0.036f, 0.03f));
            }
        }

        private void HidePointerInteraction()
        {
            draggedWindow = DragWindow.None;
            pointerFocusLastFrame = false;
            for (int i = 0; i < 2; i++)
            {
                pointerFilterReady[i] = false;
                if (pointerReticles[i] != null) pointerReticles[i].gameObject.SetActive(false);
            }
        }
        private void DestroyPointerInteraction()
        {
            foreach (var subsystem in pointerSubsystems) subsystem.trackingOriginUpdated -= PointerTrackingOriginChanged;
            pointerSubsystems.Clear();
            for (int i = 0; i < 2; i++) if (pointerReticles[i] != null) Destroy(pointerReticles[i].gameObject);
            for (int i = 0; i < 2; i++) if (windowOutlines[i] != null) Destroy(windowOutlines[i].gameObject);
            if (pointerMaterial != null) Destroy(pointerMaterial);
        }
    }
}
