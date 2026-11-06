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
using Unity.Profiling;
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
        private bool oceanReview;
        [UnityTest] public IEnumerator FixedDeckSteeringWaterChartPauseAndRestore() => Validate(false);
        [UnityTest] public IEnumerator OceanRelativeFlowSteeringPauseAndRestore() => Validate(true);
        private IEnumerator LoadScenario()
        {
#if UNITY_EDITOR
            if(oceanReview)
            {
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/Game/Scenes/Review/OceanNavigationReview.unity",new LoadSceneParameters(LoadSceneMode.Single));
                yield break;
            }
#endif
            yield return SceneManager.LoadSceneAsync("Game");
        }
        private IEnumerator Validate(bool review)
        {
            oceanReview=review;
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
            yield return Capture("SteeringBefore");
            Keys(Key.D);
            if(oceanReview)yield return RecordSteering();
            else yield return new WaitForSecondsRealtime(1.5f);
            Keys(); yield return null;
            Assert.That(oceanReview ? Math.Sqrt(Math.Pow(navigation.X-initialX,2)+Math.Pow(navigation.Y-initialY,2))
                : Math.Abs(navigation.X-initialX), Is.GreaterThan(.1));
            Assert.That(Math.Abs(navigation.HeadingDegrees - initialHeading), Is.GreaterThan(20));
            Assert.That(motor.Body.position, Is.EqualTo(playerPosition));
            Assert.That(deck.transform.position, Is.EqualTo(deckPosition)); Assert.That(deck.transform.rotation, Is.EqualTo(deckRotation));
            var water = deck.transform.Find("Sea").GetComponent<SpriteRenderer>(); var block = new MaterialPropertyBlock(); water.GetPropertyBlock(block);
            var ocean=UnityEngine.Object.FindAnyObjectByType<OceanNavigationBinding>();
            if(oceanReview)
            {
                Assert.That(ocean,Is.Not.Null);Assert.That(ocean.Flow.SamplingOffset.magnitude,Is.GreaterThan(.1f));
                var heading=(float)navigation.HeadingDegrees*Mathf.Deg2Rad;
                Assert.That(Vector2.Dot(ocean.Flow.WaterVelocity,new Vector2(Mathf.Cos(heading),Mathf.Sin(heading))),Is.LessThan(0));
                Assert.That(water.sharedMaterial.shader.name,Is.EqualTo("Great Voyage/Ocean/Toon Surface"));
            }
            else
            {
                Assert.That(block.GetFloat("_Heading"), Is.EqualTo((float)navigation.HeadingDegrees * Mathf.Deg2Rad).Within(.02f));
                Assert.That(block.GetFloat("_Sail"), Is.EqualTo(1));
            }
            yield return Capture("SteeringAfter");
            yield return ProfileSailing();
            root.Session.OpenMenu(); var frozen = navigation.Distance; var clock = root.Session.State.Clock.TotalGameSeconds;
            if(oceanReview)yield return null;
            var oceanTime=oceanReview ? ocean.Ocean.ElapsedSeconds : 0;
            var oceanOffset=oceanReview ? ocean.Flow.SamplingOffset : Vector2.zero;
            Keys(Key.D); yield return new WaitForSecondsRealtime(.3f); Keys();
            Assert.That(navigation.Distance, Is.EqualTo(frozen)); Assert.That(root.Session.State.Clock.TotalGameSeconds, Is.EqualTo(clock));
            if(oceanReview){Assert.That(ocean.Ocean.ElapsedSeconds,Is.EqualTo(oceanTime));Assert.That(ocean.Flow.SamplingOffset,Is.EqualTo(oceanOffset));}
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
            yield return Capture("LiveChart");
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
        [Serializable] private sealed class VideoSample { public string file;public double wallSeconds,visualSeconds; }
        [Serializable] private sealed class VideoRecording
        {
            public string context="Actual gameplay review scene, injected Keyboard D held for manual steering; no simulated navigation or interpolated frames";
            public int frames,width,height;public double duration,visualDuration;public VideoSample[] samples;
        }
        private IEnumerator RecordSteering()
        {
            var path="Agents/References/P5/Water/Stage3/Gameplay";Directory.CreateDirectory(path);
            var temp=Path.Combine("Temp","OceanSteeringCapture",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
            var ocean=UnityEngine.Object.FindAnyObjectByType<OceanAmbientDriver>();
            var samples=new System.Collections.Generic.List<VideoSample>();var start=Time.realtimeSinceStartupAsDouble;var initial=ocean.ElapsedSeconds;
            while(Time.realtimeSinceStartupAsDouble-start<6)
            {
                yield return new WaitForEndOfFrame();
                var file=Path.Combine(temp,"frame_"+samples.Count.ToString("D4")+".png");ScreenCapture.CaptureScreenshot(file);
                samples.Add(new VideoSample{file=file,wallSeconds=Time.realtimeSinceStartupAsDouble-start,visualSeconds=ocean.ElapsedSeconds});
                yield return new WaitForSecondsRealtime(.1f);
            }
            var record=new VideoRecording{frames=samples.Count,width=Screen.width,height=Screen.height,
                duration=Time.realtimeSinceStartupAsDouble-start,visualDuration=ocean.ElapsedSeconds-initial,samples=samples.ToArray()};
            File.WriteAllText(path+"/Recording.json",JsonUtility.ToJson(record,true));
        }
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
        private IEnumerator Capture(string name)
        {
            var path=oceanReview ? "Agents/References/P5/Water/Stage3" : "Agents/References/P5/Stage1";
            Directory.CreateDirectory(path); yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(path+"/"+ name + ".png"); yield return null;
        }
        [Serializable] private sealed class Metrics { public double median, p95, max; }
        [Serializable] private sealed class Profile
        {
            public string context = "Unity Editor Play Mode; fixed upper deck, one actor, manual helm and sailing water; includes test/tool overhead; not standalone Player performance";
            public string unity, cpu, gpu; public int samples, width, height;
            public bool mainThreadAvailable, physicsAvailable, gcAvailable;
            public Metrics frameMs, mainThreadMs, physics2DMs, gcBytes;
        }
        private static Metrics Stats(double[] values)
        {
            Array.Sort(values); return new Metrics { median = values[values.Length / 2], p95 = values[(int)(values.Length * .95)], max = values[values.Length - 1] };
        }
        private IEnumerator ProfileSailing()
        {
            using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
            using (var physics = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics2D.Simulate", 1))
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            {
                var frames = new double[600]; var cpu = new double[600]; var physicsMs = new double[600]; var allocations = new double[600];
                for (var i = 0; i < frames.Length; i++)
                {
                    yield return null; frames[i] = Time.unscaledDeltaTime * 1000;
                    cpu[i] = main.Valid ? main.LastValue * .000001 : -1;
                    physicsMs[i] = physics.Valid ? physics.LastValue * .000001 : -1;
                    allocations[i] = gc.Valid ? gc.LastValue : -1;
                }
                var report = new Profile { unity = UnityEngine.Application.unityVersion, cpu = SystemInfo.processorType,
                    gpu = SystemInfo.graphicsDeviceName, samples = 600, width = Screen.width, height = Screen.height,
                    mainThreadAvailable = main.Valid, physicsAvailable = physics.Valid, gcAvailable = gc.Valid,
                    frameMs = Stats(frames), mainThreadMs = Stats(cpu), physics2DMs = Stats(physicsMs), gcBytes = Stats(allocations) };
                File.WriteAllText((oceanReview ? "Agents/References/P5/Water/Stage3/" : "Agents/References/P5/Stage1/")+"SailingProfile.json", JsonUtility.ToJson(report, true));
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (original != null) InputSystem.settings = original;
            if (configured != null) UnityEngine.Object.Destroy(configured);
            var cleanup = SceneManager.CreateScene("P5TestCleanup"); SceneManager.SetActiveScene(cleanup);
            var game = SceneManager.GetSceneByName(oceanReview ? "OceanNavigationReview" : "Game"); if (game.IsValid()) yield return SceneManager.UnloadSceneAsync(game);
            var prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "GreatVoyageP5Tests")) + Path.DirectorySeparatorChar;
            if (directory != null && Path.GetFullPath(directory).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(directory)) Directory.Delete(directory, true);
            if (original != null) original.hideFlags = originalFlags;
        }
    }
}
