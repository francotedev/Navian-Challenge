using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityVolumeRendering;
using RenderMode = UnityVolumeRendering.RenderMode; // disambiguate from UnityEngine.RenderMode (Canvas)

namespace NavianChallenge
{
    /// <summary>
    /// The "MRI VOLUME" panel section: visibility toggle, render-mode selector
    /// (DVR / MIP / Isosurface) and a window/level (min–max) pair. Drives
    /// <see cref="VolumeController"/>; re-syncs to it when the async volume finishes loading and
    /// after a mode change (which resets the window in UVR).
    /// </summary>
    public class MriPanel : MonoBehaviour
    {
        VolumeController vc;
        CrossSectionController cut;
        readonly Dictionary<RenderMode, Image> modeButtons = new();
        readonly Dictionary<CrossSectionController.CutAxis, Image> axisButtons = new();
        Slider minSlider, maxSlider, posSlider;
        Text minValue, maxValue, visLabel, cutLabel, posValue;
        Image visBg, cutBg;
        bool suppress;

        static readonly (RenderMode mode, string label)[] Modes =
        {
            (RenderMode.DirectVolumeRendering, "DVR"),
            (RenderMode.MaximumIntensityProjectipon, "MIP"),   // UVR's spelling
            (RenderMode.IsosurfaceRendering, "Iso"),
        };

        public void Build(Transform parent, VolumeController controller, CrossSectionController cutController)
        {
            vc = controller;
            cut = cutController;

            RectTransform panel = UIFactory.Panel(parent, UITheme.PanelBg, "MriPanel");
            UIFactory.VerticalLayout(panel.gameObject, pad: 12, spacing: 6);

            var header = UIFactory.Label(panel, "MRI VOLUME", 15, UITheme.Accent);
            UIFactory.Size(header.gameObject, h: 20f);

            var visBtn = UIFactory.Button(panel, "MRI: visible", out visLabel);
            UIFactory.Size(visBtn.gameObject, h: 26f, flexW: 1f);
            visBg = visBtn.targetGraphic as Image;
            visBtn.onClick.AddListener(() => { vc.SetVisible(!vc.Visible); RefreshVisibility(); });

            var modeLabel = UIFactory.Label(panel, "Render mode", 12, UITheme.TextMuted);
            UIFactory.Size(modeLabel.gameObject, h: 16f);
            RectTransform modeRow = UIFactory.Row(panel, spacing: 6f, height: 28f);
            foreach (var (m, label) in Modes)
            {
                var btn = UIFactory.Button(modeRow, label, out _);
                UIFactory.Size(btn.gameObject, h: 26f, flexW: 1f);
                modeButtons[m] = btn.targetGraphic as Image;
                RenderMode captured = m;
                btn.onClick.AddListener(() => { vc.SetRenderMode(captured); SyncWindow(); RefreshModes(); });
            }

            minSlider = WindowRow(panel, "Window min", 0f, out minValue);
            maxSlider = WindowRow(panel, "Window max", 1f, out maxValue);
            minSlider.onValueChanged.AddListener(_ => OnWindowChanged());
            maxSlider.onValueChanged.AddListener(_ => OnWindowChanged());

            BuildSection(panel);

            if (vc != null)
            {
                vc.OnReady += OnVolumeReady;
                if (vc.Ready) OnVolumeReady();
            }
            RefreshModes();
            RefreshVisibility();
        }

