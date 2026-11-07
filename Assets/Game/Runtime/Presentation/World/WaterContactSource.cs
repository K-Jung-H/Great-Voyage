using System;
using UnityEngine;
namespace GreatVoyage.Presentation
{
    [DisallowMultipleComponent]
    public sealed class WaterContactSource : MonoBehaviour
    {
        [SerializeField] private ShipWaterSurface owner;
        [SerializeField] private Collider2D[] colliders = Array.Empty<Collider2D>();
        [SerializeField, Min(.001f)] private float minimumLineRadius = .05f;
        private bool locallyDisabled;
        public Collider2D[] Colliders => colliders;
        public float MinimumLineRadius => minimumLineRadius;
        public ShipWaterSurface Owner => owner;
        public void BindTo(ShipWaterSurface ship)
        {
            if (owner != ship)
            {
                if (owner != null)
                    owner.Unregister(this);
                owner = ship;
            }
            if (owner != null)
                owner.Register(this);
        }
        public void NotifyGeometryChanged()
        {
            if (owner != null)
                owner.NotifyGeometryChanged();
        }
        public void SetContactColliders(params Collider2D[] values)
        {
            colliders = values ?? Array.Empty<Collider2D>();
            NotifyGeometryChanged();
        }
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying)
                return;
            if (owner == null)
                BindTo(GetComponentInParent<ShipWaterSurface>(true));
            else
                owner.Register(this);
            if (locallyDisabled)
            {
                NotifyGeometryChanged();
                locallyDisabled = false;
            }
        }
        private void OnDisable()
        {
            if (UnityEngine.Application.isPlaying && owner != null && owner.gameObject.activeInHierarchy)
            {
                locallyDisabled = true;
                NotifyGeometryChanged();
            }
        }
        private void OnDestroy()
        {
            if (UnityEngine.Application.isPlaying && owner != null)
                owner.Unregister(this);
        }
        private void OnTransformParentChanged()
        {
            if (UnityEngine.Application.isPlaying)
                BindTo(GetComponentInParent<ShipWaterSurface>(true));
        }
    }
}
