using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GreatVoyage.Editor
{
    public static class OceanStaticReviewAuthoring
    {
        public const string ReviewScene = "Assets/Game/Scenes/Review/OceanStaticReview.unity";
        public const string MaterialPath = "Assets/Game/Content/Art/Ocean/ToonOceanStatic.mat";
        private const string Art = "Assets/Game/Content/Art/Ocean/";
        private const string Prefab = "Assets/Game/Content/Prefabs/World/OceanSurface.prefab";

        [MenuItem("Great Voyage/Review/Create Static Ocean")]
        public static string Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");
            if (File.Exists(ReviewScene) || File.Exists(MaterialPath))
                throw new InvalidOperationException("Review already authored; edit the existing assets.");
            Ensure("Assets/Game/Scenes/Review"); Ensure(Art.TrimEnd('/'));
            var material = new Material(Shader.Find("Great Voyage/Ocean/Toon Surface")) { name = "ToonOceanStatic" };
            AssetDatabase.CreateAsset(material, MaterialPath);
            var carrier = new Texture2D(4,4,TextureFormat.RGBA32,false) { name = "OceanSurfaceCarrier", filterMode = FilterMode.Point };
            carrier.SetPixels(Enumerable.Repeat(Color.white,16).ToArray()); carrier.Apply();
            AssetDatabase.CreateAsset(carrier, Art + "OceanSurfaceCarrier.asset");
            var sprite = Sprite.Create(carrier,new Rect(0,0,4,4),Vector2.one*.5f,4,0,SpriteMeshType.FullRect);
            sprite.name = "OceanSurfaceCarrier"; AssetDatabase.AddObjectToAsset(sprite,carrier);
            var source = new GameObject("OceanSurface");
            try
            {
                var renderer = source.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite; renderer.sharedMaterial = material;
                renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = new Vector2(20,14);
                PrefabUtility.SaveAsPrefabAsset(source,Prefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 8.4f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.18f,.31f);
            camera.transform.position = new Vector3(0,0,-10);
            var ocean = new GameObject("StaticOcean").transform;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            foreach (var x in new[] { -10f,10f }) foreach (var y in new[] { -7f,7f })
            {
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(asset,ocean);
                tile.name = "OceanSurface_"+x+"_"+y;
                tile.transform.localPosition = new Vector3(x,y,0);
            }
            EditorSceneManager.SaveScene(scene,ReviewScene);
            return "Static four-surface ocean review; no navigation, HUD, particles, lights or time driver.";
        }
        private static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash=path.LastIndexOf('/'); Ensure(path.Substring(0,slash));
            AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
    }
}
