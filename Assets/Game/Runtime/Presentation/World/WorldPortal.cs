using GreatVoyage.Application;
using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class WorldPortal : MonoBehaviour, IInteractionTarget
    {
        [SerializeField] private string portalId;
        [SerializeField] private WorldArea source;
        [SerializeField] private WorldArea destination;
        [SerializeField] private Transform arrival;
        [SerializeField] private string prompt;
        [SerializeField] private TravelKind kind;
        [SerializeField] private Facing facing = Facing.Front;
        [SerializeField] private float reach = .8f;
        public string TargetId => portalId;
        public WorldArea Destination => destination;
        public Transform Arrival => arrival;
        public TravelKind Kind => kind;
        public string SourceId => source.AreaId;

        public bool TryQuery(WorldPosition player, out InteractionSelection candidate)
        {
            candidate = default;
            if (this == null || !isActiveAndEnabled || source == null || destination == null || arrival == null
                || player.AreaId != source.AreaId) return false;
            var p = new Vector2(player.X, player.Y);
            var distance = (p - (Vector2)transform.position).sqrMagnitude;
            if (distance > reach * reach) return false;
            if (Physics2D.Linecast(p + Vector2.down * .2f, transform.position, LayerMask.GetMask("WorldSolid"))) return false;
            candidate = new InteractionSelection(portalId, prompt, 10, distance, InteractionAction.Travel);
            return true;
        }
        public TravelLink Link() => new TravelLink(portalId, source.AreaId,
            new WorldPosition(destination.AreaId, arrival.position.x, arrival.position.y), facing, kind);
    }
}
