using UnityEngine;

namespace NavianChallenge
{
    /// <summary>
    /// The clinical safety check: a cylinder of radius N mm around the trajectory that is tested
    /// against the veins mesh. Green = clear, red = the corridor crosses a vessel. This is what
    /// gives the veins object real purpose and mirrors commercial planning ("define safety
    /// corridors / avoid vascular structures").
    ///
    /// Detection walks the entry→target segment in overlapping sphere steps and asks whether each
    /// sphere overlaps the veins collider specifically (OverlapSphere works against non-convex
    /// mesh colliders). Sampling — not a true swept capsule — is the documented trade-off: simple,
    /// robust, and accurate enough at step = radius/2. It also ignores real vessel thickness and
    /// any clinical safety margin.
    /// </summary>
    public class SafetyCorridor : MonoBehaviour
    {
        public Collider veinsCollider;
        public float mmPerUnit = 1f;

        [Range(0.5f, 20f)] public float radiusMm = 5f;

        const float TubeAlpha = 0.26f;

        Transform tube;
        Material tubeMat;
        readonly Collider[] overlapBuffer = new Collider[16];

        public bool LastSafe { get; private set; } = true;
        public float RadiusMm => radiusMm;

        void Awake() => BuildTube();

        public void SetRadius(float mm) => radiusMm = Mathf.Max(0.5f, mm);

        public void Hide()
        {
            if (tube != null) tube.gameObject.SetActive(false);
        }

        /// <summary>Test the corridor along entry→target; update the tube; return true if clear.</summary>
        public bool Evaluate(Vector3 entry, Vector3 target)
        {
            bool safe = !Intersects(entry, target);
            LastSafe = safe;
            UpdateTube(entry, target, safe);
            return safe;
        }

        bool Intersects(Vector3 a, Vector3 b)
        {
            if (veinsCollider == null) return false;

            float radius = radiusMm / Mathf.Max(1e-4f, mmPerUnit);
            float length = Vector3.Distance(a, b);
            int samples = Mathf.Clamp(Mathf.CeilToInt(length / (radius * 0.5f)) + 1, 2, 512);

            for (int i = 0; i < samples; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)(samples - 1));
                int n = Physics.OverlapSphereNonAlloc(p, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < n; j++)
                    if (overlapBuffer[j] == veinsCollider) return true;
            }
            return false;
        }

        void BuildTube()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Safety Corridor";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // must not block picking

            tube = go.transform;
            var mr = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default"); // unlit, alpha-blended, tinted by _Color
            if (shader != null) { tubeMat = new Material(shader); mr.sharedMaterial = tubeMat; }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            go.SetActive(false);
        }

        void UpdateTube(Vector3 a, Vector3 b, bool safe)
        {
            if (tube == null) return;
            tube.gameObject.SetActive(true);

            Vector3 dir = b - a;
            float length = dir.magnitude;
            tube.position = (a + b) * 0.5f;
            tube.rotation = length > 1e-4f ? Quaternion.FromToRotation(Vector3.up, dir / length) : Quaternion.identity;

            // Unity's cylinder is 2 units tall with radius 0.5, so scale.y = length/2 and scale.xz = diameter.
            float diameter = (radiusMm / Mathf.Max(1e-4f, mmPerUnit)) * 2f;
            tube.localScale = new Vector3(diameter, length * 0.5f, diameter);

            if (tubeMat != null)
            {
                Color c = safe ? UITheme.Safe : UITheme.Danger;
                c.a = TubeAlpha;
                tubeMat.color = c;
            }
        }
    }
}
