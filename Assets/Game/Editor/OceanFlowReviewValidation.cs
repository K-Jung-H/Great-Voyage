using System;
using System.Collections.Generic;
using System.IO;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GreatVoyage.Editor
{
    public static class OceanFlowReviewValidation
    {
        [Serializable] private sealed class Sample
        { public double visualSeconds;public float shipX,shipY,waterX,waterY,offsetX,offsetY; }
        [Serializable] private sealed class Report
        {
            public string context="Synthetic visual test input only: stopped 0-4s, acceleration 4-8s, cruise 8-12s, turn 12-16s, deceleration 16-20s, stopped 20-24s; not game acceleration commands";
            public Sample[] samples;
        }
        private static OceanAmbientDriver driver;
        private static OceanRelativeFlow flow;
        private static int lastFrame;
        private static readonly List<Sample> samples=new List<Sample>();
        public static string Begin()
        {
            if(!EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=OceanAmbientReviewAuthoring.ScenePath)
                throw new InvalidOperationException("Play the ambient review scene.");
            driver=UnityEngine.Object.FindAnyObjectByType<OceanAmbientDriver>();
            driver.UseExternalClock=true;flow=new OceanRelativeFlow();samples.Clear();lastFrame=-1;
            OceanAmbientEvidence.Begin("Agents/References/P5/Water/Stage3/FlowComparison",24);
            EditorApplication.update-=Advance;EditorApplication.update+=Advance;
            return "24-second synthetic flow comparison in actual Game View; gameplay steering is validated separately.";
        }
        private static void Advance()
        {
            if(!EditorApplication.isPlaying || driver==null){EditorApplication.update-=Advance;return;}
            if(driver.Paused)
            {
                EditorApplication.update-=Advance;
                File.WriteAllText("Agents/References/P5/Water/Stage3/FlowComparison/FlowSamples.json",
                    JsonUtility.ToJson(new Report{samples=samples.ToArray()},true));return;
            }
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            var t=driver.ElapsedSeconds;var dt=Time.unscaledDeltaTime;
            var speed=t<4 ? 0 : t<8 ? (t-4)/4*1.152 : t<16 ? 1.152 : t<20 ? (20-t)/4*1.152 : 0;
            var heading=(t<12 ? 180 : t<16 ? 180-(t-12)*22.5 : 90)*Math.PI/180;
            var vx=speed*Math.Cos(heading);var vy=speed*Math.Sin(heading);
            flow.Advance(vx*dt,vy*dt,dt,1,.75);driver.AdvanceExternal(dt,flow.SamplingOffset);
            samples.Add(new Sample{visualSeconds=t,shipX=(float)vx,shipY=(float)vy,waterX=flow.WaterVelocity.x,
                waterY=flow.WaterVelocity.y,offsetX=flow.SamplingOffset.x,offsetY=flow.SamplingOffset.y});
        }
    }
}
