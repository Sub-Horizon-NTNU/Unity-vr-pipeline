using System;
using UnityEngine;

namespace ROVVR
{
    public enum RovHudAnchorMode { VideoSurface, Headset, CenteredHeading }

    [Serializable]
    public sealed class RovHudBoxLayout
    {
        public Vector3 position;
        public Vector3 angleOffset;
        public float scale = 1f;
        public bool faceViewer = true;

        public RovHudBoxLayout(float x, float y, float distance) { position = new Vector3(x, y, distance); }
        public RovHudBoxLayout Copy() => new RovHudBoxLayout(position.x, position.y, position.z)
            { angleOffset = angleOffset, scale = scale, faceViewer = faceViewer };

        public Quaternion Rotation => (faceViewer ? Quaternion.LookRotation(position, Vector3.up) : Quaternion.identity)
            * Quaternion.Euler(angleOffset);

        public void Validate()
        {
            foreach (float value in new[] {position.x, position.y, position.z, angleOffset.x, angleOffset.y, angleOffset.z, scale})
                if (!RovCameraPoseMath.Finite(value)) throw new ArgumentException("HUD-plassering må bestå av endelige tall.");
            position.x = Mathf.Clamp(position.x, -2f, 2f);
            position.y = Mathf.Clamp(position.y, -2f, 2f);
            position.z = Mathf.Clamp(position.z, 0.6f, 4f);
            scale = Mathf.Clamp(scale, 0.5f, 2f);
            angleOffset = new Vector3(Mathf.Clamp(angleOffset.x, -60f, 60f), Mathf.Clamp(angleOffset.y, -60f, 60f), Mathf.Clamp(angleOffset.z, -60f, 60f));
        }
    }

    [Serializable]
    public sealed class RovHudLayout
    {
        public RovHudBoxLayout status = new RovHudBoxLayout(0f, 0.65f, 2.2f);
        public RovHudBoxLayout battery = new RovHudBoxLayout(-0.8f, -0.42f, 2f);
        public RovHudBoxLayout depth = new RovHudBoxLayout(0.8f, 0.18f, 2f);
        public RovHudBoxLayout cameraTilt = new RovHudBoxLayout(-0.8f, 0.18f, 2f);
        public RovHudBoxLayout attitude = new RovHudBoxLayout(0.8f, -0.42f, 2f);
        public RovHudBoxLayout diagnostics = new RovHudBoxLayout(0f, -0.7f, 2.2f);

        // Applied explicitly so existing pilots keep their calibrated layout
        // until they select the FPV preset.
        public static RovHudLayout CreateFpv() => new RovHudLayout
        {
            status = new RovHudBoxLayout(0f, 0.7f, 2f) { faceViewer = false },
            battery = new RovHudBoxLayout(-0.92f, -0.47f, 2f) { faceViewer = false },
            depth = new RovHudBoxLayout(0.92f, -0.47f, 2f) { faceViewer = false },
            cameraTilt = new RovHudBoxLayout(-0.92f, 0.38f, 2f) { faceViewer = false },
            attitude = new RovHudBoxLayout(0.92f, 0.38f, 2f) { faceViewer = false },
            diagnostics = new RovHudBoxLayout(0f, -0.7f, 2f) { faceViewer = false }
        };

        public void Validate()
        {
            if (status == null) status = new RovHudBoxLayout(0f, 0.65f, 2.2f);
            if (battery == null) battery = new RovHudBoxLayout(-0.8f, -0.42f, 2f);
            if (depth == null) depth = new RovHudBoxLayout(0.8f, 0.18f, 2f);
            if (cameraTilt == null) cameraTilt = new RovHudBoxLayout(-0.8f, 0.18f, 2f);
            if (attitude == null) attitude = new RovHudBoxLayout(0.8f, -0.42f, 2f);
            if (diagnostics == null) diagnostics = new RovHudBoxLayout(0f, -0.7f, 2.2f);
            foreach (var box in new[] {status, battery, depth, cameraTilt, attitude, diagnostics}) box.Validate();
        }

        public RovHudLayout Copy() => new RovHudLayout
        {
            status = status.Copy(), battery = battery.Copy(), depth = depth.Copy(),
            cameraTilt = cameraTilt.Copy(), attitude = attitude.Copy(), diagnostics = diagnostics.Copy()
        };
    }
}
