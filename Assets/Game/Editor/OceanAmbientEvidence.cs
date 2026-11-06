using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GreatVoyage.Editor
{
    public static class OceanAmbientEvidence
    {
        private static string evidence="Agents/References/P5/Water/Stage2B/";
        [Serializable] private sealed class Sample { public string file; public double wallSeconds,visualSeconds; }
        [Serializable] private sealed class Recording
        {
            public string context="Actual Unity Game View screenshots; no generated/interpolated frames; ambient only";
            public int frames,width,height; public double duration,visualDuration;
            public Sample[] samples;
        }
        private static readonly List<Sample> samples=new List<Sample>();
        private static OceanAmbientDriver driver;
        private static string frameDirectory;
        private static double started,nextCapture;
        private static double duration=12;
        private static bool recording;

        public static string Begin(string outputDirectory="Agents/References/P5/Water/Stage2B/",double durationSeconds=12)
        {
            if(!EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=OceanAmbientReviewAuthoring.ScenePath)
                throw new InvalidOperationException("Play the ambient review scene first.");
            if(recording)throw new InvalidOperationException("Already recording.");
            var requested=Path.GetFullPath(outputDirectory);
            var allowed=Path.GetFullPath("Agents/References/P5/Water")+Path.DirectorySeparatorChar;
            if(!requested.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Use the water evidence directory.");
            if(double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds<1 || durationSeconds>60)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            evidence=outputDirectory.TrimEnd('/','\\')+"/";duration=durationSeconds;
            driver=UnityEngine.Object.FindAnyObjectByType<OceanAmbientDriver>();
            driver.Paused=false;driver.SetPreviewTime(0);
            samples.Clear();frameDirectory=Path.Combine("Temp","OceanAmbientCapture",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(frameDirectory);Directory.CreateDirectory(evidence);
            started=EditorApplication.timeSinceStartup;nextCapture=started;recording=true;
            EditorApplication.update+=Capture;
            return "Recording actual ambient Game View for "+duration+" seconds.";
        }
        private static void Capture()
        {
            if(!EditorApplication.isPlaying || driver==null)
            { EditorApplication.update-=Capture;recording=false;return; }
            var now=EditorApplication.timeSinceStartup;
            if(now<nextCapture)return;
            var sample=new Sample{file=Path.Combine(frameDirectory,"frame_"+samples.Count.ToString("D4")+".png"),
                wallSeconds=now-started,visualSeconds=driver.ElapsedSeconds};
            ScreenCapture.CaptureScreenshot(sample.file);samples.Add(sample);nextCapture=now+1.0/15;
            if(now-started>=duration)
            {
                driver.Paused=true;EditorApplication.update-=Capture;recording=false;
                var report=new Recording{frames=samples.Count,width=Screen.width,height=Screen.height,
                    duration=sample.wallSeconds,visualDuration=sample.visualSeconds,samples=samples.ToArray()};
                File.WriteAllText(evidence+"Recording.json",JsonUtility.ToJson(report,true));
            }
        }
        public static string Status() => recording ? "recording "+samples.Count+" frames" : "stopped "+samples.Count+" frames";

        public static object Compare(string first,string second)
        {
            var a=new Texture2D(2,2);var b=new Texture2D(2,2);
            try
            {
                a.LoadImage(File.ReadAllBytes(first));b.LoadImage(File.ReadAllBytes(second));
                if(a.width!=b.width || a.height!=b.height)throw new InvalidOperationException("Image dimensions differ.");
                var pa=a.GetPixels32();var pb=b.GetPixels32();long changed=0,total=0;int maximum=0;
                for(var i=0;i<pa.Length;i++)
                {
                    var dr=Math.Abs(pa[i].r-pb[i].r);var dg=Math.Abs(pa[i].g-pb[i].g);var db=Math.Abs(pa[i].b-pb[i].b);
                    if(dr+dg+db!=0)changed++;total+=dr+dg+db;maximum=Math.Max(maximum,Math.Max(dr,Math.Max(dg,db)));
                }
                return new {pixels=pa.Length,changedPixels=changed,maximumChannelDifference=maximum,meanAbsoluteChannelDifference=total/(3.0*pa.Length)};
            }
            finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        }

        public static string Encode()
        {
            if(!recording && samples.Count==0 && File.Exists(evidence+"Recording.json"))
                samples.AddRange(JsonUtility.FromJson<Recording>(File.ReadAllText(evidence+"Recording.json")).samples);
            if(recording || samples.Count<2)throw new InvalidOperationException("Wait for recording completion.");
            if(samples.Last().visualSeconds-samples.First().visualSeconds<1)
                throw new InvalidOperationException("The visual clock did not advance; focus Game View and record again.");
            if(samples.Any(s=>!File.Exists(s.file)))throw new InvalidOperationException("Waiting for pending screenshot writes.");
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            Texture2D encoderFrame=null;
            try
            {
                texture.LoadImage(File.ReadAllBytes(samples[0].file));
                encoderFrame=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
                var attributes=new VideoTrackAttributes{frameRate=new MediaRational(15),width=(uint)texture.width,
                    height=(uint)texture.height,includeAlpha=false};
                if(Enum.GetNames(typeof(VideoBitrateMode)).Contains("High"))
                    attributes.bitRateMode=(VideoBitrateMode)Enum.Parse(typeof(VideoBitrateMode),"High");
                using(var encoder=new MediaEncoder(evidence+"OceanAmbient.mp4",attributes))
                {
                    var origin=samples[0].wallSeconds;
                    foreach(var sample in samples)
                    {
                        texture.LoadImage(File.ReadAllBytes(sample.file));
                        encoderFrame.SetPixels32(texture.GetPixels32());encoderFrame.Apply(false);
                        var timestamp=new MediaTime((long)Math.Round((sample.wallSeconds-origin)*1000000),1000000,1);
                        if(!encoder.AddFrame(encoderFrame,timestamp))throw new InvalidOperationException("Video encoder rejected frame.");
                    }
                }
                File.Copy(samples[0].file,evidence+"AmbientStart.png",true);
                File.Copy(samples[samples.Count/2].file,evidence+"AmbientMiddle.png",true);
                File.Copy(samples[samples.Count-1].file,evidence+"AmbientEnd.png",true);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture);if(encoderFrame!=null)UnityEngine.Object.DestroyImmediate(encoderFrame); }
            return "Encoded "+samples.Count+" actual Game View frames using their capture timestamps.";
        }
        public static string EncodeRecording(string directory)
        {
            if(recording)throw new InvalidOperationException("Wait for recording completion.");
            var path=Path.GetFullPath(directory);
            if(!path.StartsWith(Path.GetFullPath("Agents/References/P5/Water")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Use the water evidence directory.");
            evidence=directory.TrimEnd('/','\\')+"/";samples.Clear();
            return Encode();
        }
    }
}
