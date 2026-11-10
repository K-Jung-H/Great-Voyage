#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using GreatVoyage.Application;
using GreatVoyage.Domain;
using GreatVoyage.Ocean;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace GreatVoyage.Tests
{
    public sealed class OceanSessionPlayTests
    {
        private sealed class Queries : IInteractionQueries
        {
            public InteractionSelection? Resolve(WorldPosition p, string id) => null;
            public bool IsStillValid(WorldPosition p, InteractionSelection s) => false;
        }

        private sealed class Store : ISessionStore
        {
            public bool TrySave(GameSessionState state, out string error) { error = ""; return true; }
        }

        [UnityTest]
        public IEnumerator OceanProjectsNavigationAndFreezesHistoryWhilePaused()
        {
            if (!SystemInfo.supportsComputeShaders || !SystemInfo.supportsAsyncGPUReadback ||
                !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                Assert.Ignore("Required GPU features are unavailable.");

            var root = new GameObject("W1 isolated ocean");
            root.SetActive(false);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.transform.SetParent(root.transform, false);
            var shipObject = new GameObject("W1 synthetic hull");
            shipObject.transform.SetParent(root.transform, false);
            var hull = shipObject.AddComponent<PolygonCollider2D>();
            hull.SetPath(0, new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(0, 1) });
            var ship = shipObject.AddComponent<OceanShip>();
            Set(ship, "hull", hull);
            var data = ScriptableObject.CreateInstance<OceanDataMap>();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Content/Art/Ocean/StylizedOcean.shader");
            var material = new Material(shader);
            material.SetFloat("_DebugMode", 1);
            var renderer = surface.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            surface.layer = 31;
            var cameraObject = new GameObject("W1 isolated camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -5);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1;
            camera.cullingMask = 1 << surface.layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var cameraOutput = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear);
            camera.targetTexture = cameraOutput;
            var opposingFlow = new Texture2D(2, 1, TextureFormat.RGBAHalf, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var state = new GameSessionState("ocean-test", 17,
                new CharacterState("player", new WorldPosition("upper-deck", 0, 0), Facing.Back),
                new GameClock(0));
            using var session = new GameSession(state, new Queries(), new SaveCoordinator(new Store()));
            try
            {
                data.Configure(new Vector2(-8, -8), Vector2Int.one, 16);
                data.SetCell(0, 0, new OceanTileCell(Vector2.right, 2, 0));
                var flow = root.AddComponent<OceanFlowSystem>();
                Set(flow, "dataMap", data);
                Set(flow, "flowCompute", AssetDatabase.LoadAssetAtPath<ComputeShader>(
                    "Assets/Game/Content/Art/Ocean/OceanFlow.compute"));
                Set(flow, "oceanSurfaceRenderer", renderer);
                Set(flow, "deckAnchor", root.transform);
                Set(flow, "deckBowDegrees", 180f);
                Set(flow, "navigationShip", ship);
                Set(flow, "fieldWidth", 16);
                Set(flow, "fieldHeight", 16);
                Set(flow, "fieldWorldOrigin", new Vector2(-8, -8));
                Set(flow, "fieldWorldSize", new Vector2(16, 16));
                flow.BindSession(session);
                root.SetActive(true);
                state.Navigation.Depart(state.PlayId, .002);
                session.Tick(.25);
                yield return null;
                yield return null;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_OceanTime"), Is.EqualTo(.25f).Within(.001f));
                Assert.That(block.GetMatrix("_OceanWorldToField").MultiplyPoint3x4(Vector3.zero).x,
                    Is.EqualTo(state.Navigation.X * 10).Within(.001));
                Assert.That(block.GetMatrix("_OceanWorldToField").MultiplyVector(Vector3.left).x,
                    Is.EqualTo(-1).Within(.001));
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(ship.WorldPosition.x, Is.EqualTo(state.Navigation.X * 10).Within(.001));
                var basePixels = Read(flow.BaseFlowMap);
                var finalPixels = Read(flow.FinalFlowMap);
                Assert.That(basePixels[0].r, Is.EqualTo(2).Within(.002));
                var surfacePixel = Read(cameraOutput)[16 * 32 + 16];
                Assert.That(surfacePixel.r, Is.GreaterThan(.8f));
                Assert.That(surfacePixel.g, Is.InRange(.35f, .7f));
                Assert.That(surfacePixel.b, Is.InRange(.25f, .65f));
                Assert.That(finalPixels, Is.Not.EqualTo(basePixels));
                var history = flow.FoamHistory;
                var foam = Read(history);
                Assert.That(System.Array.Exists(foam, p => p.r > 0), Is.True);

                session.OpenMenu();
                session.Tick(10);
                yield return null;
                yield return null;
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_OceanTime"), Is.EqualTo(.25f).Within(.001f));
                Assert.That(flow.FoamHistory, Is.SameAs(history));
                CollectionAssert.AreEqual(foam, Read(history));

                session.CloseMenu();
                session.Tick(.25);
                yield return null;
                yield return null;
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_OceanTime"), Is.EqualTo(.5f).Within(.001f));
                Assert.That(flow.FoamHistory, Is.Not.SameAs(history));
                CollectionAssert.AreEqual(basePixels, Read(flow.BaseFlowMap));

                // Render the real surface at a distant world origin across opposing flows.
                flow.enabled = false;
                opposingFlow.SetPixels(new[] { new Color(1, 0, 0, 0), new Color(-1, 0, 0, 0) });
                opposingFlow.Apply();
                material.SetFloat("_DebugMode", 0);
                material.SetFloat("_WaveSpeed", 1);
                surface.transform.localScale = new Vector3(32, 32, 1);
                camera.orthographicSize = 16;
                block.Clear();
                block.SetTexture("_OceanFinalFlowMap", opposingFlow);
                block.SetMatrix("_OceanWorldToField", Matrix4x4.Translate(new Vector3(1000, 0, 0)));
                block.SetVector("_FieldOrigin", new Vector4(984, -16, 0, 0));
                block.SetVector("_FieldWorldSize", new Vector4(32, 32, 0, 0));
                block.SetFloat("_OceanTime", 3.999f);
                renderer.SetPropertyBlock(block);
                yield return null;
                yield return null;
                var beforeWrap = Read(cameraOutput);
                Assert.That(Mathf.Abs(beforeWrap[16 * 32 + 15].g - beforeWrap[16 * 32 + 16].g),
                    Is.LessThan(.04f));

                block.SetFloat("_OceanTime", 4.001f);
                renderer.SetPropertyBlock(block);
                yield return null;
                yield return null;
                var afterWrap = Read(cameraOutput);
                Assert.That(MaxGreenDifference(beforeWrap, afterWrap), Is.LessThan(.005f));

                block.SetFloat("_OceanTime", 5);
                renderer.SetPropertyBlock(block);
                yield return null;
                yield return null;
                Assert.That(MaxGreenDifference(afterWrap, Read(cameraOutput)), Is.GreaterThan(.02f));
            }
            finally
            {
                camera.targetTexture = null;
                cameraOutput.Release();
                Object.Destroy(cameraOutput);
                Object.Destroy(root);
                Object.Destroy(data);
                Object.Destroy(material);
                Object.Destroy(opposingFlow);
            }
        }

        [UnityTest]
        public IEnumerator StartingHullProjectsItsBowWithoutPhysicsContacts()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Required GPU features are unavailable.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Content/Prefabs/World/OceanStartingHull.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                var ship = instance.GetComponent<OceanShip>();
                var collider = instance.GetComponent<PolygonCollider2D>();
                Assert.That(collider.enabled, Is.False);
                Assert.That(collider.pathCount, Is.EqualTo(1));
                Assert.That(collider.OverlapPoint(Vector2.zero), Is.False);
                Assert.That(ship.PrepareHull(), Is.True);
                Assert.That(ship.VertexCount, Is.EqualTo(10));
                Assert.That(ship.WorldBounds.width, Is.InRange(4.2f, 4.4f));
                Assert.That(ship.WorldBounds.height, Is.InRange(13.4f, 13.7f));
                var vertices = new Vector2[ship.VertexCount];
                ship.VertexBuffer.GetData(vertices);
                Assert.That(vertices[0].y, Is.EqualTo(ship.WorldBounds.yMax).Within(.001f));

                var navigation = new NavigationState("starting-ship", "starting-port", "", 0,
                    2, 3, 0, 0, 0);
                ship.ProjectNavigation(navigation, 10);
                Assert.That(ship.PrepareHull(), Is.True);
                ship.VertexBuffer.GetData(vertices);
                Assert.That(ship.WorldPosition, Is.EqualTo(new Vector2(20, 30)));
                Assert.That(Vector2.Dot(ship.Forward, Vector2.right), Is.GreaterThan(.999f));
                Assert.That(vertices[0].x, Is.EqualTo(ship.WorldBounds.xMax).Within(.001f));
                Assert.That(ship.WorldBounds.width, Is.InRange(13.4f, 13.7f));
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static Color[] Read(RenderTexture field)
        {
            var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            Assert.That(request.hasError, Is.False);
            return request.GetData<Color>().ToArray();
        }

        private static float MaxGreenDifference(Color[] a, Color[] b)
        {
            float largest = 0;
            for (int i = 0; i < a.Length; i++)
                largest = Mathf.Max(largest, Mathf.Abs(a[i].g - b[i].g));
            return largest;
        }
    }
}
#endif
