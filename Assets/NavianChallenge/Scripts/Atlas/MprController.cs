using System;
using UnityEngine;
using UnityVolumeRendering;

namespace NavianChallenge
{
    /// <summary>
    /// Multi-planar reconstruction (MPR): the classic radiological 2D read of the volume. Extracts
    /// axial / coronal / sagittal grayscale slices from the MRI <see cref="VolumeDataset"/> on the
    /// CPU, windowed by the same window/level the 3D volume uses, and keeps one crosshair in sync
    /// across the three slices and a 3D marker in the scene — so the 2D planes and the volumetric
    /// view point at the same voxel (the brief's "explorador de cortes" + "comparación 3D vs 2D").
    ///
    /// Lit up only while the Slices tool is active: it then shows the slice panel (hiding the
    /// trajectory readout) and the 3D crosshair. Slices are re-extracted lazily — one plane per
    /// changed axis, all three when the window/level changes. Axis mapping mirrors the section cut:
    /// X → sagittal (L-R), Y → axial (S-I), Z → coronal (A-P).
    ///
    /// Known simplification (README): slice image orientation is the raw dataset order (not forced
    /// to radiological convention), and the crosshair samples the nearest voxel (no interpolation).
    /// </summary>
    public class MprController : MonoBehaviour
    {
        public enum Plane { Axial, Coronal, Sagittal }

        public VolumeController volume;
        public AppState appState;
        public Camera cam;
        public float markerRadiusWorld = 3f;

        [Header("Toggled by the Slices tool")]
        public GameObject mprCanvasObject;     // the 2D slice panel
        public GameObject readoutObjectToHide; // trajectory readout, hidden while slicing

        public event Action Changed;

        VolumeDataset ds;
        int dimX, dimY, dimZ;
        float dataMin, dataRange;
        bool ready;

        Vector3 cross = new(0.5f, 0.5f, 0.5f); // normalized volume coords
        Vector2 lastWindow = new(-1f, -1f);
        int lastIx = -1, lastIy = -1, lastIz = -1;

        Texture2D texAxial, texCoronal, texSagittal;
        Color32[] bufAxial, bufCoronal, bufSagittal;
        Transform marker;

        public Vector3 Cross => cross;
        public bool Ready => ready;

        public Texture2D GetTexture(Plane p) => p switch
        {
            Plane.Axial => texAxial,
            Plane.Coronal => texCoronal,
            _ => texSagittal
        };

        void Start()
        {
            BuildMarker();
            if (volume != null)
            {
                volume.OnReady += OnVolumeReady;
                if (volume.Ready) OnVolumeReady();
            }
            if (appState != null)
            {
                appState.ToolChanged += HandleToolChanged;
                HandleToolChanged(appState.Current); // honor the tool selected at startup
            }
        }

        void OnDestroy()
        {
            if (volume != null) volume.OnReady -= OnVolumeReady;
            if (appState != null) appState.ToolChanged -= HandleToolChanged;
        }

        void OnVolumeReady()
        {
            if (ready || volume.Volume == null || volume.Volume.dataset == null) return;
            ds = volume.Volume.dataset;
            dimX = ds.dimX; dimY = ds.dimY; dimZ = ds.dimZ;
            dataMin = ds.GetMinDataValue();
            dataRange = Mathf.Max(1e-6f, ds.GetMaxDataValue() - dataMin);
            ready = true;
            if (IsSlices()) BuildAll();
        }

        bool IsSlices() => appState != null && appState.Current == Tool.Slices;

        void HandleToolChanged(Tool tool)
        {
            bool slices = tool == Tool.Slices;
            if (mprCanvasObject != null) mprCanvasObject.SetActive(slices);
            if (readoutObjectToHide != null) readoutObjectToHide.SetActive(!slices);
            if (marker != null) marker.gameObject.SetActive(slices && ready);
            if (slices && ready) BuildAll();
        }

        void Update()
        {
            if (!ready || !IsSlices()) return;
            Vector2 w = volume != null ? volume.Window : new Vector2(0f, 1f);
            if (w != lastWindow) BuildAll(); // window/level changed on the MRI panel — re-window slices
        }

