using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GreatVoyage.Tests
{
    public sealed class OceanBaseFlowGpuTests
    {
        [Test]
        public void HullFlowFollowsRelativeMotionAndLeavesBaseUnchanged()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback ||
                !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                Assert.Ignore("The Editor graphics device lacks the required compute format.");

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Game/Content/Art/Ocean/OceanFlow.compute");
            int clear = compute.FindKernel("ClearShipDelta");
            int emit = compute.FindKernel("EmitHullFlow");
            var baseFlow = new Texture2D(16, 16, TextureFormat.RGBAHalf, false, true);
            var delta = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                { enableRandomWrite = true };
            using var hull = new ComputeBuffer(4, sizeof(float) * 2);
            try
            {
                hull.SetData(new[] { new Vector2(-1,-2), new Vector2(1,-2),
                    new Vector2(1,2), new Vector2(-1,2) });
                Assert.That(delta.Create(), Is.True);
                compute.SetInts("_FieldResolution", 16, 16);
                compute.SetVector("_FieldOrigin", new Vector4(-8,-8,0,0));
                compute.SetVector("_FieldWorldSize", new Vector4(16,16,0,0));
                compute.SetInts("_ShipDispatchOffset", 0, 0);
                compute.SetInts("_ShipDispatchSize", 16, 16);
                compute.SetInt("_HullVertexCount", 4);
                compute.SetVector("_ShipPosition", Vector4.zero);
                compute.SetFloat("_ShipRadius", 3);
                compute.SetFloat("_SideFlowGain", .65f);
                compute.SetFloat("_ShipFoamEmission", .6f);
                compute.SetBuffer(emit, "_HullWorldVertices", hull);
                compute.SetTexture(emit, "_BaseFlowRead", baseFlow);
                compute.SetTexture(emit, "_ShipDeltaWrite", delta);
                compute.SetTexture(clear, "_ShipDeltaWrite", delta);

                var calm = Run(Vector2.zero, Vector2.zero, 0);
                Assert.That(At(calm, 9, 10).r, Is.Zero);
                Assert.That(At(calm, 9, 10).g, Is.Zero);

                var ahead = Run(Vector2.zero, Vector2.up, 0);
                Assert.That(At(ahead, 9, 10).r, Is.GreaterThan(.05f));
                Assert.That(At(ahead, 6, 10).r, Is.LessThan(-.05f));
                Assert.That(At(ahead, 9, 8).g, Is.LessThan(-.1f));
                Assert.That(At(ahead, 15, 15), Is.EqualTo(Color.clear));
                Assert.That(At(ahead, 8, 8), Is.EqualTo(Color.clear));

                var disabled = Run(Vector2.zero, Vector2.up, 0, 0);
                var boosted = Run(Vector2.zero, Vector2.up, 0, 4);
                Assert.That(At(disabled, 9, 10).r, Is.Zero);
                Assert.That(At(boosted, 9, 10).r, Is.EqualTo(4 * At(ahead, 9, 10).r).Within(.002f));

                var astern = Run(Vector2.zero, Vector2.down, 0);
                Assert.That(At(astern, 9, 5).r, Is.GreaterThan(.05f));
                Assert.That(At(astern, 6, 5).r, Is.LessThan(-.05f));
                Assert.That(At(astern, 9, 8).g, Is.GreaterThan(.1f));

                var turning = Run(Vector2.zero, Vector2.zero, 1);
                Assert.That(At(turning, 9, 8).g, Is.LessThan(-.1f));
                Assert.That(At(turning, 6, 8).g, Is.GreaterThan(.1f));

                var cross = Run(Vector2.right, Vector2.zero, 0);
                Assert.That(At(cross, 6, 8).r, Is.LessThan(-.1f));
                Assert.That(At(cross, 9, 8).r, Is.Zero);
            }
            finally
            {
                delta.Release();
                Object.DestroyImmediate(delta);
                Object.DestroyImmediate(baseFlow);
            }

            Color At(Color[] field, int x, int y) => field[y * 16 + x];

            Color[] Run(Vector2 water, Vector2 velocity, float angularVelocity, float strength = 1)
            {
                var input = new Color[256];
                System.Array.Fill(input, new Color(water.x, water.y, water.magnitude, 0));
                baseFlow.SetPixels(input);
                baseFlow.Apply();
                compute.SetVector("_ShipVelocity", new Vector4(velocity.x, velocity.y, 0, 0));
                compute.SetFloat("_ShipAngularVelocity", angularVelocity);
                compute.SetFloat("_ShipFlowStrength", strength);
                compute.Dispatch(clear, 2, 2, 1);
                compute.Dispatch(emit, 2, 2, 1);
                CollectionAssert.AreEqual(input, Read(baseFlow));
                return Read(delta);
            }

            Color[] Read(Texture field)
            {
                var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat);
                request.WaitForCompletion();
                Assert.That(request.hasError, Is.False);
                return request.GetData<Color>().ToArray();
            }
        }
        [Test]
        public void WaveTransportAccumulatesLocalFlowAndResetsOnlyTheHiddenChannel()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback ||
                !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                Assert.Ignore("The Editor graphics device lacks the required compute format.");

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Game/Content/Art/Ocean/OceanFlow.compute");
            int clear = compute.FindKernel("ClearWave");
            int transport = compute.FindKernel("TransportWave");
            var velocity = new Texture2D(8, 8, TextureFormat.RGBAHalf, false, true);
            var a = Field();
            var b = Field();
            try
            {
                compute.SetInts("_FieldResolution", 8, 8);
                compute.SetVector("_FieldOrigin", Vector4.zero);
                compute.SetVector("_FieldWorldSize", new Vector4(8, 8, 0, 0));
                compute.SetTexture(clear, "_WaveWrite", a);
                compute.Dispatch(clear, 1, 1, 1);
                Step(1, 1, 0);
                var first = Read();
                Assert.That(first[36].r, Is.EqualTo(-.22).Within(.002));
                Assert.That(first[35].r, Is.Zero);
                Step(-1, .01f, 2);
                var second = Read();
                Assert.That(second[36].r, Is.EqualTo(first[36].r + .0022).Within(.004));
                Assert.That(second[36].b, Is.Zero);

                if (!SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.RGFloat))
                    Assert.Ignore("The Editor graphics device lacks RGFloat random write support.");

                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                a = Field(RenderTextureFormat.RGFloat);
                b = Field(RenderTextureFormat.RGFloat);
                                compute.SetTexture(clear, "_WaveWrite", a);
                compute.Dispatch(clear, 1, 1, 1);
                // Cross several wave reset times using the light path (reset mask zero).
                for (int i = 0; i < 64; i++)
                    Step(1, .25f, 0, uniform: true);
                var forward = Read()[36];
                Assert.That(forward.r, Is.EqualTo(-3.52f).Within(.04f));

                for (int i = 0; i < 64; i++)
                    Step(-1, .25f, 0, uniform: true);
                Assert.That(Read()[36].r, Is.EqualTo(0f).Within(.04f));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(velocity);
            }

            RenderTexture Field(RenderTextureFormat format = RenderTextureFormat.ARGBHalf)
            {
                var result = new RenderTexture(8, 8, 0, format, RenderTextureReadWrite.Linear)
                {
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                Assert.That(result.Create(), Is.True);
                return result;
            }

            void Step(float speed, float dt, int reset, bool uniform = false)
            {
                var pixels = new Color[64];
                var flow = new Color(speed, 0, 0, 0);
                if (uniform)
                    System.Array.Fill(pixels, flow);
                else
                    pixels[36] = flow;
                velocity.SetPixels(pixels);
                velocity.Apply();
                compute.SetTexture(transport, "_FinalFlowRead", velocity);
                compute.SetTexture(transport, "_WaveRead", a);
                compute.SetTexture(transport, "_WaveWrite", b);
                compute.SetFloat("_WaveStep", dt);
                compute.SetFloat("_WaveFlowResponse", .22f);
                compute.SetInt("_WaveReset", reset);
                compute.Dispatch(transport, 1, 1, 1);
                (a, b) = (b, a);
            }

            Color[] Read()
            {
                var request = AsyncGPUReadback.Request(a, 0, TextureFormat.RGBAFloat);
                request.WaitForCompletion();
                Assert.That(request.hasError, Is.False);
                return request.GetData<Color>().ToArray();
            }
        }
        [Test]
        public void BuildBaseFlowBlendsAcrossCellBoundaryAndReplaysTheSameSeed()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback ||
                !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                Assert.Ignore("The Editor graphics device lacks the required compute format.");

            const string path = "Assets/Game/Content/Art/Ocean/OceanFlow.compute";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            Assert.That(compute, Is.Not.Null);
            var kernel = compute.FindKernel("BuildBaseFlow");
            var cells = new Texture2D(2, 1, TextureFormat.RGBAHalf, false, true);
            var field = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear) { enableRandomWrite = true };
            try
            {
                cells.SetPixels(new[] { new Color(1, 0, 2, .05f), new Color(0, 1, 2, .05f) });
                cells.Apply();
                Assert.That(field.Create(), Is.True);
                compute.SetInts("_DataResolution", 2, 1);
                compute.SetInts("_FieldResolution", 8, 8);
                compute.SetVector("_DataOrigin", Vector4.zero);
                compute.SetVector("_FieldOrigin", new Vector4(96, 46, 0, 0));
                compute.SetVector("_FieldWorldSize", new Vector4(8, 8, 0, 0));
                compute.SetFloat("_TileWorldSize", 100);
                compute.SetFloat("_NoiseFrequency", .025f);
                compute.SetInt("_NoiseSeed", 1337);
                compute.SetTexture(kernel, "_LowResolutionData", cells);
                compute.SetTexture(kernel, "_BaseFlowWrite", field);

                var first = ReadField();
                var left = first[35];
                var right = first[36];
                Assert.That(left.r, Is.GreaterThan(.9f));
                Assert.That(left.g, Is.GreaterThan(.9f));
                Assert.That(Mathf.Abs(left.r - right.r), Is.LessThan(.05f));
                Assert.That(Mathf.Abs(left.g - right.g), Is.LessThan(.05f));
                Assert.That(left.b, Is.EqualTo(new Vector2(left.r, left.g).magnitude).Within(.002f));
                CollectionAssert.AreEqual(first, ReadField());

                cells.SetPixels(new[] { new Color(1, 0, 1, 0), new Color(-1, 0, 1, 0) });
                cells.Apply();
                var opposing = ReadField();
                for (int x = 0; x < 8; x++)
                {
                    float expected = (3.5f - x) * .02f;
                    Assert.That(opposing[32 + x].r, Is.EqualTo(expected).Within(.001f));
                    Assert.That(opposing[32 + x].g, Is.Zero);
                }

                cells.SetPixels(new[] { new Color(1, 0, 2, 0), new Color(-1, 0, 1, 0) });
                cells.Apply();
                Assert.That(ReadField()[36].r, Is.EqualTo(.485f).Within(.001f));
            }
            finally
            {
                field.Release();
                Object.DestroyImmediate(field);
                Object.DestroyImmediate(cells);
            }

            Color[] ReadField()
            {
                compute.Dispatch(kernel, 1, 1, 1);
                var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat);
                request.WaitForCompletion();
                Assert.That(request.hasError, Is.False);
                return request.GetData<Color>().ToArray();
            }
        }
    }
}
