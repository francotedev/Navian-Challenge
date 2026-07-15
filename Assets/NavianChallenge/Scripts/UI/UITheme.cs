using UnityEngine;

namespace NavianChallenge
{
    /// <summary>Shared colors, sizing and font for the floating XR-style panels, so every
    /// panel reads as one system.</summary>
    public static class UITheme
    {
        // Panel surfaces
        public static readonly Color PanelBg     = new(0.055f, 0.085f, 0.125f, 0.88f);
        public static readonly Color PanelBorder = new(0.20f, 0.42f, 0.55f, 0.9f);
        public static readonly Color HeaderBg    = new(0.09f, 0.16f, 0.22f, 0.95f);

        // Text
        public static readonly Color TextPrimary = new(0.92f, 0.96f, 0.98f, 1f);
        public static readonly Color TextMuted   = new(0.62f, 0.72f, 0.80f, 1f);

        // Accent (cyan) — sliders, active states
        public static readonly Color Accent      = new(0.24f, 0.78f, 0.95f, 1f);
        public static readonly Color AccentDim   = new(0.16f, 0.30f, 0.40f, 1f);

        // Controls
        public static readonly Color SliderBg    = new(0.14f, 0.20f, 0.26f, 1f);
        public static readonly Color Handle      = new(0.95f, 0.98f, 1f, 1f);
        public static readonly Color ButtonBg    = new(0.13f, 0.20f, 0.27f, 1f);
        public static readonly Color ButtonActive = new(0.24f, 0.78f, 0.95f, 1f);

        // Status (used later by the safety readout)
        public static readonly Color Safe   = new(0.30f, 0.85f, 0.45f, 1f);
        public static readonly Color Danger = new(0.95f, 0.32f, 0.30f, 1f);

        /// <summary>Per-structure swatch colors used on the layer rows.</summary>
        public static Color LayerColor(AtlasLayer l) => l switch
        {
            AtlasLayer.Skin        => new Color(0.93f, 0.78f, 0.66f, 1f), // skin tone
            AtlasLayer.GrayMatter  => new Color(0.62f, 0.60f, 0.72f, 1f), // muted blue-grey
            AtlasLayer.WhiteMatter => new Color(0.95f, 0.92f, 0.80f, 1f), // cream
            AtlasLayer.Veins       => new Color(0.86f, 0.20f, 0.22f, 1f), // red
            _ => Color.white
        };

        public static string LayerName(AtlasLayer l) => l switch
        {
            AtlasLayer.Skin        => "Skin",
            AtlasLayer.GrayMatter  => "Grey matter",
            AtlasLayer.WhiteMatter => "White matter",
            AtlasLayer.Veins       => "Veins",
            _ => l.ToString()
        };

        static Font cachedFont;

        /// <summary>The built-in legacy runtime font, with an OS-font fallback.</summary>
        public static Font Font
        {
            get
            {
                if (cachedFont != null) return cachedFont;
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null) cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return cachedFont;
            }
        }
    }
}