        /// <summary>Move the crosshair from a click on a slice (u,v normalized in that plane).</summary>
        public void Pick(Plane p, float u, float v)
        {
            u = Mathf.Clamp01(u); v = Mathf.Clamp01(v);
            switch (p)
            {
                case Plane.Axial:    cross.x = u; cross.z = v; break; // horizontal = X, vertical = Z
                case Plane.Coronal:  cross.x = u; cross.y = v; break; // horizontal = X, vertical = Y
                case Plane.Sagittal: cross.z = u; cross.y = v; break; // horizontal = Z, vertical = Y
            }
            RefreshSlices();
            UpdateMarker();
            Changed?.Invoke();
        }

        void BuildAll()
        {
            lastIx = lastIy = lastIz = -1; // force all three to re-extract
            lastWindow = volume != null ? volume.Window : new Vector2(0f, 1f);
            RefreshSlices();
            UpdateMarker();
            Changed?.Invoke();
        }

        void RefreshSlices()
        {
            if (!ready) return;
            int ix = Idx(cross.x, dimX);
            int iy = Idx(cross.y, dimY);
            int iz = Idx(cross.z, dimZ);
            if (ix != lastIx) { ExtractSagittal(ix); lastIx = ix; }
            if (iy != lastIy) { ExtractAxial(iy);    lastIy = iy; }
            if (iz != lastIz) { ExtractCoronal(iz);  lastIz = iz; }
        }

        static int Idx(float n, int dim) => Mathf.Clamp(Mathf.RoundToInt(n * (dim - 1)), 0, dim - 1);

        byte Gray(float d)
        {
            float n = (d - dataMin) / dataRange;                         // 0..1 over the data range
            float g = Mathf.Clamp01(Mathf.InverseLerp(lastWindow.x, lastWindow.y, n)); // apply window
            return (byte)(g * 255f);
        }

        void ExtractAxial(int iy) // fixed Y; image X (horizontal) × Z (vertical)
        {
            EnsureTex(ref texAxial, dimX, dimZ);
            EnsureBuf(ref bufAxial, dimX * dimZ);
            for (int z = 0; z < dimZ; z++)
                for (int x = 0; x < dimX; x++)
                {
                    byte g = Gray(ds.GetData(x, iy, z));
                    bufAxial[x + z * dimX] = new Color32(g, g, g, 255);
                }
            texAxial.SetPixels32(bufAxial);
            texAxial.Apply(false);
        }

        void ExtractCoronal(int iz) // fixed Z; image X (horizontal) × Y (vertical)
        {
            EnsureTex(ref texCoronal, dimX, dimY);
            EnsureBuf(ref bufCoronal, dimX * dimY);
            for (int y = 0; y < dimY; y++)
                for (int x = 0; x < dimX; x++)
                {
                    byte g = Gray(ds.GetData(x, y, iz));
                    bufCoronal[x + y * dimX] = new Color32(g, g, g, 255);
                }
            texCoronal.SetPixels32(bufCoronal);
            texCoronal.Apply(false);
        }

        void ExtractSagittal(int ix) // fixed X; image Z (horizontal) × Y (vertical)
        {
            EnsureTex(ref texSagittal, dimZ, dimY);
            EnsureBuf(ref bufSagittal, dimZ * dimY);
            for (int y = 0; y < dimY; y++)
                for (int z = 0; z < dimZ; z++)
                {
                    byte g = Gray(ds.GetData(ix, y, z));
                    bufSagittal[z + y * dimZ] = new Color32(g, g, g, 255);
                }
            texSagittal.SetPixels32(bufSagittal);
            texSagittal.Apply(false);
        }

        static void EnsureTex(ref Texture2D tex, int w, int h)
        {
            if (tex != null && tex.width == w && tex.height == h) return;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        static void EnsureBuf(ref Color32[] buf, int n)
        {
            if (buf == null || buf.Length != n) buf = new Color32[n];
        }

        void BuildMarker()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "MPR Crosshair";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // must not block picking
            go.transform.localScale = Vector3.one * (markerRadiusWorld * 2f);

            var mr = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Color");
            if (shader != null) mr.sharedMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.3f) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            marker = go.transform;
            go.SetActive(false);
        }

        void UpdateMarker()
        {
            if (marker == null || volume == null || volume.Volume == null) return;
            Transform c = volume.Volume.volumeContainerObject.transform;
            // Normalized [0,1] maps to the volume container's unit cube [-0.5, 0.5], same frame the
            // craniotomy uses, so the marker lands on the anatomy the slices are showing.
            marker.position = c.TransformPoint(new Vector3(cross.x - 0.5f, cross.y - 0.5f, cross.z - 0.5f));
            marker.gameObject.SetActive(IsSlices() && ready);
        }
    }
}
