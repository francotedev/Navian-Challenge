using UnityEngine;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// The floating readout for the trajectory planner: live status, insertion length, approach
    /// angle, target/entry coordinates (mm from the atlas centre), a target-depth slider and a
    /// Clear button. Values are pushed in by <see cref="TrajectoryPlanner"/>; the slider and
    /// button raise events back to it.
    /// </summary>
    public class ReadoutPanel : MonoBehaviour
    {
        public System.Action OnClear;
        public System.Action<float> OnDepthChanged;  // millimetres
        public System.Action<float> OnRadiusChanged; // millimetres
        public float depthMaxMm = 100f;
        public float radiusMaxMm = 15f;

        Text status, safety, length, angle, targetCoord, entryCoord, depthValue, radiusValue;
        Slider depthSlider, radiusSlider;

        public void Build(Transform parent, float initialDepthMm, float initialRadiusMm)
        {
            RectTransform panel = UIFactory.Panel(parent, UITheme.PanelBg, "ReadoutPanel");
            UIFactory.Stretch(panel);
            UIFactory.VerticalLayout(panel.gameObject, pad: 12, spacing: 5);

            var header = UIFactory.Label(panel, "TRAJECTORY", 15, UITheme.Accent);
            UIFactory.Size(header.gameObject, h: 20f);

            status = UIFactory.Label(panel, "", 12, UITheme.TextMuted);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Size(status.gameObject, h: 18f, flexW: 1f);

            // Safety-corridor verdict — the headline clinical feedback.
            safety = UIFactory.Label(panel, "", 14, UITheme.Safe);
            safety.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Size(safety.gameObject, h: 20f, flexW: 1f);

            length = MetricRow(panel, "Insertion length");
            angle = MetricRow(panel, "Approach angle (from normal)");
            targetCoord = MetricRow(panel, "Target  (mm)");
            entryCoord = MetricRow(panel, "Entry  (mm)");

            BuildSlider(panel, "Target depth", initialDepthMm, depthMaxMm, out depthSlider, out depthValue,
                        mm => OnDepthChanged?.Invoke(mm));
            BuildSlider(panel, "Corridor radius", initialRadiusMm, radiusMaxMm, out radiusSlider, out radiusValue,
                        mm => OnRadiusChanged?.Invoke(mm));

            var clear = UIFactory.Button(panel, "Clear trajectory", out _);
            UIFactory.Size(clear.gameObject, h: 26f, flexW: 1f);
            clear.onClick.AddListener(() => OnClear?.Invoke());

            SetMetrics(false, 0, 0, default, default);
            SetSafety(false, false);
        }

        void BuildSlider(Transform parent, string label, float initialMm, float maxMm,
                         out Slider slider, out Text value, System.Action<float> onChanged)
        {
            RectTransform row = UIFactory.Row(parent, spacing: 8f, height: 20f);

            var l = UIFactory.Label(row, label, 12, UITheme.TextPrimary);
            UIFactory.Size(l.gameObject, w: 96f);

            Slider s = UIFactory.Slider(row, Mathf.Clamp01(initialMm / maxMm));
            UIFactory.Size(s.gameObject, h: 16f, flexW: 1f);

            var v = UIFactory.Label(row, Mathf.RoundToInt(initialMm) + " mm", 12,
                                    UITheme.TextMuted, TextAnchor.MiddleRight);
            UIFactory.Size(v.gameObject, w: 54f);

            Text valueRef = v;
            s.onValueChanged.AddListener(t =>
            {
                float mm = t * maxMm;
                valueRef.text = Mathf.RoundToInt(mm) + " mm";
                onChanged?.Invoke(mm);
            });

            slider = s;
            value = v;
        }

        public void SetSafety(bool ready, bool safe)
        {
            if (safety == null) return;
            if (!ready)
            {
                safety.text = "";
                return;
            }
            safety.text = safe ? "CLEAR — corridor avoids vessels"
                               : "WARNING — trajectory crosses a vessel";
            safety.color = safe ? UITheme.Safe : UITheme.Danger;
        }

        Text MetricRow(Transform parent, string label)
        {
            RectTransform row = UIFactory.Row(parent, spacing: 8f, height: 18f);
            var l = UIFactory.Label(row, label, 12, UITheme.TextMuted);
            UIFactory.Size(l.gameObject, w: 150f);
            var value = UIFactory.Label(row, "—", 12, UITheme.TextPrimary, TextAnchor.MiddleRight);
            UIFactory.Size(value.gameObject, flexW: 1f);
            return value;
        }

        public void SetStatus(string message, Color color)
        {
            if (status == null) return;
            status.text = message;
            status.color = color;
        }

        public void SetMetrics(bool ready, float lengthMm, float angleDeg, Vector3 targetMm, Vector3 entryMm)
        {
            if (length == null) return;
            if (!ready)
            {
                length.text = angle.text = targetCoord.text = entryCoord.text = "—";
                return;
            }
            length.text = lengthMm.ToString("0") + " mm";
            angle.text = angleDeg.ToString("0") + "°";
            targetCoord.text = Fmt(targetMm);
            entryCoord.text = Fmt(entryMm);
        }

        static string Fmt(Vector3 mm) => $"({mm.x:0}, {mm.y:0}, {mm.z:0})";
    }
}