        // The "corte" — a movable clipping plane through the MRI (axial / coronal / sagittal).
        void BuildSection(Transform panel)
        {
            var header = UIFactory.Label(panel, "SECTION (cut)", 13, UITheme.Accent);
            UIFactory.Size(header.gameObject, h: 18f);

            var cutBtn = UIFactory.Button(panel, "Cut: off", out cutLabel);
            UIFactory.Size(cutBtn.gameObject, h: 24f, flexW: 1f);
            cutBg = cutBtn.targetGraphic as Image;
            cutBtn.onClick.AddListener(() => { cut.SetActive(!cut.Active); RefreshCut(); });

            RectTransform axisRow = UIFactory.Row(panel, spacing: 6f, height: 26f);
            foreach (var a in new[] { CrossSectionController.CutAxis.Axial,
                                      CrossSectionController.CutAxis.Coronal,
                                      CrossSectionController.CutAxis.Sagittal })
            {
                var btn = UIFactory.Button(axisRow, a.ToString(), out _);
                UIFactory.Size(btn.gameObject, h: 24f, flexW: 1f);
                axisButtons[a] = btn.targetGraphic as Image;
                CrossSectionController.CutAxis captured = a;
                btn.onClick.AddListener(() => { cut.SetAxis(captured); RefreshCut(); });
            }

            RectTransform posRow = UIFactory.Row(panel, spacing: 8f, height: 20f);
            var posLabel = UIFactory.Label(posRow, "Position", 12, UITheme.TextPrimary);
            UIFactory.Size(posLabel.gameObject, w: 70f);
            posSlider = UIFactory.Slider(posRow, cut != null ? cut.Position : 0.5f);
            UIFactory.Size(posSlider.gameObject, h: 16f, flexW: 1f);
            posValue = UIFactory.Label(posRow, "0.50", 12, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIFactory.Size(posValue.gameObject, w: 40f);
            posSlider.onValueChanged.AddListener(v =>
            {
                if (cut == null) return;
                cut.SetPosition(v);
                posValue.text = v.ToString("0.00");
            });

            RefreshCut();
        }

        void RefreshCut()
        {
            if (cut == null) return;
            if (cutLabel != null) cutLabel.text = cut.Active ? "Cut: on" : "Cut: off";
            if (cutBg != null) cutBg.color = cut.Active ? UITheme.ButtonActive : UITheme.ButtonBg;
            foreach (var kv in axisButtons)
                kv.Value.color = kv.Key == cut.Axis ? UITheme.ButtonActive : UITheme.ButtonBg;
        }

        Slider WindowRow(Transform parent, string label, float initial, out Text valueText)
        {
            RectTransform row = UIFactory.Row(parent, spacing: 8f, height: 20f);
            var l = UIFactory.Label(row, label, 12, UITheme.TextPrimary);
            UIFactory.Size(l.gameObject, w: 86f);
            Slider slider = UIFactory.Slider(row, initial);
            UIFactory.Size(slider.gameObject, h: 16f, flexW: 1f);
            valueText = UIFactory.Label(row, initial.ToString("0.00"), 12, UITheme.TextMuted, TextAnchor.MiddleRight);
            UIFactory.Size(valueText.gameObject, w: 40f);
            return slider;
        }

        void OnWindowChanged()
        {
            if (suppress) return;
            vc.SetWindow(minSlider.value, maxSlider.value);
            SyncWindow();
        }

        void OnVolumeReady()
        {
            SyncWindow();
            RefreshModes();
            RefreshVisibility();
        }

        void SyncWindow()
        {
            suppress = true;
            Vector2 w = vc.Window;
            minSlider.value = w.x;
            maxSlider.value = w.y;
            minValue.text = w.x.ToString("0.00");
            maxValue.text = w.y.ToString("0.00");
            suppress = false;
        }

        void RefreshModes()
        {
            foreach (var kv in modeButtons)
                kv.Value.color = kv.Key == vc.Mode ? UITheme.ButtonActive : UITheme.ButtonBg;
        }

        void RefreshVisibility()
        {
            if (visLabel != null) visLabel.text = vc.Visible ? "MRI: visible" : "MRI: hidden";
            if (visBg != null) visBg.color = vc.Visible ? UITheme.ButtonBg : UITheme.AccentDim;
        }

        void OnDestroy()
        {
            if (vc != null) vc.OnReady -= OnVolumeReady;
        }
    }
}
