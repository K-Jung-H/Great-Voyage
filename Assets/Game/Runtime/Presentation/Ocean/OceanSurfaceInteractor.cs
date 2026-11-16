using UnityEngine;

namespace GreatVoyage.Ocean
{
    [DisallowMultipleComponent]
    public sealed class OceanSurfaceInteractor : MonoBehaviour
    {
        internal const int MaxEdges = 8;
        [SerializeField, Min(0f)] private float strength = 1f;

        [SerializeField] private Collider2D sourceCollider;
        [SerializeField] private OceanSpaceMode coordinateMode;
        internal OceanSpaceMode CoordinateMode => coordinateMode;
        [SerializeField] private bool surfaceContact = true;
        [SerializeField, Min(0f)] private float contactWidth = 0.5f;

        internal Collider2D SourceCollider => sourceCollider != null ? sourceCollider : GetComponent<Collider2D>();

        internal bool SupportsSurfaceContact => SourceCollider switch
        {
            PolygonCollider2D polygon => polygon.pathCount > 0 && polygon.pathCount <= MaxEdges / 3,
            CircleCollider2D circle => SupportsRoundedTransform(circle.transform),
            CapsuleCollider2D capsule => SupportsRoundedTransform(capsule.transform),
            _ => false
        };

        internal bool HasActiveCollider => isActiveAndEnabled && SourceCollider != null &&
            SourceCollider.isActiveAndEnabled;

        internal float ContactWidth => surfaceContact && HasActiveCollider &&
            SupportsSurfaceContact ? contactWidth : 0f;

        private void Reset()
        {
            sourceCollider = GetComponent<Collider2D>();
            coordinateMode = GetComponentInParent<OceanShip>() != null
                ? OceanSpaceMode.LocalFixed : OceanSpaceMode.WorldDynamic;
        }

        private void OnValidate()
        {
            if (sourceCollider == null) sourceCollider = GetComponent<Collider2D>();
        }

        [SerializeField, Min(0f)] private float foamStrength = 1f;
        [SerializeField, Min(0f)] private float flowStrength = 1f;

        internal float Strength => strength;
        internal float FoamStrength => foamStrength;
        internal float FlowStrength => flowStrength;

        internal int ReadEdges(Vector4[] edges, out float radius, int edgeBudget = MaxEdges)
        {
            radius = 0;
            if (edgeBudget < 1) return 0;
            var outline = SourceCollider;
            Vector2[] points;
            bool closed;
            switch (outline)
            {
                case EdgeCollider2D edge:
                    points = edge.points;
                    closed = false;
                    break;
                case PolygonCollider2D polygon:
                    return ReadPolygon(polygon, edges, edgeBudget);
                case CircleCollider2D circle:
                    return ReadRounded(edges, circle.transform, circle.offset, Vector2.zero, circle.radius, out radius);
                case CapsuleCollider2D capsule:
                    bool vertical = capsule.direction == CapsuleDirection2D.Vertical;
                    float diameter = vertical ? capsule.size.x : capsule.size.y;
                    float length = vertical ? capsule.size.y : capsule.size.x;
                    float localRadius = Mathf.Min(diameter, length) * 0.5f;
                    Vector2 axis = (vertical ? Vector2.up : Vector2.right) *
                        Mathf.Max(0, length * 0.5f - localRadius);
                    return ReadRounded(edges, capsule.transform, capsule.offset, axis, localRadius, out radius);
                default:
                    return 0;
            }

            int edgeCount = closed ? points.Length : points.Length - 1;
            if (edgeCount < 1 || edgeCount > edgeBudget) return 0;
            for (int i = 0; i < edgeCount; i++)
            {
                Vector2 a = outline.transform.TransformPoint(points[i] + outline.offset);
                Vector2 b = outline.transform.TransformPoint(points[(i + 1) % points.Length] + outline.offset);
                edges[i] = Pack(a, b);
            }
            return edgeCount;
        }

        private static int ReadPolygon(PolygonCollider2D polygon, Vector4[] edges, int budget)
        {
            if (polygon.pathCount > budget / 3) return 0;
            int written = 0;
            for (int path = 0; path < polygon.pathCount; path++)
            {
                var points = new System.Collections.Generic.List<Vector2>(polygon.GetPath(path));
                if (points.Count < 3) return 0;
                int limit = budget - written - 3 * (polygon.pathCount - path - 1);
                while (points.Count > limit)
                {
                    int least = 0;
                    float smallestArea = float.PositiveInfinity;
                    for (int i = 0; i < points.Count; i++)
                    {
                        Vector2 a = points[i] - points[(i + points.Count - 1) % points.Count];
                        Vector2 b = points[(i + 1) % points.Count] - points[i];
                        float area = Mathf.Abs(a.x * b.y - a.y * b.x);
                        if (area >= smallestArea) continue;
                        smallestArea = area;
                        least = i;
                    }
                    points.RemoveAt(least);
                }

                for (int i = 0; i < points.Count; i++)
                {
                    Vector2 a = polygon.transform.TransformPoint(points[i] + polygon.offset);
                    Vector2 b = polygon.transform.TransformPoint(points[(i + 1) % points.Count] + polygon.offset);
                    edges[written++] = Pack(a, b);
                }
            }
            return written;
        }

