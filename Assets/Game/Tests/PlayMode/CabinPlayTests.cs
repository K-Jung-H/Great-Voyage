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
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace GreatVoyage.Tests
{
    public sealed class CabinPlayTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private string directory;
        private CabinSessionRoot root;
        private PlayerMotor motor;
        private InputSettings originalSettings, testSettings;
        private HideFlags originalSettingsFlags;
        private int originalFrameRate, originalVsync;

        [UnityTest]
        public IEnumerator ProductSceneKeyboardCollisionJournalPauseAndRestore()
        {
            originalSettings = InputSystem.settings;
            if (originalSettings == null) originalSettings = ScriptableObject.CreateInstance<InputSettings>();
            originalSettingsFlags = originalSettings.hideFlags;
            originalSettings.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            originalFrameRate = UnityEngine.Application.targetFrameRate;
            originalVsync = QualitySettings.vSyncCount;
            testSettings = UnityEngine.Object.Instantiate(originalSettings);
            testSettings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testSettings;
            directory = Path.Combine(Path.GetTempPath(), "GreatVoyagePlayTests", Guid.NewGuid().ToString("N"));
            CabinSessionRoot.NextLoadSaveDirectory = directory;
            yield return SceneManager.LoadSceneAsync("Game");
            root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            motor = UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
            Assert.That(root.Session, Is.Not.Null);
            AssertFurnitureLayout();
            Assert.That(root.SavePath, Is.EqualTo(Path.Combine(directory, "session.json")));
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return null;

            // An out-of-range E cannot open the journal or pause the world.
            yield return Press(Key.E);
            Assert.That(root.Session.JournalOpen, Is.False);
            var initialTime = root.Session.State.Clock.TotalGameSeconds;
            Assert.That(root.Session.FocusedInteraction, Is.Null);
            yield return MoveTo(new Vector2(0.7f, -1.35f));
            // Push into the same wall repeatedly, including along it at a corner.
            SetKeys(Key.D);
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(motor.Body.position.x, Is.InRange(5.2f, 5.7f));
            var wallX = motor.Body.position.x;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(Mathf.Abs(motor.Body.position.x - wallX), Is.LessThan(0.025f));
            SetKeys(); yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(root.Session.State.Clock.TotalGameSeconds, Is.GreaterThan(initialTime));

            yield return MoveTo(new Vector2(1.6f, -1.35f));
            yield return MoveTo(new Vector2(1.6f, 0.45f));
            var configuredDesk = UnityEngine.Object.FindAnyObjectByType<JournalDesk>();
            var approach = configuredDesk.transform.Find("ApproachPoint").position;
            yield return MoveTo(new Vector2(approach.x, approach.y - .55f));
            Assert.That(root.Session.CanInteract, Is.True);
            var interactionDesk = UnityEngine.Object.FindAnyObjectByType<JournalDesk>();
            var highlight = interactionDesk.GetComponent<InteractionHighlight>();
            Assert.That(root.Session.FocusedInteraction.Value.TargetId, Is.EqualTo(interactionDesk.TargetId));
            Assert.That(highlight.IsHighlighted, Is.True);
            yield return Press(Key.E);
            Assert.That(root.Session.JournalOpen, Is.True);
            Assert.That(root.Session.Pauses.Count, Is.EqualTo(1));
            Assert.That(highlight.IsHighlighted, Is.False);
            var frozenTime = root.Session.State.Clock.TotalGameSeconds;
            var frozenPosition = motor.Body.position;
            SetKeys(Key.D); yield return new WaitForSecondsRealtime(0.3f); SetKeys();
            Assert.That(motor.Body.position, Is.EqualTo(frozenPosition));
            Assert.That(root.Session.State.Clock.TotalGameSeconds, Is.EqualTo(frozenTime));

            // The actual menu button opens a second pause without closing the journal.
            yield return Click("OpenSessionMenu");
            Assert.That(root.Session.MenuOpen, Is.True);
            Assert.That(root.Session.Pauses.Count, Is.EqualTo(2));
            yield return Press(Key.Escape);
            Assert.That(root.Session.MenuOpen, Is.False);
            Assert.That(root.Session.JournalOpen, Is.True);
            Assert.That(root.Session.Pauses.Count, Is.EqualTo(1));
            yield return Click("CloseJournal");
            Assert.That(root.Session.Pauses.Count, Is.Zero);
            Assert.That(highlight.IsHighlighted, Is.True);
            yield return Press(Key.Escape);
            Assert.That(highlight.IsHighlighted, Is.False);
            yield return Press(Key.Escape);
            Assert.That(highlight.IsHighlighted, Is.True);
            yield return MoveTo(new Vector2(1.6f, 0.45f));
            Assert.That(root.Session.State.Clock.TotalGameSeconds, Is.GreaterThan(frozenTime));

            // Furniture contact is resolved by the same actor body, not a second position model.
            var desk = UnityEngine.Object.FindAnyObjectByType<JournalDesk>();
            yield return MoveTo(desk.transform.Find("ApproachPoint").position);
            SetKeys(Key.W); yield return new WaitForSecondsRealtime(0.7f); SetKeys();
            yield return new WaitForSecondsRealtime(0.15f);
            var colliderBottom = desk.GetComponentInChildren<BoxCollider2D>().bounds.min.y;
            var playerCollider = motor.GetComponent<CircleCollider2D>();
            var contactY = colliderBottom - playerCollider.radius - playerCollider.offset.y;
            Assert.That(motor.Body.position.y, Is.InRange(contactY - .06f, contactY + .02f));
            Assert.That(root.PrepareExit(), Is.True);
            var state = root.Session.State;
            var savedPosition = motor.Body.position;
            var savedTime = state.Clock.TotalGameSeconds;
            var playId = state.PlayId;
            Assert.That(File.Exists(root.SavePath), Is.True);
            CabinSessionRoot.NextLoadSaveDirectory = directory;
            yield return SceneManager.LoadSceneAsync("Game");
            root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            motor = UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
            Assert.That(root.Session.State.PlayId, Is.EqualTo(playId));
            AssertFurnitureLayout();
            Assert.That(Vector2.Distance(motor.Body.position, savedPosition), Is.LessThan(0.01f),
                "Loaded " + motor.Body.position + " instead of " + savedPosition);
            Assert.That(root.Session.State.Clock.TotalGameSeconds, Is.InRange(savedTime, savedTime + 144));
            Assert.That(root.Session.Pauses.Count, Is.Zero);
            Assert.That(root.Session.JournalOpen, Is.False);
            yield return MoveTo(new Vector2(1.6f, 0.45f));
            yield return MoveTo(new Vector2(1.6f, -1.35f));
            QualitySettings.vSyncCount = 0;
            var rates = new List<float>();
            foreach (var fps in new[] { 30, 60, 120 })
            {
                UnityEngine.Application.targetFrameRate = fps;
                SetKeys(Key.A); yield return new WaitForSecondsRealtime(0.2f);
                var x = motor.Body.position.x;
                var start = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(0.6f);
                var speed = Mathf.Abs(motor.Body.position.x - x) / (Time.realtimeSinceStartup - start);
                rates.Add(speed);
                Assert.That(speed, Is.InRange(2.0f, 2.8f), "Movement rate at requested " + fps + " FPS");
                SetKeys(); yield return new WaitForSecondsRealtime(0.15f);
                yield return MoveTo(new Vector2(1.6f, -1.35f));
            }
            Assert.That(rates.Max() - rates.Min(), Is.LessThan(0.35f));
            if (UnityEngine.Application.isEditor)
            {
                Directory.CreateDirectory("Agents/References/P3");
                File.WriteAllText("Agents/References/P3/MovementRates.json", "{\"requestedFps\":[30,60,120],\"unitsPerSecond\":["
                    + string.Join(",", rates.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]}");
            }
            yield return MoveTo(new Vector2(0, -1.35f));
            SetKeys(Key.S); yield return new WaitForSecondsRealtime(1); SetKeys();
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(motor.Body.position.y, Is.InRange(-2.95f, -2.4f), "Unimplemented door must remain a boundary");
            Assert.That(root.PrepareExit(), Is.True);
            savedPosition = motor.Body.position;
            CabinSessionRoot.NextLoadSaveDirectory = directory;
            yield return SceneManager.LoadSceneAsync("Game");
            root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            motor = UnityEngine.Object.FindAnyObjectByType<PlayerMotor>();
            Assert.That(Vector2.Distance(motor.Body.position, savedPosition), Is.LessThan(0.01f), "Boundary-contact save must restore");
        }

        private void SetKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private static void AssertFurnitureLayout()
        {
            var zone = UnityEngine.Object.FindAnyObjectByType<PlacementSpace>();
            Assert.That(zone.Settings.CellWorldSize, Is.EqualTo(1));
            Assert.That(zone.GetComponentsInChildren<GridArea>().Length, Is.EqualTo(2));
            Assert.That(zone.GetGrid().CellCount, Is.EqualTo(63));
            var furniture = zone.GetComponentsInChildren<CellSize>();
            Assert.That(furniture.Length, Is.EqualTo(4));
            foreach (var item in furniture)
            {
                Assert.That(item.CellArea, Is.Not.Null, item.name);
                Assert.That(item.transform.Find("Visuals").GetComponent<Collider2D>(), Is.Null, item.name);
                Assert.That(item.transform.Find("Collision").GetComponent<SpriteRenderer>(), Is.Null, item.name);
                Assert.That(item.Settings, Is.SameAs(zone.Settings));
                if (item.IsConfigured) Assert.That(item.IsPlacementValid, Is.True, item.name);
            }
        }

        private IEnumerator Press(Key key)
        {
            SetKeys(key); yield return null; yield return null;
            SetKeys(); yield return null;
        }

        private IEnumerator MoveTo(Vector2 target)
        {
            var deadline = Time.realtimeSinceStartup + 6;
            while (Vector2.Distance(motor.Body.position, target) > 0.09f && Time.realtimeSinceStartup < deadline)
            {
                var delta = target - motor.Body.position;
                SetKeys(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x > 0 ? Key.D : Key.A : delta.y > 0 ? Key.W : Key.S);
                yield return null;
            }
            SetKeys(); yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Vector2.Distance(motor.Body.position, target), Is.LessThan(0.25f), "Movement did not reach " + target);
        }

        private IEnumerator Click(string name)
        {
            var buttons = Resources.FindObjectsOfTypeAll<Button>().Where(b => b.gameObject.scene.IsValid()).ToArray();
            Assert.That(buttons.Any(b => b.name == name), Is.True, "Missing " + name + ": " + string.Join(",", buttons.Select(b => b.name)));
            var button = buttons.Single(b => b.name == name);
            var canvas = button.GetComponentInParent<Canvas>();
            var p = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, button.transform.position);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = p }, hits);
            Assert.That(button.interactable, Is.True, name + " is disabled");
            Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, Is.True,
                name + " is covered at " + p + ": " + string.Join(",", hits.Select(h => h.gameObject.name)));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p });
            InputSystem.Update(); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p }.WithButton(MouseButton.Left));
            InputSystem.Update(); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = p });
            InputSystem.Update(); yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (originalSettings != null) InputSystem.settings = originalSettings;
            if (testSettings != null) UnityEngine.Object.Destroy(testSettings);
            UnityEngine.Application.targetFrameRate = originalFrameRate;
            QualitySettings.vSyncCount = originalVsync;
            var scene = SceneManager.CreateScene("CabinTestCleanup");
            SceneManager.SetActiveScene(scene);
            var game = SceneManager.GetSceneByName("Game");
            if (game.IsValid()) yield return SceneManager.UnloadSceneAsync(game);
            // This is exclusively the Guid-named slot created by this test, never product saves.
            var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GreatVoyagePlayTests")) + Path.DirectorySeparatorChar;
            if (directory != null && Path.GetFullPath(directory).StartsWith(expected, StringComparison.OrdinalIgnoreCase) && Directory.Exists(directory))
                Directory.Delete(directory, true);
            if (originalSettings != null) originalSettings.hideFlags = originalSettingsFlags;
        }
    }
}
