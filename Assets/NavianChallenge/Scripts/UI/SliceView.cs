using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NavianChallenge
{
    /// <summary>
    /// Turns a click or drag on a slice image into a normalized (u,v) pick in [0,1]², relative to
    /// the image rect — regardless of how the aspect-fitter sized it. Lives on the slice RawImage.
    /// </summary>
    public class SliceView : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public RectTransform area;
        public Action<Vector2> onPick;

        public void OnPointerDown(PointerEventData e) => Pick(e);
        public void OnDrag(PointerEventData e) => Pick(e);

        void Pick(PointerEventData e)
        {
            if (area == null || onPick == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out Vector2 lp))
                return;
            Rect r = area.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            float u = Mathf.Clamp01((lp.x - r.xMin) / r.width);
            float v = Mathf.Clamp01((lp.y - r.yMin) / r.height);
            onPick(new Vector2(u, v));
        }
    }
}
