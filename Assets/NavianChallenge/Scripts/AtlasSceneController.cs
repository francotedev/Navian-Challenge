using UnityEngine;

namespace NavianChallenge
{
    /// <summary>
    /// Minimal camera helper for the base scene: orbit + zoom + reset, plus a small
    /// on-screen note.
    ///
    /// It deliberately does NOT show/hide the MRI or the meshes: everything is loaded and
    /// visible at the same time. Implementing visibility, interaction, and any way to
    /// explore/interpret the data is part of the challenge — build it yourself.
    ///
    /// Controls (Play mode):
    ///   Left / Right mouse drag  - orbit camera around the atlas
    ///   Mouse wheel              - zoom
    ///   R                        - reset camera
    ///   H                        - toggle this help overlay
    /// </summary>
    public class AtlasSceneController : MonoBehaviour
    {
        [Header("References")]
        public Transform atlasRoot;
        public Camera cam;

        [Header("Orbit")]
        public float orbitSpeed = 4f;
        public float zoomSpeed = 0.15f;

        Vector3 pivot;
        Vector3 camStartPos;
        Quaternion camStartRot;
        bool showHelp = true;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            pivot = atlasRoot != null ? atlasRoot.position : Vector3.zero;
            if (cam != null)
            {
                camStartPos = cam.transform.position;
                camStartRot = cam.transform.rotation;
            }
        }

        void Update()
        {
            HandleOrbit();

            if (Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
            if (Input.GetKeyDown(KeyCode.R) && cam != null)
            {
                cam.transform.position = camStartPos;
                cam.transform.rotation = camStartRot;
            }
        }

        void HandleOrbit()
        {
            if (cam == null) return;

            if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
            {
                float mx = Input.GetAxis("Mouse X") * orbitSpeed;
                float my = Input.GetAxis("Mouse Y") * orbitSpeed;
                cam.transform.RotateAround(pivot, Vector3.up, mx);
                cam.transform.RotateAround(pivot, cam.transform.right, -my);
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                Vector3 dir = cam.transform.position - pivot;
                cam.transform.position = pivot + dir * (1f - scroll * zoomSpeed * 10f);
            }
        }

        void OnGUI()
        {
            if (!showHelp) return;
            const int w = 330, h = 116;
            GUI.Box(new Rect(10, 10, w, h), "Navian XR Challenge - base scene");
            var s = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            GUI.Label(new Rect(22, 34, w - 24, h - 30),
                "Drag mouse: orbit    Wheel: zoom    R: reset camera    H: hide\n\n" +
                "The MRI volume and the 4 segmentation meshes are all loaded and\n" +
                "aligned. Everything you build from here is up to you.", s);
        }
    }
}
