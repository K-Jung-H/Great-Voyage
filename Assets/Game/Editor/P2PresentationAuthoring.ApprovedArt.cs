using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GreatVoyage.Editor
{
    public static partial class P2PresentationAuthoring
    {
        private const string FloorSegmentPath = Art + "Environment/CabinFloorSegment.asset";
        private const string RepeatFloorPath = Art + "Environment/CabinPlanksTileable.png";

        // Pixel data is retained verbatim. Only Unity Sprite rectangles and prefab transforms change.
        private static SpriteRect ApprovedCell(string name, int column, int row, int columns, int rows, string path)
        {
            var result = Cell(name, column, row, columns, rows, path);
            var rect = result.rect;
            rect.xMin -= 4;
            rect.yMin -= 4;
            rect.xMax += 4;
            rect.yMax += 4;
            result.rect = rect;
            return result;
        }

        [MenuItem("Great Voyage/Apply Approved P2 Art")]
        public static void ApplyApprovedArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before changing presentation assets.");
            ImportCabin();
            ImportCharacter();
            ConfigureNativeFloorSprite();
            ConfigureNativePartitionSprite();
            EditApprovedPrefab("Facilities/NavigationDeskView.prefab", root =>
                FitApprovedProp(root, "Artwork", "NavigationDesk", new Vector2(2.9f, 1.93f)));
            EditApprovedPrefab("World/CaptainCabin.prefab", root =>
            {
                FitApprovedProp(root, "CaptainBed_FrameDisabled", "SingleBed", new Vector2(1.45f, 2.3f));
                FitApprovedProp(root, "SeaChest_FrameDisabled", "SeaChest", new Vector2(1.7f, 1.12f));
                FitApprovedProp(root, "CaptainChair", "CaptainChair", new Vector2(0.95f, 1.14f));
                var floor = root.Find("FloorPanel").GetComponent<SpriteRenderer>();
                floor.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorSegmentPath);
                floor.drawMode = SpriteDrawMode.Tiled;
                floor.tileMode = SpriteTileMode.Continuous;
                floor.size = new Vector2(11.6f, 6.5f);
                floor.transform.localScale = Vector3.one;
            });
            EditApprovedPrefab("Characters/PlayerView.prefab", ConfigureApprovedCharacter);
            ApplyModularPartitions();
            AssetDatabase.SaveAssets();
        }

        private static void EditApprovedPrefab(string relative, Action<Transform> update)
        {
            var path = Prefabs + relative;
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                update(contents.transform);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void FitApprovedProp(Transform root, string child, string name, Vector2 maximum)
        {
            var renderer = root.Find(child).GetComponent<SpriteRenderer>();
            renderer.sprite = Sprite("Environment/CaptainCabinAtlas.png", name);
            var size = renderer.sprite.bounds.size;
            var scale = Mathf.Min(maximum.x / size.x, maximum.y / size.y);
            renderer.transform.localScale = new Vector3(scale, scale, 1);
        }

        private static void ConfigureApprovedCharacter(Transform root)
        {
            Sprite Part(string name) => Sprite("Characters/CaptainPartsAtlas.png", name);
            // One scale for all parts preserves the accepted palm/body ratio rather than resizing hands.
            var scale = 0.6f / Part("BodyFront").bounds.size.x;
            var material = root.Find("Body").GetComponent<SpriteRenderer>().sharedMaterial;
            void SetPart(Transform parent, string child, string spriteName, Vector2 position, int order)
            {
                var part = parent.Find(child);
                if (part == null)
                {
                    part = new GameObject(child, typeof(SpriteRenderer)).transform;
                    part.SetParent(parent, false);
                }
                part.localPosition = position;
                part.localRotation = Quaternion.identity;
                part.localScale = new Vector3(scale, scale, 1);
                var renderer = part.GetComponent<SpriteRenderer>();
                renderer.sprite = Part(spriteName);
                renderer.sharedMaterial = material;
                renderer.sortingOrder = order;
            }
            Transform Group(string name)
            {
                var group = root.Find(name);
                if (group == null)
                {
                    group = new GameObject(name).transform;
                    group.SetParent(root, false);
                }
                group.gameObject.SetActive(false);
                return group;
            }
            var headPosition = new Vector2(0, 0.27f);
            var bodyPosition = new Vector2(0, -0.12f);
            SetPart(root, "Head", "HeadFront", headPosition, 2);
            SetPart(root, "Body", "BodyFront", bodyPosition, 0);
            SetPart(root, "LeftHand", "HandLeft", new Vector2(-0.4f, -0.14f), 1);
            SetPart(root, "RightHand", "HandRight", new Vector2(0.4f, -0.14f), 1);
            var back = Group("BackFacingParts");
            SetPart(back, "HeadBack", "HeadBack", headPosition, 2);
            SetPart(back, "BodyBack", "BodyBack", bodyPosition, 0);
            var quarter = Group("ThreeQuarterFacingParts");
            SetPart(quarter, "HeadThreeQuarter", "HeadThreeQuarter", headPosition, 2);
            SetPart(quarter, "BodyThreeQuarter", "BodyThreeQuarter", bodyPosition, 0);
            SetPart(Group("ClosedEyesParts"), "HeadFrontClosed", "HeadFrontClosed", headPosition, 2);
        }

        private static void ConfigureNativeFloorSprite()
        {
            AssetDatabase.ImportAsset(RepeatFloorPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RepeatFloorPath);
            if (importer == null) throw new InvalidOperationException("Approved repeat floor texture is missing.");
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(RepeatFloorPath);
            var rect = new Rect(0, 0, texture.width, texture.height);
            // Sixteen rows in an eight-unit tile retain approximately half-unit floorboard pitch.
            var created = UnityEngine.Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f),
                rect.width / 8f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            created.name = "CabinFloorSegment";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(FloorSegmentPath);
            if (existing == null) AssetDatabase.CreateAsset(created, FloorSegmentPath);
            else
            {
                EditorUtility.CopySerialized(created, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(created);
            }
        }

        [MenuItem("Great Voyage/Apply Repeat Floor")]
        public static void ApplyRepeatFloor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before changing floor assets.");
            ConfigureNativeFloorSprite();
            EditApprovedPrefab("World/CaptainCabin.prefab", root =>
            {
                var floor = root.Find("FloorPanel").GetComponent<SpriteRenderer>();
                floor.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FloorSegmentPath);
                floor.drawMode = SpriteDrawMode.Tiled;
                floor.tileMode = SpriteTileMode.Continuous;
                floor.size = new Vector2(11.6f, 6.5f);
                floor.transform.localScale = Vector3.one;
            });
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Sprite>(FloorSegmentPath));
        }

        public static string ValidateApprovedArt()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open the product Game Scene.");
            var roots = scene.GetRootGameObjects();
            var transforms = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var renderers = roots.SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
            var partitions = transforms.Single(x => x.name == "Partitions");
            var segments = partitions.GetComponentsInChildren<SpriteRenderer>(true);
            var colliders = partitions.GetComponentsInChildren<BoxCollider2D>(true);
            var missingScripts = transforms.Sum(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject));
            var missingSprites = renderers.Count(x => x.sprite == null);
            var nonUnitWalls = partitions.GetComponentsInChildren<Transform>(true)
                .Count(x => (x.localScale - Vector3.one).sqrMagnitude > 0.000001f);
            var player = transforms.Single(x => x.name == "PlayerView");
            var parts = player.GetComponentsInChildren<SpriteRenderer>(true);
            var commonScale = player.Find("Body").localScale;
            var samePartScale = parts.All(x => (x.transform.localScale - commonScale).sqrMagnitude < 0.000001f);
            var buttons = roots.SelectMany(x => x.GetComponentsInChildren<UnityEngine.UI.Selectable>(true)).ToArray();
            var enabledFrameControls = buttons.Count(x => x.interactable);
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset");
            var labels = roots.SelectMany(x => x.GetComponentsInChildren<TMPro.TMP_Text>(true)).ToArray();
            var wrongFont = labels.Count(x => x.font != font);
            object Inspect(string path)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(path));
                    var pixels = texture.GetPixels32();
                    return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Select(sprite =>
                    {
                        var r = sprite.rect;
                        var x0 = (int)r.xMin; var y0 = (int)r.yMin;
                        var x1 = (int)r.xMax - 1; var y1 = (int)r.yMax - 1;
                        var boundary = 0;
                        for (var x = x0; x <= x1; x++)
                        {
                            if (pixels[y0 * texture.width + x].a >= 128) boundary++;
                            if (pixels[y1 * texture.width + x].a >= 128) boundary++;
                        }
                        for (var y = y0 + 1; y < y1; y++)
                        {
                            if (pixels[y * texture.width + x0].a >= 128) boundary++;
                            if (pixels[y * texture.width + x1].a >= 128) boundary++;
                        }
                        return new { sprite.name, rect = new[] { r.x, r.y, r.width, r.height }, opaqueBoundaryPixels = boundary };
                    }).ToArray();
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            var floor = transforms.Single(x => x.name == "FloorPanel").GetComponent<SpriteRenderer>();
            var appliedPixelsUnchanged = File.ReadAllBytes(Art + "Environment/CaptainCabinAtlas.png")
                .SequenceEqual(File.ReadAllBytes("Agents/References/P2/PropStyleReview/StyleB_OriginalSamplesOnly_01.png"))
                && File.ReadAllBytes(Art + "Characters/CaptainPartsAtlas.png")
                .SequenceEqual(File.ReadAllBytes("Agents/References/P2/HumanStyleReview/Captain_Parts_FinalCandidate01.png"));
            var result = new
            {
                scope = "Editor camera and serialized asset inspection only; no input/movement/physics/save/performance certification",
                scene = scene.path,
                missingScripts, missingSprites,
                spriteRendererCount = renderers.Length,
                wallSegments = segments.Length,
                wallColliders = colliders.Length,
                nonUnitWalls,
                handAndBodySameScale = samePartScale,
                characterParts = parts.Select(x => new { x.name, sprite = x.sprite.name,
                    scale = new[] { x.transform.localScale.x, x.transform.localScale.y },
                    displayedSize = new[] { x.bounds.size.x, x.bounds.size.y } }).ToArray(),
                floorSprite = floor.sprite.name,
                floorRect = new[] { floor.sprite.rect.x, floor.sprite.rect.y, floor.sprite.rect.width, floor.sprite.rect.height },
                floorNativeSize = new[] { floor.sprite.bounds.size.x, floor.sprite.bounds.size.y },
                floorWorldSize = new[] { floor.bounds.size.x, floor.bounds.size.y },
                wallNativeSize = new[] { segments[0].sprite.bounds.size.x, segments[0].sprite.bounds.size.y },
                enabledFrameControls,
                productTextCount = labels.Length,
                wrongFont,
                appliedPixelsUnchanged,
                propSlices = Inspect(Art + "Environment/CaptainCabinAtlas.png"),
                humanSlices = Inspect(Art + "Characters/CaptainPartsAtlas.png"),
                viewsRestored = player.Find("Head").gameObject.activeSelf && player.Find("Body").gameObject.activeSelf
                    && !player.Find("BackFacingParts").gameObject.activeSelf
                    && !player.Find("ThreeQuarterFacingParts").gameObject.activeSelf
                    && !player.Find("ClosedEyesParts").gameObject.activeSelf,
                cameraOrthographicSize = Camera.main.orthographicSize
            };
            if (missingScripts != 0 || missingSprites != 0 || segments.Length != 70 || colliders.Length != 5
                || nonUnitWalls != 0 || !samePartScale || enabledFrameControls != 0 || wrongFont != 0 || !appliedPixelsUnchanged)
                throw new InvalidOperationException("Approved-art validation failed: inspect asset references, scales or controls.");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("Agents/References/P2/ApprovedArtValidation.json", json);
            return json;
        }
    }
}
