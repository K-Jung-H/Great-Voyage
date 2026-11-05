using System;
using GreatVoyage.Domain;

namespace GreatVoyage.Application
{
    public enum TravelKind { Area, Floor, Board, Disembark }

    // Immutable authored link; no actor/time/state copy is owned by a portal view.
    public sealed class TravelLink
    {
        public string Id { get; }
        public string SourceArea { get; }
        public WorldPosition Destination { get; }
        public Facing Facing { get; }
        public TravelKind Kind { get; }
        public TravelLink(string id, string sourceArea, WorldPosition destination, Facing facing, TravelKind kind)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(sourceArea)
                || destination.AreaId == sourceArea || !Enum.IsDefined(typeof(Facing), facing)
                || !Enum.IsDefined(typeof(TravelKind), kind)) throw new ArgumentException("Invalid world link.");
            Id = id; SourceArea = sourceArea; Destination = destination; Facing = facing; Kind = kind;
        }
    }

    public interface ITravelMap
    {
        bool TryResolve(string portalId, WorldPosition source, out TravelLink link);
    }
}
