using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
namespace GreatVoyage.Presentation
{
    public sealed class WaterlineGenerator : IDisposable
    {
        private readonly WaterlineGeometry.Workspace geometry = new WaterlineGeometry.Workspace();
        private readonly WaterlineDistanceField.Workspace distance = new WaterlineDistanceField.Workspace();
        private bool[] mask;
        private float[] values;
        private Texture2D current, staging;
        public Texture2D Texture => current;
        public Rect Bounds
        {
            get; private set;
        }
        public double ExtractMilliseconds
        {
            get; private set;
        }
        public double RasterMilliseconds
        {
            get; private set;
        }
        public double DistanceMilliseconds
        {
            get; private set;
        }
        public double UploadMilliseconds
        {
            get; private set;
        }
        public int Generation
        {
            get; private set;
        }
        public void Generate(Transform root, IReadOnlyList<WaterContactSource> sources, int resolution, float padding)
        {
            if (root == null || resolution < 64 || resolution > 1024 || !float.IsFinite(padding) || padding <= 0)
                throw new ArgumentException("Invalid waterline root/resolution/padding.");
            var watch = Stopwatch.StartNew();
            var inputs = Collect(root, sources);
            var shapes = inputs.Length == 0 ? new List<WaterlineGeometry.Primitive>() : geometry.Read(root, inputs);
            ExtractMilliseconds = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var bounds = new Rect(-.5f, -.5f, 1, 1);
            int w = 2, h = 2;
            if (shapes.Count > 0)
            {
                var min = shapes[0].bounds.min;
                var max = shapes[0].bounds.max;
                foreach (var shape in shapes)
                {
                    min = Vector2.Min(min, shape.bounds.min);
                    max = Vector2.Max(max, shape.bounds.max);
                }
                min -= Vector2.one * padding;
                max += Vector2.one * padding;
                bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                var longest = Mathf.Max(bounds.width, bounds.height);
                if (!float.IsFinite(longest) || longest <= 0)
                    throw new InvalidOperationException("Invalid contact bounds.");
                w = Mathf.Max(2, Mathf.CeilToInt(resolution * bounds.width / longest));
                h = Mathf.Max(2, Mathf.CeilToInt(resolution * bounds.height / longest));
            }
            if (mask == null || mask.Length != w * h)
            {
                mask = new bool[w * h];
                values = new float[w * h];
            }
            else
                Array.Clear(mask, 0, mask.Length);
            var cell = new Vector2(bounds.width / w, bounds.height / h);
            foreach (var shape in shapes)
            {
                var x0 = Mathf.Clamp(Mathf.FloorToInt((shape.bounds.xMin - bounds.xMin) / cell.x), 0, w - 1);
                var x1 = Mathf.Clamp(Mathf.CeilToInt((shape.bounds.xMax - bounds.xMin) / cell.x), 0, w - 1);
                var y0 = Mathf.Clamp(Mathf.FloorToInt((shape.bounds.yMin - bounds.yMin) / cell.y), 0, h - 1);
                var y1 = Mathf.Clamp(Mathf.CeilToInt((shape.bounds.yMax - bounds.yMin) / cell.y), 0, h - 1);
                for (var y = y0; y <= y1; y++)
                    for (var x = x0; x <= x1; x++)
                    {
                        var i = y * w + x;
                        if (!mask[i])
                            mask[i] = shape.Contains(bounds.min + new Vector2((x + .5f) * cell.x, (y + .5f) * cell.y));
                    }
            }
            RasterMilliseconds = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            if (shapes.Count == 0)
                Array.Fill(values, 1f);
            else
            {
                var found = false;
                foreach (var bit in mask)
                    found |= bit;
                if (!found)
                    throw new InvalidOperationException("No interior sample. Increase resolution or contact thickness.");
                distance.Calculate(mask, w, h, cell, values);
            }
            DistanceMilliseconds = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            if (staging == null)
                staging = new Texture2D(w, h, TextureFormat.RFloat, false, true) { name = "Waterline SDF (memory)", hideFlags = HideFlags.HideAndDontSave };
            else if (staging.width != w || staging.height != h)
                staging.Reinitialize(w, h, TextureFormat.RFloat, false);
            staging.wrapMode = TextureWrapMode.Clamp;
            staging.filterMode = FilterMode.Bilinear;
            staging.SetPixelData(values, 0);
            staging.Apply(false);
            var previous = current;
            current = staging;
            staging = previous;
            Bounds = bounds;
            Generation++;
            UploadMilliseconds = watch.Elapsed.TotalMilliseconds;
        }
        public static WaterlineGeometry.Input[] Collect(Transform root, IReadOnlyList<WaterContactSource> sources)
        {
            var result = new List<WaterlineGeometry.Input>();
            var seen = new HashSet<Collider2D>();
            foreach (var source in sources)
            {
                if (source == null || !WaterlineGeometry.Participates(source, root))
                    continue;
                if (source.Colliders == null || source.Colliders.Length == 0)
                    throw new WaterlineInputException(source, "접촉 Collider 참조가 없습니다.");
                if (!float.IsFinite(source.MinimumLineRadius) || source.MinimumLineRadius <= 0)
                    throw new WaterlineInputException(source, "선형 접촉 반경은 양수여야 합니다.");
                foreach (var c in source.Colliders)
                {
                    if (c == null || c.gameObject != source.gameObject)
                        throw new WaterlineInputException(source, "같은 객체의 접촉 Collider를 다시 지정하세요.");
                    if (!WaterlineGeometry.PhysicallyInactive(c))
                        throw new WaterlineInputException(c, "접촉 Collider 체크를 끄세요. 일반 Collider에는 Rigidbody가 필요하지 않습니다.");
                    if (!seen.Add(c))
                        throw new WaterlineInputException(c, "Collider 중복 참조입니다.");
                    var m = WaterlineGeometry.RelativeMatrix(root, c.transform);
                    if (Mathf.Abs(m.determinant) < 1e-8f || Mathf.Abs(m.m20) > 1e-5f || Mathf.Abs(m.m21) > 1e-5f || Mathf.Abs(m.m23) > 1e-4f)
                        throw new WaterlineInputException(c, "선박 XY 평면의 유효한 형상이 필요합니다.");
                    if (c.attachedRigidbody != null && !c.attachedRigidbody.transform.IsChildOf(root))
                        throw new WaterlineInputException(c, "Rigidbody는 같은 선박에 속해야 합니다.");
                    if (c is CompositeCollider2D && (c.attachedRigidbody == null || c.attachedRigidbody.simulated))
                        throw new WaterlineInputException(c, "Composite만 비시뮬레이션 Rigidbody2D가 필요합니다.");
                    result.Add(new WaterlineGeometry.Input { owner = source, collider = c });
                }
            }
            var selected = new HashSet<Collider2D>(result.Select(x => x.collider));
            foreach (var input in result)
            {
                if (input.collider.compositeOperation != Collider2D.CompositeOperation.None && (input.collider.composite == null || !selected.Contains(input.collider.composite)))
                    throw new WaterlineInputException(input.collider, "기여 Collider 대신 결과 Composite도 지정하세요.");
                foreach (var dependency in WaterlineGeometry.Dependencies(input))
                    if (!WaterlineGeometry.PhysicallyInactive(dependency))
                        throw new WaterlineInputException(dependency, "Composite의 접촉 기여 Collider도 물리에 참여하지 않아야 합니다.");
            }
            result.RemoveAll(x => x.collider.compositeOperation != Collider2D.CompositeOperation.None);
            return result.ToArray();
        }
        public void Dispose()
        {
            geometry.Dispose();
            Release(current);
            Release(staging);
            current = null;
            staging = null;
        }
        private static void Release(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
    public sealed class WaterlineInputException : InvalidOperationException
    {
        public UnityEngine.Object Target
        {
            get;
        }
        public WaterlineInputException(UnityEngine.Object target, string message) : base(target.name + ": " + message) { Target = target; }
    }
}
