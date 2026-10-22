using System;
using System.IO;
using UnityEngine;

namespace ROVVR
{
    public static class RovPilotTextEntry
    {
        private static int typedFrame = -10;
        public static bool Active => Time.frameCount - typedFrame <= 1;
        public static void MarkActive() { typedFrame = Time.frameCount; }
    }

    public enum RovPilotAction { Follow, Hud, Center, Save, Menu, ZoomIn, ZoomOut, AimZoom, ResetZoom, Load }
    public enum RovWindowGrabButton { LeftTrigger, LeftGrip, LeftStick }
    public enum RovPilotButton { None, X, Y, A, B, LeftStick, RightStick, LeftMenu, LeftTrigger, RightTrigger, LeftGrip, RightGrip, RightUp, RightDown }
    public enum RovPilotKey { None, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, C, Z, V, G, H, J, K, L }

    [Serializable]
    public sealed class RovPilotBinding
    {
        public RovPilotAction action;
        public RovPilotButton button;
        public RovPilotKey key;
        public RovPilotBinding(RovPilotAction action, RovPilotButton button, RovPilotKey key)
        { this.action = action; this.button = button; this.key = key; }
    }

    [Serializable]
    public sealed class RovPilotSettings
    {
        public float mainZoom = 1f, detailZoom = 3f;
        // Normalized displayed image coordinates: (0,0) bottom-left, (1,1) top-right.
        public Vector2 zoomCenter = new Vector2(0.5f, 0.5f);
        public bool detailEnabled, showControllers = true, showPointers = true;
        public Vector3 detailPosition = new Vector3(1.5f, 0.1f, 1.8f);
        public float detailWidth = 0.8f;
        public Vector3 detailAngles = new Vector3(0f, 25f, 0f);
        public float menuDistance = 1.25f, menuHeight = -0.05f;
        public float menuSide;
        public bool windowDraggingEnabled = true;
        public RovWindowGrabButton windowGrabButton = RovWindowGrabButton.LeftTrigger;
        public float windowMoveSpeed = 0.6f;
        public float windowRotateSpeed = 45f;
        public bool windowFollowHand;
        public Vector2 menuAngles;
        public float pointerYawSensitivity = 1.2f, pointerPitchSensitivity = 0.8f;
        public float pointerPitchOffset, pointerYawOffset;
        public float pointerSmoothingSeconds = 0.025f;
        public bool stickCalibrationEnabled = true;
        public RovPilotBinding[] bindings = Defaults();

        public static RovPilotBinding[] Defaults() => new[] {
            new RovPilotBinding(RovPilotAction.Follow, RovPilotButton.A, RovPilotKey.None),
            new RovPilotBinding(RovPilotAction.Hud, RovPilotButton.B, RovPilotKey.H),
            new RovPilotBinding(RovPilotAction.Center, RovPilotButton.X, RovPilotKey.C),
            new RovPilotBinding(RovPilotAction.Save, RovPilotButton.Y, RovPilotKey.F5),
            new RovPilotBinding(RovPilotAction.Menu, RovPilotButton.RightStick, RovPilotKey.F3),
            new RovPilotBinding(RovPilotAction.ZoomIn, RovPilotButton.RightUp, RovPilotKey.Z),
            new RovPilotBinding(RovPilotAction.ZoomOut, RovPilotButton.RightDown, RovPilotKey.V),
            new RovPilotBinding(RovPilotAction.AimZoom, RovPilotButton.RightTrigger, RovPilotKey.G),
            new RovPilotBinding(RovPilotAction.ResetZoom, RovPilotButton.None, RovPilotKey.J),
            new RovPilotBinding(RovPilotAction.Load, RovPilotButton.None, RovPilotKey.F2)
        };

