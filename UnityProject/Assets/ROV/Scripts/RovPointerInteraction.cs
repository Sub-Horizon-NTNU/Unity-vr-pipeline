using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace ROVVR
{
    public static class RovPointerMath
    {
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
        private bool grabWasHeld, pointerFocusLastFrame;
        private Plane dragPlane;
        private Vector3 previousDragPoint;
        private Vector3 menuAnchorPosition;
        private Quaternion menuAnchorRotation = Quaternion.identity;
        private readonly Quaternion[] pointerReferences = new Quaternion[2], pointerTargets = new Quaternion[2];
        private readonly bool[] pointerInitialized = new bool[2];
        private readonly bool[] pointerFilterReady = new bool[2];
        private readonly Quaternion[] rawAimRotations = new Quaternion[2];
        private readonly Vector3[] smoothAimDirections = new Vector3[2];
        private readonly LineRenderer[] pointerReticles = new LineRenderer[2], pointerDots = new LineRenderer[2];
        private Material pointerMaterial;
        private bool WindowDragActive => draggedWindow != DragWindow.None;

        private void PreparePointerInput()
        {
            if (eye == null) return;
            EnsurePilotMaterials();
            UpdateController(0); UpdateController(1);
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var grabUsage = pilot.windowGrabButton == RovWindowGrabButton.LeftTrigger ? CommonUsages.triggerButton
                : pilot.windowGrabButton == RovWindowGrabButton.LeftGrip ? CommonUsages.gripButton : CommonUsages.primary2DAxisClick;
            left.TryGetFeatureValue(grabUsage, out bool held);
            left.TryGetFeatureValue(CommonUsages.gripButton, out bool depthModifier);
            left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick);
            ProcessWindowDrag(held, handTracked[0] && IsHeadReady(), depthModifier, stick, handRays[0], Mathf.Min(Time.unscaledDeltaTime, 0.05f));
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
            if (!ready || !pilot.windowDraggingEnabled || !held) { draggedWindow = DragWindow.None; return; }
            if (!WindowDragActive && edge)
            {
                RectTransform target = null;
                if (pilotMenuOpen && RovPointerMath.Hit(pilotMenuRoot, ray, out _, out _))
                { target = pilotMenuRoot; draggedWindow = DragWindow.Menu; }
                else if (pilot.detailEnabled && detailCanvas != null && RovPointerMath.Hit((RectTransform)detailCanvas.transform, ray, out _, out _))
                { target = (RectTransform)detailCanvas.transform; draggedWindow = DragWindow.Detail; }
                if (target == null) return;
                dragPlane = new Plane(target.forward, target.position);
                dragPlane.Raycast(ray, out float distance); previousDragPoint = ray.GetPoint(distance);
            }
            if (!WindowDragActive) return;
            RectTransform active = draggedWindow == DragWindow.Menu ? pilotMenuRoot : detailCanvas == null ? null : (RectTransform)detailCanvas.transform;
            if (active == null || !active.gameObject.activeInHierarchy) { draggedWindow = DragWindow.None; return; }
            if (!dragPlane.Raycast(ray, out float enter) || enter < 0f || enter > 8f) return;
            Vector3 point = ray.GetPoint(enter);
            Vector2 axis = RovPointerMath.Stick(stick);
            bool depth = depthModifier && pilot.windowGrabButton != RovWindowGrabButton.LeftGrip;
            Vector3 stickDelta = (active.right * axis.x + (depth ? active.forward : active.up) * axis.y)
                * pilot.windowMoveSpeed * delta;
            Vector3 desired = active.position + point - previousDragPoint + stickDelta;
            previousDragPoint = point;
            if (draggedWindow == DragWindow.Menu)
            {
                Vector3 p = Quaternion.Inverse(menuAnchorRotation) * (desired - menuAnchorPosition);
                pilot.menuSide = Mathf.Clamp(p.x, -2f, 2f); pilot.menuHeight = Mathf.Clamp(p.y, -1.5f, 1.5f);
                pilot.menuDistance = Mathf.Clamp(p.z, 0.8f, 3f); ApplyMenuPlacement();
            }
            else
            {
                Vector3 p = Quaternion.Inverse(PilotHudRotation) * (desired - PilotHudPosition);
                pilot.detailPosition = new Vector3(Mathf.Clamp(p.x, -3f, 3f), Mathf.Clamp(p.y, -2f, 2f), Mathf.Clamp(p.z, 0.6f, 4f));
                UpdateDetailPose();
            }
        }

        private bool GrabConsumesPilotInput(RovPilotButton button)
        {
            if (WindowDragActive) return true;
            RovPilotButton grab = pilot.windowGrabButton == RovWindowGrabButton.LeftTrigger ? RovPilotButton.LeftTrigger
                : pilot.windowGrabButton == RovWindowGrabButton.LeftGrip ? RovPilotButton.LeftGrip : RovPilotButton.LeftStick;
            return pilot.windowDraggingEnabled && button == grab && TryPointedWindow(0, out _, out _, out _);
        }

        private bool TryRuntimeAim(int index, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
#if ENABLE_INPUT_SYSTEM
            var controller = index == 0 ? XRController.leftHand : XRController.rightHand;
            if (controller != null && controller.added && controller.enabled)
            {
                var p = controller.TryGetChildControl<Vector3Control>("pointerPosition");
                var r = controller.TryGetChildControl<QuaternionControl>("pointerRotation");
                if (p != null && r != null)
                {
                    position = p.ReadValue(); rotation = r.ReadValue();
                    if (rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w > 0.5f) return true;
                }
            }
#endif
            var device = InputDevices.GetDeviceAtXRNode(index == 0 ? XRNode.LeftHand : XRNode.RightHand);
            return device.TryGetFeatureValue(new InputFeatureUsage<Vector3>("PointerPosition"), out position)
                && device.TryGetFeatureValue(new InputFeatureUsage<Quaternion>("PointerRotation"), out rotation);
        }

        public void CenterPointer(int hand)
        {
            if (hand < 0 || hand > 1 || !handTracked[hand] || eye == null) return;
            Vector3 corrected = rawAimRotations[hand] * Quaternion.Euler(-pilot.pointerPitchOffset, pilot.pointerYawOffset, 0f) * Vector3.forward;
            pointerReferences[hand] = Quaternion.LookRotation(corrected, Vector3.up);
            pointerTargets[hand] = Quaternion.LookRotation(eye.forward, Vector3.up);
            pointerInitialized[hand] = true; smoothAimDirections[hand] = eye.forward;
            pointerFilterReady[hand] = true;
        }

        private Vector3 AdjustPointer(int hand, Quaternion raw)
        {
            rawAimRotations[hand] = raw;
            bool initialize = !pointerInitialized[hand];
            if (initialize)
            {
                pointerReferences[hand] = pointerTargets[hand] = Quaternion.LookRotation(eye.forward, Vector3.up);
                pointerInitialized[hand] = true;
            }
            Vector3 direction = RovPointerMath.Direction(raw, pointerReferences[hand], pointerTargets[hand],
                pilot.pointerYawSensitivity, pilot.pointerPitchSensitivity, pilot.pointerPitchOffset, pilot.pointerYawOffset);
            bool resetFilter = !pointerFilterReady[hand];
            float blend = pilot.pointerSmoothingSeconds <= 0f || resetFilter ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / pilot.pointerSmoothingSeconds);
            smoothAimDirections[hand] = resetFilter ? direction : Vector3.Slerp(smoothAimDirections[hand], direction, blend).normalized;
            pointerFilterReady[hand] = true;
            return smoothAimDirections[hand];
        }

        private LineRenderer PointerLine(string name, Transform parent, int count, float width, Color color)
        {
            if (pointerMaterial == null) pointerMaterial = new Material(Resources.Load<Shader>("RovHudOverlay")) { renderQueue = 4090 };
            var line = new GameObject(name).AddComponent<LineRenderer>();
            if (parent != null) line.transform.SetParent(parent, false);
            line.sharedMaterial = pointerMaterial; line.useWorldSpace = false; line.positionCount = count;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
            return line;
        }

        private void UpdatePointerFeedback()
        {
            for (int i = 0; i < 2; i++)
            {
                if (pointerReticles[i] == null)
                {
                    pointerReticles[i] = PointerLine("ROV pointer ring", null, 33, 0.0018f, Color.white);
                    pointerReticles[i].sortingOrder = 1000;
                    for (int j = 0; j <= 32; j++)
                    {
                        float a = j * Mathf.PI * 2f / 32f;
                        pointerReticles[i].SetPosition(j, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.009f);
                    }
                    pointerDots[i] = PointerLine("ROV pointer dot", pointerReticles[i].transform, 2, 0.004f, Color.cyan);
                    pointerDots[i].sortingOrder = 1001;
                    pointerDots[i].SetPosition(0, Vector3.zero); pointerDots[i].SetPosition(1, new Vector3(0.0001f, 0f, 0f));
                }
                bool valid = handTracked[i] && IsHeadReady() && TryPointedWindow(i, out _, out _, out _);
                bool grabbed = i == 0 && WindowDragActive;
                if (grabbed && handTracked[i] && IsHeadReady()) valid = true;
                pointerReticles[i].gameObject.SetActive(valid);
                if (!valid) continue;
                TryPointedWindow(i, out DragWindow window, out Vector3 hit, out float distance);
                if (grabbed)
                {
                    window = draggedWindow;
                    var heldSurface = window == DragWindow.Menu ? pilotMenuRoot : (RectTransform)detailCanvas.transform;
                    var heldPlane = new Plane(heldSurface.forward, heldSurface.position);
                    if (heldPlane.Raycast(handRays[i], out float enter) && enter >= 0f)
                    {
                        Vector3 local = heldSurface.InverseTransformPoint(handRays[i].GetPoint(enter));
                        local.x = Mathf.Clamp(local.x, heldSurface.rect.xMin, heldSurface.rect.xMax);
                        local.y = Mathf.Clamp(local.y, heldSurface.rect.yMin, heldSurface.rect.yMax); local.z = 0f;
                        hit = heldSurface.TransformPoint(local); distance = Vector3.Distance(handRays[i].origin, hit);
                    }
                    else { hit = heldSurface.position; distance = Vector3.Distance(handRays[i].origin, hit); }
                }
                Color color = grabbed ? new Color(1f, 0.7f, 0.2f) : i == 0 ? new Color(0.55f, 0.9f, 1f) : Color.white;
                var surface = window == DragWindow.Menu ? pilotMenuRoot : (RectTransform)detailCanvas.transform;
                pointerReticles[i].transform.SetPositionAndRotation(hit - surface.forward * 0.003f, surface.rotation);
                pointerReticles[i].transform.localScale = Vector3.one * Mathf.Clamp(distance, 0.7f, 3f);
                pointerReticles[i].startColor = pointerReticles[i].endColor = color;
                pointerDots[i].startColor = pointerDots[i].endColor = grabbed ? color : new Color(0.2f, 1f, 0.9f);
                controllerRays[i].SetPosition(1, hit);
            }
            if (pilotMenuTitle != null) pilotMenuTitle.color = draggedWindow == DragWindow.Menu ? new Color(1f, 0.75f, 0.3f) : Color.white;
            if (detailCaption != null) detailCaption.color = draggedWindow == DragWindow.Detail ? new Color(1f, 0.75f, 0.3f) : Color.white;
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
            for (int i = 0; i < 2; i++) if (pointerReticles[i] != null) Destroy(pointerReticles[i].gameObject);
            if (pointerMaterial != null) Destroy(pointerMaterial);
        }
    }
}
