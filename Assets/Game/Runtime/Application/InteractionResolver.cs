using System;
using System.Collections.Generic;
using GreatVoyage.Domain;

namespace GreatVoyage.Application
{
    public enum InteractionAction { OpenJournal, Travel, UseHelm }

    public readonly struct InteractionSelection
    {
        public string TargetId { get; }
        public string Prompt { get; }
        public int Priority { get; }
        public double DistanceSquared { get; }
        public InteractionAction Action { get; }
        public InteractionSelection(string targetId, string prompt, int priority, double distanceSquared, InteractionAction action)
        {
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(prompt) || distanceSquared < 0
                || double.IsNaN(distanceSquared) || double.IsInfinity(distanceSquared) || !Enum.IsDefined(typeof(InteractionAction), action))
                throw new ArgumentException("Invalid interaction candidate.");
            TargetId = targetId; Prompt = prompt; Priority = priority; DistanceSquared = distanceSquared; Action = action;
        }
    }

    public interface IInteractionTarget
    {
        string TargetId { get; }
        bool TryQuery(WorldPosition player, out InteractionSelection candidate);
    }

    public interface IInteractionQueries
    {
        InteractionSelection? Resolve(WorldPosition player, string retainedTargetId);
        bool IsStillValid(WorldPosition player, InteractionSelection selected);
    }

    public sealed class InteractionResolver : IInteractionQueries
    {
        private readonly IInteractionTarget[] targets;
        private readonly double switchMargin;
        public InteractionResolver(IEnumerable<IInteractionTarget> targets, double switchMargin = .12)
        {
            if (targets == null || double.IsNaN(switchMargin) || double.IsInfinity(switchMargin) || switchMargin < 0)
                throw new ArgumentException("Invalid interaction resolver.");
            var list = new List<IInteractionTarget>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in targets)
            {
                if (target == null || string.IsNullOrWhiteSpace(target.TargetId) || !ids.Add(target.TargetId))
                    throw new ArgumentException("Interaction target IDs must be unique.");
                list.Add(target);
            }
            this.targets = list.ToArray(); this.switchMargin = switchMargin;
        }

        public InteractionSelection? Resolve(WorldPosition player, string retainedTargetId)
        {
            InteractionSelection? best = null, retained = null;
            foreach (var target in targets)
            {
                if (!target.TryQuery(player, out var candidate)) continue;
                if (candidate.TargetId != target.TargetId) throw new InvalidOperationException("Candidate ID mismatch.");
                if (candidate.TargetId == retainedTargetId) retained = candidate;
                if (!best.HasValue || Better(candidate, best.Value)) best = candidate;
            }
            if (retained.HasValue && best.HasValue && retained.Value.Priority == best.Value.Priority
                && Math.Sqrt(retained.Value.DistanceSquared) <= Math.Sqrt(best.Value.DistanceSquared) + switchMargin)
                return retained;
            return best;
        }

        private static bool Better(InteractionSelection a, InteractionSelection b) => a.Priority > b.Priority
            || (a.Priority == b.Priority && (a.DistanceSquared < b.DistanceSquared
            || (a.DistanceSquared == b.DistanceSquared && string.CompareOrdinal(a.TargetId, b.TargetId) < 0)));

        public bool IsStillValid(WorldPosition player, InteractionSelection selected)
        {
            foreach (var target in targets)
                if (target.TargetId == selected.TargetId)
                    return target.TryQuery(player, out var current) && current.TargetId == selected.TargetId && current.Action == selected.Action;
            return false;
        }
    }
}
