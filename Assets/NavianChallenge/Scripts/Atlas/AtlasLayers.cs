using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NavianChallenge
{
    /// <summary>The four segmentation structures shipped with the atlas.</summary>
    public enum AtlasLayer { Skin, GrayMatter, WhiteMatter, Veins }

    /// <summary>
    /// Runtime control of the atlas structures: per-layer opacity, visibility toggle, and
    /// "isolate" (fade everything except one). The meshes ship with opaque Standard
    /// materials; on registration each material instance is switched to Fade transparency so
    /// its alpha can be driven live. Render queues are ordered outer→last so the outer shell
    /// (skin) blends on top of the interior structures.
    ///
    /// Known limitation (documented for the README): alpha-blended overlapping meshes without
    /// depth write can show minor sorting artifacts on concave surfaces. Acceptable for a
    /// planning explorer; a depth-peeling / OIT pass would remove it.
    /// </summary>
    public class AtlasLayers : MonoBehaviour
    {
        class Entry
        {
            public Renderer renderer;
            public Material material; // per-renderer instance (never the shared asset)
            public float opacity = 1f; // user-chosen opacity, preserved across isolate/show-all
            public bool visible = true;
        }

        // Structures kept clearly readable when another layer is isolated.
        const float IsolatedDimAlpha = 0.06f;

        readonly Dictionary<AtlasLayer, Entry> layers = new();
        AtlasLayer? isolated;

        public IReadOnlyCollection<AtlasLayer> RegisteredLayers => layers.Keys;
        public bool Has(AtlasLayer layer) => layers.ContainsKey(layer);

        /// <summary>Register a structure's renderer and prepare its material for live opacity.</summary>
        public void Register(AtlasLayer layer, Renderer r)
        {
            if (r == null) return;
            Material mat = r.material; // instantiates a per-object copy; shared .mat asset stays untouched
            ConfigureMaterial(mat);
            AssignQueue(layer, mat);

            // Give each structure a legible default colour (veins red, etc.); the meshes ship
            // all-white, so this is what makes the anatomy readable. The user can recolour live.
            const float startAlpha = 1f;
            Color def = UITheme.LayerColor(layer);
            mat.color = new Color(def.r, def.g, def.b, startAlpha);

            var e = new Entry { renderer = r, material = mat, opacity = startAlpha, visible = r.enabled };
            layers[layer] = e;
            ApplyAlpha(e, startAlpha);
        }

        public float GetOpacity(AtlasLayer l) => layers.TryGetValue(l, out var e) ? e.opacity : 0f;
        public bool GetVisible(AtlasLayer l) => layers.TryGetValue(l, out var e) && e.visible;
        public bool IsIsolated(AtlasLayer l) => isolated == l;

        public Color GetColor(AtlasLayer l)
        {
            if (!layers.TryGetValue(l, out var e)) return Color.white;
            Color c = e.material.color; c.a = 1f; return c;
        }

        public void SetColor(AtlasLayer l, Color rgb)
        {
            if (!layers.TryGetValue(l, out var e)) return;
            Color c = e.material.color; // preserve current alpha (opacity)
            c.r = rgb.r; c.g = rgb.g; c.b = rgb.b;
            e.material.color = c;
        }

        public void SetOpacity(AtlasLayer l, float value)
        {
            if (!layers.TryGetValue(l, out var e)) return;
            e.opacity = Mathf.Clamp01(value);
            // Dragging a slider is an explicit intent, so it exits any isolate preset.
            isolated = null;
            if (e.visible) ApplyAlpha(e, e.opacity);
        }

        public void SetVisible(AtlasLayer l, bool visible)
        {
            if (!layers.TryGetValue(l, out var e)) return;
            e.visible = visible;
            e.renderer.enabled = visible;
        }

        /// <summary>Toggle a layer's isolate state: fade all others to a low alpha, or clear.</summary>
        public void ToggleIsolate(AtlasLayer focus)
        {
            if (isolated == focus) { ShowAll(); return; }
            isolated = focus;
            foreach (var kv in layers)
            {
                var e = kv.Value;
                if (kv.Key == focus)
                {
                    e.visible = true;
                    e.renderer.enabled = true;
                    ApplyAlpha(e, 1f);
                }
                else if (e.visible)
                {
                    ApplyAlpha(e, IsolatedDimAlpha);
                }
            }
        }

        /// <summary>Restore every layer to visible at its user-chosen opacity.</summary>
        public void ShowAll()
        {
            isolated = null;
            foreach (var e in layers.Values)
            {
                e.visible = true;
                e.renderer.enabled = true;
                ApplyAlpha(e, e.opacity);
            }
        }

        static void ApplyAlpha(Entry e, float a)
        {
            Color c = e.material.color;
            c.a = a;
            e.material.color = c;
        }

        static void AssignQueue(AtlasLayer layer, Material m)
        {
            // Interior structures draw first, skin last, so the shell blends over the inside.
            m.renderQueue = layer switch
            {
                AtlasLayer.Veins => 3000,
                AtlasLayer.WhiteMatter => 3010,
                AtlasLayer.GrayMatter => 3020,
                AtlasLayer.Skin => 3050,
                _ => 3000
            };
        }

        // Use our clippable surface shader so the meshes cut together with the MRI volume
        // (driven by global cut params from CrossSectionController). Alpha still drives opacity.
        // Falls back to Standard's Fade mode if the shader isn't found.
        static void ConfigureMaterial(Material m)
        {
            Shader clip = Shader.Find("Navian/ClippedSurface");
            if (clip != null) { m.shader = clip; return; }

            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }
}
