using UnityEngine;

namespace GreatVoyage.Presentation
{
    // Rect values are cell coordinates in the parent PlacementSpace, not separate grid origins.
    public sealed class GridArea : MonoBehaviour
    {
        [SerializeField] private string areaId;
        [SerializeField] private RectInt bounds = new RectInt(0, 0, 1, 1);
        private PlacementSpace registered;
        public string AreaId => areaId;
        public RectInt Bounds => bounds;
        public PlacementSpace Space => GetComponentInParent<PlacementSpace>(true);
        private void Notify()
        {
            if (registered != null) registered.Invalidate();
            registered = Space;
            if (registered != null) registered.Invalidate();
        }
        private void OnEnable() => Notify();
        private void OnDisable() => Notify();
        private void OnValidate() => Notify();
        private void OnTransformParentChanged() => Notify();
    }
}
