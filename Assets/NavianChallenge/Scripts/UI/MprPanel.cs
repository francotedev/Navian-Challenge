using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// The MPR slice panel: a bottom strip of three grayscale views (Axial / Coronal / Sagittal),
    /// each aspect-fitted and overlaid with a crosshair you can click or drag to move the shared
    /// focus point. Reads slice textures and the crosshair position from <see cref="MprController"/>
    /// and pushes picks back to it; the controller toggles this canvas with the Slices tool.
    /// </summary>
    public class MprPanel : MonoBehaviour
    {
        class Cell
        {
            public MprController.Plane plane;
            public RawImage image;
            public AspectRatioFitter fitter;
            public RectTransform vLine, hLine;
        }

        MprController mpr;
        readonly List<Cell> cells = new();

        public void Build(Transform parent, MprController controller, Camera camera)
        {
            mpr = controller;

            RectTransform panel = UIFactory.Panel(parent, UITheme.PanelBg, "MprPanel");
            UIFactory.Stretch(panel);
            UIFactory.VerticalLayout(panel.gameObject, pad: 10, spacing: 4);

            var header = UIFactory.Label(panel, "MPR — click a slice to move the crosshair", 13, UITheme.Accent);
            UIFactory.Size(header.gameObject, h: 18f);

            RectTransform row = UIFactory.Row(panel, spacing: 8f, height: 10f);
            var rowLE = row.GetComponent<LayoutElement>();
            if (rowLE != null) rowLE.flexibleHeight = 1f;

            cells.Add(BuildCell(row, MprController.Plane.Axial, "Axial"));
            cells.Add(BuildCell(row, MprController.Plane.Coronal, "Coronal"));
            cells.Add(BuildCell(row, MprController.Plane.Sagittal, "Sagittal"));

            if (mpr != null) mpr.Changed += Refresh;
            Refresh();
        }

        Cell BuildCell(Transform rowParent, MprController.Plane plane, string label)
        {
            var cellGO = new GameObject($"Cell_{label}", typeof(RectTransform));
            cellGO.transform.SetParent(rowParent, false);
            UIFactory.VerticalLayout(cellGO, pad: 0, spacing: 2);
            UIFactory.Size(cellGO, flexW: 1f);

            var cellLabel = UIFactory.Label(cellGO.transform, label, 12, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIFactory.Size(cellLabel.gameObject, h: 16f);

            // Image area: fills the remaining cell height; a dark backdrop behind the slice.
            var areaGO = new GameObject("Area", typeof(RectTransform), typeof(Image));
            areaGO.transform.SetParent(cellGO.transform, false);
            var areaImg = areaGO.GetComponent<Image>();
            areaImg.color = new Color(0f, 0f, 0f, 0.55f);
            areaImg.raycastTarget = false;
            var areaLE = areaGO.AddComponent<LayoutElement>();
            areaLE.flexibleHeight = 1f;
            areaLE.flexibleWidth = 1f;

            // The slice image itself: centered, aspect-fitted within the area (no layout group here,
            // so the fitter owns its size). Crosshair lines are children so they track the fit.
            var imgGO = new GameObject("Slice", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            imgGO.transform.SetParent(areaGO.transform, false);
            var rawImg = imgGO.GetComponent<RawImage>();
            var rt = (RectTransform)imgGO.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var fitter = imgGO.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            RectTransform vLine = MakeLine(rt, true);
            RectTransform hLine = MakeLine(rt, false);

            var view = imgGO.AddComponent<SliceView>();
            view.area = rt;
            MprController.Plane captured = plane;
            view.onPick = uv => { if (mpr != null) mpr.Pick(captured, uv.x, uv.y); };

            return new Cell { plane = plane, image = rawImg, fitter = fitter, vLine = vLine, hLine = hLine };
        }

        static RectTransform MakeLine(RectTransform parent, bool vertical)
        {
            var go = new GameObject(vertical ? "VLine" : "HLine", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.8f);
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = vertical ? new Vector2(1.5f, 0f) : new Vector2(0f, 1.5f);
            return rt;
        }

        void Refresh()
        {
            if (mpr == null) return;
            Vector3 c = mpr.Cross;
            foreach (var cell in cells)
            {
                Texture2D tex = mpr.GetTexture(cell.plane);
                if (tex != null)
                {
                    cell.image.texture = tex;
                    cell.fitter.aspectRatio = (float)tex.width / tex.height;
                }
                float u, v;
                switch (cell.plane)
                {
                    case MprController.Plane.Axial:   u = c.x; v = c.z; break;
                    case MprController.Plane.Coronal: u = c.x; v = c.y; break;
                    default:                          u = c.z; v = c.y; break; // Sagittal
                }
                Place(cell.vLine, true, u);
                Place(cell.hLine, false, v);
            }
        }

        static void Place(RectTransform line, bool vertical, float t)
        {
            if (vertical) { line.anchorMin = new Vector2(t, 0f); line.anchorMax = new Vector2(t, 1f); }
            else          { line.anchorMin = new Vector2(0f, t); line.anchorMax = new Vector2(1f, t); }
            line.anchoredPosition = Vector2.zero;
        }

        void OnDestroy()
        {
            if (mpr != null) mpr.Changed -= Refresh;
        }
    }
}
