using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace GreatVoyage.Editor
{
    public static class ColliderWaterlineValidation
    {
        private const string Evidence = "Agents/References/P5/Water/Stage4A_Revision/";
        private static void Require(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
        }
        private static bool Inside(WaterlineGenerator g, Vector2 p)
        {
            if (!g.Bounds.Contains(p))
                return false;
            var tex = g.Texture;
            var x = Mathf.Clamp((int)((p.x - g.Bounds.xMin) / g.Bounds.width * tex.width), 0, tex.width - 1);
            var y = Mathf.Clamp((int)((p.y - g.Bounds.yMin) / g.Bounds.height * tex.height), 0, tex.height - 1);
            return tex.GetPixelData<float>(0)[y * tex.width + x] < 0;
        }
        private static GameObject Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            go.transform.SetParent(parent, false);
            return go;
        }
        private static void Write(string name, object data)
        {
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + name, Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
        }
        public static object TestAllTypes()
        {
            var results = new List<object>();
            foreach (var kind in new[] { "Box", "Circle", "Capsule", "Polygon", "Edge", "Composite", "Tilemap", "Custom" })
            {
                var scene = EditorSceneManager.NewPreviewScene();
                var assets = new List<UnityEngine.Object>();
                using var g = new WaterlineGenerator();
                try
                {
                    var root = new GameObject("ProbeShip");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    var go = Child("ContactPart_" + kind, root.transform);
                    Collider2D collider;
                    switch (kind)
                    {
                        case "Box":
                            var box = go.AddComponent<BoxCollider2D>();
                            box.size = new Vector2(2, 1);
                            collider = box;
                            break;
                        case "Circle":
                            var circle = go.AddComponent<CircleCollider2D>();
                            circle.radius = .7f;
                            collider = circle;
                            break;
                        case "Capsule":
                            var capsule = go.AddComponent<CapsuleCollider2D>();
                            capsule.size = new Vector2(1, 3);
                            collider = capsule;
                            break;
                        case "Polygon":
                            var poly = go.AddComponent<PolygonCollider2D>();
                            poly.points = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
                            collider = poly;
                            break;
                        case "Edge":
                            var edge = go.AddComponent<EdgeCollider2D>();
                            edge.points = new[] { new Vector2(-1, 0), new Vector2(1, 0) };
                            edge.edgeRadius = 0;
                            collider = edge;
                            break;
                        case "Composite":
                            var body = go.AddComponent<Rigidbody2D>();
                            body.bodyType = RigidbodyType2D.Static;
                            body.simulated = false;
                            var composite = go.AddComponent<CompositeCollider2D>();
                            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                            composite.generationType = CompositeCollider2D.GenerationType.Manual;
                            foreach (var x in new[] { -.4f, .4f })
                            {
                                var part = Child("Contributor", go.transform);
                                part.transform.localPosition = Vector3.right * x;
                                var c = part.AddComponent<BoxCollider2D>();
                                c.size = Vector2.one * 1.2f;
                                c.compositeOperation = Collider2D.CompositeOperation.Merge;
                                c.enabled = false;
                            }
                            collider = composite;
                            break;
                        case "Tilemap":
                            root.AddComponent<Grid>();
                            var map = go.AddComponent<Tilemap>();
                            var tile = ScriptableObject.CreateInstance<Tile>();
                            tile.name = "GridTile";
                            tile.colliderType = Tile.ColliderType.Grid;
                            assets.Add(tile);
                            map.SetTile(new Vector3Int(-1, -1, 0), tile);
                            map.SetTile(Vector3Int.zero, tile);
                            map.RefreshAllTiles();
                            collider = go.AddComponent<TilemapCollider2D>();
                            break;
                        default:
                            var custom = go.AddComponent<CustomCollider2D>();
                            var group = new PhysicsShapeGroup2D();
                            group.AddCircle(Vector2.zero, .6f);
                            group.AddBox(new Vector2(.7f, 0), new Vector2(.6f, 1));
                            custom.SetCustomShapes(group);
                            collider = custom;
                            break;
                    }
                    collider.enabled = false;
                    var source = ColliderWaterlineReviewAuthoring.Assign(go, collider);
                    var sources = new[] { source };
                    g.Generate(root.transform, sources, 512, 1);
                    var sample = kind == "Tilemap" ? new Vector2(.5f, .5f) : Vector2.zero;
                    Require(Inside(g, sample), kind + " missing known interior.");
                    Require(!Inside(g, new Vector2(100, 100)), "Distant point included.");
                    var before = g.Texture.GetPixelData<float>(0).ToArray();
                    go.layer = 5;
                    root.transform.position = new Vector3(20, -7, 0);
                    root.transform.rotation = Quaternion.Euler(0, 0, 29);
                    g.Generate(root.transform, sources, 512, 1);
                    Require(g.Texture.GetPixelData<float>(0).ToArray().SequenceEqual(before), "Root movement/Layer changed local SDF.");
                    Require(WaterlineGeometry.PhysicallyInactive(collider), "Original physics activated.");
                    results.Add(new
                    {
                        type = kind,
                        passed = true,
                        width = g.Texture.width,
                        height = g.Texture.height
                    });
                }
                catch (Exception ex) { results.Add(new { type = kind, passed = false, error = ex.ToString() }); }
                finally { EditorSceneManager.ClosePreviewScene(scene); foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset); }
            }
            Write("ColliderTypes.json", results);
            return results;
        }
        public static object TestBuildStrip()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("VisibleHull");
                SceneManager.MoveGameObjectToScene(root, scene);
                var renderer = root.AddComponent<SpriteRenderer>();
                var water = root.AddComponent<PolygonCollider2D>();
                water.enabled = false;
                var gameplay = root.AddComponent<BoxCollider2D>();
                var source = ColliderWaterlineReviewAuthoring.Assign(root, water);
                WaterContactBuildValidator.Strip(scene);
                Require(root != null && renderer != null && gameplay != null && gameplay.enabled && source != null && water != null && !water.enabled, "Build validation changed runtime definitions or visuals.");
                var result = new
                {
                    passed = true,
                    runtimeSourceAndColliderPreserved = true,
                    gameplayPreserved = true,
                    actualPlayerBuild = "Not run"
                };
                Write("BuildRetention.json", result);
                return result;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static object TestCompositeSemantics()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            using var g = new WaterlineGenerator();
            try
            {
                var root = new GameObject("CompositeShip");
                SceneManager.MoveGameObjectToScene(root, scene);
                var part = Child("RingHull", root.transform);
                var body = part.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                body.simulated = false;
                var composite = part.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.generationType = CompositeCollider2D.GenerationType.Manual;
                ColliderWaterlineReviewAuthoring.Assign(part, composite);
                var outer = Child("Outer", part.transform).AddComponent<BoxCollider2D>();
                outer.size = Vector2.one * 4;
                outer.compositeOperation = Collider2D.CompositeOperation.Merge;
                outer.compositeOrder = 0;
                outer.enabled = false;
                var hole = Child("Hole", part.transform).AddComponent<BoxCollider2D>();
                hole.size = Vector2.one;
                hole.compositeOperation = Collider2D.CompositeOperation.Difference;
                hole.compositeOrder = 1;
                hole.enabled = false;
                // Deliberately mark contributors too: collection must use only their resulting Composite.
                ColliderWaterlineReviewAuthoring.Assign(outer.gameObject, outer);
                ColliderWaterlineReviewAuthoring.Assign(hole.gameObject, hole);
                var sources = root.GetComponentsInChildren<WaterContactSource>(true);
                Require(WaterlineGenerator.Collect(root.transform, sources).Length == 1, "Composite double count.");
                g.Generate(root.transform, sources, 512, 1);
                Require(!Inside(g, Vector2.zero) && Inside(g, new Vector2(1.5f, 0)), "Composite hole lost.");
                Require(!body.simulated && !outer.enabled && !hole.enabled, "Original physics changed.");
                var result = new
                {
                    passed = true,
                    compositeOnlySelection = true,
                    differenceHolePreserved = true
                };
                Write("Composite.json", result);
                return result;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static object TestReview()
        {
            var a = UnityEngine.Object.FindAnyObjectByType<WaterlineBakeAuthoring>();
            var surface = a.Surface;
            var capsule = surface.Sources.Single().GetComponent<CapsuleCollider2D>();
            Require(capsule != null && !capsule.enabled, "Review requires inactive Capsule.");
            var shape = EditorJsonUtility.ToJson(capsule);
            a.GenerateReview();
            var first = a.Review.Texture.GetPixelData<float>(0).ToArray();
            var objectCount = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            capsule.offset += Vector2.right * .05f;
            Require(first.SequenceEqual(a.Review.Texture.GetPixelData<float>(0).ToArray()), "Editor generated without button.");
            capsule.offset -= Vector2.right * .05f;
            ColliderWaterlineBaker.Generate(a);
            Require(first.SequenceEqual(a.Review.Texture.GetPixelData<float>(0).ToArray()), "Manual review not repeatable.");
            Require(shape == EditorJsonUtility.ToJson(capsule), "Capsule settings changed.");
            Require(surface.GetComponent<WorldArea>() == null && surface.transform.Find("Deck/UpperDeck").GetComponent<WorldArea>() != null, "Ship/Deck ownership wrong.");
            var deck = surface.transform.Find("Deck/UpperDeck").gameObject;
            var placement = deck.GetComponent<PlacementSpace>();
            Require(placement.Contains(deck.GetComponentInChildren<CellSize>()), "Helm no longer belongs to valid deck grid.");
            deck.SetActive(false);
            Require(surface.transform.Find("Hull/MainHull").gameObject.activeInHierarchy, "Deck switch hides Hull.");
            deck.SetActive(true);
            var mask = ColliderWaterlineBaker.MaskPreview(a.Review);
            File.WriteAllBytes(Evidence + "Mask.png", mask.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(mask);
            var sdf = ColliderWaterlineBaker.DistancePreview(a.Review);
            File.WriteAllBytes(Evidence + "SignedDistance.png", sdf.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sdf);
            var result = new
            {
                passed = true,
                capsulePreserved = true,
                editorManualOnly = true,
                shipDeckSeparated = true,
                assetRequired = false,
                width = a.Review.Texture.width,
                height = a.Review.Texture.height,
                extract = a.Review.ExtractMilliseconds,
                raster = a.Review.RasterMilliseconds,
                distance = a.Review.DistanceMilliseconds,
                upload = a.Review.UploadMilliseconds
            };
            Write("Review.json", result);
            return result;
        }
    }
}
