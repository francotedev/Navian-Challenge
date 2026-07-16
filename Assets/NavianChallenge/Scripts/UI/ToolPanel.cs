using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// The tool selector: one button per <see cref="Tool"/>, wired to <see cref="AppState"/>.
    /// The active tool is highlighted; the panel re-reads state on every change so any system
    /// can switch tools and the UI stays in sync. Only Explore is functional in this
    /// foundation pass — the others are placeholders that select cleanly and will gain
    /// behaviour as their features land.
    /// </summary>
    public class ToolPanel : MonoBehaviour
    {
        AppState state;
        readonly Dictionary<Tool, Image> buttons = new();

        static readonly (Tool tool, string label)[] Tools =
        {
            (Tool.Explore,        "Explore"),
            (Tool.Slices,         "Slices"),
            (Tool.PlanTrajectory, "Plan trajectory"),
            (Tool.Measure,        "Measure"),
            (Tool.Craniotomy,     "Craniotomy"),
        };

        public void Build(Transform parent, AppState appState)
        {
            state = appState;

            RectTransform panel = UIFactory.Panel(parent, UITheme.PanelBg, "ToolPanel");
            UIFactory.VerticalLayout(panel.gameObject, pad: 12, spacing: 6);

            var header = UIFactory.Label(panel, "TOOLS", 15, UITheme.Accent);
            UIFactory.Size(header.gameObject, h: 22f);

            foreach (var (tool, label) in Tools)
            {
                var btn = UIFactory.Button(panel, label, out _);
                UIFactory.Size(btn.gameObject, h: 30f, flexW: 1f);
                buttons[tool] = btn.targetGraphic as Image;
                Tool captured = tool;
                btn.onClick.AddListener(() => state.SetTool(captured));
            }

            state.ToolChanged += HandleToolChanged;
            Refresh();
        }

        void HandleToolChanged(Tool _) => Refresh();

        void OnDestroy()
        {
            if (state != null) state.ToolChanged -= HandleToolChanged;
        }

        void Refresh()
        {
            foreach (var kv in buttons)
                kv.Value.color = kv.Key == state.Current ? UITheme.ButtonActive : UITheme.ButtonBg;
        }
    }
}
