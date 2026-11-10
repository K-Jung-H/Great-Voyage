using System;
using System.Linq;
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
    public static class P5NavigationAuthoring
    {
        private const string Content = "Assets/Game/Content/";
        private static readonly Color Ink = new Color(.22f, .17f, .1f);
        private static TMP_FontAsset font;
        public static string Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != "Assets/Game/Scenes/Game.unity")
                throw new InvalidOperationException("Open clean Game scene outside Play.");
            if (UnityEngine.Object.FindAnyObjectByType<NavigationView>() != null) throw new InvalidOperationException("Already authored; edit existing assets.");
            var settings = ScriptableObject.CreateInstance<NavigationSettings>();
            AssetDatabase.CreateAsset(settings, Content + "Definitions/NavigationSettings.asset");
            var kind = ScriptableObject.CreateInstance<FurnitureDefinition>(); Set(kind, "kindId", "helm");
            AssetDatabase.CreateAsset(kind, Content + "Definitions/Helm.asset");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Content + "Art/Environment/OriginalTavernProps.png");
            var wheel = Sprite.Create(texture, new Rect(1046, 413, 122, 117), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            wheel.name = "HelmWheel"; AssetDatabase.CreateAsset(wheel, Content + "Art/Environment/HelmWheel.asset");
            var helmRoot = new GameObject("HelmView");
            try
            {
                var cell = helmRoot.AddComponent<CellSize>();
                Set(cell, "settings", AssetDatabase.LoadAssetAtPath<GridSettings>(Content + "Definitions/GridSettings.asset"));
                Set(cell, "definition", kind);
                var area = Child("CellArea", helmRoot.transform).AddComponent<FurnitureCellArea>(); Set(cell, "cellArea", area);
                var serialized = new SerializedObject(cell); serialized.FindProperty("cells").vector2IntValue = Vector2Int.one; serialized.ApplyModifiedPropertiesWithoutUndo();
                var visuals = Child("Visuals", helmRoot.transform); visuals.transform.localScale = Vector3.one * .75f;
                var renderer = visuals.AddComponent<SpriteRenderer>(); renderer.sprite = wheel; renderer.sortingOrder = 99;
                var collision = Child("Collision", helmRoot.transform); collision.layer = LayerMask.NameToLayer("WorldSolid");
                collision.transform.localPosition = Vector3.down * .15f;
                var collider = collision.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.7f, .25f);
                var approach = Child("ApproachPoint", helmRoot.transform).transform; approach.localPosition = Vector3.down * .95f;
                var helm = helmRoot.AddComponent<HelmStation>(); Set(helm, "approachPoint", approach);
                var highlight = helmRoot.AddComponent<InteractionHighlight>(); Set(highlight, "artwork", renderer);
                Set(highlight, "outlineMaterial", AssetDatabase.LoadAssetAtPath<Material>(Content + "Art/InteractionOutline.mat"));
                PrefabUtility.SaveAsPrefabAsset(helmRoot, Content + "Prefabs/Facilities/HelmView.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(helmRoot); }
            var upperPath = Content + "Prefabs/World/UpperDeck.prefab";
            var upper = PrefabUtility.LoadPrefabContents(upperPath);
            try
            {
                var helm = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Content + "Prefabs/Facilities/HelmView.prefab"), upper.transform);
                helm.transform.localPosition = new Vector3(-3.5f, .5f);
                PrefabUtility.SaveAsPrefabAsset(upper, upperPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(upper); }
            AssetDatabase.ImportAsset(upperPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var canvas = GameObject.Find("ScreenCanvas").transform;
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Content + "Fonts/Galmuri11World.asset");
            var view = canvas.gameObject.AddComponent<NavigationView>();
            var status = Label("NavigationStatus", canvas, new Vector2(0, -55), new Vector2(660, 42), 23, new Color(1, .93f, .76f));
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(.5f, 1);
            status.text = "정박 중";
            status.transform.SetAsFirstSibling();
            var page = canvas.Find("JournalScreen/LeatherBinding/ChartPage");
            var viewport = Ui("LiveSeaChart", page, Vector2.zero, Vector2.zero); viewport.AddComponent<RectMask2D>();
            var chart = (RectTransform)viewport.transform; chart.anchorMin = Vector2.zero; chart.anchorMax = Vector2.one;
            chart.offsetMin = new Vector2(30, 135); chart.offsetMax = new Vector2(-30, -60);
            var seaBackground = viewport.AddComponent<Image>(); seaBackground.color = new Color(.57f, .7f, .71f); seaBackground.raycastTarget = false;
            var mapArt = Ui("MapArtwork", chart, Vector2.zero, Vector2.zero).AddComponent<Image>();
            mapArt.sprite = page.GetComponent<Image>().sprite; mapArt.raycastTarget = false;
            page.GetComponent<Image>().sprite = AssetDatabase.LoadAllAssetsAtPath(Content + "Art/UI/JournalMaterialsAtlas.png")
                .OfType<Sprite>().Single(s => s.name == "ParchmentPanel");
            for (var i = 1; i < 10; i++)
            {
                var x = Ui("Longitude", mapArt.transform, Vector2.zero, Vector2.zero).AddComponent<Image>();
                x.color = new Color(Ink.r, Ink.g, Ink.b, .12f); x.raycastTarget = false;
                x.rectTransform.anchorMin = new Vector2(i / 10f, 0); x.rectTransform.anchorMax = new Vector2(i / 10f, 1); x.rectTransform.sizeDelta = new Vector2(1, 0);
                var y = Ui("Latitude", mapArt.transform, Vector2.zero, Vector2.zero).AddComponent<Image>();
                y.color = x.color; y.raycastTarget = false;
                y.rectTransform.anchorMin = new Vector2(0, i / 10f); y.rectTransform.anchorMax = new Vector2(1, i / 10f); y.rectTransform.sizeDelta = new Vector2(0, 1);
            }
            var north = Label("North", chart, new Vector2(0, -20), new Vector2(80, 28), 20, Ink);
            north.text = "N"; north.rectTransform.anchorMin = north.rectTransform.anchorMax = new Vector2(.5f, 1);
            var marker = Ui("CurrentShip", chart, Vector2.zero, new Vector2(54, 22)).AddComponent<Image>();
            marker.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Content + "Art/Environment/TravelHull.asset"); marker.preserveAspect = true; marker.raycastTarget = false;
            Label("ChartTitle", page, new Vector2(0, 345), new Vector2(750, 36), 25, Ink).text = "현재 함선 위치 · 근해도";
            var plate = Ui("ChartReadoutPlate", page, new Vector2(0, -320), new Vector2(990, 110)).AddComponent<Image>();
            plate.sprite = AssetDatabase.LoadAllAssetsAtPath(Content + "Art/UI/JournalMaterialsAtlas.png").OfType<Sprite>().Single(s => s.name == "ParchmentPanel");
            plate.type = Image.Type.Sliced; plate.raycastTarget = false;
            var readout = Label("NavigationReadout", page, new Vector2(-155, -320), new Vector2(650, 105), 18, Ink);
            readout.text = "현재 함선 위치 · 근해도";
            var zoomIn = Button("ChartZoomIn", page, new Vector2(215, -335), new Vector2(50, 44), "+");
            var zoomOut = Button("ChartZoomOut", page, new Vector2(275, -335), new Vector2(50, 44), "−");
            var locate = Button("ChartLocate", page, new Vector2(390, -335), new Vector2(150, 44), "내 위치");
            Set(view, "status", status); Set(view, "chartReadout", readout); Set(view, "chart", chart); Set(view, "marker", marker.rectTransform);
            Set(view, "mapArtwork", mapArt.rectTransform);
            Set(view, "zoomIn", zoomIn); Set(view, "zoomOut", zoomOut); Set(view, "locate", locate);
            var root = UnityEngine.Object.FindAnyObjectByType<CabinSessionRoot>();
            Set(root, "navigationView", view); Set(root, "navigationSettings", settings);
            var rootData = new SerializedObject(root); var targets = rootData.FindProperty("interactionTargets");
            targets.arraySize++; targets.GetArrayElementAtIndex(targets.arraySize - 1).objectReferenceValue =
                GameObject.Find("WorldContent").GetComponentsInChildren<HelmStation>(true).Single(); rootData.ApplyModifiedPropertiesWithoutUndo();
            var chars = "정박 중항해방향방위리수동 조타직진현재 위치근해도위쪽 북쪽표시 폭지도 단위범위 밖입니다내 출항하여조작종료저장재시도−+°0123456789.,: ·()/ADEsckn";
            var missing = new string(chars.Where(c => !font.HasCharacter(c)).Distinct().ToArray()); if (missing.Length > 0) font.TryAddCharacters(missing, out _);
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssetIfDirty(font);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "Authored helm, fixed-deck sailing surface and live chart; cabin furniture unchanged.";
        }
        private static GameObject Child(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go; }
        private static GameObject Ui(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return go;
        }
        private static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var label = Ui(name, parent, position, size).AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = fontSize; label.color = color;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; return label;
        }
        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string text)
        {
            var go = Ui(name, parent, position, size); var image = go.AddComponent<Image>(); image.color = new Color(.85f, .75f, .55f, .7f);
            var button = go.AddComponent<Button>(); button.targetGraphic = image;
            Label("Label", go.transform, Vector2.zero, size, 21, Ink).text = text;
            return button;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(UnityEngine.Object target, string field, string value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).stringValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
