using UnityEngine;
using UnityEngine.UI;

namespace ROVVR
{
    public sealed partial class RovVideoViewCalibration
    {
        private readonly Text[] feedbackLabels = new Text[2];
        private static readonly Color PointerHover = new Color(0.25f, 0.95f, 1f);
        private static readonly Color PointerHeld = new Color(1f, 0.72f, 0.15f);

        private RectTransform CreatePointerCanvas(string name, Transform parent, int order, bool cursor, out RovPointerVisualGraphic graphic)
        {
            if (pointerMaterial == null) pointerMaterial = new Material(Resources.Load<Shader>("RovHudOverlay")) { renderQueue = 4990 };
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(RovPointerVisualGraphic));
            var root = (RectTransform)go.transform;
            if (parent != null) root.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true; canvas.sortingOrder = order;
            canvas.worldCamera = eye == null ? null : eye.GetComponent<Camera>();
            graphic = go.GetComponent<RovPointerVisualGraphic>(); graphic.cursor = cursor;
            graphic.raycastTarget = false; graphic.maskable = false; graphic.material = pointerMaterial;
            return root;
        }

        private void DrawPointerCursor(int hand, RectTransform surface, Vector3 hit, bool grabbed)
        {
            if (pointerReticles[hand] == null)
            {
                pointerReticles[hand] = CreatePointerCanvas("ROV foreground cursor", null, 20000, true, out cursorGraphics[hand]);
                pointerReticles[hand].sizeDelta = Vector2.one * 96f;
            }
            var cursor = pointerReticles[hand]; cursor.gameObject.SetActive(true);
            Vector3 towardEye = (eye.position - hit).normalized;
            // Bias toward the eye, rather than assuming the panel's front is -Z.
            cursor.SetPositionAndRotation(hit + towardEye * 0.015f, Quaternion.LookRotation(-towardEye, eye.up));
            cursor.localScale = Vector3.one * (0.0004f * Mathf.Clamp(Vector3.Distance(eye.position, hit), 0.7f, 3f));
            Color color = grabbed ? PointerHeld : PointerHover;
            if (cursorGraphics[hand].color != color) cursorGraphics[hand].color = color;
            // Endpoint and visible cursor agree; no hidden continuation through the window.
            controllerRays[hand].SetPosition(1, cursor.position);
            controllerRays[hand].startColor = controllerRays[hand].endColor = color;
        }

        private void DrawWindowOutline(int index, RectTransform window, bool visible, bool grabbed)
        {
            if (window == null) return;
            if (windowOutlines[index] == null && visible)
            {
                windowOutlines[index] = CreatePointerCanvas("ROV selected window frame", window, 10000, false, out outlineGraphics[index]);
                windowOutlines[index].localPosition = Vector3.zero; windowOutlines[index].localRotation = Quaternion.identity;
                windowOutlines[index].localScale = Vector3.one;
                feedbackLabels[index] = PilotLabel(windowOutlines[index], "", 0, window.rect.yMax + 28f, window.rect.width, 38, 24);
                feedbackLabels[index].material = pointerMaterial;
            }
            if (windowOutlines[index] == null) return;
            windowOutlines[index].gameObject.SetActive(visible);
            if (!visible) return;
            windowOutlines[index].sizeDelta = window.rect.size + Vector2.one * 20f;
            Color color = grabbed ? PointerHeld : PointerHover;
            if (outlineGraphics[index].color != color) outlineGraphics[index].color = color;
            string label = (index == 0 ? "MENU" : "ZOOM") + (grabbed ? " / HELD - right stick: rotate" : " / left trigger: grab");
            if (feedbackLabels[index].text != label) feedbackLabels[index].text = label;
            if (feedbackLabels[index].color != color) feedbackLabels[index].color = color;
        }

        private void UpdatePointerFeedback()
        {
            bool menuHover = false, detailHover = false;
            for (int hand = 0; hand < 2; hand++)
            {
                bool ready = handTracked[hand] && IsHeadReady();
                bool valid = ready && TryPointedWindow(hand, out _, out _, out _);
                bool grabbed = hand == 0 && WindowDragActive && ready;
                if (!valid && !grabbed)
                {
                    if (pointerReticles[hand] != null) pointerReticles[hand].gameObject.SetActive(false);
                    continue;
                }
                TryPointedWindow(hand, out DragWindow window, out Vector3 hit, out _);
                if (grabbed) window = draggedWindow;
                var surface = window == DragWindow.Menu ? pilotMenuRoot : detailCanvas == null ? null : (RectTransform)detailCanvas.transform;
                if (surface == null || !surface.gameObject.activeInHierarchy) continue;
                if (grabbed)
                {
                    // The beam remains attached to the actual point originally grabbed,
                    // even when the sticks move/rotate the panel away from the hand ray.
                    hit = surface.TransformPoint(new Vector3(Mathf.Lerp(surface.rect.xMin, surface.rect.xMax, grabbedPoint.x),
                        Mathf.Lerp(surface.rect.yMin, surface.rect.yMax, grabbedPoint.y), 0f));
                }
                if (window == DragWindow.Menu) menuHover = true; else detailHover = true;
                DrawPointerCursor(hand, surface, hit, grabbed);
            }
            DrawWindowOutline(0, pilotMenuRoot, menuHover || draggedWindow == DragWindow.Menu, draggedWindow == DragWindow.Menu);
            DrawWindowOutline(1, detailCanvas == null ? null : (RectTransform)detailCanvas.transform,
                detailHover || draggedWindow == DragWindow.Detail, draggedWindow == DragWindow.Detail);
            if (pilotMenuTitle != null) pilotMenuTitle.color = draggedWindow == DragWindow.Menu ? PointerHeld : Color.white;
            if (detailCaption != null) detailCaption.color = draggedWindow == DragWindow.Detail ? PointerHeld : Color.white;
        }
    }
}
