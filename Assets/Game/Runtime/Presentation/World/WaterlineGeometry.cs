using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace GreatVoyage.Presentation
{
    public static class WaterlineGeometry
    {
        public sealed class Input
        {
            public WaterContactSource owner; public Collider2D collider;
        }
        public sealed class Primitive
        {
            public PhysicsShapeType2D type; public Vector2[] points; public float radius; public Rect bounds;
            public bool Contains(Vector2 p)
            {
                if (!bounds.Contains(p))
                    return false;
                if (type == PhysicsShapeType2D.Circle)
                    return (p - points[0]).sqrMagnitude <= radius * radius;
                if (type == PhysicsShapeType2D.Capsule)
                    return SegmentDistanceSquared(p, points[0], points[1]) <= radius * radius;
                var closed = type == PhysicsShapeType2D.Polygon;
                var count = closed ? points.Length : points.Length - 1;
                var inside = false;
                for (var i = 0; i < count; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Length];
                    if (radius > 0 && SegmentDistanceSquared(p, a, b) <= radius * radius)
                        return true;
                    if (closed && (a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                        inside = !inside;
                }
                return closed && inside;
            }
            private static float SegmentDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
            {
                var ab = b - a;
                var t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0;
                return (p - a - ab * t).sqrMagnitude;
            }
        }
        public static Matrix4x4 RelativeMatrix(Transform root, Transform child)
        {
            var m = Matrix4x4.identity;
            while (child != root)
            {
                if (child == null)
                    throw new InvalidOperationException("Source is outside this Ship.");
                m = Matrix4x4.TRS(child.localPosition, child.localRotation, child.localScale) * m;
                child = child.parent;
            }
            return m;
        }
        public static bool Participates(WaterContactSource source, Transform root)
        {
            if (source == null || !source.enabled)
                return false;
            for (var t = source.transform; t != root; t = t.parent)
            {
                if (t == null)
                    return false;
                if (!t.gameObject.activeSelf)
                    return false;
            }
            return true;
        }
        public static Collider2D[] Dependencies(Input input)
        {
            var c = input.collider;
            if (c is CompositeCollider2D composite)
                return composite.attachedRigidbody.GetComponentsInChildren<Collider2D>(true)
                    .Where(x => x == composite || (x.attachedRigidbody == composite.attachedRigidbody && x.compositeOperation != Collider2D.CompositeOperation.None && (x.composite == composite || x.composite == null))).ToArray();
            return new[] { c };
        }
        public static bool PhysicallyInactive(Collider2D collider) => !collider.enabled || (collider.attachedRigidbody != null && !collider.attachedRigidbody.simulated);
        public sealed class Workspace : IDisposable
        {
            private Scene scene;
            private Transform ownerRoot, root;
            private Collider2D[] previousDependencies = Array.Empty<Collider2D>();
            private readonly Dictionary<Transform, Transform> clones = new Dictionary<Transform, Transform>();
            private readonly Dictionary<Rigidbody2D, Rigidbody2D> bodies = new Dictionary<Rigidbody2D, Rigidbody2D>();
            private readonly Dictionary<Collider2D, Collider2D> colliders = new Dictionary<Collider2D, Collider2D>();
            public List<Primitive> Read(Transform shipRoot, Input[] inputs)
            {
                var dependencies = inputs.SelectMany(Dependencies).Distinct().ToArray();
                if (!scene.IsValid() || ownerRoot != shipRoot || !dependencies.SequenceEqual(previousDependencies))
                {
                    Dispose();
                    ownerRoot = shipRoot;
                    previousDependencies = dependencies;
#if UNITY_EDITOR
                    if(!UnityEngine.Application.isPlaying)scene=EditorSceneManager.NewPreviewScene();
                    else
#endif
                    scene = SceneManager.CreateScene("WaterlineGeometry_" + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics2D));
                    if (scene.GetPhysicsScene2D().Equals(Physics2D.defaultPhysicsScene))
                        throw new InvalidOperationException("Isolated geometry physics scene required.");
                    root = new GameObject("WaterlineGeometry").transform;
                    root.gameObject.hideFlags = HideFlags.HideAndDontSave;
                    SceneManager.MoveGameObjectToScene(root.gameObject, scene);
                    clones[shipRoot] = root;
                }
                foreach (var c in colliders.Values)
                    if (c != null)
                        c.enabled = false;
                Transform CloneTransform(Transform source)
                {
                    if (clones.TryGetValue(source, out var found))
                    {
                        if (source != ownerRoot)
                        {
                            found.SetParent(CloneTransform(source.parent), false);
                            found.localPosition = source.localPosition;
                            found.localRotation = source.localRotation;
                            found.localScale = source.localScale;
                            found.gameObject.SetActive(source.gameObject.activeSelf);
                        }
                        return found;
                    }
                    var parent = CloneTransform(source.parent);
                    var go = new GameObject(source.name);
                    SceneManager.MoveGameObjectToScene(go, scene);
                    var t = go.transform;
                    t.SetParent(parent, false);
                    t.localPosition = source.localPosition;
                    t.localRotation = source.localRotation;
                    t.localScale = source.localScale;
                    go.SetActive(source.gameObject.activeSelf);
                    clones.Add(source, t);
                    var grid = source.GetComponent<Grid>();
                    if (grid != null)
                        CopyGrid(grid, go.AddComponent<Grid>());
                    return t;
                }
                var gridRoot = shipRoot.GetComponent<Grid>();
                if (gridRoot != null)
                {
                    var gridCopy = root.GetComponent<Grid>();
                    if (gridCopy == null)
                        gridCopy = root.gameObject.AddComponent<Grid>();
                    CopyGrid(gridRoot, gridCopy);
                }

                foreach (var c in dependencies)
                {
                    if (c.attachedRigidbody == null)
                        continue;
                    if (bodies.TryGetValue(c.attachedRigidbody, out var existingBody))
                    {
                        CloneTransform(c.attachedRigidbody.transform);
                        existingBody.bodyType = c.attachedRigidbody.bodyType;
                        continue;
                    }
                    var source = c.attachedRigidbody;
                    var body = CloneTransform(source.transform).gameObject.AddComponent<Rigidbody2D>();
                    body.bodyType = source.bodyType;
                    body.gravityScale = 0;
                    body.simulated = true;
                    bodies.Add(source, body);
                }
                // Create Composite components before configuring their contributors. Copy public geometry only,
                // never native serialized ownership/cached-geometry references from the original physics scene.
                foreach (var c in dependencies.OrderBy(x => x is CompositeCollider2D ? 0 : 1))
                {
                    var target = CloneTransform(c.transform).gameObject;
                    if (c is TilemapCollider2D)
                    {
                        var map = c.GetComponent<Tilemap>();
                        var copy = target.GetComponent<Tilemap>();
                        if (copy == null)
                            copy = target.AddComponent<Tilemap>();
                        copy.tileAnchor = map.tileAnchor;
                        copy.orientation = map.orientation;
                        copy.orientationMatrix = map.orientationMatrix;
                        copy.ClearAllTiles();
                        copy.SetTilesBlock(map.cellBounds, map.GetTilesBlock(map.cellBounds));
                        foreach (var cell in map.cellBounds.allPositionsWithin)
                            if (map.GetTile(cell) != null)
                            {
                                copy.SetTransformMatrix(cell, map.GetTransformMatrix(cell));
                                copy.SetColliderType(cell, map.GetColliderType(cell));
                            }
                        copy.RefreshAllTiles();
                    }
                    if (colliders.ContainsKey(c))
                        continue;
                    var clone = (Collider2D)target.AddComponent(c.GetType());
                    clone.enabled = false;
                    colliders.Add(c, clone);
                }
                foreach (var pair in colliders)
                    CopyColliderGeometry(pair.Key, pair.Value);
                foreach (var clone in colliders.Values)
                    clone.enabled = true;
                foreach (var tile in colliders.Values.OfType<TilemapCollider2D>())
                    tile.ProcessTilemapChanges();
                Physics2D.SyncTransforms();
                foreach (var composite in colliders.Values.OfType<CompositeCollider2D>())
                    composite.GenerateGeometry();
                var result = new List<Primitive>();
                foreach (var input in inputs)
                {
                    var g = new PhysicsShapeGroup2D();
                    colliders[input.collider].GetShapes(g);
                    if (g.shapeCount == 0)
                        throw new InvalidOperationException(input.collider.GetType().Name + " produced no geometry. Check its authoring data.");
                    for (var i = 0; i < g.shapeCount; i++)
                    {
                        var shape = g.GetShape(i);
                        var points = new Vector2[shape.vertexCount];
                        for (var j = 0; j < points.Length; j++)
                            points[j] = g.localToWorldMatrix.MultiplyPoint3x4(g.GetShapeVertex(i, j));
                        var radius = shape.shapeType == PhysicsShapeType2D.Edges && shape.radius <= 0 ? input.owner.MinimumLineRadius : shape.radius;
                        var min = points.Aggregate(Vector2.Min) - Vector2.one * radius;
                        var max = points.Aggregate(Vector2.Max) + Vector2.one * radius;
                        result.Add(new Primitive { type = shape.shapeType, points = points, radius = radius, bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y) });
                    }
                }
                return result;
            }
            public void Dispose()
            {
                if (scene.IsValid())
                {
#if UNITY_EDITOR
                    if (EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene);
                    else if (!UnityEngine.Application.isPlaying) EditorSceneManager.CloseScene(scene, true);
                    else
#endif
                    {
                        foreach (var r in scene.GetRootGameObjects())
                            r.SetActive(false);
                        SceneManager.UnloadSceneAsync(scene);
                    }
                }
                scene = default;
                ownerRoot = null;
                root = null;
                previousDependencies = Array.Empty<Collider2D>();
                clones.Clear();
                bodies.Clear();
                colliders.Clear();
            }
        }
        private static void CopyGrid(Grid source, Grid target)
        {
            target.cellSize = source.cellSize;
            target.cellGap = source.cellGap;
            target.cellLayout = source.cellLayout;
            target.cellSwizzle = source.cellSwizzle;
        }
        private static void CopyColliderGeometry(Collider2D source, Collider2D target)
        {
            target.offset = source.offset;
            target.isTrigger = source.isTrigger;
            switch (source)
            {
                case BoxCollider2D box:
                    var b = (BoxCollider2D)target;
                    b.autoTiling = false;
                    b.size = box.size;
                    b.edgeRadius = box.edgeRadius;
                    break;
                case CircleCollider2D circle:
                    ((CircleCollider2D)target).radius = circle.radius;
                    break;
                case CapsuleCollider2D capsule:
                    var cap = (CapsuleCollider2D)target;
                    cap.size = capsule.size;
                    cap.direction = capsule.direction;
                    break;
                case PolygonCollider2D polygon:
                    var p = (PolygonCollider2D)target;
                    p.pathCount = polygon.pathCount;
                    for (var i = 0; i < p.pathCount; i++)
                        p.SetPath(i, polygon.GetPath(i));
                    break;
                case EdgeCollider2D edge:
                    var e = (EdgeCollider2D)target;
                    e.points = edge.points;
                    e.edgeRadius = edge.edgeRadius;
                    e.useAdjacentStartPoint = edge.useAdjacentStartPoint;
                    e.useAdjacentEndPoint = edge.useAdjacentEndPoint;
                    e.adjacentStartPoint = edge.adjacentStartPoint;
                    e.adjacentEndPoint = edge.adjacentEndPoint;
                    break;
                case CompositeCollider2D composite:
                    var comp = (CompositeCollider2D)target;
                    comp.generationType = CompositeCollider2D.GenerationType.Manual;
                    comp.geometryType = composite.geometryType;
                    comp.vertexDistance = composite.vertexDistance;
                    comp.offsetDistance = composite.offsetDistance;
                    comp.edgeRadius = composite.edgeRadius;
                    comp.useDelaunayMesh = composite.useDelaunayMesh;
                    break;
                case TilemapCollider2D tile:
                    ((TilemapCollider2D)target).extrusionFactor = tile.extrusionFactor;
                    break;
                case CustomCollider2D custom:
                    var data = new PhysicsShapeGroup2D();
                    custom.GetCustomShapes(data);
                    ((CustomCollider2D)target).SetCustomShapes(data);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported Collider2D subtype: " + source.GetType().FullName);
            }
            if (target.compositeCapable)
            {
                target.compositeOperation = source.compositeOperation;
                target.compositeOrder = source.compositeOrder;
            }
        }
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
