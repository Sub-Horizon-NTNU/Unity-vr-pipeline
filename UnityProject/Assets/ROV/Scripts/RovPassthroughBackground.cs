using System;
using UnityEngine;

namespace ROVVR
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class RovPassthroughBackground : MonoBehaviour
    {
        public string Status { get; private set; } = "Waiting for a Meta OpenXR session.";
        public bool CapabilityChecked { get; private set; }
        public bool Supported { get; private set; }
        public bool LayerResumed { get; private set; }
        public bool BackgroundTransparent { get; private set; }
        public bool ManagerLinked => manager != null;
        public bool Initialized => OVRPlugin.initialized && OVRManager.IsInsightPassthroughInitialized();

        private Camera viewCamera;
        private CameraClearFlags originalClearFlags;
        private Color originalBackground;
        private OVRManager manager;
        private bool originalPassthroughEnabled;
        private GameObject ownedManager;
        private GameObject layerObject;
        private OVRPassthroughLayer layer;
        private bool setupAttempted;
        private string setupFailure;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            viewCamera = GetComponent<Camera>();
            originalClearFlags = viewCamera.clearFlags;
            originalBackground = viewCamera.backgroundColor;
            setupAttempted = false;
            setupFailure = null;
            CapabilityChecked = Supported = LayerResumed = BackgroundTransparent = false;
            Status = "Waiting for a Meta OpenXR session.";
        }

        private void Update()
        {
            if (!OVRPlugin.initialized)
            {
                SetTransparent(false);
                Status = "Waiting for a Meta OpenXR session; the normal VR background is retained.";
                return;
            }

            if (!setupAttempted)
            {
                setupAttempted = true;
                CapabilityChecked = true;
                Supported = OVRManager.IsInsightPassthroughSupported();
                if (Supported)
                {
                    try { CreatePassthrough(); }
                    catch (Exception error)
                    {
                        setupFailure = "Could not create the passthrough layer: " + error.Message;
                        Debug.LogWarning("[ROV passthrough] " + setupFailure, this);
                    }
                }
            }

            if (setupFailure != null)
            {
                SetTransparent(false);
                Status = setupFailure;
            }
            else if (!Supported)
            {
                SetTransparent(false);
                Status = "Passthrough is unavailable. Enable Passthrough over Link in Meta, then restart Unity.";
            }
            else if (OVRManager.HasInsightPassthroughInitFailed())
            {
                SetTransparent(false);
                Status = "Passthrough initialization failed. Check Meta's Passthrough over Link setting and restart Unity.";
            }
            else if (LayerResumed && Initialized && manager != null && manager.isInsightPassthroughEnabled)
            {
                SetTransparent(true);
                Status = "Room passthrough active behind the Atlas video and HUD.";
            }
            else
            {
                SetTransparent(false);
                Status = "Waiting for the passthrough compositor layer; the normal VR background is retained.";
            }
        }

        private void CreatePassthrough()
        {
            manager = OVRManager.instance;
            if (manager == null) manager = FindFirstObjectByType<OVRManager>();
            if (manager == null)
            {
                // Meta supports OVRManager without OVRCameraRig. Keep the existing XR rig and pose driver.
                ownedManager = new GameObject("ROV passthrough manager");
                ownedManager.SetActive(false);
                ownedManager.transform.SetParent(transform, false);
                manager = ownedManager.AddComponent<OVRManager>();
                manager.trackingOriginType = (OVRManager.TrackingOrigin)OVRPlugin.GetTrackingOriginType();
                manager.isInsightPassthroughEnabled = true;
                ownedManager.SetActive(true);
                // This feature does not need the SDK's development-build TCP metrics server.
                var metrics = ownedManager.GetComponent<OVRSystemPerfMetrics.OVRSystemPerfMetricsTcpServer>();
                if (metrics != null) metrics.enabled = false;
            }
            else
            {
                originalPassthroughEnabled = manager.isInsightPassthroughEnabled;
                manager.isInsightPassthroughEnabled = true;
            }

            layerObject = new GameObject("ROV room passthrough underlay");
            layerObject.SetActive(false);
            layerObject.transform.SetParent(transform, false);
            layer = layerObject.AddComponent<OVRPassthroughLayer>();
            layer.overlayType = OVROverlay.OverlayType.Underlay;
            layer.textureOpacity = 1f;
            layer.passthroughLayerResumed.AddListener(OnLayerResumed);
            layerObject.SetActive(true);
        }

        private void OnLayerResumed(OVRPassthroughLayer resumedLayer)
        {
            if (resumedLayer == layer) LayerResumed = true;
        }

        private void SetTransparent(bool transparent)
        {
            if (viewCamera == null || BackgroundTransparent == transparent) return;
            viewCamera.clearFlags = transparent ? CameraClearFlags.SolidColor : originalClearFlags;
            viewCamera.backgroundColor = transparent ? Color.clear : originalBackground;
            BackgroundTransparent = transparent;
        }

        private void OnDisable()
        {
            if (layer != null) layer.passthroughLayerResumed.RemoveListener(OnLayerResumed);
            if (layerObject != null) Destroy(layerObject);
            if (manager != null && ownedManager == null) manager.isInsightPassthroughEnabled = originalPassthroughEnabled;
            if (ownedManager != null) Destroy(ownedManager);
            SetTransparent(false);
            layer = null;
            layerObject = ownedManager = null;
            manager = null;
            LayerResumed = false;
            Status = "Room passthrough disabled; the normal VR background is retained.";
        }
    }
}
