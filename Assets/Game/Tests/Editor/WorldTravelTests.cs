using GreatVoyage.Application;
using GreatVoyage.Domain;
using NUnit.Framework;

namespace GreatVoyage.Tests
{
    public sealed class WorldTravelTests
    {
        private sealed class Queries : IInteractionQueries, ITravelMap
        {
            public TravelKind Kind;
            public bool Valid = true;
            public InteractionSelection? Resolve(WorldPosition p, string retained) => p.AreaId == "a" && Valid
                ? new InteractionSelection("a-b", "이동", 1, 0, InteractionAction.Travel) : (InteractionSelection?)null;
            public bool IsStillValid(WorldPosition p, InteractionSelection s) => Valid && p.AreaId == "a";
            public bool TryResolve(string id, WorldPosition p, out TravelLink link)
            { link = new TravelLink("a-b", "a", new WorldPosition("b", 2, 3), Facing.Left, Kind); return Valid; }
        }
        private sealed class Store : ISessionStore
        {
            public int Writes; public bool Success = true; public string Area;
            public bool TrySave(GameSessionState s, out string error)
            { Writes++; Area = s.Player.Position.AreaId; error = Success ? "" : "failure"; return Success; }
        }
        private static GameSession Create(Queries queries, Store store) => new GameSession(
            new GameSessionState("same-play", 17, new CharacterState("player", new WorldPosition("a", 0, 0), Facing.Front),
                new GameClock(28800)), queries, new SaveCoordinator(store), queries);

        [Test] public void TransitionFreezesClockAndRejectsRepeatedInputOrExit()
        {
            var q = new Queries(); var store = new Store();
            using (var session = Create(q, store))
            {
                Assert.That(session.Interact(), Is.True); session.Tick(2); session.Move(1, 0);
                Assert.That(session.State.Clock.TotalGameSeconds, Is.EqualTo(28800));
                Assert.That(session.Movement.X, Is.Zero); Assert.That(session.Interact(), Is.False);
                Assert.That(session.PrepareNormalExit(), Is.False); session.Back();
                Assert.That(session.MenuOpen, Is.False); Assert.That(store.Writes, Is.Zero);
            }
        }
        [Test] public void FailedPreparationKeepsPoseIdentityAndDoesNotSave()
        {
            var q = new Queries { Kind = TravelKind.Disembark }; var store = new Store();
            using (var session = Create(q, store))
            {
                session.Interact(); var request = session.PendingTravel;
                Assert.That(session.CompleteTravel(request, false), Is.False);
                Assert.That(session.State.Player.Position.AreaId, Is.EqualTo("a"));
                Assert.That(session.State.PlayId, Is.EqualTo("same-play"));
                Assert.That(session.MenuOpen, Is.True); Assert.That(store.Writes, Is.Zero);
                Assert.That(session.CompleteTravel(request, true), Is.False);
            }
        }
        [TestCase(TravelKind.Area, 0)] [TestCase(TravelKind.Floor, 0)]
        [TestCase(TravelKind.Board, 1)] [TestCase(TravelKind.Disembark, 1)]
        public void SuccessfulTransitionCommitsOnceAndOnlyBoardingBoundariesSave(TravelKind kind, int writes)
        {
            var q = new Queries { Kind = kind }; var store = new Store();
            using (var session = Create(q, store))
            {
                var player = session.State.Player; session.Interact(); var request = session.PendingTravel;
                Assert.That(session.CompleteTravel(request, true), Is.True);
                Assert.That(session.State.Player, Is.SameAs(player));
                Assert.That(player.Position.AreaId, Is.EqualTo("b")); Assert.That(player.Facing, Is.EqualTo(Facing.Left));
                Assert.That(session.Pauses.Count, Is.Zero); Assert.That(store.Writes, Is.EqualTo(writes));
                Assert.That(session.CompleteTravel(request, true), Is.False);
                Assert.That(store.Writes, Is.EqualTo(writes));
            }
        }
        [Test] public void SaveFailureKeepsCompletedMoveAndRetryDoesNotRepeatTravel()
        {
            var q = new Queries { Kind = TravelKind.Disembark }; var store = new Store { Success = false };
            using (var session = Create(q, store))
            {
                session.Interact(); session.CompleteTravel(session.PendingTravel, true);
                Assert.That(session.State.Player.Position.AreaId, Is.EqualTo("b"));
                Assert.That(session.BoundarySavePending && session.MenuOpen, Is.True);
                session.Back(); Assert.That(session.MenuOpen, Is.True);
                store.Success = true; Assert.That(session.RetryBoundarySave(), Is.True); session.CloseMenu();
                Assert.That(store.Writes, Is.EqualTo(2)); Assert.That(store.Area, Is.EqualTo("b"));
                Assert.That(session.MenuOpen, Is.False); Assert.That(session.State.Player.Position.X, Is.EqualTo(2));
            }
        }
    }
}
