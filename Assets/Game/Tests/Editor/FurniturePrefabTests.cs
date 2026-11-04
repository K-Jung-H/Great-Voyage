using GreatVoyage.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GreatVoyage.Tests
{
    public sealed class FurniturePrefabTests
    {
        [Test] public void VisualScalingDoesNotResizeCollisionOrCellArea()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var name in new[] { "NavigationDeskView", "CaptainBedView", "CaptainChairView", "SeaChestView" })
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Facilities/" + name + ".prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                    var furniture = instance.GetComponent<CellSize>();
                    var visuals = instance.transform.Find("Visuals");
                    var collision = instance.transform.Find("Collision").GetComponent<BoxCollider2D>();
                    Assert.That(furniture.CellArea.transform.parent, Is.EqualTo(instance.transform));
                    Assert.That(furniture.Settings.CellWorldSize, Is.EqualTo(1));
                    Assert.That(visuals.GetComponent<Collider2D>(), Is.Null);
                    Assert.That(collision.GetComponent<SpriteRenderer>(), Is.Null);
                    Physics2D.SyncTransforms();
                    var size = collision.bounds.size;
                    Assert.That(size.sqrMagnitude, Is.GreaterThan(.001f), name + " collision must have real bounds");
                    var cellPosition = furniture.CellArea.transform.position;
                    visuals.localScale *= .5f;
                    Physics2D.SyncTransforms();
                    Assert.That(Vector3.Distance(size, collision.bounds.size), Is.LessThan(.0001f), name);
                    Assert.That(furniture.CellArea.transform.position, Is.EqualTo(cellPosition), name);
                    Object.DestroyImmediate(instance);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void SharedSettingsChangeGridAndFurnitureDimensionsWithoutMovingEntities()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GridSettings settings = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/World/CaptainCabin.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var space = root.GetComponent<PlacementSpace>();
                settings = Object.Instantiate(space.Settings);
                var so = new SerializedObject(space); so.FindProperty("settings").objectReferenceValue = settings;
                so.ApplyModifiedPropertiesWithoutUndo();
                foreach (var cell in root.GetComponentsInChildren<CellSize>())
                {
                    var serialized = new SerializedObject(cell); serialized.FindProperty("settings").objectReferenceValue = settings;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var desk = root.GetComponentInChildren<JournalDesk>().GetComponent<CellSize>();
                var position = desk.transform.position;
                Assert.That(desk.WorldSize, Is.EqualTo(new Vector2(2, 1)));
                Assert.That(space.GetGrid().CellSize, Is.EqualTo(1));
                var global = new SerializedObject(settings); global.FindProperty("cellWorldSize").floatValue = 2;
                global.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(desk.WorldSize, Is.EqualTo(new Vector2(4, 2)));
                Assert.That(space.GetGrid().CellSize, Is.EqualTo(2));
                Assert.That(space.GetGrid().CellCount, Is.EqualTo(63));
                Assert.That(desk.transform.position, Is.EqualTo(position));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (settings != null) Object.DestroyImmediate(settings);
            }
        }
    }
}
