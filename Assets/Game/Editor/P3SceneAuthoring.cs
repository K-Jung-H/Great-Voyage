using System;
using System.IO;
using System.Linq;
using GreatVoyage.Composition;
using GreatVoyage.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GreatVoyage.Editor
{
    public static class P3SceneAuthoring
    {
        private const string Game = "Assets/Game/";
        private const string Prefabs = Game + "Content/Prefabs/";
        private static TMP_FontAsset font;
        private static Sprite leather;
        private static PhysicsMaterial2D contact;

        [MenuItem("Great Voyage/Configure P3 Cabin Slice")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != Game + "Scenes/Game.unity" || scene.isDirty)
                throw new InvalidOperationException("Open the saved product Game Scene outside Play mode.");
            Directory.CreateDirectory(Game + "Content/Input");
            Directory.CreateDirectory(Game + "Content/Physics");
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Galmuri11 SDF.asset");
            leather = AssetDatabase.LoadAllAssetsAtPath(Game + "Content/Art/UI/JournalMaterialsAtlas.png")
                .OfType<Sprite>().Single(x => x.name == "LeatherPanel");
            var solid = EnsureLayer("WorldSolid");
            var actor = EnsureLayer("CharacterBody");
            var materialPath = Game + "Content/Physics/CabinContact.asset";
            contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
            if (contact == null)
            {
                contact = new PhysicsMaterial2D("CabinContact") { friction = 0, bounciness = 0 };
                AssetDatabase.CreateAsset(contact, materialPath);
            }
            var input = WriteControls();
            Edit("Characters/PlayerView.prefab", root =>
            {
                root.gameObject.layer = actor;
                var body = Get<Rigidbody2D>(root.gameObject);
                body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 0;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.linearDamping = 0; body.angularDamping = 0;
                var collider = Get<CircleCollider2D>(root.gameObject);
                collider.offset = new Vector2(0, -0.2f); collider.radius = 0.22f; collider.sharedMaterial = contact;
                collider.excludeLayers = ~((1 << solid) | (1 << actor));
                var motor = Get<PlayerMotor>(root.gameObject);
                var visuals = root.Find("Visuals");
                if (visuals == null)
                {
                    var children = root.Cast<Transform>().ToArray();
                    visuals = new GameObject("Visuals").transform; visuals.SetParent(root, false);
                    foreach (var child in children) child.SetParent(visuals, false);
                }
                var view = Get<CharacterView>(root.gameObject);
                Set(view, "visuals", visuals);
                Set(view, "frontHead", visuals.Find("Head"));
                Set(view, "frontBody", visuals.Find("Body"));
                Set(view, "backParts", visuals.Find("BackFacingParts"));
                Set(view, "quarterParts", visuals.Find("ThreeQuarterFacingParts"));
                Set(view, "closedParts", visuals.Find("ClosedEyesParts"));
                Set(view, "leftHand", visuals.Find("LeftHand"));
                Set(view, "rightHand", visuals.Find("RightHand"));
                Set(view, "sorting", root.GetComponent<UnityEngine.Rendering.SortingGroup>());
            });
            Edit("Facilities/NavigationDeskView.prefab", root =>
            {
                var art = root.Find("Artwork").GetComponent<SpriteRenderer>();
                SolidFixture(art, solid, new Vector2(0.92f, 0.65f));
                var desk = Get<JournalDesk>(root.gameObject);
                Set(desk, "approachPoint", root.Find("ApproachPoint"));
                var serialized = new SerializedObject(desk);
                serialized.FindProperty("obstructionMask").intValue = 1 << solid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
            Edit("World/CaptainCabin.prefab", root =>
            {
                foreach (var c in root.Find("Partitions").GetComponentsInChildren<BoxCollider2D>(true))
                { c.gameObject.layer = solid; c.sharedMaterial = contact; c.excludeLayers = ~(1 << actor); }
                foreach (var r in root.Find("Partitions").GetComponentsInChildren<SpriteRenderer>(true))
                    r.sortingOrder = 100 - Mathf.RoundToInt(r.transform.position.y * 20);
                SolidFixture(root.Find("CaptainBed_FrameDisabled").GetComponent<SpriteRenderer>(), solid, new Vector2(0.8f, 0.84f));
                SolidFixture(root.Find("SeaChest_FrameDisabled").GetComponent<SpriteRenderer>(), solid, new Vector2(0.9f, 0.75f));
                SolidFixture(root.Find("CaptainChair").GetComponent<SpriteRenderer>(), solid, new Vector2(0.8f, 0.55f));
                var exit = root.Find("ExitBoundary_FrameDisabled");
                if (exit == null) { exit = new GameObject("ExitBoundary_FrameDisabled").transform; exit.SetParent(root, false); }
                exit.localPosition = new Vector3(0, -3.25f, 0);
                exit.gameObject.layer = solid;
                var block = Get<BoxCollider2D>(exit.gameObject);
                block.size = new Vector2(2.05f, 0.36f); block.sharedMaterial = contact; block.excludeLayers = ~(1 << actor);
            });
            Edit("UI/JournalScreen.prefab", root =>
            {
                var close = root.GetComponentInChildren<Button>(true);
                close.gameObject.name = "CloseJournal";
                close.interactable = true;
                close.targetGraphic.raycastTarget = true;
                close.transition = Selectable.Transition.ColorTint;
            });
            var roots = scene.GetRootGameObjects();
            var all = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var player = all.Single(x => x.name == "PlayerView");
            var deskView = roots.SelectMany(x => x.GetComponentsInChildren<JournalDesk>(true)).Single();
            var journal = all.Single(x => x.name == "JournalScreen");
            var canvas = all.Single(x => x.name == "ScreenCanvas");
            if (canvas.Find("CabinHud") != null) throw new InvalidOperationException("P3 UI already exists; edit rather than duplicating it.");
            var hudRoot = Rect("CabinHud", canvas, Vector2.zero, Vector2.zero);
            Stretch(hudRoot);
            hudRoot.SetAsFirstSibling();
            var clock = Label("GameClock", hudRoot, new Vector2(570, 500), new Vector2(340, 44), "", 25);
            clock.alignment = TextAlignmentOptions.Right;
            var hint = Label("InteractionHint", hudRoot, new Vector2(0, -480), new Vector2(1100, 50), "", 24);
            var menuButton = Button("OpenSessionMenu", canvas, new Vector2(835, 500), new Vector2(170, 48), "메뉴");
            canvas.GetComponent<Canvas>().sortingOrder = 1000;
            var menu = Rect("SessionMenu", canvas, Vector2.zero, Vector2.zero);
            Stretch(menu);
            var scrim = menu.gameObject.AddComponent<Image>(); scrim.color = new Color(0, 0, 0, 0.7f);
            var panel = Rect("LeatherPanel", menu, Vector2.zero, new Vector2(740, 460));
            var image = panel.gameObject.AddComponent<Image>(); image.sprite = leather; image.type = Image.Type.Sliced;
            Label("Title", panel, new Vector2(0, 155), new Vector2(620, 55), "일시 정지", 32);
            var resume = Button("Resume", panel, new Vector2(0, 55), new Vector2(340, 62), "계속하기");
            var exitButton = Button("SaveAndExit", panel, new Vector2(0, -35), new Vector2(340, 62), "저장 후 종료");
            var message = Label("SessionMessage", panel, new Vector2(0, -145), new Vector2(640, 95), "", 21);
            var hud = hudRoot.gameObject.AddComponent<CabinHud>();
            Set(hud, "clockLabel", clock); Set(hud, "hintLabel", hint); Set(hud, "messageLabel", message);
            Set(hud, "journal", journal.gameObject); Set(hud, "menu", menu.gameObject);
            Set(hud, "journalClose", journal.GetComponentInChildren<Button>(true));
            Set(hud, "menuButton", menuButton); Set(hud, "resume", resume); Set(hud, "saveExit", exitButton);
            menu.gameObject.SetActive(false); journal.gameObject.SetActive(false);
            var composition = new GameObject("GameSession");
            var session = composition.AddComponent<CabinSessionRoot>();
            Set(session, "player", player.GetComponent<PlayerMotor>());
            Set(session, "character", player.GetComponent<CharacterView>());
            Set(session, "desk", deskView); Set(session, "hud", hud); Set(session, "controls", input);
            var camera = Camera.main;
            var follow = Get<CabinCamera>(camera.gameObject); Set(follow, "target", player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssetIfDirty(contact);
            AssetDatabase.SaveAssets();
        }

        private static void SolidFixture(SpriteRenderer art, int layer, Vector2 fraction)
        {
            art.gameObject.layer = layer;
            var c = Get<BoxCollider2D>(art.gameObject);
            c.size = Vector2.Scale(art.sprite.bounds.size, fraction);
            c.sharedMaterial = contact;
            c.excludeLayers = ~(1 << LayerMask.NameToLayer("CharacterBody"));
            art.sortingOrder = 100 - Mathf.RoundToInt((art.bounds.min.y + 0.05f) * 20);
        }

        private static InputActionAsset WriteControls()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var world = asset.AddActionMap("World");
            var move = world.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            world.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            asset.AddActionMap("UI").AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
            var path = Game + "Content/Input/GameControls.inputactions";
            File.WriteAllText(path, asset.ToJson());
            UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        private static int EnsureLayer(string name)
        {
            var current = LayerMask.NameToLayer(name);
            if (current >= 0) return current;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("layers");
            for (var i = 8; i < layers.arraySize; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = name;
                    settings.ApplyModifiedPropertiesWithoutUndo(); return i;
                }
            throw new InvalidOperationException("No free layer.");
        }

        private static T Get<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
        private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
        {
            var s = new SerializedObject(owner); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Edit(string relative, Action<Transform> configure)
        {
            var path = Prefabs + relative; var contents = PrefabUtility.LoadPrefabContents(path);
            try { configure(contents.transform); PrefabUtility.SaveAsPrefabAsset(contents, path); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text, int pixels)
        {
            var t = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font; t.fontSize = pixels; t.color = new Color32(245, 226, 184, 255);
            t.alignment = TextAlignmentOptions.Center; t.text = text; t.raycastTarget = false;
            return t;
        }
        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string text)
        {
            var r = Rect(name, parent, position, size);
            var image = r.gameObject.AddComponent<Image>(); image.sprite = leather; image.type = Image.Type.Sliced;
            var button = r.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.highlightedColor = new Color(1, 0.88f, 0.65f);
            colors.pressedColor = new Color(0.65f, 0.6f, 0.5f); button.colors = colors;
            Label("Label", r, Vector2.zero, size - new Vector2(18, 8), text, 24);
            return button;
        }
    }
}
