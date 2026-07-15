using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// The "Structures" panel: one row per atlas layer with an opacity slider, a clickable
    /// colour swatch that toggles visibility, and an isolate button (fade everything else).
    /// A "Show all" button clears any isolate/hide state. All controls drive
    /// <see cref="AtlasLayers"/>.
    /// </summary>
    public class LayerPanel : MonoBehaviour
    {
        class Row
        {
            public AtlasLayer layer;
            public Slider slider;
            public Text value;
            public Image visSwatch; // colour = visible, dim = hidden
            public Image isoBg;
        }

        static readonly Color DimSwatch = new(0.22f, 0.26f, 0.30f, 1f);

        AtlasLayers layers;
        readonly List<Row> rows = new();
        bool suppress;

        // Manual colour editor (edits one layer at a time).
        AtlasLayer colorTarget = AtlasLayer.Veins;
        Slider rSlider, gSlider, bSlider;
        Text colorHeader;
        readonly Dictionary<AtlasLayer, Image> colorSelect = new();
        bool suppressColor;

        // Layout order: outer structure first, vessels last (they're the clinical focus).
        static readonly AtlasLayer[] Order =
        {
            AtlasLayer.Skin, AtlasLayer.GrayMatter, AtlasLayer.WhiteMatter, AtlasLayer.Veins
        };

        public void Build(Transform parent, AtlasLayers atlasLayers)
        {
            layers = atlasLayers;

            RectTransform panel = UIFactory.Panel(parent, UITheme.PanelBg, "LayerPanel");
            UIFactory.VerticalLayout(panel.gameObject, pad: 12, spacing: 8);

            Header(panel, "STRUCTURES");

            foreach (var layer in Order)
                if (layers.Has(layer))
                    BuildRow(panel, layer);

            Button(panel, "Show all", () => { layers.ShowAll(); RefreshAll(); });

            BuildColorEditor(panel);

            RefreshAll();
            RefreshColorEditor();
        }

        // Pick a structure, then drag R/G/B to recolour it live.
        void BuildColorEditor(Transform parent)
        {
            colorHeader = UIFactory.Label(parent, "COLOR", 13, UITheme.Accent);
            UIFactory.Size(colorHeader.gameObject, h: 18f);

            RectTransform selRow = UIFactory.Row(parent, spacing: 6f, height: 24f);
            foreach (var layer in Order)
            {
                if (!layers.Has(layer)) continue;
                Image sw = UIFactory.Swatch(selRow, layers.GetColor(layer), 22f);
                colorSelect[layer] = sw;
                var btn = sw.gameObject.AddComponent<Button>();
                btn.targetGraphic = sw;
                AtlasLayer captured = layer;
                btn.onClick.AddListener(() => { colorTarget = captured; RefreshColorEditor(); });
            }

            rSlider = ColorSlider(parent, "R", new Color(0.9f, 0.4f, 0.4f));
            gSlider = ColorSlider(parent, "G", new Color(0.4f, 0.85f, 0.45f));
            bSlider = ColorSlider(parent, "B", new Color(0.45f, 0.6f, 0.95f));
            rSlider.onValueChanged.AddListener(_ => ApplyColor());
            gSlider.onValueChanged.AddListener(_ => ApplyColor());
            bSlider.onValueChanged.AddListener(_ => ApplyColor());
        }

        Slider ColorSlider(Transform parent, string label, Color labelColor)
        {
            RectTransform row = UIFactory.Row(parent, spacing: 8f, height: 18f);
            var l = UIFactory.Label(row, label, 12, labelColor);
            UIFactory.Size(l.gameObject, w: 16f);
            Slider s = UIFactory.Slider(row, 1f);
            UIFactory.Size(s.gameObject, h: 14f, flexW: 1f);
            return s;
        }

        void ApplyColor()
        {
            if (suppressColor) return;
            var c = new Color(rSlider.value, gSlider.value, bSlider.value, 1f);
            layers.SetColor(colorTarget, c);
            if (colorSelect.TryGetValue(colorTarget, out var sw)) sw.color = c;
            RefreshRowSwatches();
        }

        void RefreshColorEditor()
        {
            if (rSlider == null) return;
            Color c = layers.GetColor(colorTarget);
            suppressColor = true;
            rSlider.value = c.r; gSlider.value = c.g; bSlider.value = c.b;
            suppressColor = false;

            if (colorHeader != null) colorHeader.text = "COLOR — " + UITheme.LayerName(colorTarget);
            foreach (var kv in colorSelect) kv.Value.color = layers.GetColor(kv.Key);
            RefreshRowSwatches();
        }

        void RefreshRowSwatches()
        {
            foreach (var row in rows)
                row.visSwatch.color = layers.GetVisible(row.layer) ? layers.GetColor(row.layer) : DimSwatch;
        }

        void Header(Transform parent, string text)
        {
            var t = UIFactory.Label(parent, text, 15, UITheme.Accent);
            UIFactory.Size(t.gameObject, h: 22f);
        }

        void BuildRow(Transform parent, AtlasLayer layer)
        {
            var row = new Row { layer = layer };

            var container = UIFactory.Panel(parent, new Color(1, 1, 1, 0.02f), "Row_" + layer);
            UIFactory.Size(container.gameObject, h: 46f, flexW: 1f);
            UIFactory.VerticalLayout(container.gameObject, pad: 4, spacing: 2);

            // Top line: [swatch/visibility] [name] [value%] [isolate]
            RectTransform top = UIFactory.Row(container, spacing: 8f, height: 20f);

            var swatchBtn = UIFactory.Swatch(top, UITheme.LayerColor(layer), 18f);
            row.visSwatch = swatchBtn;
            var visBtn = swatchBtn.gameObject.AddComponent<Button>();
            visBtn.targetGraphic = swatchBtn;
            visBtn.onClick.AddListener(() =>
            {
                bool now = !layers.GetVisible(layer);
                layers.SetVisible(layer, now);
                RefreshAll();
            });

            var name = UIFactory.Label(top, UITheme.LayerName(layer), 13, UITheme.TextPrimary);
            UIFactory.Size(name.gameObject, flexW: 1f);

            row.value = UIFactory.Label(top, "100%", 12, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIFactory.Size(row.value.gameObject, w: 42f);

            var isoBtn = UIFactory.Button(top, "iso", out _);
            row.isoBg = isoBtn.targetGraphic as Image;
            UIFactory.Size(isoBtn.gameObject, w: 40f);
            isoBtn.onClick.AddListener(() => { layers.ToggleIsolate(layer); RefreshAll(); });

            // Opacity slider
            var slider = UIFactory.Slider(container, layers.GetOpacity(layer));
            UIFactory.Size(slider.gameObject, h: 16f, flexW: 1f);
            row.slider = slider;
            slider.onValueChanged.AddListener(v =>
            {
                if (suppress) return;
                layers.SetOpacity(layer, v);
                RefreshAll();
            });

            rows.Add(row);
        }

        void Button(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var btn = UIFactory.Button(parent, text, out _);
            UIFactory.Size(btn.gameObject, h: 26f, flexW: 1f);
            btn.onClick.AddListener(onClick);
        }

        void RefreshAll()
        {
            suppress = true;
            foreach (var row in rows)
            {
                bool visible = layers.GetVisible(row.layer);
                row.visSwatch.color = visible ? layers.GetColor(row.layer) : DimSwatch;
                row.isoBg.color = layers.IsIsolated(row.layer) ? UITheme.ButtonActive : UITheme.ButtonBg;
                row.value.text = Mathf.RoundToInt(layers.GetOpacity(row.layer) * 100f) + "%";
                row.slider.value = layers.GetOpacity(row.layer);
            }
            suppress = false;
        }
    }
}
