using UnityEngine;
using UnityVolumeRendering;

namespace NavianChallenge
{
    /// <summary>
    /// A single movable clipping plane through the MRI volume — the "corte" / longitudinal
    /// section. Uses UVR's built-in <see cref="CrossSectionPlane"/>: the plane's transform
    /// defines the cut (the shader discards voxels on the plane's +forward side), so sliding it
    /// along an anatomical axis sweeps the section through the volume, revealing the interior on
    /// the approach — the clipping-plane inspection the brief calls out.
    /// </summary>
    public class CrossSectionController : MonoBehaviour
    {
        public enum CutAxis { Axial, Coronal, Sagittal }

        public VolumeController volume;

        CrossSectionPlane plane;
        GameObject planeGO;

        bool active;
        CutAxis axis = CutAxis.Axial;
        float position = 0.5f; // 0..1 along the axis

        public bool Active => active;
        public CutAxis Axis => axis;
        public float Position => position;
        public bool Ready => plane != null;

        void Start()
        {
            if (volume == null) return;
            volume.OnReady += OnVolumeReady;
            if (volume.Ready) OnVolumeReady();
        }

        void OnDestroy()
        {
            if (volume != null) volume.OnReady -= OnVolumeReady;
        }

        void OnVolumeReady()
        {
            if (planeGO != null) return;
            planeGO = new GameObject("MRI Cross Section");
            plane = planeGO.AddComponent<CrossSectionPlane>();
            plane.SetTargetObject(volume.Volume); // registers with the volume's CrossSectionManager
            Apply();
            planeGO.SetActive(active); // honor any state set before the volume finished loading
        }

        public void SetActive(bool on)
        {
            active = on;
            if (planeGO != null) planeGO.SetActive(on); // OnEnable/OnDisable (un)register the cut
            Apply();                                    // refresh the mesh-clip globals
        }

        public void SetAxis(CutAxis a) { axis = a; Apply(); }

        public void SetPosition(float t) { position = Mathf.Clamp01(t); Apply(); }

        void Apply()
        {
            if (plane == null || volume == null || volume.Volume == null) return;
            Transform c = volume.Volume.volumeContainerObject.transform;

            Vector3 dir;
            float halfExtent;
            switch (axis)
            {
                case CutAxis.Sagittal: dir = c.right;   halfExtent = 0.5f * c.lossyScale.x; break;
                case CutAxis.Coronal:  dir = c.forward; halfExtent = 0.5f * c.lossyScale.z; break;
                default:               dir = c.up;      halfExtent = 0.5f * c.lossyScale.y; break; // Axial
            }
            dir = dir.normalized;

            float offset = (position - 0.5f) * 2f * halfExtent;
            Vector3 point = c.position + dir * offset;
            // forward = dir → the shader clips the +dir half; sweeping offset moves the section.
            planeGO.transform.SetPositionAndRotation(point, Quaternion.LookRotation(dir));

            // Drive the mesh clip (Navian/ClippedSurface) with the exact same plane and side,
            // so the segmentation meshes slice away together with the volume.
            Shader.SetGlobalVector("_SectionPoint", point);
            Shader.SetGlobalVector("_SectionNormal", dir);
            Shader.SetGlobalFloat("_SectionEnabled", active ? 1f : 0f);
        }
    }
}
