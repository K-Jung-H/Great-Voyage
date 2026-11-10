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
using UnityEngine.UI;

namespace GreatVoyage.Tests
{
    public sealed class NavigationPlayTests
    {
        private Keyboard keyboard;
        private InputSettings original, configured;
        private HideFlags originalFlags;
        private CabinSessionRoot root;
        private PlayerMotor motor;
        private WorldTravelView world;
        private string directory;
        [UnityTest] public IEnumerator FixedDeckSteeringChartPauseAndRestore() => Validate();
        private IEnumerator LoadScenario() { yield return SceneManager.LoadSceneAsync("Game"); }
        private IEnumerator Validate()
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
            directory = Path.Combine(Path.GetTempPath(), "GreatVoyageP5Tests", Guid.NewGuid().ToString("N"));
            CabinSessionRoot.NextLoadSaveDirectory = directory; yield return LoadScenario(); Bind();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Assert.That(root.Session.State.Navigation.IsUnderway, Is.False);
            yield return Walk(new Vector2(0, -2)); yield return Travel("upper-deck");

            var deck = world.ActiveArea; var deckPosition = deck.transform.position; var deckRotation = deck.transform.rotation;
            var helm = deck.GetComponentInChildren<HelmStation>();
            Assert.That(helm.GetComponent<CellSize>().IsPlacementValid, Is.True);
            yield return Walk(helm.ApproachPoint.position); yield return Press(Key.E);
            Assert.That(root.Session.IsSteering && root.Session.State.Navigation.IsUnderway, Is.True);
            Assert.That(motor.Body.simulated, Is.False);
            Assert.That(root.Session.State.Player.Facing, Is.EqualTo(GreatVoyage.Domain.Facing.Back));
            var playerPosition = motor.Body.position; var navigation = root.Session.State.Navigation;
            var initialX = navigation.X;var initialY=navigation.Y; var initialHeading = navigation.HeadingDegrees;
            Keys(Key.D);
            yield return new WaitForSecondsRealtime(1.5f);
            Keys(); yield return null;
            Assert.That(Math.Abs(navigation.X-initialX), Is.GreaterThan(.1));
            Assert.That(Math.Abs(navigation.HeadingDegrees - initialHeading), Is.GreaterThan(20));
            Assert.That(motor.Body.position, Is.EqualTo(playerPosition));
            Assert.That(deck.transform.position, Is.EqualTo(deckPosition)); Assert.That(deck.transform.rotation, Is.EqualTo(deckRotation));
            root.Session.OpenMenu(); var frozen = navigation.Distance; var clock = root.Session.State.Clock.TotalGameSeconds;
            Keys(Key.D); yield return new WaitForSecondsRealtime(.3f); Keys();
            Assert.That(navigation.Distance, Is.EqualTo(frozen)); Assert.That(root.Session.State.Clock.TotalGameSeconds, Is.EqualTo(clock));
            yield return Press(Key.Escape); Assert.That(root.Session.IsSteering, Is.True);
            yield return Press(Key.Escape); Assert.That(root.Session.IsSteering, Is.False); Assert.That(motor.Body.simulated, Is.True);
            yield return Walk(new Vector2(0, -.6f)); root.Session.RefreshInteraction();
            Assert.That(root.Session.FocusedInteraction?.TargetId, Is.Not.EqualTo("upper-deck:harbor-dock"));
            yield return Walk(new Vector2(4, .4f)); yield return Travel("captain-cabin");
            var headingBefore = navigation.HeadingDegrees; var distanceBefore = navigation.Distance;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(navigation.Distance, Is.GreaterThan(distanceBefore)); Assert.That(navigation.HeadingDegrees, Is.EqualTo(headingBefore));
            yield return Walk(new Vector2(1.6f, -1.35f)); yield return Walk(new Vector2(1.6f, .45f));
            var desk = UnityEngine.Object.FindAnyObjectByType<JournalDesk>(); var approach = desk.transform.Find("ApproachPoint").position;
            yield return Walk(new Vector2(approach.x, approach.y - .55f)); yield return Press(Key.E);
            Assert.That(root.Session.JournalOpen, Is.True);
            var view = UnityEngine.Object.FindAnyObjectByType<NavigationView>();
            Assert.That(view.Marker.anchoredPosition.magnitude, Is.GreaterThan(1));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(view.Marker.localEulerAngles.z, (float)navigation.HeadingDegrees - 180)), Is.LessThan(.1f));
            var art = view.transform.Find("JournalScreen/LeatherBinding/ChartPage/LiveSeaChart/MapArtwork");
            view.transform.Find("JournalScreen/LeatherBinding/ChartPage/ChartZoomIn").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(art.localScale.x, Is.EqualTo(2));
            view.transform.Find("JournalScreen/LeatherBinding/ChartPage/ChartZoomOut").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(art.localScale.x, Is.EqualTo(1));
            frozen = navigation.Distance; yield return new WaitForSecondsRealtime(.3f); Assert.That(navigation.Distance, Is.EqualTo(frozen));
            Assert.That(root.PrepareExit(), Is.True); var savedX = navigation.X; var savedY = navigation.Y; var voyage = navigation.VoyageId;
            CabinSessionRoot.NextLoadSaveDirectory = directory; yield return LoadScenario(); Bind();
            Assert.That(root.Session.State.Navigation.VoyageId, Is.EqualTo(voyage));
            Assert.That(root.Session.State.Navigation.X, Is.EqualTo(savedX).Within(.03));
            Assert.That(root.Session.State.Navigation.Y, Is.EqualTo(savedY).Within(.03));
            Assert.That(root.Session.IsSteering, Is.False); Assert.That(root.Session.State.Player.Position.AreaId, Is.EqualTo("captain-cabin"));
        }
        private void Bind()
        {
            root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>(); motor = UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
            world = UnityEngine.Object.FindAnyObjectByType<WorldTravelView>(); Assert.That(root.Session, Is.Not.Null);
        }
        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        private IEnumerator Press(Key key) { Keys(key); yield return null; yield return null; Keys(); yield return null; }
        private IEnumerator Walk(Vector2 goal)
        {
            var deadline = Time.realtimeSinceStartup + 10;
            while (Vector2.Distance(motor.Body.position, goal) > .1f && Time.realtimeSinceStartup < deadline)
            {
                var d = goal - motor.Body.position;
                Keys(new[] { Mathf.Abs(d.x) > .07f ? (d.x > 0 ? Key.D : Key.A) : Key.None,
                    Mathf.Abs(d.y) > .07f ? (d.y > 0 ? Key.W : Key.S) : Key.None }.Where(k => k != Key.None).ToArray());
                yield return null;
            }
            Keys(); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(Vector2.Distance(motor.Body.position, goal), Is.LessThan(.18f), "Blocked walk to " + goal + " at " + motor.Body.position);
        }
        private IEnumerator Travel(string destination)
        {
            yield return null; root.Session.RefreshInteraction();
            Assert.That(root.Session.FocusedInteraction?.TargetId, Does.EndWith(":" + destination));
            yield return Press(Key.E); var deadline = Time.realtimeSinceStartup + 3;
            while (world.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(world.ActiveArea.AreaId, Is.EqualTo(destination)); Assert.That(root.Session.Pauses.Count, Is.Zero);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (original != null) InputSystem.settings = original;
            if (configured != null) UnityEngine.Object.Destroy(configured);
            var cleanup = SceneManager.CreateScene("P5TestCleanup"); SceneManager.SetActiveScene(cleanup);
            var game = SceneManager.GetSceneByName("Game"); if (game.IsValid()) yield return SceneManager.UnloadSceneAsync(game);
            var prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GreatVoyageP5Tests")) + Path.DirectorySeparatorChar;
            if (directory != null && Path.GetFullPath(directory).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(directory)) Directory.Delete(directory, true);
            if (original != null) original.hideFlags = originalFlags;
        }
    }
}
