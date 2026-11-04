using System;
using GreatVoyage.Application;
using GreatVoyage.Domain;
using NUnit.Framework;

namespace GreatVoyage.Tests
{
    public sealed class InteractionTests
    {
        private sealed class Target : IInteractionTarget
        {
            public string TargetId { get; set; }
            public double Distance = 1;
            public int Priority;
            public bool Available = true;
            public bool TryQuery(WorldPosition p, out InteractionSelection selected)
            {
                selected = new InteractionSelection(TargetId, "Use " + TargetId, Priority, Distance * Distance, InteractionAction.OpenJournal);
                return Available;
            }
        }
        private sealed class Store : ISessionStore { public bool TrySave(GameSessionState s, out string e) { e = ""; return true; } }
        private static WorldPosition Position => new WorldPosition("captain-cabin", 0, 0);
        private static GameSessionState State() => new GameSessionState("test", 1,
            new CharacterState("player", Position, Facing.Front), new GameClock(28800));

        [Test] public void SelectionHasStablePriorityDistanceAndIdOrder()
        {
            var a = new Target { TargetId = "a" }; var b = new Target { TargetId = "b" };
            var resolver = new InteractionResolver(new[] { b, a });
            Assert.That(resolver.Resolve(Position, null).Value.TargetId, Is.EqualTo("a"));
            b.Distance = .5;
            Assert.That(resolver.Resolve(Position, null).Value.TargetId, Is.EqualTo("b"));
            a.Priority = 1;
            Assert.That(resolver.Resolve(Position, null).Value.TargetId, Is.EqualTo("a"));
            a.Available = false;
            Assert.That(resolver.Resolve(Position, null).Value.TargetId, Is.EqualTo("b"));
        }

        [Test] public void RetentionAvoidsJitterButYieldsToHigherPriorityOrClearDistanceAdvantage()
        {
            var a = new Target { TargetId = "a", Distance = .5 }; var b = new Target { TargetId = "b", Distance = .45 };
            var resolver = new InteractionResolver(new[] { a, b });
            Assert.That(resolver.Resolve(Position, "a").Value.TargetId, Is.EqualTo("a"));
            b.Distance = .2;
            Assert.That(resolver.Resolve(Position, "a").Value.TargetId, Is.EqualTo("b"));
            b.Distance = .48; b.Priority = 1;
            Assert.That(resolver.Resolve(Position, "a").Value.TargetId, Is.EqualTo("b"));
        }

        [Test] public void InputCannotExecuteADifferentTargetWhenHighlightedTargetBecomesInvalid()
        {
            var a = new Target { TargetId = "a", Distance = .4 }; var b = new Target { TargetId = "b", Distance = .6 };
            using (var session = new GameSession(State(), new InteractionResolver(new[] { a, b }), new SaveCoordinator(new Store())))
            {
                Assert.That(session.FocusedInteraction.Value.TargetId, Is.EqualTo("a"));
                a.Available = false;
                Assert.That(session.Interact(), Is.False);
                Assert.That(session.JournalOpen, Is.False);
                Assert.That(session.FocusedInteraction, Is.Null);
                session.Tick(0);
                Assert.That(session.FocusedInteraction.Value.TargetId, Is.EqualTo("b"));
                session.OpenMenu(); Assert.That(session.FocusedInteraction, Is.Null);
                session.CloseMenu(); Assert.That(session.FocusedInteraction.Value.TargetId, Is.EqualTo("b"));
                Assert.That(session.Interact(), Is.True);
                Assert.That(session.FocusedInteraction, Is.Null);
            }
        }

        [Test] public void DuplicateTargetIdsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new InteractionResolver(new[] { new Target { TargetId = "same" }, new Target { TargetId = "same" } }));
        }
    }
}
