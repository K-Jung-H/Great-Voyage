using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreatVoyage.Application;
using GreatVoyage.Composition;
using GreatVoyage.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GreatVoyage.Editor
{
    // One-time serialized product authoring. No runtime world/UI construction.
    public static class P4WorldAuthoring
    {
        private const string Art = "Assets/Game/Content/Art/Environment/";
        private const string Prefabs = "Assets/Game/Content/Prefabs/World/";
        private static Sprite hull, stairs, board, house, paving, planks;
        private static TMP_FontAsset font;
        private static Material highlight;

        [MenuItem("Great Voyage/Author P4 World")]
        public static string Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != "Assets/Game/Scenes/Game.unity") throw new InvalidOperationException("Open clean Game scene outside Play.");
            if (UnityEngine.Object.FindAnyObjectByType<WorldTravelView>() != null)
                throw new InvalidOperationException("P4 already authored; edit existing serialized assets instead.");
            AssetDatabase.Refresh();
            var path = Art + "PortTravelAtlas.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            hull = SpriteAsset(texture, "TravelHull", new Rect(31, 685, 452, 148));
            stairs = SpriteAsset(texture, "TravelStairs", new Rect(646, 632, 245, 263));
            board = SpriteAsset(texture, "TravelBoard", new Rect(1098, 624, 363, 269));
            house = SpriteAsset(texture, "TravelHouse", new Rect(68, 97, 380, 365));
            paving = SpriteAsset(texture, "PortPaving", new Rect(603, 119, 330, 326));
            planks = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "CabinFloorSegment.asset");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset");
            highlight = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Content/Art/InteractionOutline.mat");
            var world = GameObject.Find("WorldContent").transform;
            var cabin = world.Find("CaptainCabin").gameObject;
            var captain = cabin.AddComponent<WorldArea>();
            Set(captain, "areaId", "captain-cabin"); Set(captain, "displayName", "선장실");
            Set(captain, "transitionIllustration", AssetDatabase.LoadAllAssetsAtPath(Art + "CaptainCabinAtlas.png").OfType<Sprite>().First());
            var areas = new List<WorldArea> { captain };
            areas.Add(Build(world, "UpperDeck", "upper-deck", "상갑판", new Rect(-8, -4.5f, 16, 9), root =>
            {
                Prop(root, "ShipDeck", hull, Vector2.zero, 14.5f, false, -100);
                Bounds(root, new Rect(-5.3f, -1.7f, 10.6f, 3.4f));
                var space = root.gameObject.AddComponent<PlacementSpace>();
                Set(space, "spaceId", "upper-deck");
                Set(space, "settings", AssetDatabase.LoadAssetAtPath<GridSettings>("Assets/Game/Content/Definitions/GridSettings.asset"));
                var so = new SerializedObject(space); so.FindProperty("origin").vector2Value = new Vector2(-5, -1);
                so.ApplyModifiedPropertiesWithoutUndo();
                var grid = root.gameObject.AddComponent<GridArea>();
                var gs = new SerializedObject(grid); gs.FindProperty("areaId").stringValue = "upper-deck-placement";
                gs.FindProperty("bounds").rectIntValue = new RectInt(0, 0, 10, 2); gs.ApplyModifiedPropertiesWithoutUndo();
            }, hull));
            areas.Add(Build(world, "HarborDock", "harbor-dock", "선착장", new Rect(-8, -4.5f, 16, 9), root =>
            {
                Tile(root, "Land", paving, new Vector2(4, 0), new Vector2(7, 7), -100);
                Tile(root, "Pier", planks, new Vector2(-2.5f, 0), new Vector2(7, 1.8f), -90);
                Prop(root, "MooredShip", hull, new Vector2(-5.6f, 2), 4.6f, true);
                Prop(root, "Noticeboard_FrameDisabled", board, new Vector2(-3, 1.1f), 1, true);
                Sign(root, "게시판 · 준비 중", new Vector2(-3, 2), .8f);
                Bounds(root, new Rect(-7.3f, -3.3f, 14.6f, 6.6f));
                Solid(root, "PierWaterNorth", new Vector2(-3.5f, 2.3f), new Vector2(8, 2.8f));
                Solid(root, "PierWaterSouth", new Vector2(-3.5f, -2.3f), new Vector2(8, 2.8f));
            }, board));
            areas.Add(Build(world, "HarborCrossroads", "harbor-crossroads", "항구 교차로", new Rect(-8, -4.5f, 16, 9), root =>
            {
                Tile(root, "Paving", paving, Vector2.zero, new Vector2(15, 8), -100);
                Bounds(root, new Rect(-7.3f, -3.7f, 14.6f, 7.4f));
                Prop(root, "CityHall_FrameDisabled", house, new Vector2(0, 2.4f), 2.7f, true);
                Sign(root, "시청 · 준비 중", new Vector2(0, 4), 1);
                Prop(root, "NorthWestBuilding", house, new Vector2(-4.5f, 2.4f), 2.2f, true);
                Prop(root, "SouthEastBuilding", house, new Vector2(4.5f, -2.2f), 2.2f, true);
            }, house));
            areas.Add(Build(world, "HarborTown", "harbor-town", "마을 · 주점과 조선소", new Rect(-8, -4.5f, 16, 9), root =>
            {
                Tile(root, "Paving", paving, Vector2.zero, new Vector2(15, 8), -100);
                Bounds(root, new Rect(-7.3f, -3.7f, 14.6f, 7.4f));
                Prop(root, "Tavern", house, new Vector2(-4, 2.2f), 3, true);
                Prop(root, "Shipyard_FrameDisabled", house, new Vector2(2, -2), 3, true);
                Sign(root, "조선소 · 준비 중", new Vector2(2, -.4f), 1.2f);
                Prop(root, "DryDockShip", hull, new Vector2(-3, -2.4f), 3.4f, true);
            }, house));
            areas.Add(Build(world, "HarborMarket", "harbor-market", "상점가", new Rect(-8, -4.5f, 16, 9), root =>
            {
                Tile(root, "Paving", paving, Vector2.zero, new Vector2(15, 8), -100);
                Bounds(root, new Rect(-7.3f, -3.7f, 14.6f, 7.4f));
                foreach (var x in new[] { -3.5f, 0, 3.5f }) foreach (var y in new[] { -2.3f, 2.3f })
                    Prop(root, "Merchant_FrameDisabled", house, new Vector2(x, y), 1.8f, true);
                Prop(root, "Broker_FrameDisabled", board, new Vector2(6, 0), 1.2f, true);
                Sign(root, "거래 대행인 · 준비 중", new Vector2(5.5f, 1.2f), .9f);
            }, board));
            areas.Add(Build(world, "TavernInterior", "tavern-interior", "주점 내부", new Rect(-6.1f, -3.5f, 12.2f, 7), root =>
            {
                Tile(root, "Floor", planks, Vector2.zero, new Vector2(11.6f, 6.5f), -100);
                Bounds(root, new Rect(-5.7f, -3.1f, 11.4f, 6.2f));
                var deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Facilities/NavigationDeskView.prefab");
                var deskSprite = deskPrefab.GetComponentInChildren<SpriteRenderer>().sprite;
                foreach (var x in new[] { -3.5f, -.8f }) foreach (var y in new[] { 1.8f, -.3f })
                    Prop(root, "Table_FrameDisabled", deskSprite, new Vector2(x, y), 1.5f, true);
                var chest = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Facilities/SeaChestView.prefab")
                    .GetComponentInChildren<SpriteRenderer>().sprite;
                for (var i = 0; i < 4; i++) Prop(root, "Counter_FrameDisabled", chest, new Vector2(4, -1.5f + i), 1.4f, true);
                Sign(root, "주점 · 서비스 준비 중", new Vector2(2.4f, 2.7f), 1.1f);
            }, stairs));

            var portals = new List<WorldPortal>();
            Pair(portals, areas, 0, new Vector2(0, -2.45f), new Vector2(0, -1.8f),
                1, new Vector2(4, .8f), new Vector2(4, -.2f), TravelKind.Floor, TravelKind.Floor);
            Pair(portals, areas, 1, new Vector2(0, -.9f), new Vector2(0, .1f),
                2, new Vector2(-5.4f, 0), new Vector2(-4.4f, 0), TravelKind.Disembark, TravelKind.Board);
            Pair(portals, areas, 2, new Vector2(6.5f, 0), new Vector2(5.5f, 0),
                3, new Vector2(0, -3), new Vector2(0, -2), TravelKind.Area, TravelKind.Area);
            Pair(portals, areas, 3, new Vector2(-6.5f, 1), new Vector2(-5.5f, 1),
                4, new Vector2(6.5f, 0), new Vector2(5.5f, 0), TravelKind.Area, TravelKind.Area);
            Pair(portals, areas, 3, new Vector2(6.5f, 0), new Vector2(5.5f, 0),
                5, new Vector2(-6.5f, 0), new Vector2(-5.5f, 0), TravelKind.Area, TravelKind.Area);
            Pair(portals, areas, 4, new Vector2(-4, .6f), new Vector2(-4, -.4f),
                6, new Vector2(-4, -2.4f), new Vector2(-4, -1.4f), TravelKind.Area, TravelKind.Area);

            var composition = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            var view = composition.gameObject.AddComponent<WorldTravelView>();
            var vs = new SerializedObject(view); Array(vs, "areas", areas.Cast<UnityEngine.Object>().ToArray());
            Array(vs, "portals", portals.Cast<UnityEngine.Object>().ToArray()); vs.ApplyModifiedPropertiesWithoutUndo();
            Set(view, "cameraRig", UnityEngine.Object.FindAnyObjectByType<CabinCamera>());
            var canvas = GameObject.Find("ScreenCanvas").transform;
            var panel = Ui("WorldTransition", canvas, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero);
            var panelImage = panel.AddComponent<Image>(); panelImage.color = new Color(.07f, .09f, .12f, 1);
            var cover = panel.AddComponent<CanvasGroup>(); cover.alpha = 0; cover.blocksRaycasts = false;
            var image = Ui("Illustration", panel.transform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(550, 280)).AddComponent<Image>();
            image.preserveAspect = true; image.raycastTarget = false;
            var label = UiText("Destination", panel.transform, new Vector2(0, -190), new Vector2(700, 70), 38);
            var areaLabel = UiText("AreaName", canvas, new Vector2(-740, 490), new Vector2(400, 55), 24);
            panel.transform.SetAsLastSibling();
            Set(view, "cover", cover); Set(view, "illustration", image); Set(view, "destinationLabel", label); Set(view, "areaLabel", areaLabel);
            var cs = new SerializedObject(composition); cs.FindProperty("worldTravel").objectReferenceValue = view;
            var desk = cabin.GetComponentInChildren<JournalDesk>();
            Array(cs, "interactionTargets", new UnityEngine.Object[] { desk }.Concat(portals).ToArray());
            cs.ApplyModifiedPropertiesWithoutUndo();
            foreach (var area in areas) area.gameObject.SetActive(area == captain);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "P4 authored: 7 areas, 12 directional links. Original cabin prefab and furniture sizes preserved.";
        }

        private static Sprite SpriteAsset(Texture2D tex, string name, Rect rect)
        {
            var p = Art + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(p) != null) return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            var sprite = Sprite.Create(tex, rect, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            sprite.name = name; AssetDatabase.CreateAsset(sprite, p); return sprite;
        }

        public static string FinishSurfacesAndTavern()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset");
            var shader = Shader.Find("Great Voyage/Mirrored World Surface");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Surface shader unavailable.");
            var source = "Assets/Game/Content/Art/Environment/OriginalTavernProps.png";
            if (!File.Exists(source)) File.Copy("Agents/References/ArtStyle_2026-11-02/Sheets/기본 결과물4.png", source);
            AssetDatabase.ImportAsset(source, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(source); ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true; ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048; ti.SaveAndReimport();
            var original = AssetDatabase.LoadAssetAtPath<Texture2D>(source);
            var table = SpriteAsset(original, "TavernRoundTable", new Rect(240, 719, 225, 208));
            var counter = SpriteAsset(original, "TavernCounter", new Rect(860, 730, 384, 220));
            var wall = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "PartitionWallSegment.asset");
            var materials = new Dictionary<string, Material>();
            foreach (var pair in new[] { ("PortPaving", "PortPavingSurface") })
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + pair.Item1 + ".asset");
                var path = "Assets/Game/Content/Art/" + pair.Item2 + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                var r = sprite.rect; var tex = sprite.texture;
                material.SetVector("_UvRect", new Vector4(r.xMin / tex.width, r.yMin / tex.height, r.xMax / tex.width, r.yMax / tex.height));
                material.SetVector("_TileSize", new Vector4(sprite.bounds.size.x, sprite.bounds.size.y, 0, 0));
                EditorUtility.SetDirty(material); materials.Add(pair.Item1, material);
            }
            foreach (var name in new[] { "UpperDeck", "HarborDock", "HarborCrossroads", "HarborTown", "HarborMarket", "TavernInterior" })
            {
                var path = Prefabs + name + ".prefab";
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sr.sprite != null && materials.TryGetValue(sr.sprite.name, out var mat)) sr.sharedMaterial = mat;
                    if (name == "TavernInterior" && root.transform.Find("FinishedTavern") == null)
                    {
                        foreach (var old in root.transform.Cast<Transform>().Where(t => t.name.StartsWith("Table_") || t.name.StartsWith("Counter_")).ToArray())
                            UnityEngine.Object.DestroyImmediate(old.gameObject);
                        var decor = new GameObject("FinishedTavern").transform; decor.SetParent(root.transform, false);
                        foreach (var x in new[] { -3.5f, -.8f }) foreach (var y in new[] { 1.8f, -.3f })
                            Prop(decor, "RoundTable_FrameDisabled", table, new Vector2(x, y), 1.5f, true);
                        Prop(decor, "Counter_FrameDisabled", counter, new Vector2(3.4f, .5f), 3.3f, true);
                        // Native partition modules, one collider per run already supplied by Bounds().
                        for (var i = 0; i < 23; i++) foreach (var y in new[] { -3.25f, 3.25f })
                        {
                            var go = new GameObject("WallModule"); go.transform.SetParent(decor, false);
                            go.transform.localPosition = new Vector3(-5.5f + i * .5f, y);
                            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = wall;
                            renderer.sortingOrder = 100 - Mathf.RoundToInt(y * 20);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.SaveAssets();
            var scene = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "Original supplied tavern art sliced; mirrored surface sampling; native wall modules. Cabin assets excluded.";
        }
        private static WorldArea Build(Transform parent, string name, string id, string label, Rect bounds, Action<Transform> build, Sprite art)
        {
            var root = new GameObject(name); var area = root.AddComponent<WorldArea>();
            Set(area, "areaId", id); Set(area, "displayName", label); Set(area, "transitionIllustration", art);
            var so = new SerializedObject(area); so.FindProperty("cameraBounds").rectValue = bounds; so.ApplyModifiedPropertiesWithoutUndo();
            build(root.transform);
            var path = Prefabs + name + ".prefab"; PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent)).GetComponent<WorldArea>();
        }

        public static string CleanCounterSpriteMesh()
        {
            // Precise standard sprite extraction, preserving the old pixel pivot and source PNG.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "OriginalTavernProps.png");
            var spritePath = Art + "TavernCounterTrimmed.asset";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                sprite = Sprite.Create(texture, new Rect(860, 730, 384, 180),
                    new Vector2(.5f, 110f / 180f), 100, 0, SpriteMeshType.FullRect);
                sprite.name = "TavernCounterTrimmed"; AssetDatabase.CreateAsset(sprite, spritePath);
            }
            var path = Prefabs + "TavernInterior.prefab";
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var bar = root.transform.Find("FinishedTavern/Counter_FrameDisabled");
                foreach (var old in bar.GetComponents<MonoBehaviour>().Where(c => c != null && c.GetType().Name == "SpriteSilhouette").ToArray())
                    UnityEngine.Object.DestroyImmediate(old);
                bar.GetComponent<SpriteRenderer>().sprite = sprite;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            return "Standard Sprite Rect excludes neighboring chair fragments; source pixels, placement and collider unchanged.";
        }
        public static string ConfigureWalkableRegions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play.");
            foreach (var name in new[] { "UpperDeck", "HarborDock", "HarborCrossroads", "HarborTown", "HarborMarket", "TavernInterior" })
            {
                var path = Prefabs + name + ".prefab";
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var regions = name == "UpperDeck" ? new[] { new Rect(-5.3f, -1.7f, 10.6f, 3.4f) }
                        : name == "HarborDock" ? new[] { new Rect(-7.3f, -.9f, 7.8f, 1.8f), new Rect(.5f, -3.3f, 6.8f, 6.6f) }
                        : name == "TavernInterior" ? new[] { new Rect(-5.7f, -3.1f, 11.4f, 6.2f) }
                        : new[] { new Rect(-7.3f, -3.7f, 14.6f, 7.4f) };
                    var so = new SerializedObject(root.GetComponent<WorldArea>());
                    var p = so.FindProperty("walkableRegions"); p.arraySize = regions.Length;
                    for (var i = 0; i < regions.Length; i++) p.GetArrayElementAtIndex(i).rectValue = regions[i];
                    so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            var scene = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "Walkable regions distinguished from camera extents; no collider or furniture resizing.";
        }

        public static string PrepareWorldFont()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play.");
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset");
            var folder = "Assets/Game/Content/Fonts"; if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Game/Content", "Fonts");
            var path = folder + "/Galmuri11World.asset";
            var target = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (target == null)
            {
                target = TMP_FontAsset.CreateFontAsset(source.sourceFontFile, (int)source.faceInfo.pointSize,
                    source.atlasPadding, source.atlasRenderMode, source.atlasWidth, source.atlasHeight,
                    AtlasPopulationMode.Dynamic, true);
                target.name = "Galmuri11World";
            }
            var world = UnityEngine.Object.FindAnyObjectByType<WorldTravelView>();
            var chars = string.Concat(SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true)).Select(t => t.text))
                + string.Concat(source.characterTable.Select(c => char.ConvertFromUtf32((int)c.unicode)))
                + "이동은 완료됐지만 저장하지 못했습니다. 재개 버튼으로 저장을 재시도하세요.이동할 공간을 준비하지 못했습니다. 원래 위치를 유지합니다.저장 재시도";
            chars = new string(chars.Where(c => !char.IsControl(c)).Distinct().ToArray());
            var toAdd = new string(chars.Where(c => !target.HasCharacter(c)).ToArray());
            if (toAdd.Length > 0) target.TryAddCharacters(toAdd, out _);
            var missing = new string(chars.Where(c => !target.HasCharacter(c)).ToArray());
            if (missing.Length > 0) throw new InvalidOperationException("Missing source glyphs: " + missing);
            if (AssetDatabase.GetAssetPath(target) == "")
            {
                AssetDatabase.CreateAsset(target, path);
                AssetDatabase.AddObjectToAsset(target.material, target);
                foreach (var atlas in target.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, target);
            }
            foreach (var area in world.Areas)
            {
                if (area.AreaId == "captain-cabin") continue;
                var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(area.gameObject);
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) { label.font = target; EditorUtility.SetDirty(label); }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            // Same source face/point size: extend required glyph coverage without resizing existing UI.
            foreach (var label in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true)))
                if (label.name == "InteractionHint" || label.name == "SessionMessage" || label.name == "AreaName" || label.name == "Destination"
                    || label.name == "Sign" || (label.text ?? "").Any(c => !char.IsControl(c) && !label.font.HasCharacter(c)))
                {
                    label.font = target; EditorUtility.SetDirty(label);
                    if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
            EditorUtility.SetDirty(target); AssetDatabase.SaveAssets();
            var scene = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "Same Galmuri11 source; world label glyphs " + target.characterTable.Count + "; atlases " + target.atlasTextures.Length;
        }
        private static SpriteRenderer Prop(Transform parent, string name, Sprite sprite, Vector2 position, float width, bool solid, int? order = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite;
            var scale = width / sprite.bounds.size.x; go.transform.localScale = Vector3.one * scale;
            sr.sortingOrder = order ?? 100 - Mathf.RoundToInt((position.y - sprite.bounds.size.y * scale * .5f) * 20);
            if (solid) Solid(parent, name + "_Collision", position + Vector2.down * sprite.bounds.size.y * scale * .25f,
                new Vector2(width * .9f, sprite.bounds.size.y * scale * .45f));
            return sr;
        }
        private static void Tile(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size; sr.sortingOrder = order;
        }
        private static void Solid(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.layer = LayerMask.NameToLayer("WorldSolid"); var box = go.AddComponent<BoxCollider2D>(); box.size = size;
        }
        private static void Bounds(Transform parent, Rect r)
        {
            Solid(parent, "BoundaryWest", new Vector2(r.xMin - .15f, r.center.y), new Vector2(.3f, r.height + .6f));
            Solid(parent, "BoundaryEast", new Vector2(r.xMax + .15f, r.center.y), new Vector2(.3f, r.height + .6f));
            Solid(parent, "BoundaryNorth", new Vector2(r.center.x, r.yMax + .15f), new Vector2(r.width, .3f));
            Solid(parent, "BoundarySouth", new Vector2(r.center.x, r.yMin - .15f), new Vector2(r.width, .3f));
        }
        private static void Pair(List<WorldPortal> list, List<WorldArea> areas, int a, Vector2 ap, Vector2 aa,
            int b, Vector2 bp, Vector2 ba, TravelKind ab, TravelKind back)
        {
            var arrivalA = new GameObject("Arrival_" + areas[b].AreaId).transform; arrivalA.SetParent(areas[a].transform, false); arrivalA.localPosition = aa;
            var arrivalB = new GameObject("Arrival_" + areas[a].AreaId).transform; arrivalB.SetParent(areas[b].transform, false); arrivalB.localPosition = ba;
            list.Add(Portal(areas[a], ap, areas[b], arrivalB, ab)); list.Add(Portal(areas[b], bp, areas[a], arrivalA, back));
        }
        private static WorldPortal Portal(WorldArea source, Vector2 p, WorldArea dest, Transform arrival, TravelKind kind)
        {
            var go = new GameObject("Portal_" + dest.AreaId); go.transform.SetParent(source.transform, false); go.transform.localPosition = p;
            var portal = go.AddComponent<WorldPortal>();
            Set(portal, "portalId", source.AreaId + ":" + dest.AreaId); Set(portal, "source", source); Set(portal, "destination", dest);
            Set(portal, "arrival", arrival); Set(portal, "prompt", dest.DisplayName + " 이동");
            var so = new SerializedObject(portal); so.FindProperty("kind").enumValueIndex = (int)kind; so.ApplyModifiedPropertiesWithoutUndo();
            var sr = Prop(go.transform, "Visuals", stairs, Vector2.zero, .65f, false, 80);
            var h = go.AddComponent<InteractionHighlight>(); Set(h, "artwork", sr); Set(h, "outlineMaterial", highlight);
            Sign(go.transform, dest.DisplayName, new Vector2(0, .65f), .7f);
            return portal;
        }
        private static void Sign(Transform parent, string text, Vector2 position, float width)
        {
            var go = new GameObject("Sign"); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.AddComponent<TextMeshPro>(); label.font = font; label.text = text; label.fontSize = 1.6f;
            label.alignment = TextAlignmentOptions.Center; label.color = new Color(1, .93f, .76f);
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(2, width * 3), .55f);
            label.GetComponent<MeshRenderer>().sortingOrder = 500;
        }
        private static GameObject Ui(string name, Transform parent, Vector2 min, Vector2 max, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.sizeDelta = size; return go;
        }
        private static TMP_Text UiText(string name, Transform parent, Vector2 p, Vector2 size, float fontSize)
        {
            var go = Ui(name, parent, Vector2.one * .5f, Vector2.one * .5f, size);
            ((RectTransform)go.transform).anchoredPosition = p; var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1, .93f, .76f); label.raycastTarget = false; return label;
        }
        private static void Array(SerializedObject so, string name, UnityEngine.Object[] values)
        {
            var p = so.FindProperty(name); p.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        private static void Set(UnityEngine.Object target, string field, object value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(field);
            if (value is string s) p.stringValue = s; else p.objectReferenceValue = value as UnityEngine.Object;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
