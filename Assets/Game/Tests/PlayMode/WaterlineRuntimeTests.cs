using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using GreatVoyage.Presentation;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
namespace GreatVoyage.Tests
{
    public sealed class WaterlineRuntimeTests
    {
        [Serializable]
        private sealed class Sample
        {
            public string policy; public int resolution, parts, interval, frames, generations, width, height;
            public double callMedianMs, callP95Ms, callMaxMs, cpuMsPerFrame, extractMeanMs, rasterMeanMs, distanceMeanMs, uploadMeanMs, allocatedBytesPerGeneration;
            public double frameMedianMs, frameP95Ms, profilerPeakNs;
        }
        [Serializable]
        private sealed class Report
        {
            public string environment, device, cpu, gpu, scope; public Sample[] samples;
        }
        private static void Resolution(ShipWaterSurface surface, int value) => typeof(ShipWaterSurface).GetField("longestEdge", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(surface, value);
        private static ShipWaterSurface Create(int parts, out GameObject root, out CapsuleCollider2D capsule)
        {
            root = new GameObject("WaterlineRuntimeProbe");
            var surface = root.AddComponent<ShipWaterSurface>();
            capsule = null;
            for (var i = 0; i < parts; i++)
            {
                var go = new GameObject("Contact_" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3((i % 4) * 2.5f, (i / 4) * 1.5f, 0);
                var c = go.AddComponent<CapsuleCollider2D>();
                c.enabled = false;
                c.direction = CapsuleDirection2D.Horizontal;
                c.size = new Vector2(12.83186f, 2.40597f);
                c.offset = new Vector2(.57743f, -.41704f);
                var source = go.AddComponent<WaterContactSource>();
                source.SetContactColliders(c);
                source.BindTo(surface);
                if (i == 0)
                    capsule = c;
            }
            return surface;
        }
        [UnityTest]
        public IEnumerator NotificationsBatchPreserveLastGoodAndAllowEmptyContact()
        {
            var surface = Create(1, out var root, out var capsule);
            Resolution(surface, 128);
            yield return null;
            try
            {
                Assert.That(surface.Sdf, Is.Not.Null);
                var count = surface.Generation;
                for (var i = 0; i < 20; i++)
                    surface.NotifyGeometryChanged();
                yield return null;
                Assert.That(surface.Generation, Is.EqualTo(count + 1));
                Assert.That(surface.IsDirty, Is.False);
                count = surface.Generation;
                root.transform.position = new Vector3(25, -10, 0);
                root.transform.Rotate(0, 0, 25);
                yield return null;
                Assert.That(surface.Generation, Is.EqualTo(count));
                var good = surface.Sdf;
                capsule.enabled = true;
                surface.NotifyGeometryChanged();
                Assert.That(surface.RefreshIfNeeded(), Is.False);
                Assert.That(surface.Sdf, Is.SameAs(good));
                Assert.That(surface.IsDirty, Is.True);
                capsule.enabled = false;
                capsule.size += Vector2.right;
                surface.Sources[0].NotifyGeometryChanged();
                Assert.That(surface.RefreshIfNeeded(), Is.True);
                count = surface.Generation;
                surface.Sources[0].enabled = false;
                yield return null;
                Assert.That(surface.Generation, Is.EqualTo(count + 1));
                Assert.That(surface.Sdf.GetPixelData<float>(0).All(x => x > 0), Is.True);
                surface.Sources[0].enabled = true;
                yield return null;
                Assert.That(surface.Sdf.width, Is.GreaterThan(2));
                var second = new GameObject("AddedPart");
                second.transform.SetParent(root.transform, false);
                var box = second.AddComponent<BoxCollider2D>();
                box.enabled = false;
                var source = second.AddComponent<WaterContactSource>();
                source.SetContactColliders(box);
                source.BindTo(surface);
                yield return null;
                Assert.That(surface.Sources.Count, Is.EqualTo(2));
                UnityEngine.Object.Destroy(second);
                yield return null;
                yield return null;
                Assert.That(surface.Sources.Count, Is.EqualTo(1));
                count = surface.Generation;
                root.SetActive(false);
                yield return null;
                root.SetActive(true);
                yield return null;
                Assert.That(surface.Generation, Is.EqualTo(count));
            }
            finally { UnityEngine.Object.Destroy(root); }
            yield return null;
        }
        [UnityTest]
        public IEnumerator MeasureDirtyAndPeriodicRegeneration()
        {
            var allocationStart=GC.GetAllocatedBytesForCurrentThread(); var allocationProbe=new byte[1048576]; var allocationSupported=GC.GetAllocatedBytesForCurrentThread()>allocationStart; GC.KeepAlive(allocationProbe);
            var rows = new List<Sample>();
            var oldRun = UnityEngine.Application.runInBackground;
            UnityEngine.Application.runInBackground = true;
            var configurations = new[] { new[] { 128, 1 }, new[] { 256, 1 }, new[] { 512, 1 }, new[] { 1024, 1 }, new[] { 512, 16 } };
            foreach (var config in configurations)
            {
                var surface = Create(config[1], out var root, out var capsule);
                Resolution(surface, config[0]);
                Assert.That(surface.RefreshIfNeeded(), Is.True, surface.LastError);
                for (var warm = 0; warm < 4; warm++)
                {
                    Assert.That(surface.RefreshIfNeeded(true), Is.True, surface.LastError);
                    yield return null;
                }
                foreach (var interval in new[] { 0, 1, 2, 5, 10, 30 })
                {
                    const int frames = 90;
                    var call = new List<double>();
                    var frame = new List<double>();
                    var watch = new Stopwatch();
                    var beforeCount = surface.Generation;
                    double total = 0, extract = 0, raster = 0, distance = 0, upload = 0, profilerPeak = 0;
                    long bytes = 0;
                    using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "GreatVoyage.Waterline.Generate", 128);
                    for (var f = 0; f < frames; f++)
                    {
                        // Dirty-only has exactly two batches; periodic mode forces regeneration even when unchanged.
                        if (interval == 0 && (f == 15 || f == 60))
                        {
                            capsule.offset += Vector2.right * .01f;
                            for (var n = 0; n < 20; n++)
                                surface.NotifyGeometryChanged();
                        }
                        var regenerate = interval == 0 ? surface.IsDirty : f % interval == 0;
                        var allocated = GC.GetAllocatedBytesForCurrentThread();
                        watch.Restart();
                        var ok = surface.RefreshIfNeeded(interval > 0 && regenerate);
                        watch.Stop();
                        Assert.That(ok, Is.True, surface.LastError);
                        var ms = watch.Elapsed.TotalMilliseconds;
                        total += ms;
                        if (regenerate)
                        {
                            call.Add(ms);
                            bytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
                            extract += surface.Generator.ExtractMilliseconds;
                            raster += surface.Generator.RasterMilliseconds;
                            distance += surface.Generator.DistanceMilliseconds;
                            upload += surface.Generator.UploadMilliseconds;
                        }
                        yield return null;
                        frame.Add(Time.unscaledDeltaTime * 1000);
                        profilerPeak = Math.Max(profilerPeak, recorder.LastValue);
                    }
                    rows.Add(new Sample { policy = interval == 0 ? "Dirty batches at frames 15/60" : "Force every N frames", resolution = config[0], parts = config[1], interval = interval, frames = frames, generations = surface.Generation - beforeCount, width = surface.Sdf.width, height = surface.Sdf.height, callMedianMs = P(call, .5), callP95Ms = P(call, .95), callMaxMs = call.Max(), cpuMsPerFrame = total / frames, extractMeanMs = extract / call.Count, rasterMeanMs = raster / call.Count, distanceMeanMs = distance / call.Count, uploadMeanMs = upload / call.Count, allocatedBytesPerGeneration = allocationSupported ? (double)bytes / call.Count : -1, frameMedianMs = P(frame, .5), frameP95Ms = P(frame, .95), profilerPeakNs = profilerPeak });
                }
                UnityEngine.Object.Destroy(root);
                yield return null;
                yield return null;
            }
            UnityEngine.Application.runInBackground = oldRun;
            var report = new Report { environment = "Unity " + UnityEngine.Application.unityVersion + " / Editor PlayMode / " + SystemInfo.operatingSystem, device = SystemInfo.deviceModel, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, scope = "Isolated runtime Capsule scene; no file save/review textures. Stopwatch submission time is not GPU completion. 90 frames/case; Editor/test/VSync overhead in frame times. Dirty-only has 2 samples, not stable p95. Not standalone Player qualification. Allocation -1 means unavailable (counter self-probe); not zero-GC.", samples = rows.ToArray() };
            var dir = Path.Combine(UnityEngine.Application.dataPath, "../Agents/References/P5/Water/Stage4A_Revision");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "RuntimePerformance.json"), JsonUtility.ToJson(report, true));
        }
        private static double P(List<double> values, double q)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            return sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * q) - 1)];
        }
    }
}
