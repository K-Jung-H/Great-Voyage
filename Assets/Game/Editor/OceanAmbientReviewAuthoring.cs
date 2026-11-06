using System;
using System.IO;
using System.Linq;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GreatVoyage.Editor
{
    public static class OceanAmbientReviewAuthoring
    {
        public const string ScenePath="Assets/Game/Scenes/Review/OceanAmbientReview.unity";
        public const string MaterialPath="Assets/Game/Content/Art/Ocean/ToonOceanAmbient.mat";
        public static string Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play and save current scene first.");
            if(File.Exists(ScenePath) || File.Exists(MaterialPath)) throw new InvalidOperationException("Already authored; edit existing assets.");
            EditorSceneManager.OpenScene(OceanStaticReviewAuthoring.ReviewScene);
            var source=AssetDatabase.LoadAssetAtPath<Material>(OceanStaticReviewAuthoring.MaterialPath);
            var material=new Material(source){ name="ToonOceanAmbient" };
            material.SetVector("_OceanPhase",Vector4.zero);
            AssetDatabase.CreateAsset(material,MaterialPath);
            var root=GameObject.Find("StaticOcean");root.name="AmbientOcean";
            var surfaces=root.GetComponentsInChildren<SpriteRenderer>();
            foreach(var surface in surfaces)
            {
                surface.sharedMaterial=material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
            }
            var driver=root.AddComponent<OceanAmbientDriver>();
            var data=new SerializedObject(driver);
            data.FindProperty("sourceMaterial").objectReferenceValue=material;
            var array=data.FindProperty("surfaces");array.arraySize=surfaces.Length;
            for(var i=0;i<surfaces.Length;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=surfaces[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath,true);
            EditorSceneManager.OpenScene(ScenePath);
            return "Ambient review created; approved static scene/material and gameplay scene preserved.";
        }
    }
}