        private static bool SupportsRoundedTransform(Transform shape)
        {
            Vector3 x = shape.TransformVector(Vector3.right);
            Vector3 y = shape.TransformVector(Vector3.up);
            return x.sqrMagnitude >= 0.000001f &&
                Mathf.Abs(x.sqrMagnitude - y.sqrMagnitude) <= x.sqrMagnitude * 0.001f &&
                Mathf.Abs(Vector3.Dot(x, y)) <= x.sqrMagnitude * 0.001f;
        }

        private static int ReadRounded(Vector4[] edges, Transform shape, Vector2 center,
            Vector2 halfAxis, float localRadius, out float radius)
        {
            radius = 0f;
            if (!SupportsRoundedTransform(shape)) return 0;

            radius = localRadius * shape.TransformVector(Vector3.right).magnitude;
            edges[0] = Pack(shape.TransformPoint(center - halfAxis),
                shape.TransformPoint(center + halfAxis));
            return radius > 0f ? 1 : 0;
        }

        internal static Vector4 Pack(Vector2 a, Vector2 b) => new Vector4(a.x, a.y, b.x, b.y);

        private readonly Vector4[] gizmoEdges = new Vector4[MaxEdges];

        internal static void DrawOutline(Vector4 edge, float radius, System.Action<Vector2, Vector2> draw)
        {
            var a = new Vector2(edge.x, edge.y);
            var b = new Vector2(edge.z, edge.w);
            if (radius <= 0f) { draw(a, b); return; }
            Vector2 axis = (b - a).normalized;
            if (axis == Vector2.zero) axis = Vector2.right;
            Vector2 normal = new Vector2(-axis.y, axis.x);
            draw(a + normal * radius, b + normal * radius);
            draw(a - normal * radius, b - normal * radius);
            Vector2 left = a + normal * radius;
            Vector2 right = b - normal * radius;
            for (int i = 1; i <= 12; i++)
            {
                float angle = Mathf.PI * i / 12;
                Vector2 offset = radius * (Mathf.Cos(angle) * normal - Mathf.Sin(angle) * axis);
                draw(left, a + offset);
                draw(right, b - offset);
                left = a + offset;
                right = b - offset;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() => DrawSceneOutline();

        internal void DrawSceneOutline()
        {
            int count = ReadEdges(gizmoEdges, out float radius);
            for (int i = 0; i < count; i++)
            {
                Vector4 edge = gizmoEdges[i];
                DrawOutline(edge, radius, (a, b) => DrawSceneLine(
                    new Vector3(a.x, a.y, transform.position.z),
                    new Vector3(b.x, b.y, transform.position.z), Color.magenta));
            }
        }

        internal static void DrawSceneLine(Vector3 a, Vector3 b, Color color)
        {
            var depthTest = UnityEditor.Handles.zTest;
            UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            using (new UnityEditor.Handles.DrawingScope(Color.black))
                UnityEditor.Handles.DrawLine(a, b, 6f);
            using (new UnityEditor.Handles.DrawingScope(color))
                UnityEditor.Handles.DrawLine(a, b, 3f);
            UnityEditor.Handles.zTest = depthTest;
        }
#endif
    }
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(OceanSurfaceInteractor))]
    internal sealed class OceanSurfaceInteractorInspector : UnityEditor.Editor
    {
        private static readonly string[] CoordinateModes = { "Local (Fixed)", "World (Dynamic)" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceCollider"));
            var mode = serializedObject.FindProperty("coordinateMode");
            UnityEditor.EditorGUILayout.LabelField("Coordinate Mode");
            int selection = GUILayout.SelectionGrid(mode.enumValueIndex, CoordinateModes, 2,
                UnityEditor.EditorStyles.radioButton);
            if (selection != mode.enumValueIndex) mode.enumValueIndex = selection;
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("strength"),
                new GUIContent("Wake Strength"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("foamStrength"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("flowStrength"));
            serializedObject.ApplyModifiedProperties();

            var emitter = (OceanSurfaceInteractor)target;
            var contact = serializedObject.FindProperty("surfaceContact");
            if (emitter.SupportsSurfaceContact)
            {
                UnityEditor.EditorGUILayout.PropertyField(contact);
                using (new UnityEditor.EditorGUI.DisabledScope(!contact.boolValue))
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("contactWidth"));
            }
            else
            {
                using (new UnityEditor.EditorGUI.DisabledScope(true))
                    UnityEditor.EditorGUILayout.Toggle("Surface Contact", false);
                UnityEditor.EditorGUILayout.HelpBox("Surface Contact requires a supported closed collider.",
                    UnityEditor.MessageType.Info);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
