using UnityEngine;
using GreatVoyage.Domain;

namespace GreatVoyage.Ocean
{
    public sealed class OceanShip : MonoBehaviour
    {
        [SerializeField] private PolygonCollider2D hull;
        [SerializeField, Min(0.01f)] private float interactionRadius = 4f;
        [SerializeField, Min(0f)] private float sideFlowGain = 0.65f;
        [SerializeField, Min(0f)] private float foamEmission = 0.6f;

        private Vector2[] localVertices;
        private Vector2[] worldVertices;
        private ComputeBuffer vertexBuffer;
        private Vector2 previousPosition;
        private float previousAngle;
        private bool motionInitialized;

        public float InteractionRadius => interactionRadius;
        internal float FlowStrength { get; set; } = 1f;
        public float SideFlowGain => sideFlowGain;
        public float FoamEmission => foamEmission;
        public Vector2 Velocity { get; private set; }
        public float AngularVelocity { get; private set; }
        public Vector2 WorldPosition => transform.position;
        public Vector2 Forward => transform.up;
        public Rect WorldBounds { get; private set; }
        public int VertexCount => worldVertices == null ? 0 : worldVertices.Length;
        public ComputeBuffer VertexBuffer => vertexBuffer;

        private void OnEnable()
        {
            ResetMotion();
            InvalidateHull();
        }

        private void OnDisable()
        {
            ReleaseBuffer();
        }

        public void ResetMotion()
        {
            previousPosition = transform.position;
            previousAngle = transform.eulerAngles.z;
            motionInitialized = true;
            Velocity = Vector2.zero;
            AngularVelocity = 0f;
        }

        public void SyncMotion(float deltaTime)
        {
            if (!motionInitialized)
                ResetMotion();
            Vector2 position = transform.position;
            float angle = transform.eulerAngles.z;
            float dt = Mathf.Max(0.00001f, deltaTime);
            Velocity = (position - previousPosition) / dt;
            AngularVelocity = Mathf.DeltaAngle(previousAngle, angle) * Mathf.Deg2Rad / dt;
            previousPosition = position;
            previousAngle = angle;
        }

        public void ProjectNavigation(NavigationState navigation, float fieldUnitsPerNauticalMile)
        {
            transform.SetPositionAndRotation(
                new Vector3((float)(navigation.X * fieldUnitsPerNauticalMile),
                    (float)(navigation.Y * fieldUnitsPerNauticalMile), transform.position.z),
                Quaternion.Euler(0, 0, (float)navigation.HeadingDegrees - 90));
        }

        public void InvalidateHull()
        {
            ReleaseBuffer();
            localVertices = null;
            worldVertices = null;
        }

        public bool PrepareHull()
        {
            if (hull == null || hull.pathCount != 1)
                return false;
            if (localVertices == null)
            {
                var path = hull.GetPath(0);
                if (!IsValidPath(path))
                    return false;
                localVertices = path;
                worldVertices = new Vector2[localVertices.Length];
                vertexBuffer = new ComputeBuffer(localVertices.Length, sizeof(float) * 2);
            }

            Vector2 offset = hull.offset;
            Transform sourceTransform = hull.transform;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < localVertices.Length; i++)
            {
                Vector2 localPoint = localVertices[i] + offset;
                Vector3 point = sourceTransform.TransformPoint(new Vector3(localPoint.x, localPoint.y));
                Vector2 worldPoint = new Vector2(point.x, point.y);
                worldVertices[i] = worldPoint;
                min = Vector2.Min(min, worldPoint);
                max = Vector2.Max(max, worldPoint);
            }
            WorldBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            vertexBuffer.SetData(worldVertices);
            return true;
        }

        private static bool IsValidPath(Vector2[] path)
        {
            if (path.Length < 3)
                return false;

            float twiceArea = 0;
            for (int i = 0; i < path.Length; i++)
            {
                var vertex = path[i];
                for (int j = 0; j < i; j++)
                    if ((path[j] - vertex).sqrMagnitude < 1e-8f)
                        return false;

                var next = path[(i + 1) % path.Length];
                twiceArea += vertex.x * next.y - vertex.y * next.x;
            }
            return Mathf.Abs(twiceArea) > 1e-6f && !float.IsInfinity(twiceArea);
        }

        private void ReleaseBuffer()
        {
            if (vertexBuffer == null)
                return;
            vertexBuffer.Release();
            vertexBuffer = null;
        }
    }
}
