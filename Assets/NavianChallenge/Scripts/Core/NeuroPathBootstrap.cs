using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// Single entry point that assembles the NeuroPath app on top of the provided base scene
    /// at runtime: it wires AppState, the atlas-layer controller, the camera interaction
    /// (orbit + raycast pointer), and the floating world-space panels. Everything is built in
    /// code and auto-found by name, so dropping this one component into the scene is the whole
    /// integration — no inspector wiring, nothing extra serialized into the scene file.
    ///
    /// The panels are world-space canvases head-locked to the camera (parented at a fixed
    /// offset in front of it). That keeps the XR "floating panel" look while staying readable
    /// and reachable no matter how the user orbits the anatomy.
    /// </summary>
    [DisallowMultipleComponent]
    public class NeuroPathBootstrap : MonoBehaviour
    {
        [Header("Auto-found if left empty")]
        public Transform atlasRoot;
        public Transform meshesRoot;
        public Camera camera;

        [Header("Panel placement")]
        [Tooltip("Panel distance in front of the camera, as a fraction of the camera→atlas distance.")]
        public float panelDepthFrac = 0.4f;
        [Range(0f, 0.2f)] public float edgeMargin = 0.05f;

        Bounds atlasBounds;

        void Start()
        {
            Resolve();
            if (camera == null) { Debug.LogError("[NeuroPath] No camera found; aborting setup."); return; }

            atlasBounds = ComputeAtlasBounds();

            EnsureEventSystem();
            DisableBaseController();

            AppState state = gameObject.AddComponent<AppState>();
            AtlasLayers layers = BuildLayers();
            var volume = gameObject.AddComponent<VolumeController>();
            volume.atlasRoot = atlasRoot;
            var cut = gameObject.AddComponent<CrossSectionController>();
            cut.volume = volume;

            SetupInteraction(state);
            ReadoutPanel readout = BuildUI(state, layers, volume, cut);
            SetupPlanning(state, readout);

            Debug.Log("[NeuroPath] Ready (MRI volume + layers + raycast UI + trajectory planner).");
        }

        // --- resolution ---

        void Resolve()
        {
            if (atlasRoot == null)
            {
                var go = GameObject.Find("AtlasRoot");
                if (go != null) atlasRoot = go.transform;
            }
            if (meshesRoot == null && atlasRoot != null)
                meshesRoot = atlasRoot.Find("MeshesRoot");
            if (camera == null) camera = Camera.main;
        }

        Bounds ComputeAtlasBounds()
        {
            var b = new Bounds(atlasRoot != null ? atlasRoot.position : Vector3.zero, Vector3.one);
            if (meshesRoot == null) return b;
            bool has = false;
            foreach (var r in meshesRoot.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // --- atlas layers ---

        AtlasLayers BuildLayers()
        {
            var layers = gameObject.AddComponent<AtlasLayers>();
            if (meshesRoot == null)
            {
                Debug.LogWarning("[NeuroPath] MeshesRoot not found; layer controls will be empty.");
                return layers;
            }
            // Colliders on Skin (pointer lands on the head surface) and Veins (needed by the
            // safety corridor later). Grey/White get colliders when their features arrive.
            RegisterLayer(layers, AtlasLayer.Skin, "Skin", addCollider: true);
            RegisterLayer(layers, AtlasLayer.GrayMatter, "GrayMatter", addCollider: false);
            RegisterLayer(layers, AtlasLayer.WhiteMatter, "WhiteMatter", addCollider: false);
            RegisterLayer(layers, AtlasLayer.Veins, "Veins", addCollider: true);
            return layers;
        }

        void RegisterLayer(AtlasLayers layers, AtlasLayer layer, string childName, bool addCollider)
        {
            Transform t = meshesRoot.Find(childName);
            if (t == null) { Debug.LogWarning($"[NeuroPath] Mesh '{childName}' not found."); return; }

            var r = t.GetComponent<Renderer>();
            if (r == null) { Debug.LogWarning($"[NeuroPath] '{childName}' has no Renderer."); return; }
            layers.Register(layer, r);

            if (addCollider && t.GetComponent<Collider>() == null)
                t.gameObject.AddComponent<MeshCollider>();
        }

        // --- interaction ---

        PointerRaycaster SetupInteraction(AppState state)
        {
            var orbit = camera.gameObject.AddComponent<CameraOrbit>();
            orbit.cam = camera;
            orbit.pivotTarget = atlasRoot;
            orbit.appState = state;

            Transform reticle = BuildReticle();
            var pointer = camera.gameObject.AddComponent<PointerRaycaster>();
            pointer.cam = camera;
            pointer.reticle = reticle;
            // Exclude the Ignore-Raycast layer so the reticle skips the planner's handle spheres.
            pointer.pickMask = Physics.DefaultRaycastLayers;
            return pointer;
        }

        Transform BuildReticle()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pointer Reticle";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // must not block the pointer ray

            float s = Mathf.Max(0.002f, atlasBounds.size.magnitude * 0.012f);
            go.transform.localScale = Vector3.one * s;

            var mr = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var m = new Material(shader) { color = UITheme.Accent };
                mr.sharedMaterial = m;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            go.SetActive(false);
            return go.transform;
        }

        // --- UI ---

        ReadoutPanel BuildUI(AppState state, AtlasLayers layers, VolumeController volume, CrossSectionController cut)
        {
            float camDist = Vector3.Distance(camera.transform.position, atlasBounds.center);
            float d = Mathf.Max(camera.nearClipPlane * 3f, camDist * panelDepthFrac);
            float aspect = camera.aspect > 0.01f ? camera.aspect : 16f / 9f;
            float halfH = d * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float halfW = halfH * aspect;

            // Left canvas: two stacked sections — MRI volume controls on top, structures below.
            Canvas left = MakePanel("Canvas_Visualization", new Vector2(360f, 800f), 0.97f, -1f, halfH, halfW, d);
            RectTransform sections = UIFactory.Panel(left.transform, new Color(0f, 0f, 0f, 0f), "Sections");
            UIFactory.Stretch(sections);
            var sectionsImg = sections.GetComponent<Image>();
            if (sectionsImg != null) sectionsImg.raycastTarget = false;
            UIFactory.VerticalLayout(sections.gameObject, pad: 0, spacing: 8);
            left.gameObject.AddComponent<MriPanel>().Build(sections, volume, cut);
            left.gameObject.AddComponent<LayerPanel>().Build(sections, layers);

            Canvas right = MakePanel("Canvas_Tools", new Vector2(300f, 300f), 0.52f, +1f, halfH, halfW, d);
            right.gameObject.AddComponent<ToolPanel>().Build(right.transform, state);

            // Bottom-centre readout for the trajectory planner.
            float rWorldH = halfH * 2f * 0.30f;
            Canvas bottom = UIFactory.WorldCanvas("Canvas_Readout", new Vector2(480f, 210f), rWorldH, camera);
            float ry = -(halfH - rWorldH * 0.5f - halfH * edgeMargin);
            bottom.transform.SetParent(camera.transform, worldPositionStays: false);
            bottom.transform.localPosition = new Vector3(0f, ry, d);
            bottom.transform.localRotation = Quaternion.identity;

            var readout = bottom.gameObject.AddComponent<ReadoutPanel>();
            readout.Build(bottom.transform, initialDepthMm: 40f, initialRadiusMm: 5f);
            return readout;
        }

        void SetupPlanning(AppState state, ReadoutPanel readout)
        {
            var corridor = gameObject.AddComponent<SafetyCorridor>();
            corridor.mmPerUnit = 1f;
            corridor.SetRadius(5f);
            corridor.veinsCollider = meshesRoot != null ? meshesRoot.Find("Veins")?.GetComponent<Collider>() : null;

            var planner = gameObject.AddComponent<TrajectoryPlanner>();
            planner.appState = state;
            planner.cam = camera;
            planner.readout = readout;
            planner.corridor = corridor;
            planner.atlasCenter = atlasBounds.center;
            planner.mmPerUnit = 1f; // scene maps the MRI's mm FOV to world units 1:1
            planner.surfaceMask = Physics.DefaultRaycastLayers;
            planner.handleMask = 1 << 2; // Ignore Raycast layer used for the handle spheres
            planner.handleRadiusWorld = Mathf.Max(0.5f, atlasBounds.size.magnitude * 0.016f);
            planner.lineColor = UITheme.Accent;
        }

        Canvas MakePanel(string name, Vector2 pixel, float heightFrac, float side,
                         float halfH, float halfW, float d)
        {
            float worldH = halfH * 2f * heightFrac;
            Canvas c = UIFactory.WorldCanvas(name, pixel, worldH, camera);

            float worldW = worldH * (pixel.x / pixel.y);
            float x = side * (halfW - worldW * 0.5f - halfW * edgeMargin);

            c.transform.SetParent(camera.transform, worldPositionStays: false);
            c.transform.localPosition = new Vector3(x, 0f, d);
            c.transform.localRotation = Quaternion.identity;
            return c;
        }

        // --- scene housekeeping ---

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        static void DisableBaseController()
        {
            // The base scene's placeholder camera helper (orbit + IMGUI help box) is superseded
            // by CameraOrbit; switch it off so the two don't both drive the camera.
            var baseCtrl = Object.FindFirstObjectByType<AtlasSceneController>();
            if (baseCtrl != null) baseCtrl.enabled = false;
        }
    }
}
