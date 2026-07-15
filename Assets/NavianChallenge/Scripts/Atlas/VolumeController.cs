using System;
using System.Collections;
using UnityEngine;
using UnityVolumeRendering;
using RenderMode = UnityVolumeRendering.RenderMode; // disambiguate from UnityEngine.RenderMode (Canvas)

namespace NavianChallenge
{
    /// <summary>
    /// Thin runtime wrapper over UnityVolumeRendering's <see cref="VolumeRenderedObject"/> that
    /// exposes the three controls that matter for exploring the MRI — render mode (DVR / MIP /
    /// Isosurface), window/level (min–max value), and visibility. These are UVR built-ins; our
    /// job is to surface them at runtime, which is the actual "neuronavigator" imaging core.
    ///
    /// The volume is created asynchronously on Play (<c>VolumeObjectFactory.CreateObjectAsync</c>),
    /// so we poll for it and fire <see cref="OnReady"/> once it exists. Control state set before
    /// then is stored and applied on bind.
    /// </summary>
    public class VolumeController : MonoBehaviour
    {
        public Transform atlasRoot;
        public float bindTimeout = 20f;

        public VolumeRenderedObject Volume { get; private set; }
        public bool Ready => Volume != null;
        public event Action OnReady;

        RenderMode mode = RenderMode.DirectVolumeRendering;
        Vector2 window = new(0f, 1f);
        bool visible = true;

        public RenderMode Mode => mode;
        public Vector2 Window => window;
        public bool Visible => visible;

        void Start() => StartCoroutine(BindWhenReady());

        IEnumerator BindWhenReady()
        {
            float t = 0f;
            VolumeRenderedObject found = null;
            while (found == null && t < bindTimeout)
            {
                found = Find();
                if (found == null) { t += Time.deltaTime; yield return null; }
            }
            if (found == null)
            {
                Debug.LogWarning("[NeuroPath] MRI volume never appeared; volume controls disabled.");
                yield break;
            }

            Volume = found;
            mode = Volume.GetRenderMode();
            window = Volume.GetVisibilityWindow();
            visible = Volume.meshRenderer == null || Volume.meshRenderer.enabled;
            OnReady?.Invoke();
        }

        VolumeRenderedObject Find()
        {
            if (atlasRoot != null) return atlasRoot.GetComponentInChildren<VolumeRenderedObject>(true);
            return FindFirstObjectByType<VolumeRenderedObject>();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (Volume != null && Volume.meshRenderer != null)
                Volume.meshRenderer.enabled = value;
        }

        public void SetRenderMode(RenderMode m)
        {
            mode = m;
            // UVR resets the visibility window to (0,1) when the mode changes; mirror that here
            // so the UI and our cached state stay in sync.
            window = new Vector2(0f, 1f);
            if (Volume != null) Volume.SetRenderMode(m);
        }

        public void SetWindow(float min, float max)
        {
            min = Mathf.Clamp01(min);
            max = Mathf.Clamp01(max);
            if (max < min) max = min;
            window = new Vector2(min, max);
            if (Volume != null) Volume.SetVisibilityWindow(window);
        }
    }
}
