using System;
using System.Collections.Generic;
using System.Linq;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace GreatVoyage.Editor
{
    public static class ColliderWaterlineReviewAuthoring
    {
        public const string ScenePath = "Assets/Game/Scenes/Review/OceanColliderBakeReview.unity";
        public const string PrefabPath = "Assets/Game/Content/Prefabs/World/StartingShipWaterline.prefab";
        public static WaterContactSource Assign(GameObject go, params Collider2D[] colliders)
        {
            var source = go.GetComponent<WaterContactSource>() ?? go.AddComponent<WaterContactSource>();
            source.SetContactColliders(colliders);
            return source;
        }
        public static string Migrate()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open stopped Collider review first.");
            var ship = SceneManager.GetActiveScene().GetRootGameObjects().Single(x => x.name == "Ship");
            if (PrefabUtility.IsPartOfPrefabInstance(ship))
                PrefabUtility.UnpackPrefabInstance(ship, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            var upper = ship.transform.Find("Deck/UpperDeck");
            if (upper == null)
            {
                upper = new GameObject("UpperDeck").transform;
                upper.SetParent(ship.transform.Find("Deck"), false);
                var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                foreach (var type in new[] { typeof(WorldArea), typeof(PlacementSpace), typeof(GridArea) })
                {
                    var old = ship.GetComponent(type);
                    if (old == null)
                        continue;
                    var copy = upper.gameObject.AddComponent(type);
                    EditorUtility.CopySerialized(old, copy);
                    replacements.Add(old, copy);
                }
                foreach (var component in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Component>(true)))
                {
                    if (component == null)
                        continue;
                    var so = new SerializedObject(component);
                    var it = so.GetIterator();
                    var changed = false;
                    while (it.Next(true))
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null && replacements.TryGetValue(it.objectReferenceValue, out var replacement))
                        {
                            it.objectReferenceValue = replacement;
                            changed = true;
                        }
                    if (changed)
                        so.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var old in replacements.Keys)
                    UnityEngine.Object.DestroyImmediate(old);
            }
            foreach (Transform child in upper.parent.Cast<Transform>().Where(t => t != upper).ToArray())
                child.SetParent(upper, true);
            var placement = upper.GetComponent<PlacementSpace>();
            placement.Invalidate();
            foreach (var cell in upper.GetComponentsInChildren<CellSize>(true))
                if (!placement.Contains(cell)) throw new InvalidOperationException(cell.name + ": invalid grid after hierarchy migration.");
            var surface = ship.GetComponent<ShipWaterSurface>() ?? ship.AddComponent<ShipWaterSurface>();
            foreach (var source in ship.GetComponentsInChildren<WaterContactSource>(true))
            {
                if (source.Colliders == null || source.Colliders.Length == 0 || source.Colliders.Any(c => c == null))
                {
                    var local = source.GetComponents<Collider2D>();
                    if (local.Length != 1)
                        throw new InvalidOperationException(source.name + ": ambiguous Collider repair.");
                    source.SetContactColliders(local);
                }
                source.BindTo(surface);
                EditorUtility.SetDirty(source);
            }
            var authoring = ship.GetComponentInChildren<WaterlineBakeAuthoring>(true);
            var data = new SerializedObject(authoring);
            data.FindProperty("surface").objectReferenceValue = surface;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (authoring.ReviewOverlay != null)
                authoring.ReviewOverlay.sprite = null;
            EditorUtility.SetDirty(surface);
            PrefabUtility.SaveAsPrefabAssetAndConnect(ship, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = authoring.gameObject;
            return "Migrated Ship/Hull, Deck/UpperDeck, Editor. Preserved current contact shape, repaired explicit references. No SDF asset required.";
        }
    }
}
