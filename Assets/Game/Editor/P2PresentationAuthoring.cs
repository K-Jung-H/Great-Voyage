using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GreatVoyage.Presentation.UI;

namespace GreatVoyage.Editor
{
    public static partial class P2PresentationAuthoring
    {
        private const string Root = "Assets/Game/";
        private const string Art = Root + "Content/Art/";
        private const string Prefabs = Root + "Content/Prefabs/";
        private const string ScenePath = Root + "Scenes/Game.unity";
        private static readonly Color Ink = new Color32(57, 38, 26, 255);
        private static readonly Color Cream = new Color32(245, 226, 184, 255);
        private static TMP_FontAsset font;
        private static Material spriteMaterial;

        [MenuItem("Great Voyage/Preview/Show Journal Frame")]
        public static void ShowJournalFrame() => SetJournalVisible(true);

        [MenuItem("Great Voyage/Preview/Show Captain Cabin")]
        public static void ShowCaptainCabin() => SetJournalVisible(false);

        private static void SetJournalVisible(bool visible)
        {
            var journal = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(rect => rect.name == "JournalScreen" || rect.name == "JournalScreen_FrameDisabled");
            if (journal == null) throw new InvalidOperationException("Open Assets/Game/Scenes/Game.unity first.");
            Undo.RecordObject(journal.gameObject, "Preview Journal Frame");
            journal.gameObject.SetActive(visible);
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        [MenuItem("Great Voyage/Author P2 Presentation (new Scene only)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before authoring assets.");
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Game Scene already exists. Edit the Scene and Prefabs directly; authoring will not overwrite them.");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current Scene before creating Game.");

            ImportCabin();
            ImportCharacter();
            ImportJournal();
            ImportFloor();
            ConfigureNativePartitionSprite();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset");
            if (font == null) throw new InvalidOperationException("Galmuri11 SDF is required.");
            spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (spriteMaterial == null) throw new InvalidOperationException("URP Sprite Unlit material is required.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 44, 53, 255);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var world = new GameObject("WorldContent");
            var cabin = BuildCabin();
            SaveInstance(cabin, Prefabs + "World/CaptainCabin.prefab", world.transform, Vector3.zero);
            var character = BuildCharacter();
            SaveInstance(character, Prefabs + "Characters/PlayerView.prefab", world.transform, new Vector3(0.7f, -0.9f, 0));
            var canvas = BuildCanvas(camera);
            var journal = BuildJournal();
            var journalInstance = SaveInstance(journal, Prefabs + "UI/JournalScreen.prefab", canvas.transform, Vector3.zero);
            var rect = (RectTransform)journalInstance.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            journalInstance.SetActive(false);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath)
                .Select(s => new EditorBuildSettingsScene(s.path, false))
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(13, 9, 1)), false);
        }

        private static void ImportCabin()
        {
            var path = Art + "Environment/CaptainCabinAtlas.png";
            Import(path, new[]
            {
                ApprovedCell("NavigationDesk", 0, 0, 3, 2, path),
                ApprovedCell("SingleBed", 1, 0, 3, 2, path),
                ApprovedCell("SeaChest", 2, 0, 3, 2, path),
                ApprovedCell("CabinFloorPanel", 0, 1, 3, 2, path),
                ApprovedCell("PartitionWall", 1, 1, 3, 2, path),
                ApprovedCell("CaptainChair", 2, 1, 3, 2, path)
            });
        }

        private static void ImportCharacter()
        {
            var path = Art + "Characters/CaptainPartsAtlas.png";
            Import(path, new[]
            {
                ApprovedCell("HeadFront", 0, 0, 3, 3, path),
                ApprovedCell("HeadBack", 1, 0, 3, 3, path),
                ApprovedCell("HeadThreeQuarter", 2, 0, 3, 3, path),
                ApprovedCell("BodyFront", 0, 1, 3, 3, path),
                ApprovedCell("BodyBack", 1, 1, 3, 3, path),
                ApprovedCell("BodyThreeQuarter", 2, 1, 3, 3, path),
                ApprovedCell("HeadFrontClosed", 0, 2, 3, 3, path),
                ApprovedCell("HandLeft", 1, 2, 3, 3, path),
                ApprovedCell("HandRight", 2, 2, 3, 3, path)
            });
        }

        private static void ImportJournal()
        {
            var path = Art + "UI/JournalMaterialsAtlas.png";
            Import(path, new[]
            {
                Cell("LeatherPanel", 0, 0, 2, 2, path),
                Cell("ParchmentPanel", 1, 0, 2, 2, path),
                Cell("LeatherTab", 0, 1, 2, 2, path),
                Cell("ChartSurface", 1, 1, 2, 2, path)
            });
        }

        public static void RefineJournalSurfaces()
        {
            ImportJournal();
            var path = Prefabs + "UI/JournalScreen.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var image in contents.GetComponentsInChildren<Image>(true))
                    image.type = image.sprite != null && (image.sprite.name == "LeatherPanel" || image.sprite.name == "ParchmentPanel")
                        ? Image.Type.Sliced : Image.Type.Simple;
                var book = contents.transform.Find("LeatherBinding");
                var index = book.Find("LeatherDayIndex_FrameDisabled");
                if (index.Find("ContinuousLeatherIndex") == null)
                {
                    var spine = Panel("ContinuousLeatherIndex", index, Vector2.zero, new Vector2(190, 760),
                        Sprite("UI/JournalMaterialsAtlas.png", "LeatherPanel"), new Color(0.8f, 0.8f, 0.8f, 1));
                    spine.transform.SetAsFirstSibling();
                }
                foreach (var toggle in index.GetComponentsInChildren<Toggle>(true))
                {
                    var background = toggle.GetComponent<Image>();
                    background.sprite = null;
                    background.color = Color.clear;
                    background.raycastTarget = true;
                }
                var information = book.Find("EmptySelection").GetComponent<RectTransform>();
                information.anchoredPosition = new Vector2(655, -200);
                if (book.Find("SelectionIllustration_FrameDisabled") == null)
                    Panel("SelectionIllustration_FrameDisabled", book, new Vector2(655, 130), new Vector2(230, 260), null, Color.clear);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void ImportFloor()
        {
            var path = Art + "Environment/CabinPlanks.png";
            Import(path, new[] { FloorSlice(path) });
        }

        public static void ApplyJournalPlaceholders()
        {
            var path = Prefabs + "UI/JournalScreen.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AddJournalPlaceholders(contents.transform);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void AddJournalPlaceholders(Transform root)
        {
            var book = root.Find("LeatherBinding");
            var index = book.Find("LeatherDayIndex_FrameDisabled");
            foreach (var toggle in index.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle.transform.Find("DashedSelectionArea") != null) continue;
                var outline = new GameObject("DashedSelectionArea", typeof(RectTransform), typeof(CanvasRenderer), typeof(DashedBorderGraphic));
                outline.transform.SetParent(toggle.transform, false);
                ((RectTransform)outline.transform).sizeDelta = new Vector2(160, 96);
                var border = outline.GetComponent<DashedBorderGraphic>();
                border.color = new Color32(245, 226, 184, 185);
                border.raycastTarget = false;
                outline.transform.SetAsFirstSibling();
            }

            var placeholderColor = new Color32(110, 83, 53, 48);
            var illustration = book.Find("SelectionIllustration_FrameDisabled").GetComponent<Image>();
            illustration.color = placeholderColor;
            if (illustration.transform.Find("ImageInsetSkeleton") == null)
                Panel("ImageInsetSkeleton", illustration.transform, Vector2.zero, new Vector2(202, 232), null, new Color32(245, 226, 184, 45));
            var emptySelection = book.Find("EmptySelection");
            if (emptySelection != null) emptySelection.gameObject.SetActive(false);
            if (book.Find("DescriptionSkeleton_FrameDisabled") == null)
            {
                var description = new GameObject("DescriptionSkeleton_FrameDisabled", typeof(RectTransform));
                description.transform.SetParent(book, false);
                var rect = (RectTransform)description.transform;
                rect.anchoredPosition = new Vector2(655, -200);
                rect.sizeDelta = new Vector2(234, 140);
                var widths = new[] { 234f, 218f, 234f, 154f };
                for (var i = 0; i < widths.Length; i++)
                    Panel("TextLineSkeleton_" + (i + 1), description.transform,
                        new Vector2((widths[i] - 234) / 2, 48 - 32 * i), new Vector2(widths[i], 14), null, placeholderColor);
            }
        }

        public static void ApplyCabinFloor()
        {
            ImportFloor();
            var path = Prefabs + "World/CaptainCabin.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SetFloor(contents.transform.Find("FloorPanel").GetComponent<SpriteRenderer>());
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void SetFloor(SpriteRenderer renderer)
        {
            renderer.sprite = Sprite("Environment/CabinPlanks.png", "CabinPlanks");
            var scale = 3f / renderer.sprite.bounds.size.x;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(11.6f / scale, 6.5f / scale);
            renderer.transform.localScale = new Vector3(scale, scale, 1);
        }

        private static SpriteRect Slice(string name, int x, int top, int width, int height, int imageHeight)
        {
            return new SpriteRect { name = name, rect = new Rect(x, imageHeight - top - height, width, height),
                alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f), spriteID = GUID.Generate() };
        }

        private static SpriteRect Cell(string name, int column, int row, int columns, int rows, string path)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Invalid image: " + path);
                var cellWidth = texture.width / columns;
                var cellHeight = texture.height / rows;
                var minX = (column + 1) * cellWidth;
                var maxX = column * cellWidth;
                var minY = texture.height - row * cellHeight;
                var maxY = texture.height - (row + 1) * cellHeight;
                var pixels = texture.GetPixels32();
                for (var y = texture.height - (row + 1) * cellHeight; y < texture.height - row * cellHeight; y++)
                for (var x = column * cellWidth; x < (column + 1) * cellWidth; x++)
                {
                    if (pixels[y * texture.width + x].a < 32) continue;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
                if (maxX <= minX || maxY <= minY) throw new InvalidOperationException("Empty sprite cell: " + name);
                return new SpriteRect { name = name, rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f), spriteID = GUID.Generate() };
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void Import(string path, SpriteRect[] rects)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 128;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name);
            foreach (var rect in rects)
            {
                if (existing.TryGetValue(rect.name, out var previous)) rect.spriteID = previous.spriteID;
                if (path.Contains("/UI/") && rect.name != "ChartSurface") rect.border = new Vector4(36, 36, 36, 36);
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static Sprite Sprite(string relativePath, string name)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(Art + relativePath).OfType<Sprite>().SingleOrDefault(s => s.name == name);
            if (sprite == null) throw new InvalidOperationException("Missing Sprite: " + name);
            return sprite;
        }

        private static GameObject Visual(string name, Sprite sprite, Transform parent, Vector2 position, Vector2 size, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = order;
            return go;
        }

        private static GameObject BuildCabin()
        {
            var cabin = new GameObject("CaptainCabin");
            Sprite S(string name) => Sprite("Environment/CaptainCabinAtlas.png", name);
            var floor = Visual("FloorPanel", S("CabinFloorPanel"), cabin.transform, Vector2.zero, new Vector2(11.6f, 6.5f), -100);
            SetFloor(floor.GetComponent<SpriteRenderer>());
            var walls = new GameObject("Partitions");
            walls.transform.SetParent(cabin.transform, false);
            PopulatePartitions(walls.transform);
            var doorway = new GameObject("Doorway_FrameDisabled");
            doorway.transform.SetParent(cabin.transform, false);
            doorway.transform.localPosition = new Vector3(0, -3.25f, 0);
            var desk = new GameObject("NavigationDeskView");
            Visual("Artwork", S("NavigationDesk"), desk.transform, Vector2.zero, new Vector2(2.9f, 1.93f), 10);
            var approach = new GameObject("ApproachPoint");
            approach.transform.SetParent(desk.transform, false);
            approach.transform.localPosition = new Vector3(0, -1.25f, 0);
            SaveInstance(desk, Prefabs + "Facilities/NavigationDeskView.prefab", cabin.transform, new Vector3(2.6f, 1.5f, 0));
            Visual("CaptainBed_FrameDisabled", S("SingleBed"), cabin.transform, new Vector2(-4.25f, 1.5f), new Vector2(1.45f, 2.3f), 10);
            Visual("SeaChest_FrameDisabled", S("SeaChest"), cabin.transform, new Vector2(-4.2f, -1.85f), new Vector2(1.7f, 1.12f), 10);
            Visual("CaptainChair", S("CaptainChair"), cabin.transform, new Vector2(2.6f, -0.4f), new Vector2(0.95f, 1.14f), 10);
            return cabin;
        }

        private static GameObject BuildCharacter()
        {
            var player = new GameObject("PlayerView", typeof(SortingGroup));
            player.GetComponent<SortingGroup>().sortingOrder = 20;
            Sprite S(string name) => Sprite("Characters/CaptainPartsAtlas.png", name);
            Visual("Body", S("BodyFront"), player.transform, new Vector2(0, -0.12f), new Vector2(0.55f, 0.6f), 0);
            Visual("Head", S("HeadFront"), player.transform, new Vector2(0, 0.32f), new Vector2(0.64f, 0.5f), 2);
            Visual("LeftHand", S("HandLeft"), player.transform, new Vector2(-0.36f, -0.14f), new Vector2(0.18f, 0.2f), 1);
            Visual("RightHand", S("HandRight"), player.transform, new Vector2(0.36f, -0.14f), new Vector2(0.18f, 0.2f), 1);
            var back = new GameObject("BackFacingParts");
            back.transform.SetParent(player.transform, false);
            Visual("HeadBack", S("HeadBack"), back.transform, new Vector2(0, 0.32f), new Vector2(0.64f, 0.5f), 2);
            Visual("BodyBack", S("BodyBack"), back.transform, new Vector2(0, -0.12f), new Vector2(0.55f, 0.6f), 0);
            back.SetActive(false);
            return player;
        }

        private static Canvas BuildCanvas(Camera camera)
        {
            var go = new GameObject("ScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static GameObject BuildJournal()
        {
            var root = new GameObject("JournalScreen_FrameDisabled", typeof(RectTransform), typeof(CanvasGroup));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(1920, 1080);
            Sprite S(string name) => Sprite("UI/JournalMaterialsAtlas.png", name);
            var shade = Panel("ModalScrim", root.transform, Vector2.zero, new Vector2(1920, 1080), null, new Color(0.025f, 0.035f, 0.04f, 0.86f));
            shade.raycastTarget = true;
            var book = Panel("LeatherBinding", root.transform, Vector2.zero, new Vector2(1740, 928), S("LeatherPanel"), Color.white);
            Panel("ChartPage", book.transform, new Vector2(-70, -20), new Vector2(1070, 774), S("ChartSurface"), Color.white);
            Panel("InformationPage", book.transform, new Vector2(655, -20), new Vector2(305, 774), S("ParchmentPanel"), Color.white);
            Label("Title", book.transform, new Vector2(-400, 408), new Vector2(600, 50), "항해 일지", 32, Cream, TextAlignmentOptions.Left);

            var index = new GameObject("LeatherDayIndex_FrameDisabled", typeof(RectTransform));
            index.transform.SetParent(book.transform, false);
            var indexRect = (RectTransform)index.transform;
            indexRect.anchoredPosition = new Vector2(-720, 0);
            indexRect.sizeDelta = new Vector2(190, 760);
            var disabled = index.AddComponent<CanvasGroup>();
            disabled.interactable = false;
            Panel("ContinuousLeatherIndex", index.transform, Vector2.zero, new Vector2(190, 760),
                S("LeatherPanel"), new Color(0.8f, 0.8f, 0.8f, 1));
            for (var i = 0; i < 5; i++)
            {
                var tab = Panel("DayTab_" + (i + 1), index.transform, new Vector2(0, 300 - 133 * i), new Vector2(183, 112), null, Color.clear);
                tab.raycastTarget = true;
                var toggle = tab.gameObject.AddComponent<Toggle>();
                toggle.interactable = false;
                toggle.transition = Selectable.Transition.None;
                toggle.targetGraphic = tab;
                toggle.isOn = false;
                Label("DayLabel", tab.transform, Vector2.zero, new Vector2(135, 50), "Day " + (i + 1), 25, Cream, TextAlignmentOptions.Center);
            }
            Label("InformationTitle", book.transform, new Vector2(655, 310), new Vector2(240, 60), "항구 · 사건", 26, Ink, TextAlignmentOptions.Center);
            Panel("SelectionIllustration_FrameDisabled", book.transform, new Vector2(655, 130), new Vector2(230, 260), null, Color.clear);
            Label("EmptySelection", book.transform, new Vector2(655, -200), new Vector2(234, 140), "선택한 항목이\n없습니다", 22, Ink, TextAlignmentOptions.Center);
            var exit = Panel("Close_FrameDisabled", book.transform, new Vector2(751, 408), new Vector2(124, 48), S("LeatherTab"), Color.white);
            var button = exit.gameObject.AddComponent<Button>();
            button.interactable = false;
            button.transition = Selectable.Transition.None;
            button.targetGraphic = exit;
            Label("CloseLabel", exit.transform, Vector2.zero, new Vector2(100, 40), "나가기", 22, Cream, TextAlignmentOptions.Center);
            var pens = Panel("PenTools_FrameDisabled", book.transform, new Vector2(-70, -425), new Vector2(470, 45), S("LeatherTab"), Color.white);
            Label("ToolsLabel", pens.transform, Vector2.zero, new Vector2(440, 40), "계획     기록     메모", 21, Cream, TextAlignmentOptions.Center);
            AddJournalPlaceholders(root.transform);
            return root;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            if (sprite != null && (sprite.name == "LeatherPanel" || sprite.name == "ParchmentPanel")) image.type = Image.Type.Sliced;
            image.color = tint;
            image.raycastTarget = false;
            return image;
        }

        private static void Label(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color tint, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = tint;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static GameObject SaveInstance(GameObject authored, string path, Transform parent, Vector3 position)
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(authored, path);
            UnityEngine.Object.DestroyImmediate(authored);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            return instance;
        }
    }
}
