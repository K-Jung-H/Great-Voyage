using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class WorldArea : MonoBehaviour
    {
        [SerializeField] private string areaId;
        [SerializeField] private string displayName;
        [SerializeField] private Rect cameraBounds = new Rect(-6.1f, -3.5f, 12.2f, 7);
        [SerializeField] private Rect[] walkableRegions = { new Rect(-5.75f, -2.95f, 11.5f, 6.2f) };
        [SerializeField] private Sprite transitionIllustration;
        public string AreaId => areaId;
        public string DisplayName => displayName;
        public Rect CameraBounds => cameraBounds;
        public Sprite Illustration => transitionIllustration;
        private readonly Collider2D[] overlaps = new Collider2D[16];

        // Called with this area's colliders active and all other areas disabled, behind the transition cover.
        public bool PlacementIsClear(WorldPosition position)
        {
            if (position.AreaId != areaId || !cameraBounds.Contains(new Vector2(position.X, position.Y))) return false;
            var inside = false;
            foreach (var region in walkableRegions) if (region.Contains(new Vector2(position.X, position.Y))) { inside = true; break; }
            if (!inside) return false;
            var filter = new ContactFilter2D { useLayerMask = true,
                layerMask = LayerMask.GetMask("WorldSolid"), useTriggers = false };
            return Physics2D.OverlapCircle(new Vector2(position.X, position.Y - .2f), .209f, filter, overlaps) == 0;
        }
    }
}
