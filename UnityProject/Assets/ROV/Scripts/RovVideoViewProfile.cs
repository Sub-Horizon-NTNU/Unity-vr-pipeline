using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ROVVR
{
    [Serializable]
    public sealed class RovVideoViewProfile
    {
        public int schemaVersion = 1;
        public float horizontalFov = 110f;
        public float curveDegrees = 45f;
        public float distance = 2f;
        public float verticalPositionMeters;
        public float verticalOffsetDegrees;
        public float imageRollDegrees;
        public bool anchorToRovImu = true;
        public bool cylindricalSurface = true;
        public bool headFollowingEnabled = true;
        public bool compensateRovPitch = true;
        public float pitchCompensationStrength = 1f;
        public bool hudEnabled = true, hudShowAttitude = true, hudShowBattery = true, hudShowDepth = true;
        public bool hudShowDiagnostics;
        public RovHudAnchorMode hudAnchorMode = RovHudAnchorMode.VideoSurface;
        public bool hudShowCompass = true;
        public float compassRadius = 1.7f, compassDrop = 0.9f;
        public float hudScale = 1f, hudOpacity = 0.85f;
        public float telemetrySmoothingSeconds = 0.06f;
        public float servoSmoothingSeconds = 0.12f, servoMaximumSpeed = 45f;
        public RovHudLayout hudLayout = new RovHudLayout();

        public static string FilePath => Path.Combine(Application.persistentDataPath, "rov-video-view.json");

        public static RovVideoViewProfile Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
                throw new ArgumentException("Konfigurasjonen må være et JSON-objekt.");
            var profile = new RovVideoViewProfile();
            JsonUtility.FromJsonOverwrite(json, profile);
            profile.Validate();
            return profile;
        }

        public void Validate()
        {
            if (schemaVersion != 1) throw new ArgumentException("Ukjent versjon av visningskonfigurasjonen.");
            if (!Enum.IsDefined(typeof(RovHudAnchorMode), hudAnchorMode)) throw new ArgumentException("Ukjent HUD-forankring.");
            foreach (float value in new[] { horizontalFov, curveDegrees, distance, verticalPositionMeters, verticalOffsetDegrees, imageRollDegrees, pitchCompensationStrength,
                hudScale, hudOpacity, compassRadius, compassDrop, telemetrySmoothingSeconds, servoSmoothingSeconds, servoMaximumSpeed })
                if (!RovCameraPoseMath.Finite(value)) throw new ArgumentException("Visningsverdier må være endelige tall.");
            horizontalFov = Mathf.Clamp(horizontalFov, 50f, 150f);
            curveDegrees = Mathf.Clamp(curveDegrees, 0f, 150f);
            distance = Mathf.Clamp(distance, 1f, 4f);
            verticalPositionMeters = Mathf.Clamp(verticalPositionMeters, -2f, 2f);
            verticalOffsetDegrees = Mathf.Clamp(verticalOffsetDegrees, -25f, 25f);
            imageRollDegrees = Mathf.Clamp(imageRollDegrees, -15f, 15f);
            pitchCompensationStrength = Mathf.Clamp(pitchCompensationStrength, 0f, 1.5f);
            hudScale = Mathf.Clamp(hudScale, 0.7f, 1.5f);
            hudOpacity = Mathf.Clamp(hudOpacity, 0.2f, 1f);
            compassRadius = Mathf.Clamp(compassRadius, 1f, 3f);
            compassDrop = Mathf.Clamp(compassDrop, 0.4f, 1.8f);
            telemetrySmoothingSeconds = Mathf.Clamp(telemetrySmoothingSeconds, 0f, 0.2f);
            servoSmoothingSeconds = Mathf.Clamp(servoSmoothingSeconds, 0f, 0.5f);
            servoMaximumSpeed = Mathf.Clamp(servoMaximumSpeed, 5f, 90f);
            if (hudLayout == null) hudLayout = new RovHudLayout();
            hudLayout.Validate();
        }

        public void Save(string path)
        {
            Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(this, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

    }
}
