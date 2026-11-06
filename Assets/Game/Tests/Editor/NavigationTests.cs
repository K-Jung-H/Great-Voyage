using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GreatVoyage.Application;
using GreatVoyage.Domain;
using GreatVoyage.Infrastructure;
using NUnit.Framework;

namespace GreatVoyage.Tests
{
    public sealed class NavigationTests
    {
        [Serializable] private sealed class LegacyEnvelope
        { public int Version; public string Payload; public string Sha256; }
        private sealed class Helm : IInteractionQueries
        {
            public InteractionSelection? Resolve(WorldPosition p, string id) => new InteractionSelection("helm", "조타", 1, 0, InteractionAction.UseHelm);
            public bool IsStillValid(WorldPosition p, InteractionSelection s) => true;
        }
        private sealed class Store : ISessionStore
        { public bool TrySave(GameSessionState s, out string error) { error = ""; return true; } }
        private static GameSessionState State() => new GameSessionState("play", 17,
            new CharacterState("player", new WorldPosition("upper-deck", 2.5f, -.45f), Facing.Back), new GameClock(28800));

        [Test] public void DockedStateDoesNotMoveAndDepartureCannotCreateDuplicateVoyages()
        {
            var n = NavigationState.Docked(); n.Advance(100, 1, .2);
            Assert.That(n.Distance, Is.Zero); Assert.That(n.Depart("play", .002), Is.True);
            Assert.That(n.Depart("play", .002), Is.False);
            Assert.That(n.VoyageId, Is.EqualTo("play:voyage:1")); Assert.That(n.VoyageSequence, Is.EqualTo(1));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(-.37)]
        public void ConstantSteeringIsIndependentOfStepPartition(double steering)
        {
            var a = NavigationState.Docked(); var b = NavigationState.Docked(); a.Depart("p", .002); b.Depart("p", .002);
            a.Advance(86400, steering, .2);
            for (var i = 0; i < 6000; i++) b.Advance(14.4, steering, .2);
            Assert.That(b.X, Is.EqualTo(a.X).Within(1e-8)); Assert.That(b.Y, Is.EqualTo(a.Y).Within(1e-8));
            Assert.That(b.HeadingDegrees, Is.EqualTo(a.HeadingDegrees).Within(1e-7)); Assert.That(b.Distance, Is.EqualTo(a.Distance).Within(1e-8));
        }
        [Test] public void HelmOwnsMovementWithoutPausingAndMenuCannotCauseCatchUp()
        {
            var state = State(); using (var s = new GameSession(state, new Helm(), new SaveCoordinator(new Store())))
            {
                Assert.That(s.Interact(), Is.True); s.Move(1, 1); s.Tick(1);
                Assert.That(s.IsSteering, Is.True); Assert.That(s.Pauses.Count, Is.Zero); Assert.That(s.CharacterMovementAllowed, Is.False);
                Assert.That(s.Movement.X, Is.Zero); Assert.That(state.Navigation.HeadingDegrees, Is.EqualTo(151.2).Within(1e-5));
                var x = state.Navigation.X; var heading = state.Navigation.HeadingDegrees; var clock = state.Clock.TotalGameSeconds;
                s.OpenMenu(); s.Move(1, 0); s.Tick(200);
                Assert.That(state.Navigation.X, Is.EqualTo(x)); Assert.That(state.Clock.TotalGameSeconds, Is.EqualTo(clock));
                s.CloseMenu(); s.Tick(1); Assert.That(state.Navigation.HeadingDegrees, Is.EqualTo(heading));
                s.Back(); Assert.That(s.IsSteering, Is.False); Assert.That(s.MenuOpen, Is.False);
                Assert.That(s.CharacterMovementAllowed, Is.True); s.Tick(1); Assert.That(state.Navigation.Distance, Is.GreaterThan(0));
                Assert.That(state.Player.Position.X, Is.EqualTo(2.5));
            }
        }
        [Test] public void ActualSaveRoundTripRetainsNavigationAndVersionOneMigrationDoesNotRewriteFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "GreatVoyageNavigation", Guid.NewGuid().ToString("N"));
            try
            {
                var store = new JsonFileSessionStore(directory); var state = State(); state.Navigation.Depart(state.PlayId, .002);
                state.Navigation.Advance(1000, .7, .2); Assert.That(store.TrySave(state, out _), Is.True);
                var read = store.Load(); Assert.That(read.Navigation.X, Is.EqualTo(state.Navigation.X));
                Assert.That(read.Navigation.Y, Is.EqualTo(state.Navigation.Y)); Assert.That(read.Navigation.VoyageId, Is.EqualTo(state.Navigation.VoyageId));
                Assert.That(read.Navigation.HeadingDegrees, Is.EqualTo(state.Navigation.HeadingDegrees));
                var payload = "{\"PlayId\":\"legacy-play\",\"RootSeed\":17,\"Player\":{\"Id\":\"player\",\"Position\":{\"AreaId\":\"upper-deck\",\"X\":2.5,\"Y\":-0.45},\"Facing\":1},\"Clock\":{\"TotalGameSeconds\":28800}}";
                string hash;
                using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
                var json = UnityEngine.JsonUtility.ToJson(new LegacyEnvelope { Version = 1, Payload = payload, Sha256 = hash });
                File.WriteAllText(store.FilePath, json); read = store.Load();
                Assert.That(read.Navigation.IsUnderway, Is.False); Assert.That(read.Player.Position.AreaId, Is.EqualTo("upper-deck"));
                Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo(json));
                File.WriteAllText(store.FilePath, UnityEngine.JsonUtility.ToJson(new LegacyEnvelope { Version = 2, Payload = payload, Sha256 = hash }));
                Assert.Throws<InvalidDataException>(() => store.Load());
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
