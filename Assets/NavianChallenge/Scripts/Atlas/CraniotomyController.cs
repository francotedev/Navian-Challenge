using UnityEngine;
using UnityVolumeRendering;

namespace NavianChallenge
{
    /// <summary>
    /// The "craniotomy window": a rounded (or box) cutout that carves the MRI volume and the
    /// segmentation meshes away together, exposing the structures underneath — the surgical
    /// bone-flap opening, in miniature. Complements the single clipping plane
    /// (<see cref="CrossSectionController"/>): a plane sweeps a flat section, this removes a
    /// finite shape you position over the head. Defaults to a sphere (a real craniotomy is
    /// rounded); a Box option is exposed on the panel.
    ///
    /// Volume side: UVR's built-in <see cref="CutoutSphere"/> / <see cref="CutoutBox"/> in
    /// Exclusive mode (voxels inside the shape are discarded), registered with the volume's
    /// cross-section manager. Mesh side: the exact same shape is pushed into Navian/ClippedSurface
    /// via global shader params (<c>_CraniotomyMatrix</c> maps world → shape-local; inside the unit
    /// shape is removed), so the meshes cut in lockstep with the volume — matching UVR's Exclusive test.
    ///
    /// Active only while the Craniotomy tool is selected: picking the tool opens the window,
    /// leaving it closes it. Position (over the head) and size (mm) are live via the panel.
    /// </summary>
    public class CraniotomyController : MonoBehaviour
    {
        public VolumeController volume;
        public AppState appState;
        public float mmPerUnit = 1f;

        public enum Shape { Sphere, Box }

        CutoutBox box;
        CutoutSphere sphere;
        GameObject boxGO;

        bool active;
        Shape shape = Shape.Sphere; // a craniotomy is rounded; Box is available via the panel toggle
        // Window centre as a 0..1 fraction along each anatomical axis of the volume box
        // (x → sagittal L-R, y → axial S-I, z → coronal A-P). Default sits high and central so
        // toggling on removes a visible skullcap window rather than an internal cavity.
        Vector3 centerNorm = new(0.5f, 0.80f, 0.5f);
        float sizeMm = 70f;

        public const float SizeMinMm = 20f;
        public const float SizeMaxMm = 140f;

        public bool Active => active;
        public Vector3 CenterNorm => centerNorm;
        public float SizeMm => sizeMm;
        public Shape CurrentShape => shape;
        public bool Ready => box != null;

        void Start()
        {
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

        void HandleToolChanged(Tool tool) => SetActive(tool == Tool.Craniotomy);

        void OnVolumeReady()
        {
            if (boxGO != null) return;
            boxGO = new GameObject("MRI Craniotomy");

            box = boxGO.AddComponent<CutoutBox>();
            sphere = boxGO.AddComponent<CutoutSphere>();
            box.cutoutType = CutoutType.Exclusive;
            sphere.CutoutType = CutoutType.Exclusive;

            // Both share this GameObject's transform; only the active shape registers with the
            // volume's CrossSectionManager (SetTargetObject skips a disabled component).
            box.enabled = shape == Shape.Box;
            sphere.enabled = shape == Shape.Sphere;
            box.SetTargetObject(volume.Volume);
            sphere.SetTargetObject(volume.Volume);

            Apply();
            boxGO.SetActive(active); // honor any state set before the volume finished loading
        }

        public void SetActive(bool on)
        {
            active = on;
            if (boxGO != null) boxGO.SetActive(on); // OnEnable/OnDisable (un)register the cutout
            Apply();                                // refresh the mesh-clip globals
        }

        public void SetCenter(int axis, float t)
        {
            t = Mathf.Clamp01(t);
            if (axis == 0) centerNorm.x = t;
            else if (axis == 1) centerNorm.y = t;
            else centerNorm.z = t;
            Apply();
        }

        public void SetSizeMm(float mm)
        {
            sizeMm = Mathf.Clamp(mm, SizeMinMm, SizeMaxMm);
            Apply();
        }

        public void SetShape(Shape s)
        {
            shape = s;
            // Enabling/disabling (un)registers each cutout with the volume via OnEnable/OnDisable.
            if (box != null) box.enabled = s == Shape.Box;
            if (sphere != null) sphere.enabled = s == Shape.Sphere;
            Apply();
        }

        void Apply()
        {
            if (box == null || volume == null || volume.Volume == null) return;
            Transform c = volume.Volume.volumeContainerObject.transform;

            // The volume renders on a unit cube, so its world extent along each axis is lossyScale.
            Vector3 ext = c.lossyScale;
            Vector3 offset =
                c.right   * ((centerNorm.x - 0.5f) * ext.x) +
                c.up      * ((centerNorm.y - 0.5f) * ext.y) +
                c.forward * ((centerNorm.z - 0.5f) * ext.z);

            float sizeWorld = sizeMm / Mathf.Max(1e-4f, mmPerUnit);
            boxGO.transform.SetPositionAndRotation(c.position + offset, c.rotation);
            boxGO.transform.localScale = Vector3.one * sizeWorld;

            // Drive the mesh clip with the exact same box. worldToLocalMatrix maps a world point
            // into the box's local frame where the unit cube spans ±0.5 → inside = removed, the
            // same convention CutoutBox feeds the volume shader (VolumeCutout.cginc, BOX_EXCL).
            Shader.SetGlobalMatrix("_CraniotomyMatrix", boxGO.transform.worldToLocalMatrix);
            Shader.SetGlobalFloat("_CraniotomySphere", shape == Shape.Sphere ? 1f : 0f);
            Shader.SetGlobalFloat("_CraniotomyEnabled", active ? 1f : 0f);
        }
    }
}
