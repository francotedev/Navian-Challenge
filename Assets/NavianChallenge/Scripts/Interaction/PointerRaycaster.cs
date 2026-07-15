using UnityEngine;
using UnityEngine.EventSystems;

namespace NavianChallenge
{
    /// <summary>
    /// Casts a ray from the camera through the mouse onto the atlas surface (mesh colliders)
    /// and drives a world-space reticle at the hit point. This gives the desktop build the
    /// "pointer into space" feel of the XR product, and is the single source of surface hits
    /// that the placement tools (target/entry, measure) will consume later.
    ///
    /// The ray is suppressed while the pointer is over a world-space panel, so hovering the UI
    /// doesn't paint a reticle on the anatomy behind it.
    /// </summary>
    public class PointerRaycaster : MonoBehaviour
    {
        public Camera cam;
        public LayerMask pickMask = ~0;
        public Transform reticle;
        public float maxDistance = 10000f;

        /// <summary>True when this frame's ray hit a pickable surface.</summary>
        public bool HasHit { get; private set; }
        public Vector3 HitPoint { get; private set; }
        public Vector3 HitNormal { get; private set; }
        public Collider HitCollider { get; private set; }

        /// <summary>The ray used this frame (valid even when nothing was hit).</summary>
        public Ray CurrentRay { get; private set; }

        void Start()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            HasHit = false;
            if (cam == null) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ShowReticle(false);
                return;
            }

            CurrentRay = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(CurrentRay, out RaycastHit hit, maxDistance, pickMask))
            {
                HasHit = true;
                HitPoint = hit.point;
                HitNormal = hit.normal;
                HitCollider = hit.collider;

                if (reticle != null)
                {
                    reticle.position = hit.point;
                    reticle.rotation = Quaternion.LookRotation(hit.normal);
                }
                ShowReticle(true);
            }
            else
            {
                ShowReticle(false);
            }
        }

        void ShowReticle(bool on)
        {
            if (reticle != null && reticle.gameObject.activeSelf != on)
                reticle.gameObject.SetActive(on);
        }
    }
}
