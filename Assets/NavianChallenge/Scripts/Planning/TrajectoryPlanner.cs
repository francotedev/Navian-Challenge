using UnityEngine;
using UnityEngine.EventSystems;

namespace NavianChallenge
{
    /// <summary>
    /// The clinical core: place a surgical trajectory and read back its geometry.
    ///
    /// Flow (only while the Plan-trajectory tool is active):
    ///   click 1 → TARGET  (picked on the head surface, then pushed inward by the depth slider)
    ///   click 2 → ENTRY   (picked on the head surface)
    /// A line is drawn between them; either end can be re-dragged by its handle. The readout
    /// shows insertion length, approach angle (vs the entry surface normal) and coordinates.
    ///
    /// Simplifications (documented for the README): the target has no segmented lesion to snap
    /// to, so it is a surface pick pushed to an adjustable depth along the inward normal; picks
    /// are constrained to the skin mesh. Distances are millimetres because the scene maps the
    /// MRI's mm FOV to world units 1:1. Vein-collision safety feedback is the next feature.
    /// </summary>
    public class TrajectoryPlanner : MonoBehaviour
    {
        [Header("Wiring")]
        public AppState appState;
        public Camera cam;
        public ReadoutPanel readout;

        [Header("Space")]
        public Vector3 atlasCenter;
        public float mmPerUnit = 1f;

        [Header("Picking")]
        public LayerMask surfaceMask;  // skin / atlas surface for placing + dragging
        public LayerMask handleMask;   // handle spheres, for grabbing

        [Header("Safety")]
        public SafetyCorridor corridor;

        [Header("Look")]
        public float handleRadiusWorld = 5f;
        public Color lineColor = new(0.24f, 0.78f, 0.95f, 1f);

        enum Step { Target, Entry, Done }
        Step next = Step.Target;
        bool hasTarget, hasEntry;
        Vector3 targetSurface, targetNormal, entryPos, entryNormal;
        float targetDepthMm = 40f;

        LineRenderer line;
        Transform targetHandle, entryHandle, dragging;

        Vector3 TargetPos => targetSurface - targetNormal.normalized * (targetDepthMm / Mathf.Max(1e-4f, mmPerUnit));

        void Start()
        {
            if (cam == null) cam = Camera.main;
            BuildVisuals();

            if (readout != null)
            {
                readout.OnClear += Clear;
                readout.OnDepthChanged += SetDepth;
                readout.OnRadiusChanged += SetRadius;
            }
            if (appState != null) appState.ToolChanged += HandleToolChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (readout != null)
            {
                readout.OnClear -= Clear;
                readout.OnDepthChanged -= SetDepth;
                readout.OnRadiusChanged -= SetRadius;
            }
            if (appState != null) appState.ToolChanged -= HandleToolChanged;
        }

        void SetRadius(float mm) { if (corridor != null) corridor.SetRadius(mm); Refresh(); }

        void HandleToolChanged(Tool _) => Refresh();

        void Update()
        {
            if (appState == null || appState.Current != Tool.PlanTrajectory) { dragging = null; return; }
            if (cam == null) return;

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit handleHit, 1e6f, handleMask))
                    dragging = handleHit.transform;                       // grab an existing point
                else if (Physics.Raycast(ray, out RaycastHit surfHit, 1e6f, surfaceMask))
                    Place(surfHit.point, surfHit.normal);                 // place the next point
            }
            else if (Input.GetMouseButton(0) && dragging != null && !overUI)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit surfHit, 1e6f, surfaceMask))
                    Drag(surfHit.point, surfHit.normal);
            }

            if (Input.GetMouseButtonUp(0)) dragging = null;
        }

        void Place(Vector3 point, Vector3 normal)
        {
            if (next == Step.Target) { targetSurface = point; targetNormal = normal; hasTarget = true; next = Step.Entry; }
            else if (next == Step.Entry) { entryPos = point; entryNormal = normal; hasEntry = true; next = Step.Done; }
            Refresh();
        }

        void Drag(Vector3 point, Vector3 normal)
        {
            if (dragging == targetHandle) { targetSurface = point; targetNormal = normal; }
            else if (dragging == entryHandle) { entryPos = point; entryNormal = normal; }
            Refresh();
        }

        void SetDepth(float mm) { targetDepthMm = mm; Refresh(); }

        void Clear()
        {
            hasTarget = hasEntry = false;
            next = Step.Target;
            dragging = null;
            Refresh();
        }

        void Refresh()
        {
            Vector3 tp = TargetPos;

            if (targetHandle) { targetHandle.gameObject.SetActive(hasTarget); targetHandle.position = tp; }
            if (entryHandle) { entryHandle.gameObject.SetActive(hasEntry); entryHandle.position = entryPos; }

            bool both = hasTarget && hasEntry;
            if (line)
            {
                line.enabled = both;
                if (both) { line.SetPosition(0, entryPos); line.SetPosition(1, tp); }
            }

            UpdateReadout(tp, both);
        }

        void UpdateReadout(Vector3 tp, bool both)
        {
            if (readout == null) return;

            if (both)
            {
                float len = Vector3.Distance(entryPos, tp) * mmPerUnit;
                float ang = Vector3.Angle(tp - entryPos, -entryNormal);
                readout.SetMetrics(true, len, ang, (tp - atlasCenter) * mmPerUnit, (entryPos - atlasCenter) * mmPerUnit);

                // Safety corridor: test the tube entry→target against the veins mesh.
                bool safe = corridor == null || corridor.Evaluate(entryPos, tp);
                readout.SetSafety(true, safe);
                readout.SetStatus("Trajectory set — drag handles, or adjust depth / corridor radius.", UITheme.TextMuted);
                return;
            }

            if (corridor != null) corridor.Hide();
            readout.SetSafety(false, false);
            readout.SetMetrics(false, 0, 0, default, default);
            bool planning = appState != null && appState.Current == Tool.PlanTrajectory;
            if (planning && next == Step.Target) readout.SetStatus("Click on the head to place the TARGET.", UITheme.Accent);
            else if (planning && next == Step.Entry) readout.SetStatus("Now click to place the ENTRY point.", UITheme.Accent);
            else readout.SetStatus("Pick the 'Plan trajectory' tool, then click to place points.", UITheme.TextMuted);
        }

        // --- visuals ---

        void BuildVisuals()
        {
            targetHandle = MakeHandle("Target Handle", new Color(0.95f, 0.30f, 0.30f));
            entryHandle = MakeHandle("Entry Handle", new Color(0.35f, 0.85f, 0.45f));

            var go = new GameObject("Trajectory Line");
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = Mathf.Max(0.001f, handleRadiusWorld * 0.5f);
            line.numCapVertices = 4;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader);
            line.startColor = line.endColor = lineColor;
            line.enabled = false;
        }

        Transform MakeHandle(string name, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.layer = 2; // Ignore Raycast: kept off the pointer reticle; grabbed via handleMask
            go.transform.localScale = Vector3.one * (handleRadiusWorld * 2f);

            var mr = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Color");
            if (shader != null) mr.sharedMaterial = new Material(shader) { color = color };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            go.SetActive(false);
            return go.transform;
        }
    }
}
