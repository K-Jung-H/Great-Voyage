using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GreatVoyage.Tests
{
    public sealed class OceanLightAreaGpuTests
    {
        private ComputeShader compute;
        private RenderTexture pattern;
        [SetUp]
        public void SetUp()
        {
            if(!SystemInfo.supportsComputeShaders) Assert.Ignore("Compute shaders required.");
            compute=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/Game/Content/Art/Ocean/OceanFlow.compute"));
            pattern=new RenderTexture(512,512,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                {enableRandomWrite=true,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat};
            pattern.Create();
            Bake();
        }
        private void Bake()
        {
            int kernel=compute.FindKernel("BakeLightArea");
            compute.SetTexture(kernel,"_LightPatternWrite",pattern);
            compute.Dispatch(kernel,64,64,1);
        }
        private static Color[] Read(RenderTexture rt)
        {
            var old=RenderTexture.active;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);
            try {RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);return texture.GetPixels();}
            finally {RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(texture);}
        }
        [Test]
        public void FixedPatternStoresNarrowEdgesAndBoundedJunctions()
        {
            var a=Read(pattern);
            compute.SetFloat("_OceanTime",1234); Bake();
            CollectionAssert.AreEqual(a,Read(pattern));
            int lit=0; float minEdge=float.PositiveInfinity, minJ=float.PositiveInfinity, maxJ=0;
            foreach(var p in a)
            {
                minEdge=Mathf.Min(minEdge,p.r);
                minJ=Mathf.Min(minJ,p.g); maxJ=Mathf.Max(maxJ,p.g);
                if(p.r<.028f*p.g*p.b) lit++;
            }
            Assert.That(minEdge,Is.GreaterThanOrEqualTo(0));
            Assert.That(minJ,Is.GreaterThanOrEqualTo(1));
            Assert.That(maxJ,Is.LessThanOrEqualTo(1.951f));
            Assert.That(lit/(float)a.Length,Is.InRange(.03f,.30f));
        }
        [TestCase(.4f)]
        [TestCase(-.4f)]
        public void WakeDoesNotEraseLightArea(float wakeHeight)
        {
            var material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Content/Art/Ocean/StylizedOcean.shader"));
            var boat=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
            boat.SetPixel(0,0,new Color(wakeHeight,0,0,0));boat.Apply();
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
            var output=new RenderTexture(128,128,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            output.Create();
            try {
                material.SetTexture("_OceanLightPattern",pattern);
                material.SetFloat("_OceanLightReady",1);
                material.SetFloat("_OceanTime",2);
                material.SetTexture("_OceanBoatFlowMap",boat);
                material.SetTexture("_OceanWaveOffsets",Texture2D.blackTexture);
                material.SetTexture("_OceanFoamMap",Texture2D.blackTexture);
                material.SetMatrix("_OceanWorldToField",Matrix4x4.identity);
                material.SetVector("_FieldOrigin",new Vector4(-32,-32,0,0));
                material.SetVector("_FieldWorldSize",new Vector4(64,64,0,0));
                material.SetColor("_CrestColor",Color.white);
                var off=Draw(0);var on=Draw(1);
                double change=0;
                for(int i=0;i<on.Length;i++) change+=Math.Abs(on[i].g-off[i].g);
                Assert.That(change/on.Length,Is.GreaterThan(.002),
                    "Signed Boat Height must distort/attenuate, never erase all light.");
                Color[] Draw(float strength)
                {
                    material.SetFloat("_CellHighlights",strength);
                    using(var cb=new CommandBuffer()) {
                        cb.SetRenderTarget(output);cb.ClearRenderTarget(false,true,Color.black);
                        cb.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-32,32,-32,32,-1,1),true));
                        cb.DrawMesh(quad.GetComponent<MeshFilter>().sharedMesh,Matrix4x4.Scale(new Vector3(64,64,1)),material);
                        Graphics.ExecuteCommandBuffer(cb);
                    }
                    return Read(output);
                }
            }
            finally {output.Release();UnityEngine.Object.DestroyImmediate(output);UnityEngine.Object.DestroyImmediate(quad);
                UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(boat);}
        }
        [TearDown]
        public void TearDown()
        {
            if(pattern!=null){pattern.Release();UnityEngine.Object.DestroyImmediate(pattern);}
            if(compute!=null) UnityEngine.Object.DestroyImmediate(compute);
        }
    }
}
