using System;
using System.Collections.Generic;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEngine;
namespace GreatVoyage.Editor
{
    public static class ColliderWaterlineBaker
    {
        private static readonly Dictionary<WaterlineBakeAuthoring, Sprite> overlays = new Dictionary<WaterlineBakeAuthoring, Sprite>();
        public static void Generate(WaterlineBakeAuthoring authoring)
        {
            authoring.GenerateReview();
            var result = authoring.Review;
            var r = authoring.ReviewOverlay;
            if (r != null)
            {
                if (overlays.TryGetValue(authoring, out var old) && old != null)
                {
                    var oldTexture = old.texture;
                    UnityEngine.Object.DestroyImmediate(old);
                    if (oldTexture != null)
                        UnityEngine.Object.DestroyImmediate(oldTexture);
                }
                var tex = MaskPreview(result);
                var b = result.Bounds;
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * .5f, tex.width / b.width, 0, SpriteMeshType.FullRect);
                sprite.name = "Water contact review (temporary)";
                sprite.hideFlags = HideFlags.HideAndDontSave;
                overlays[authoring] = sprite;
                r.sprite = sprite;
                r.enabled = true;
                r.transform.position = authoring.ShipRoot.TransformPoint(b.center);
                r.transform.rotation = authoring.ShipRoot.rotation;
                var desired = authoring.ShipRoot.lossyScale;
                var parent = r.transform.parent.lossyScale;
                r.transform.localScale = new Vector3(desired.x / parent.x * b.width / sprite.bounds.size.x, desired.y / parent.y * b.height / sprite.bounds.size.y, 1);
            }
            SceneView.RepaintAll();
        }
        public static Texture2D MaskPreview(WaterlineGenerator result)
        {
            var source = result.Texture;
            var d = source.GetPixelData<float>(0);
            var c = new Color32[d.Length];
            var edge = Mathf.Min(result.Bounds.width / source.width, result.Bounds.height / source.height);
            for (var i = 0; i < c.Length; i++)
                c[i] = Mathf.Abs(d[i]) < edge * 1.3f ? new Color32(30, 255, 220, 240) : d[i] < 0 ? new Color32(255, 140, 25, 85) : new Color32(0, 0, 0, 0);
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true) { name = "Contact review mask", hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(c);
            texture.Apply(false);
            return texture;
        }
        public static Texture2D DistancePreview(WaterlineGenerator result)
        {
            var source = result.Texture;
            var d = source.GetPixelData<float>(0);
            var c = new Color[d.Length];
            var edge = Mathf.Min(result.Bounds.width / source.width, result.Bounds.height / source.height);
            for (var i = 0; i < c.Length; i++)
            {
                var fade = Mathf.Clamp01(Mathf.Abs(d[i]) / 1.5f);
                c[i] = Mathf.Abs(d[i]) < edge * 1.3f ? new Color(.1f, 1, .85f) : d[i] < 0 ? Color.Lerp(new Color(1, .38f, .18f), new Color(.4f, .07f, .04f), fade) : Color.Lerp(new Color(.16f, .62f, 1), new Color(.03f, .12f, .35f), fade);
                c[i].a = 1;
            }
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true) { name = "Signed distance review", hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(c);
            texture.Apply(false);
            return texture;
        }
        [InitializeOnLoadMethod]
        private static void InitializeCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }
        private static void Cleanup()
        {
            foreach (var authoring in Resources.FindObjectsOfTypeAll<WaterlineBakeAuthoring>())
                authoring.ReleaseReview();
            foreach (var sprite in overlays.Values)
                if (sprite != null)
                {
                    var texture = sprite.texture;
                    UnityEngine.Object.DestroyImmediate(sprite);
                    if (texture != null)
                        UnityEngine.Object.DestroyImmediate(texture);
                }
            overlays.Clear();
        }
    }
}
