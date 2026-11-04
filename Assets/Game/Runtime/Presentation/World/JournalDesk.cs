using GreatVoyage.Application;
using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class JournalDesk : MonoBehaviour, IInteractionTarget
    {
        [SerializeField] private Transform approachPoint;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField, Min(0.1f)] private float reach = 0.8f;
        [SerializeField] private string targetId = "navigation-desk-main";
        [SerializeField] private string prompt = "항해 일지 열기";
        [SerializeField] private int priority;
        public string TargetId => targetId;
        private readonly Collider2D[] overlaps = new Collider2D[16];

        public bool TryQuery(WorldPosition player, out InteractionSelection candidate)
        {
            candidate = default;
            if (this == null || !isActiveAndEnabled || player.AreaId != "captain-cabin" || approachPoint == null) return false;
            var source = new Vector2(player.X, player.Y);
            var target = (Vector2)approachPoint.position;
            if ((source - target).sqrMagnitude > reach * reach) return false;
            if (Physics2D.Linecast(source + Vector2.down * 0.2f, target, obstructionMask)) return false;
            candidate = new InteractionSelection(targetId, prompt, priority, (source - target).sqrMagnitude, InteractionAction.OpenJournal);
            return true;
        }

        public bool PlacementIsClear(WorldPosition player)
        {
            // A broad cabin envelope plus exact solid footprints permits valid wall-contact poses.
            if (player.AreaId != "captain-cabin" || Mathf.Abs(player.X) > 5.75f
                || player.Y < -2.95f || player.Y > 3.25f) return false;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstructionMask, useTriggers = false };
            return Physics2D.OverlapCircle(new Vector2(player.X, player.Y - 0.2f), 0.209f, filter, overlaps) == 0;
        }
    }
}
