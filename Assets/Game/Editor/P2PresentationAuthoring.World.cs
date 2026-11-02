using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GreatVoyage.Editor
{
    public static partial class P2PresentationAuthoring
    {
        private const string SegmentPath = Art + "Environment/PartitionWallSegment.asset";

        public static void ConfigureNativePartitionSprite()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Environment/CaptainCabinAtlas.png");
            if (texture == null) throw new InvalidOperationException("Partition source texture is missing.");
            var source = Sprite("Environment/CaptainCabinAtlas.png", "PartitionWall");
            var rect = PartitionCore(source.rect);
            var created = UnityEngine.Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f),
                rect.width / 0.5f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            created.name = "PartitionWallSegment";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentPath);
            if (existing == null) AssetDatabase.CreateAsset(created, SegmentPath);
            else
            {
                EditorUtility.CopySerialized(created, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(created);
            }
            AssetDatabase.SaveAssets();
        }

        private static Rect PartitionCore(Rect bounds)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(Art + "Environment/CaptainCabinAtlas.png"));
                var pixels = texture.GetPixels32();
                var centerX = Mathf.RoundToInt(bounds.center.x);
                var minY = (int)bounds.yMax;
                var maxY = (int)bounds.yMin;
                for (var y = (int)bounds.yMin; y < (int)bounds.yMax; y++)
                    if (pixels[y * texture.width + centerX].a >= 32) { minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                if (maxY <= minY) throw new InvalidOperationException("Partition beam center is empty.");
                var height = maxY - minY + 1;
                var width = Mathf.Min(bounds.width, Mathf.RoundToInt(height * 140f / 78f));
                return new Rect(Mathf.RoundToInt(bounds.center.x - width / 2), minY, width, height);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static SpriteRect FloorSlice(string path)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(path));
                var pixels = texture.GetPixels32();
                int SeamNear(int center)
                {
                    var best = center;
                    long bestValue = long.MaxValue;
                    var radius = Math.Max(2, texture.height / 40);
                    for (var top = center - radius; top <= center + radius; top++)
                    {
                        if (top <= 0 || top >= texture.height - 1) continue;
                        long value = 0;
                        for (var x = 0; x < texture.width; x += Math.Max(1, texture.width / 256))
                        {
                            var pixel = pixels[(texture.height - 1 - top) * texture.width + x];
                            value += pixel.r + pixel.g + pixel.b;
                        }
                        if (value < bestValue) { bestValue = value; best = top; }
                    }
                    return best;
                }
                var start = SeamNear(texture.height / 8);
                var end = SeamNear(texture.height * 7 / 8);
                return Slice("CabinPlanks", 0, start, texture.width, end - start + 1, texture.height);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        public static void ApplyPainterlyWorldArt()
        {
            ImportCabin();
            ImportCharacter();
            ImportFloor();
            void Fit(Transform root, string child, Sprite sprite, Vector2 size)
            {
                var renderer = root.Find(child).GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            }
            void Edit(string path, Action<Transform> update)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try { update(contents.transform); PrefabUtility.SaveAsPrefabAsset(contents, path); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            Sprite Cabin(string name) => Sprite("Environment/CaptainCabinAtlas.png", name);
            Edit(Prefabs + "Facilities/NavigationDeskView.prefab", root => Fit(root, "Artwork", Cabin("NavigationDesk"), new Vector2(2.9f, 1.93f)));
            Edit(Prefabs + "World/CaptainCabin.prefab", root =>
            {
                Fit(root, "CaptainBed_FrameDisabled", Cabin("SingleBed"), new Vector2(1.45f, 2.3f));
                Fit(root, "SeaChest_FrameDisabled", Cabin("SeaChest"), new Vector2(1.7f, 1.12f));
                Fit(root, "CaptainChair", Cabin("CaptainChair"), new Vector2(0.95f, 1.14f));
                SetFloor(root.Find("FloorPanel").GetComponent<SpriteRenderer>());
            });
            Edit(Prefabs + "Characters/PlayerView.prefab", root =>
            {
                Sprite Part(string name) => Sprite("Characters/CaptainPartsAtlas.png", name);
                Fit(root, "Head", Part("HeadFront"), new Vector2(0.64f, 0.5f));
                Fit(root, "Body", Part("BodyFront"), new Vector2(0.55f, 0.6f));
                Fit(root, "LeftHand", Part("HandLeft"), new Vector2(0.18f, 0.2f));
                Fit(root, "RightHand", Part("HandRight"), new Vector2(0.18f, 0.2f));
                Fit(root, "BackFacingParts/HeadBack", Part("HeadBack"), new Vector2(0.64f, 0.5f));
                Fit(root, "BackFacingParts/BodyBack", Part("BodyBack"), new Vector2(0.55f, 0.6f));
            });
            ConfigureNativePartitionSprite();
            ApplyModularPartitions();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyModularPartitions()
        {
            var segment = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentPath);
            if (segment == null) throw new InvalidOperationException("A native-size partition Sprite is required.");
            var path = Prefabs + "World/CaptainCabin.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var previous = contents.transform.Find("Partitions");
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var partitions = new GameObject("Partitions");
                partitions.transform.SetParent(contents.transform, false);
                PopulatePartitions(partitions.transform);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void PopulatePartitions(Transform parent)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SegmentPath);
            if (sprite == null || Mathf.Abs(sprite.bounds.size.x - 0.5f) > 0.0001f)
                throw new InvalidOperationException("Partition segment native width must be 0.5 unit; Transform scaling is prohibited.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            WallRun("NorthWall", parent, new Vector2(0, 3.25f), 24, false, sprite, material);
            WallRun("WestWall", parent, new Vector2(-5.9f, 0), 13, true, sprite, material);
            WallRun("EastWall", parent, new Vector2(5.9f, 0), 13, true, sprite, material);
            WallRun("SouthWallLeft", parent, new Vector2(-3.5f, -3.25f), 10, false, sprite, material);
            WallRun("SouthWallRight", parent, new Vector2(3.5f, -3.25f), 10, false, sprite, material);
        }

        private static void WallRun(string name, Transform parent, Vector2 center, int count, bool vertical, Sprite sprite, Material material)
        {
            var run = new GameObject(name, typeof(BoxCollider2D));
            run.transform.SetParent(parent, false);
            run.transform.localPosition = center;
            run.transform.localRotation = Quaternion.Euler(0, 0, vertical ? 90 : 0);
            var width = sprite.bounds.size.x;
            var collider = run.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(count * width, sprite.bounds.size.y);
            for (var i = 0; i < count; i++)
            {
                var segment = new GameObject("Segment_" + i.ToString("D2"), typeof(SpriteRenderer));
                segment.transform.SetParent(run.transform, false);
                segment.transform.localPosition = new Vector3((i - (count - 1) / 2f) * width, 0, 0);
                var renderer = segment.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 0;
            }
        }
    }
}
