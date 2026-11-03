using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreatVoyage.Composition;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GreatVoyage.Editor
{
    // Editor-only measurement, isolated save-slot setup and evidence export. No product UI or state owner.
    public static class P3Validation
    {
        private const string Evidence = "Agents/References/P3/";
        private const string OriginalSlot = "GreatVoyage.P3.OriginalSlot";
        private static ProfilerRecorder main, physics, gc;
        private static readonly double[] frames = new double[600];
        private static readonly double[] cpu = new double[600];
        private static readonly double[] physicsMs = new double[600];
        private static readonly long[] allocations = new long[600];
        private static int count, lastFrame;
        private static string profileName;

        public static string PrepareSlot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            var serialized = new SerializedObject(root);
            var property = serialized.FindProperty("saveDirectoryName");
            if (SessionState.GetString(OriginalSlot, "") == "") SessionState.SetString(OriginalSlot, property.stringValue);
            property.stringValue = "Validation/P3_" + Guid.NewGuid().ToString("N");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return property.stringValue;
        }

        public static void RestoreSlot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var original = SessionState.GetString(OriginalSlot, "");
            if (string.IsNullOrEmpty(original)) throw new InvalidOperationException("No validation slot to restore.");
            var root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            var serialized = new SerializedObject(root);
            serialized.FindProperty("saveDirectoryName").stringValue = original;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            SessionState.EraseString(OriginalSlot);
        }

        public static string Sample(string name)
        {
            var root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            var session = root.Session;
            var p = session.State.Player.Position;
            var report = new { playMode = EditorApplication.isPlaying, session.State.PlayId, session.State.RootSeed,
                time = session.State.Clock.TotalGameSeconds, position = new { p.AreaId, p.X, p.Y },
                facing = session.State.Player.Facing.ToString(), session.JournalOpen, session.MenuOpen,
                pauseCount = session.Pauses.Count, session.ExitPrepared, root.SavePath, session.Error };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
            Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + name + ".json", json);
            return json;
        }

        public static void BeginProfile(string name)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play required.");
            EditorApplication.update -= Record;
            main.Dispose(); physics.Dispose(); gc.Dispose();
            main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            physics = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics2D.Simulate", 1);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            count = 0; lastFrame = -1; profileName = name;
            EditorApplication.update += Record;
        }

        private static void Record()
        {
            if (!EditorApplication.isPlaying || count == frames.Length) { EditorApplication.update -= Record; return; }
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            frames[count] = Time.unscaledDeltaTime * 1000;
            cpu[count] = main.Valid ? main.LastValue * 0.000001 : -1;
            physicsMs[count] = physics.Valid ? physics.LastValue * 0.000001 : -1;
            allocations[count] = gc.Valid ? gc.LastValue : -1;
            count++;
        }

        public static string EndProfile()
        {
            EditorApplication.update -= Record;
            var report = new { context = "Unity Editor Play Mode; one actor, cabin, HUD; includes Editor/tool overhead; not Player benchmark",
                samples = count, mainThreadAvailable = main.Valid, physicsAvailable = physics.Valid, gcAvailable = gc.Valid,
                frameMs = Stats(frames), mainThreadMs = Stats(cpu), physics2DMs = Stats(physicsMs),
                gcBytes = Stats(allocations.Select(x => (double)x).ToArray()),
                unity = UnityEngine.Application.unityVersion, cpuModel = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName };
            main.Dispose(); physics.Dispose(); gc.Dispose();
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
            Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + profileName + ".json", json);
            return json;
        }

        private static object Stats(double[] source)
        {
            var values = source.Take(count).Where(x => x >= 0).OrderBy(x => x).ToArray();
            if (values.Length == 0) return null;
            return new { median = values[values.Length / 2], p95 = values[Math.Min(values.Length - 1, (int)(values.Length * .95))], max = values.Last() };
        }

        public static string AvailablePhysicsMarkers()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            return string.Join("\n", handles.Select(ProfilerRecorderHandle.GetDescription).Where(d => d.Name.Contains("Physics2D")).Select(d => d.Name));
        }
    }
}
