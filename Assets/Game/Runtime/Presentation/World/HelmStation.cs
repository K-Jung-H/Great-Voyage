using GreatVoyage.Application;
using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class HelmStation : MonoBehaviour, IInteractionTarget
    {
        [SerializeField] private Transform approachPoint;
        [SerializeField] private float reach = .7f;
        private GameSession session;
        public string TargetId => "upper-deck:helm";
        public Transform ApproachPoint => approachPoint;
        public void Bind(GameSession value) => session = value;
        public bool TryQuery(WorldPosition player, out InteractionSelection candidate)
        {
            candidate = default;
            if (!isActiveAndEnabled || approachPoint == null || player.AreaId != "upper-deck") return false;
            var p = new Vector2(player.X, player.Y);
            var target = (Vector2)approachPoint.position;
            var distance = (p - target).sqrMagnitude;
            if (distance > reach * reach || Physics2D.Linecast(p + Vector2.down * .2f, target, LayerMask.GetMask("WorldSolid"))) return false;
            candidate = new InteractionSelection(TargetId, session != null && session.State.Navigation.IsUnderway
                ? "방향타 조작" : "출항하여 조타하기", 5, distance, InteractionAction.UseHelm);
            return true;
        }
    }
}
