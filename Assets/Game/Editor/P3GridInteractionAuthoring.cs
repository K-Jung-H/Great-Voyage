using System;
using System.IO;
using System.Linq;
using GreatVoyage.Composition;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GreatVoyage.Editor
{
    public static class P3GridInteractionAuthoring
    {
        private const string Definitions = "Assets/Game/Content/Definitions/";
        private const string Prefabs = "Assets/Game/Content/Prefabs/Facilities/";
        private const string Cabin = "Assets/Game/Content/Prefabs/World/CaptainCabin.prefab";
        private const string Evidence = "Agents/References/P3/GridInteraction/";

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Great Voyage/Apply Grid Areas and Interaction")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != "Assets/Game/Scenes/Game.unity")
                throw new InvalidOperationException("Open the saved product scene outside Play.");
            var settings = AssetDatabase.LoadAssetAtPath<GridSettings>(Definitions + "GridSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GridSettings>();
                AssetDatabase.CreateAsset(settings, Definitions + "GridSettings.asset");
            }
            var materialPath = "Assets/Game/Content/Art/InteractionOutline.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Content/Art/InteractionOutline.shader");
                if (shader == null) throw new InvalidOperationException("Outline shader not imported.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
            }

            foreach (var name in new[] { "NavigationDesk", "CaptainBed", "CaptainChair", "SeaChest" })
            {
                var path = Prefabs + name + "View.prefab";
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var cell = root.GetComponent<CellSize>();
                    if (cell == null)
                    {
                        cell = root.AddComponent<CellSize>();
                        var legacy = root.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "FurnitureFootprint");
                        var data = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(Definitions + name + ".asset");
                        var dimensions = name == "NavigationDesk" ? new Vector2Int(2, 1)
                            : name == "CaptainBed" ? new Vector2Int(1, 2) : Vector2Int.zero;
                        if (legacy != null)
                        {
                            var oldData = new SerializedObject(legacy).FindProperty("definition").objectReferenceValue;
                            var oldSize = oldData == null ? null : new SerializedObject(oldData).FindProperty("footprintCells");
                            if (oldSize != null) dimensions = oldSize.vector2IntValue;
                        }
                        var so = new SerializedObject(cell);
                        so.FindProperty("definition").objectReferenceValue = data;
                        so.FindProperty("cells").vector2IntValue = dimensions;
                        so.FindProperty("cellArea").objectReferenceValue = root.GetComponentInChildren<FurnitureCellArea>();
                        so.ApplyModifiedPropertiesWithoutUndo();
                        if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
                    }
                    Set(cell, "settings", settings);
                    EditorUtility.SetDirty(cell.Definition);
                    AssetDatabase.SaveAssetIfDirty(cell.Definition);
                    if (name == "NavigationDesk")
                    {
                        var highlight = root.GetComponent<InteractionHighlight>();
                        if (highlight == null) highlight = root.AddComponent<InteractionHighlight>();
                        Set(highlight, "artwork", root.GetComponentInChildren<SpriteRenderer>());
                        Set(highlight, "outlineMaterial", material);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<GameObject>(Cabin));
            var cabin = PrefabUtility.LoadPrefabContents(Cabin);
            try
            {
                var space = cabin.GetComponent<PlacementSpace>();
                if (space == null)
                {
                    space = cabin.AddComponent<PlacementSpace>();
                    var so = new SerializedObject(space);
                    var excluded = so.FindProperty("excludedCells"); excluded.arraySize = 3;
                    for (var i = 0; i < 3; i++) excluded.GetArrayElementAtIndex(i).vector2IntValue = new Vector2Int(4 + i, 0);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Set(space, "settings", settings);
                var floor = cabin.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.name.Contains("Floor"));
                if (floor.GetComponents<GridArea>().Length == 0)
                {
                    Area(floor.gameObject, "cabin-left", new RectInt(0, 0, 6, 6));
                    Area(floor.gameObject, "cabin-right", new RectInt(5, 0, 6, 6));
                }
                PrefabUtility.SaveAsPrefabAsset(cabin, Cabin);
            }
            finally { PrefabUtility.UnloadPrefabContents(cabin); }
            AssetDatabase.ImportAsset(Cabin, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            // Remove only the superseded scene component; entity poses and the floor are preserved.
            foreach (var old in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(c => c != null && c.GetType().Name == "PlacementZone").ToArray()) UnityEngine.Object.DestroyImmediate(old);
            var session = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CabinSessionRoot>(true)).Single();
            var desk = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JournalDesk>(true)).Single();
            var rootSettings = new SerializedObject(session); var targets = rootSettings.FindProperty("interactionTargets");
            targets.arraySize = 1; targets.GetArrayElementAtIndex(0).objectReferenceValue = desk;
            rootSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Validate();
        }

        private static void Area(GameObject floor, string id, RectInt rect)
        {
            var area = floor.AddComponent<GridArea>(); var so = new SerializedObject(area);
            so.FindProperty("areaId").stringValue = id; so.FindProperty("bounds").rectIntValue = rect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static string Validate()
        {
            var scene = SceneManager.GetActiveScene();
            var space = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlacementSpace>(true)).Single();
            space.Invalidate(); var grid = space.GetGrid();
            var furniture = space.GetComponentsInChildren<CellSize>(true);
            if (furniture.Length != 4 || furniture.Any(c => c.Settings != space.Settings || c.CellArea == null)
                || furniture.Where(c => c.IsConfigured).Any(c => !c.IsPlacementValid))
                throw new InvalidOperationException("Furniture/global grid references or placement are invalid.");
            var targets = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JournalDesk>(true)).ToArray();
            if (targets.Any(t => t.GetComponent<InteractionHighlight>() == null)) throw new InvalidOperationException("Missing highlight view.");
            var report = new { scene = scene.path, dirty = scene.isDirty, playing = EditorApplication.isPlaying,
                settings = AssetDatabase.GetAssetPath(space.Settings), cellSize = space.Settings.CellWorldSize,
                spaceId = space.SpaceId, origin = new[] { space.Origin.x, space.Origin.y },
                allowedCells = grid.CellCount,
                areas = space.GetComponentsInChildren<GridArea>(true).Select(a => new { a.AreaId,
                    rect = new[] { a.Bounds.x, a.Bounds.y, a.Bounds.width, a.Bounds.height } }).ToArray(),
                excluded = space.ExcludedCells.Select(c => new[] { c.x, c.y }).ToArray(),
                furniture = furniture.Select(c => new { c.name, kind = c.Definition.KindId,
                    cells = new[] { c.Cells.x, c.Cells.y }, c.IsConfigured, c.IsPlacementValid,
                    children = c.transform.Cast<Transform>().Select(t => t.name).ToArray() }).ToArray(),
                targets = targets.Select(t => new { t.TargetId, t.GetComponent<InteractionHighlight>().IsHighlighted }).ToArray() };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
            Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + "Structure.json", json); return json;
        }
    }
}