        public RovPilotSettings Copy() => JsonUtility.FromJson<RovPilotSettings>(JsonUtility.ToJson(this));
        public void Validate()
        {
            foreach (float f in new[] {mainZoom, detailZoom, zoomCenter.x, zoomCenter.y, detailPosition.x,
                detailPosition.y, detailPosition.z, detailWidth, detailAngles.x, detailAngles.y, detailAngles.z, menuDistance, menuHeight,
                menuSide, windowMoveSpeed, windowRotateSpeed, menuAngles.x, menuAngles.y, pointerYawSensitivity, pointerPitchSensitivity, pointerPitchOffset, pointerYawOffset, pointerSmoothingSeconds})
                if (!RovCameraPoseMath.Finite(f)) throw new ArgumentException("Pilot settings contain a non-finite number.");
            mainZoom = Mathf.Clamp(mainZoom, 1f, 8f); detailZoom = Mathf.Clamp(detailZoom, 1f, 8f);
            zoomCenter = new Vector2(Mathf.Clamp01(zoomCenter.x), Mathf.Clamp01(zoomCenter.y));
            detailWidth = Mathf.Clamp(detailWidth, 0.3f, 2f);
            detailPosition = new Vector3(Mathf.Clamp(detailPosition.x, -3f, 3f), Mathf.Clamp(detailPosition.y, -2f, 2f), Mathf.Clamp(detailPosition.z, 0.6f, 4f));
            detailAngles = new Vector3(Mathf.Clamp(detailAngles.x, -60f, 60f), Mathf.Clamp(detailAngles.y, -80f, 80f), Mathf.Clamp(detailAngles.z, -45f, 45f));
            menuDistance = Mathf.Clamp(menuDistance, 0.8f, 3f); menuHeight = Mathf.Clamp(menuHeight, -1.5f, 1.5f);
            menuSide = Mathf.Clamp(menuSide, -2f, 2f);
            windowMoveSpeed = Mathf.Clamp(windowMoveSpeed, 0.1f, 2f);
            windowRotateSpeed = Mathf.Clamp(windowRotateSpeed, 10f, 120f);
            menuAngles = new Vector2(Mathf.Clamp(menuAngles.x, -60f, 60f), Mathf.Clamp(menuAngles.y, -80f, 80f));
            pointerYawSensitivity = Mathf.Clamp(pointerYawSensitivity, 0.4f, 2.5f);
            pointerPitchSensitivity = Mathf.Clamp(pointerPitchSensitivity, 0.4f, 2.5f);
            pointerPitchOffset = Mathf.Clamp(pointerPitchOffset, -45f, 45f);
            pointerYawOffset = Mathf.Clamp(pointerYawOffset, -30f, 30f);
            pointerSmoothingSeconds = Mathf.Clamp(pointerSmoothingSeconds, 0f, 0.15f);
            if (!Enum.IsDefined(typeof(RovWindowGrabButton), windowGrabButton)) throw new ArgumentException("Invalid window grab button.");
            var defaults = Defaults();
            if (bindings == null || bindings.Length != defaults.Length) bindings = defaults;
            var actions = new bool[defaults.Length];
            var buttons = new bool[Enum.GetValues(typeof(RovPilotButton)).Length];
            var keys = new bool[Enum.GetValues(typeof(RovPilotKey)).Length];
            foreach (var b in bindings)
            {
                if (b == null || !Enum.IsDefined(typeof(RovPilotAction), b.action) || !Enum.IsDefined(typeof(RovPilotButton), b.button)
                    || !Enum.IsDefined(typeof(RovPilotKey), b.key) || actions[(int)b.action]) throw new ArgumentException("Invalid pilot binding.");
                actions[(int)b.action] = true;
                if (b.key == RovPilotKey.F4 || (b.key >= RovPilotKey.F6 && b.key <= RovPilotKey.F10))
                    throw new ArgumentException("F4 and F6-F10 are reserved desktop calibration/status keys.");
                if (b.button != RovPilotButton.None && buttons[(int)b.button]) throw new ArgumentException("Controller binding is used twice.");
                if (b.key != RovPilotKey.None && keys[(int)b.key]) throw new ArgumentException("Keyboard binding is used twice.");
                buttons[(int)b.button] = true; keys[(int)b.key] = true;
            }
        }

        // Keep the entire crop inside the image; edges never repeat or turn black.
        public static Rect Crop(Vector2 center, float zoom)
        {
            float size = 1f / Mathf.Clamp(zoom, 1f, 8f);
            return new Rect(Mathf.Clamp(center.x - size / 2f, 0f, 1f - size), Mathf.Clamp(center.y - size / 2f, 0f, 1f - size), size, size);
        }
    }

    public static class RovProfileLibrary
    {
        public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "Profiles");
        public static string ProfilePath(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || name != name.Trim() || name.EndsWith(".")
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(new[] {'/', '\\', ':'}) >= 0
                || name == "." || name == "..") throw new ArgumentException("Use a profile name of 1-64 letters/numbers, without path characters.");
            string stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
                || System.Text.RegularExpressions.Regex.IsMatch(stem, @"^(COM|LPT)[1-9]$")) throw new ArgumentException("Reserved Windows file name.");
            return Path.Combine(DirectoryPath, name + ".json");
        }
        public static string[] Names()
        {
            if (!Directory.Exists(DirectoryPath)) return Array.Empty<string>();
            var files = Directory.GetFiles(DirectoryPath, "*.json");
            for (int i = 0; i < files.Length; i++) files[i] = Path.GetFileNameWithoutExtension(files[i]);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase); return files;
        }
    }
}
