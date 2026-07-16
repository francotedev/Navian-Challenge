using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// The "CRANIOTOMY" panel section: a toggle that opens/closes the box cutout (by switching the
    /// active tool, so it stays in sync with the TOOLS selector), three position sliders that slide
    /// the window along the anatomical axes (L–R / S–I / A–P — the same axes the SECTION cut uses),
    /// and a size slider in millimetres. Drives <see cref="CraniotomyController"/>.
    /// </summary>
    public class CraniotomyPanel : MonoBehaviour
    {
        CraniotomyController crani;
        AppState state;
        Text toggleLabel, sizeValue;
        Image toggleBg;
        readonly Dictionary<CraniotomyController.Shape, Image> shapeButtons = new();

        public void Build(Transform parent, CraniotomyController controller, AppState appState)
        {
            crani = controller;
            state = appState;

            RectTransform panel = UIFactory.Panel(parent, UITheme.PanelBg, "CraniotomyPanel");
            UIFactory.VerticalLayout(panel.gameObject, pad: 12, spacing: 6);

            var header = UIFactory.Label(panel, "CRANIOTOMY (window)", 15, UITheme.Accent);
            UIFactory.Size(header.gameObject, h: 20f);

            var toggleBtn = UIFactory.Button(panel, "Craniotomy: off", out toggleLabel);
            UIFactory.Size(toggleBtn.gameObject, h: 26f, flexW: 1f);
            toggleBg = toggleBtn.targetGraphic as Image;
            toggleBtn.onClick.AddListener(() =>
            {
                if (state == null) return;
                bool on = state.Current == Tool.Craniotomy;
                state.SetTool(on ? Tool.Explore : Tool.Craniotomy);
            });

            // Window shape: rounded (a real craniotomy) or box.
            RectTransform shapeRow = UIFactory.Row(panel, spacing: 6f, height: 24f);
            foreach (var (s, label) in new[]
            {
                (CraniotomyController.Shape.Sphere, "Round"),
                (CraniotomyController.Shape.Box, "Box"),
            })
            {
                var btn = UIFactory.Button(shapeRow, label, out _);
                UIFactory.Size(btn.gameObject, h: 22f, flexW: 1f);
                shapeButtons[s] = btn.targetGraphic as Image;
                var captured = s;
                btn.onClick.AddListener(() => { if (crani != null) crani.SetShape(captured); RefreshShape(); });
            }

            // Position of the window centre along each anatomical axis.
            PosRow(panel, "L–R", 0, crani != null ? crani.CenterNorm.x : 0.5f);
            PosRow(panel, "S–I", 1, crani != null ? crani.CenterNorm.y : 0.8f);
            PosRow(panel, "A–P", 2, crani != null ? crani.CenterNorm.z : 0.5f);

            // Window size (mm), mapped from the slider's 0..1 range.
            RectTransform sizeRow = UIFactory.Row(panel, spacing: 8f, height: 20f);
            var sizeLabel = UIFactory.Label(sizeRow, "Size", 12, UITheme.TextPrimary);
            UIFactory.Size(sizeLabel.gameObject, w: 64f);
            float initMm = crani != null ? crani.SizeMm : 70f;
            var sizeSlider = UIFactory.Slider(sizeRow, Norm(initMm));
            UIFactory.Size(sizeSlider.gameObject, h: 16f, flexW: 1f);
            sizeValue = UIFactory.Label(sizeRow, $"{initMm:0} mm", 12, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIFactory.Size(sizeValue.gameObject, w: 52f);
            sizeSlider.onValueChanged.AddListener(v =>
            {
                float mm = Mathf.Lerp(CraniotomyController.SizeMinMm, CraniotomyController.SizeMaxMm, v);
                if (crani != null) crani.SetSizeMm(mm);
                if (sizeValue != null) sizeValue.text = $"{mm:0} mm";
            });

            if (state != null) state.ToolChanged += HandleToolChanged;
            RefreshToggle();
            RefreshShape();
        }

        static float Norm(float mm) =>
            Mathf.InverseLerp(CraniotomyController.SizeMinMm, CraniotomyController.SizeMaxMm, mm);

        void PosRow(Transform parent, string label, int axis, float initial)
        {
            RectTransform row = UIFactory.Row(parent, spacing: 8f, height: 20f);
            var l = UIFactory.Label(row, label, 12, UITheme.TextPrimary);
            UIFactory.Size(l.gameObject, w: 64f);
            var slider = UIFactory.Slider(row, initial);
            UIFactory.Size(slider.gameObject, h: 16f, flexW: 1f);
            int capturedAxis = axis;
            slider.onValueChanged.AddListener(v => { if (crani != null) crani.SetCenter(capturedAxis, v); });
        }

        void HandleToolChanged(Tool _) => RefreshToggle();

        void RefreshToggle()
        {
            bool on = state != null && state.Current == Tool.Craniotomy;
            if (toggleLabel != null) toggleLabel.text = on ? "Craniotomy: on" : "Craniotomy: off";
            if (toggleBg != null) toggleBg.color = on ? UITheme.ButtonActive : UITheme.ButtonBg;
        }

        void RefreshShape()
        {
            if (crani == null) return;
            foreach (var kv in shapeButtons)
                kv.Value.color = kv.Key == crani.CurrentShape ? UITheme.ButtonActive : UITheme.ButtonBg;
        }

        void OnDestroy()
        {
            if (state != null) state.ToolChanged -= HandleToolChanged;
        }
    }
}
