using System;
using System.IO;
using GreatVoyage.Application;
using GreatVoyage.Domain;
using GreatVoyage.Infrastructure;
using NUnit.Framework;

namespace GreatVoyage.Tests
{
    public sealed class CabinDomainTests
    {
        private string directory;
        private sealed class Reach : IInteractionQueries
        {
            public bool Allowed = true;
            public InteractionSelection? Resolve(WorldPosition p, string retained) => Allowed
                ? new InteractionSelection("desk", "일지", 0, 0, InteractionAction.OpenJournal) : (InteractionSelection?)null;
            public bool IsStillValid(WorldPosition p, InteractionSelection selected) => Allowed && selected.TargetId == "desk";
        }
        private sealed class Store : ISessionStore
        {
            public bool Success = true; public int Writes;
            public bool TrySave(GameSessionState state, out string error) { Writes++; error = Success ? "" : "denied"; return Success; }
        }
        private static GameSessionState State() => new GameSessionState("test-play", 1717,
            new CharacterState("player", new WorldPosition("captain-cabin", 0.7f, -0.9f), Facing.Front), new GameClock(28800));

        [SetUp] public void Setup() => directory = Path.Combine(Path.GetTempPath(), "GreatVoyageTests", Guid.NewGuid().ToString("N"));
        [TearDown] public void Teardown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test] public void ClockUsesOneTenMinuteDayAndNeverCatchesUpPausedTime()
        {
            var state = State();
            using (var session = new GameSession(state, new Reach(), new SaveCoordinator(new Store())))
            {
                session.Tick(600);
                Assert.That(state.Clock.TotalGameSeconds, Is.EqualTo(115200));
                using (session.Pauses.Acquire("dialog")) session.Tick(120);
                session.Tick(1);
                Assert.That(state.Clock.TotalGameSeconds, Is.EqualTo(115344));
            }
        }

        [Test] public void NestedMenuDoesNotReleaseJournalPauseAndTokensAreIdempotent()
        {
            using (var session = new GameSession(State(), new Reach(), new SaveCoordinator(new Store())))
            {
                Assert.That(session.Interact(), Is.True);
                session.OpenMenu();
                Assert.That(session.Pauses.Count, Is.EqualTo(2));
                session.Back();
                Assert.That(session.JournalOpen && session.Pauses.IsPaused, Is.True);
                Assert.That(session.MenuOpen, Is.False);
                session.Back();
                Assert.That(session.WorldInputAllowed, Is.True);
                var token = session.Pauses.Acquire("external"); token.Dispose(); token.Dispose();
                Assert.That(session.Pauses.Count, Is.Zero);
            }
        }

        [Test] public void RejectedInteractionCannotPauseOrWriteSave()
        {
            var store = new Store();
            using (var session = new GameSession(State(), new Reach { Allowed = false }, new SaveCoordinator(store)))
            {
                Assert.That(session.Interact(), Is.False);
                Assert.That(session.Pauses.Count, Is.Zero);
                Assert.That(store.Writes, Is.Zero);
                session.Move(1, 1);
                Assert.That(session.Movement.X * session.Movement.X + session.Movement.Y * session.Movement.Y,
                    Is.EqualTo(1).Within(0.0001));
            }
        }

        [Test] public void SaveOnlyHappensAtNormalExitAndFailureAllowsRetry()
        {
            var store = new Store { Success = false };
            using (var session = new GameSession(State(), new Reach(), new SaveCoordinator(store)))
            {
                session.Tick(2); session.Interact(); session.CloseJournal();
                Assert.That(store.Writes, Is.Zero);
                Assert.That(session.PrepareNormalExit(), Is.False);
                Assert.That(session.ExitPrepared, Is.False);
                Assert.That(session.MenuOpen, Is.True);
                store.Success = true;
                Assert.That(session.PrepareNormalExit(), Is.True);
                Assert.That(session.PrepareNormalExit(), Is.True);
                Assert.That(store.Writes, Is.EqualTo(2));
            }
        }

        [Test] public void RealFileRoundTripRestoresCommonPositionFacingClockAndSeed()
        {
            var store = new JsonFileSessionStore(directory); var state = State();
            state.Player.CommitResolvedPose(new WorldPosition("captain-cabin", 1.25f, 0.4f), Facing.Right);
            state.Clock.Advance(3);
            Assert.That(store.TrySave(state, out var error), Is.True, error);
            var read = new JsonFileSessionStore(directory).Load();
            Assert.That(read.PlayId, Is.EqualTo(state.PlayId));
            Assert.That(read.Player.Position.X, Is.EqualTo(1.25f));
            Assert.That(read.Player.Position.Y, Is.EqualTo(0.4f));
            Assert.That(read.Player.Facing, Is.EqualTo(Facing.Right));
            Assert.That(read.Clock.TotalGameSeconds, Is.EqualTo(state.Clock.TotalGameSeconds));
            Assert.That(read.RootSeed, Is.EqualTo(1717));
        }

        [Test] public void CorruptedMainUsesBackupAndNextSavePreservesThatGoodBackup()
        {
            var store = new JsonFileSessionStore(directory); var state = State();
            Assert.That(store.TrySave(state, out _), Is.True);
            state.Clock.Advance(2);
            Assert.That(store.TrySave(state, out _), Is.True);
            var backup = File.ReadAllBytes(store.FilePath + ".bak");
            File.WriteAllText(store.FilePath, "{broken");
            var read = store.Load();
            Assert.That(store.RecoveredBackup, Is.True);
            Assert.That(read.Clock.TotalGameSeconds, Is.EqualTo(28800));
            Assert.That(store.TrySave(read, out _), Is.True);
            Assert.That(File.ReadAllBytes(store.FilePath + ".bak"), Is.EqualTo(backup));
        }

        [Test] public void InvalidSaveIsNotSilentlyReplacedWithNewSession()
        {
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "session.json");
            File.WriteAllText(file, "{bad");
            Assert.Throws<InvalidDataException>(() => new JsonFileSessionStore(directory).Load());
            Assert.That(File.ReadAllText(file), Is.EqualTo("{bad"));
        }

        [Test] public void FailedTemporaryWriteLeavesPreviousSaveUntouched()
        {
            var store = new JsonFileSessionStore(directory);
            Assert.That(store.TrySave(State(), out _), Is.True);
            var before = File.ReadAllBytes(store.FilePath);
            Directory.CreateDirectory(store.FilePath + ".tmp");
            Assert.That(store.TrySave(State(), out _), Is.False);
            Assert.That(File.ReadAllBytes(store.FilePath), Is.EqualTo(before));
        }
    }
}
