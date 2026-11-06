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
    public static class OceanNavigationAuthoring
    {
        public const string ScenePath="Assets/Game/Scenes/Review/OceanNavigationReview.unity";
        public const string MaterialPath="Assets/Game/Content/Art/Ocean/ToonOceanNavigation.mat";
        public static string Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save the current scene first.");
            if(File.Exists(ScenePath) || File.Exists(MaterialPath))throw new InvalidOperationException("Already authored.");
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Game.unity");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath,true);
            EditorSceneManager.OpenScene(ScenePath);
            var roots=SceneManager.GetActiveScene().GetRootGameObjects();
            var area=roots.SelectMany(x=>x.GetComponentsInChildren<WorldArea>(true)).Single(x=>x.AreaId=="upper-deck");
            var sea=area.transform.Find("Sea").GetComponent<SpriteRenderer>();
            var size=sea.size;
            var material=new Material(AssetDatabase.LoadAssetAtPath<Material>(OceanAmbientReviewAuthoring.MaterialPath)){name="ToonOceanNavigation"};
            material.SetVector("_OceanPhase",Vector4.zero);material.SetVector("_OceanTravel",Vector4.zero);
            material.SetVector("_RelativeOceanOffset",Vector4.zero);AssetDatabase.CreateAsset(material,MaterialPath);
            sea.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Game/Content/Art/Ocean/OceanSurfaceCarrier.asset").OfType<Sprite>().Single();
            sea.sharedMaterial=material;sea.drawMode=SpriteDrawMode.Sliced;sea.size=size;
            sea.SetPropertyBlock(null);PrefabUtility.RecordPrefabInstancePropertyModifications(sea);
            var group=new GameObject("OceanPresentation");
            var driver=group.AddComponent<OceanAmbientDriver>();Set(driver,"surfaces",new UnityEngine.Object[]{sea});Set(driver,"sourceMaterial",material);
            var binding=group.AddComponent<OceanNavigationBinding>();Set(binding,"ocean",driver);
            var nav=roots.SelectMany(x=>x.GetComponentsInChildren<NavigationView>(true)).Single();Set(nav,"oceanBinding",binding);
            var session=roots.SelectMany(x=>x.GetComponentsInChildren<CabinSessionRoot>(true)).Single();
            var data=new SerializedObject(session);data.FindProperty("saveDirectoryName").stringValue="Review/WaterStage3";data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return "Created real gameplay review scene with isolated save directory; Game scene and UpperDeck prefab preserved.";
        }
        private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
        {var s=new SerializedObject(target);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        private static void Set(UnityEngine.Object target,string field,UnityEngine.Object[] values)
        {var s=new SerializedObject(target);var a=s.FindProperty(field);a.arraySize=values.Length;for(var i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];s.ApplyModifiedPropertiesWithoutUndo();}
    }
}
