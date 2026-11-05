using System;
using System.Collections;
using System.IO;
using System.Linq;
using GreatVoyage.Composition;
using GreatVoyage.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GreatVoyage.Tests
{
    public sealed class WorldTravelPlayTests
    {
        private Keyboard keyboard;
        private InputSettings original, configured;
        private HideFlags originalFlags;
        private CabinSessionRoot root;
        private PlayerMotor motor;
        private WorldTravelView world;
        private string directory, playId;

        [UnityTest] public IEnumerator AuthoredWorldRoundTripRollbackBoardingAndRestore()
        {
            original = InputSystem.settings;
            if (original == null) original = ScriptableObject.CreateInstance<InputSettings>();
            originalFlags = original.hideFlags; original.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            configured = UnityEngine.Object.Instantiate(original); configured.hideFlags = HideFlags.DontUnloadUnusedAsset;
            configured.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            configured.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = configured;
            directory = Path.Combine(Path.GetTempPath(), "GreatVoyageP4Tests", Guid.NewGuid().ToString("N"));
            CabinSessionRoot.NextLoadSaveDirectory = directory;
            yield return SceneManager.LoadSceneAsync("Game");
            Bind(); keyboard = InputSystem.AddDevice<Keyboard>(); playId = root.Session.State.PlayId;
            Assert.That(world.Areas.Length, Is.EqualTo(7)); Assert.That(world.Portals.Length, Is.EqualTo(12));
            AssertArea("captain-cabin");
            yield return Walk(new Vector2(0, -2));
            var entry = world.Portals.Single(p => p.TargetId == "captain-cabin:upper-deck");
            var destination = entry.Arrival.position;
            entry.Arrival.position = new Vector3(999, 999);
            yield return Press(Key.E); yield return new WaitForSecondsRealtime(.7f);
            AssertArea("captain-cabin"); Assert.That(root.Session.MenuOpen, Is.True);
            Assert.That(root.Session.Pauses.Count, Is.EqualTo(1));
            entry.Arrival.position = destination;
            yield return Press(Key.Escape);
            yield return Travel("upper-deck");
            Assert.That(File.Exists(root.SavePath), Is.False, "Floor transition is not a SaveEvent");
            yield return Capture("UpperDeck");
            yield return Walk(new Vector2(4, .4f)); yield return Travel("captain-cabin");
            yield return Capture("CaptainCabin");
            yield return Walk(new Vector2(0, -2)); yield return Travel("upper-deck");
            yield return Walk(new Vector2(0, -.6f)); yield return Travel("harbor-dock");
            Assert.That(File.Exists(root.SavePath), Is.True, "Disembark must save");
            yield return Capture("HarborDock");
            yield return Walk(new Vector2(6, 0)); yield return Travel("harbor-crossroads");
            yield return Capture("Crossroads");
            yield return Walk(new Vector2(-6, 1)); yield return Travel("harbor-town");
            yield return Capture("Town");
            yield return Walk(new Vector2(-4, .3f)); yield return Travel("tavern-interior");
            yield return Capture("Tavern");
            yield return Walk(new Vector2(-4, -2)); yield return Travel("harbor-town");
            yield return Walk(new Vector2(6, 0)); yield return Travel("harbor-crossroads");
            yield return Walk(new Vector2(6, 0)); yield return Travel("harbor-market");
            yield return Capture("Market");
            var saved = motor.Body.position;
            Assert.That(root.PrepareExit(), Is.True);
            CabinSessionRoot.NextLoadSaveDirectory = directory;
            yield return SceneManager.LoadSceneAsync("Game"); Bind();
            AssertArea("harbor-market"); Assert.That(root.Session.State.PlayId, Is.EqualTo(playId));
            Assert.That(Vector2.Distance(motor.Body.position, saved), Is.LessThan(.01f));
            yield return Walk(new Vector2(-6, 0)); yield return Travel("harbor-crossroads");
            yield return Walk(new Vector2(0, -2.6f)); yield return Travel("harbor-dock");
            yield return Walk(new Vector2(-5, 0)); yield return Travel("upper-deck");
            yield return Walk(new Vector2(4, .4f)); yield return Travel("captain-cabin");
            Assert.That(root.Session.State.PlayId, Is.EqualTo(playId));
            Assert.That(root.Session.Pauses.Count, Is.Zero);
        }
        private void Bind()
        {
            root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            motor = UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
            world = UnityEngine.Object.FindAnyObjectByType<WorldTravelView>();
            Assert.That(root.Session, Is.Not.Null);
        }
        private void AssertArea(string id)
        {
            Assert.That(root.Session.State.Player.Position.AreaId, Is.EqualTo(id));
            Assert.That(world.ActiveArea.AreaId, Is.EqualTo(id));
            Assert.That(world.Areas.Count(a => a.gameObject.activeSelf), Is.EqualTo(1));
            Assert.That(world.ActiveArea.PlacementIsClear(new GreatVoyage.Domain.WorldPosition(id, 7.9f, 4.4f)), Is.False,
                "Camera extent must not make outside-wall positions valid");
        }
        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        private IEnumerator Press(Key key) { Keys(key); yield return null; yield return null; Keys(); yield return null; }
        private IEnumerator Walk(Vector2 goal)
        {
            var deadline = Time.realtimeSinceStartup + 10;
            while (Vector2.Distance(motor.Body.position, goal) > .1f && Time.realtimeSinceStartup < deadline)
            {
                var delta = goal - motor.Body.position;
                var x = Mathf.Abs(delta.x) > .07f ? (delta.x > 0 ? Key.D : Key.A) : Key.None;
                var y = Mathf.Abs(delta.y) > .07f ? (delta.y > 0 ? Key.W : Key.S) : Key.None;
                Keys(new[] { x, y }.Where(k => k != Key.None).ToArray()); yield return null;
            }
            Keys(); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(Vector2.Distance(motor.Body.position, goal), Is.LessThan(.18f), "Blocked walk to " + goal + " at " + motor.Body.position);
        }
        private IEnumerator Travel(string destination)
        {
            yield return null; root.Session.RefreshInteraction();
            Assert.That(root.Session.FocusedInteraction?.TargetId, Does.EndWith(":" + destination));
            var clock = root.Session.State.Clock.TotalGameSeconds;
            yield return Press(Key.E);
            Assert.That(root.Session.WorldInputAllowed, Is.False);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(root.Session.State.Clock.TotalGameSeconds, Is.InRange(clock, clock + 5));
            var deadline = Time.realtimeSinceStartup + 3;
            while (world.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.IsTransitioning, Is.False); AssertArea(destination);
            Assert.That(root.Session.MenuOpen, Is.False, root.Session.Error);
            Assert.That(root.Session.Pauses.Count, Is.Zero);
        }
        private static IEnumerator Capture(string name)
        {
            yield return null; yield return null;
            Directory.CreateDirectory("Agents/References/P4");
            ScreenCapture.CaptureScreenshot("Agents/References/P4/" + name + ".png");
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (original != null) InputSystem.settings = original;
            if (configured != null) UnityEngine.Object.Destroy(configured);
            var cleanup = SceneManager.CreateScene("P4TestCleanup"); SceneManager.SetActiveScene(cleanup);
            var game = SceneManager.GetSceneByName("Game"); if (game.IsValid()) yield return SceneManager.UnloadSceneAsync(game);
            var prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GreatVoyageP4Tests")) + Path.DirectorySeparatorChar;
            if (directory != null && Path.GetFullPath(directory).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(directory)) Directory.Delete(directory, true);
            if (original != null) original.hideFlags = originalFlags;
        }
    }
}
