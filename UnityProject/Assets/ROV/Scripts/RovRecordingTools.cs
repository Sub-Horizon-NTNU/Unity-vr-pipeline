using UnityEngine;
namespace ROVVR
{
    public sealed partial class RovVideoViewCalibration
    {
        private RovVideoRecorder recorder;
        private UnityEngine.UI.Text recordingBadge;
        private string RecordingStatus => recorder == null ? "Ready / MP4 / no audio" : recorder.Status;
        private Texture RecordingTexture()
        {
            var mat = GetComponent<MeshRenderer>().sharedMaterial;
            return mat != null && mat.HasProperty("_rgb") ? mat.GetTexture("_rgb") : null;
        }
        public void ToggleRecording()
        {
            if (!Application.isPlaying) return;
            if (recorder == null) recorder = gameObject.AddComponent<RovVideoRecorder>();
            if (recorder.Recording) recorder.Stop();
            else if (!recorder.Busy) recorder.Begin(RecordingTexture, pilot.recordPresentation, pilot.recordFlipVertical, pilot.recordFullHd ? 1920 : 1280);
        }
        private void UpdateRecordingFeedback()
        {
            bool visible = recorder != null && recorder.Busy;
            if (visible && recordingBadge == null)
            {
                var go = new GameObject("ROV recording indicator", typeof(RectTransform), typeof(Canvas));
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 260;
                canvas.worldCamera = eye.GetComponent<Camera>();
                var root = (RectTransform)go.transform; root.sizeDelta = new Vector2(560, 48); root.localScale = Vector3.one * 0.0007f;
                recordingBadge = PilotLabel(root, "REC", 0, 0, 560, 48, 28); recordingBadge.color = new Color(1f, 0.25f, 0.2f);
            }
            if (recordingBadge == null) return;
            recordingBadge.transform.parent.gameObject.SetActive(visible);
            if (!visible) return;
            recordingBadge.transform.parent.SetPositionAndRotation(PilotHudPosition + PilotHudRotation * new Vector3(0, 0.6f, 1.4f), PilotHudRotation);
            recordingBadge.text = RecordingStatus;
        }
        private void DrawRecordingControls()
        {
            GUILayout.Label("VIDEO RECORDING / " + RecordingStatus);
            pilot.recordPresentation = GUILayout.Toggle(pilot.recordPresentation, "Record PC mirror with HUD/menu (off = clean camera video)");
            pilot.recordFullHd = GUILayout.Toggle(pilot.recordFullHd, "Record 1920x1080 (off = 1280x720), 30 fps");
            pilot.recordFlipVertical = GUILayout.Toggle(pilot.recordFlipVertical, "Flip recording vertically");
            if (Application.isPlaying && GUILayout.Button(recorder != null && recorder.Recording ? "STOP recording" : "START recording")) ToggleRecording();
            GUILayout.Label(RovVideoRecorder.Folder);
            if (recorder != null && !string.IsNullOrEmpty(recorder.LastPath)) GUILayout.Label(recorder.LastPath);
            if (GUILayout.Button("Open recordings folder")) { System.IO.Directory.CreateDirectory(RovVideoRecorder.Folder); Application.OpenURL(new System.Uri(RovVideoRecorder.Folder).AbsoluteUri); }
        }
    }
}
