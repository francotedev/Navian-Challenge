using UnityEngine;
using UnityEngine.EventSystems;

namespace NavianChallenge
{
    /// <summary>
    /// Orbit / zoom / pan around the atlas.
    ///  - Right-drag always orbits (never collides with UI clicks or point placement).
    ///  - Left-drag also orbits, but only in Explore mode, so placement tools own the left
    ///    button in their own modes.
    ///  - Middle-drag pans, wheel zooms, R resets.
    /// Input is ignored while the pointer is over a world-space panel so dragging a slider
    /// doesn't also spin the camera.
    /// </summary>
    public class CameraOrbit : MonoBehaviour
    {
        [Header("References")]
        public Camera cam;
        public Transform pivotTarget; // usually AtlasRoot
        public AppState appState;

        [Header("Tuning")]
        public float orbitSpeed = 4f;
        public float panSpeed = 1.0f;
        public float zoomSpeed = 1.2f;
        public float minDistance = 0.05f;

        Vector3 pivot;
        Vector3 homePos;
        Quaternion homeRot;
        Vector3 homePivot;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            pivot = pivotTarget != null ? pivotTarget.position : Vector3.zero;
            if (cam != null) { homePos = cam.transform.position; homeRot = cam.transform.rotation; }
            homePivot = pivot;
        }

        void Update()
        {
            if (cam == null) return;

            if (Input.GetKeyDown(KeyCode.R)) ResetView();

            // Never drive the camera from input that is interacting with the UI.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            bool explore = appState == null || appState.Current == Tool.Explore;

            if (Input.GetMouseButton(1) || (explore && Input.GetMouseButton(0)))
                Orbit();

            if (Input.GetMouseButton(2))
                Pan();

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
                Zoom(scroll);
        }

        void Orbit()
        {
            float mx = Input.GetAxis("Mouse X") * orbitSpeed;
            float my = Input.GetAxis("Mouse Y") * orbitSpeed;
            cam.transform.RotateAround(pivot, Vector3.up, mx);
            cam.transform.RotateAround(pivot, cam.transform.right, -my);
        }

        void Pan()
        {
            float dist = Vector3.Distance(cam.transform.position, pivot);
            // Scale pan by distance so it feels consistent at any zoom level.
            Vector3 move = (-cam.transform.right * Input.GetAxis("Mouse X")
                            - cam.transform.up * Input.GetAxis("Mouse Y")) * panSpeed * dist * 0.1f;
            cam.transform.position += move;
            pivot += move;
        }

        void Zoom(float scroll)
        {
            Vector3 dir = cam.transform.position - pivot;
            float newDist = Mathf.Max(minDistance, dir.magnitude * (1f - scroll * zoomSpeed));
            cam.transform.position = pivot + dir.normalized * newDist;
        }

        public void ResetView()
        {
            if (cam == null) return;
            cam.transform.position = homePos;
            cam.transform.rotation = homeRot;
            pivot = homePivot;
        }
    }
}
