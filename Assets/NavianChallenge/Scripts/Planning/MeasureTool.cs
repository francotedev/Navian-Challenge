using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// A two-point ruler. In Measure mode, click to drop point A then point B on the anatomy
    /// surface; a line connects them and a billboarded label shows the straight-line distance in
    /// millimetres (the scene maps the MRI's mm FOV to world units 1:1). Either end can be
    /// re-dragged by its handle; a further click starts a fresh measurement.
    ///
    /// Mirrors <see cref="TrajectoryPlanner"/>'s pick/drag model so the two clinical tools feel the
    /// same. Only acts while the Measure tool is active — and since <see cref="CameraOrbit"/> only
    /// takes the left button in Explore mode, measuring never spins the camera (right-drag still
    /// orbits, so you can look around mid-measurement).
    /// </summary>
    public class MeasureTool : MonoBehaviour
    {
        [Header("Wiring")]
        public AppState appState;
        public Camera cam;
        public float mmPerUnit = 1f;

        [Header("Picking")]
        public LayerMask surfaceMask; // anatomy surface for placing + dragging
        public LayerMask handleMask;  // handle spheres, for grabbing

        [Header("Look")]
        public float handleRadiusWorld = 5f;
        public Color lineColor = new(0.24f, 0.78f, 0.95f, 1f);

        enum Step { A, B }
        Step next = Step.A;
        bool hasA, hasB;
        Vector3 a, b;

        LineRenderer line;
        Transform handleA, handleB, dragging;
        Transform labelPivot;
        Text labelText;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            BuildVisuals();
            if (appState != null) appState.ToolChanged += HandleToolChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (appState != null) appState.ToolChanged -= HandleToolChanged;
        }

        void HandleToolChanged(Tool _) => Refresh();

        void Update()
        {
            if (appState == null || appState.Current != Tool.Measure) { dragging = null; return; }
            if (cam == null) return;

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                // Only grab our own handles; ignore other tools' handles on the same layer so a
                // click there still places a point on the surface behind them.
                if (Physics.Raycast(ray, out RaycastHit handleHit, 1e6f, handleMask)
                    && (handleHit.transform == handleA || handleHit.transform == handleB))
                    dragging = handleHit.transform;
                else if (Physics.Raycast(ray, out RaycastHit surfHit, 1e6f, surfaceMask))
                    Place(surfHit.point);
            }
            else if (Input.GetMouseButton(0) && dragging != null && !overUI)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit surfHit, 1e6f, surfaceMask))
                    Drag(surfHit.point);
            }

            if (Input.GetMouseButtonUp(0)) dragging = null;

            BillboardLabel();
        }

        void Place(Vector3 p)
        {
            if (next == Step.A) { a = p; hasA = true; hasB = false; next = Step.B; } // start fresh
            else { b = p; hasB = true; next = Step.A; }
            Refresh();
        }

        void Drag(Vector3 p)
        {
            if (dragging == handleA) a = p;
            else if (dragging == handleB) b = p;
            Refresh();
        }

        void Refresh()
        {
            if (handleA) { handleA.gameObject.SetActive(hasA); handleA.position = a; }
            if (handleB) { handleB.gameObject.SetActive(hasB); handleB.position = b; }

            bool both = hasA && hasB;
            if (line)
            {
                line.enabled = both;
                if (both) { line.SetPosition(0, a); line.SetPosition(1, b); }
            }

            if (labelPivot) labelPivot.gameObject.SetActive(both);
            if (both && labelText != null)
                labelText.text = $"{Vector3.Distance(a, b) * mmPerUnit:0.0} mm";
            BillboardLabel();
        }

        void BillboardLabel()
        {
            if (labelPivot == null || !labelPivot.gameObject.activeSelf || cam == null) return;
            labelPivot.position = (a + b) * 0.5f;
            labelPivot.rotation = cam.transform.rotation; // screen-aligned, always readable
        }

        // --- visuals ---

        void BuildVisuals()
        {
            handleA = MakeHandle("Measure A", new Color(0.24f, 0.78f, 0.95f));
            handleB = MakeHandle("Measure B", new Color(0.95f, 0.85f, 0.35f));

            var go = new GameObject("Measure Line");
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = Mathf.Max(0.001f, handleRadiusWorld * 0.4f);
            line.numCapVertices = 4;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader);
            line.startColor = line.endColor = lineColor;
            line.enabled = false;

            BuildLabel();
        }

        void BuildLabel()
        {
            float worldH = Mathf.Max(1f, handleRadiusWorld * 3f);
            Canvas c = UIFactory.WorldCanvas("Measure Label", new Vector2(240f, 76f), worldH, cam);
            labelPivot = c.transform;

            // The label is a passive readout — it must never intercept the measuring clicks.
            var gr = c.GetComponent<GraphicRaycaster>();
            if (gr != null) Destroy(gr);

            RectTransform bg = UIFactory.Panel(c.transform, UITheme.PanelBg, "Bg");
            UIFactory.Stretch(bg);
            var bgImg = bg.GetComponent<Image>();
            if (bgImg != null) bgImg.raycastTarget = false;

            labelText = UIFactory.Label(bg, "0.0 mm", 30, UITheme.Accent, TextAnchor.MiddleCenter);
            labelText.raycastTarget = false;
            UIFactory.Stretch(labelText.rectTransform);

            c.gameObject.SetActive(false);
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
