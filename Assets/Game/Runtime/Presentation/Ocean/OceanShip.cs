using UnityEngine;
using GreatVoyage.Domain;

namespace GreatVoyage.Ocean
{
    public sealed class OceanShip : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float interactionRadius = 4f;
        [SerializeField, Min(0f)] private float fullInfluenceRadius = 1f;
        [SerializeField, Min(0f)] private float sideFlowGain = 0.65f;
        [SerializeField, Min(0f)] private float foamEmission = 0.6f;

        [SerializeField, Min(0f)] private float heightAmplitude = 0.12f;
        [SerializeField, Min(0.1f)] private float wavelength = 2f;
        public float HeightAmplitude { get => heightAmplitude; set => heightAmplitude = Mathf.Max(0, value); }
        public float Wavelength => wavelength;

        private Vector2 previousPosition;
        private float previousAngle;
        private bool motionInitialized;

        public float InteractionRadius { get => interactionRadius; set => interactionRadius = Mathf.Max(0.01f, value); }
        public float FullInfluenceRadius { get => Mathf.Clamp(fullInfluenceRadius, 0, interactionRadius - 0.001f); set => fullInfluenceRadius = Mathf.Max(0, value); }
        internal float FlowStrength { get; set; } = 1f;
        public float SideFlowGain => sideFlowGain;
        public bool FoamEmissionEnabled { get; set; } = true;
        public float FoamEmission => FoamEmissionEnabled ? foamEmission : 0f;
        public Vector2 Velocity { get; private set; }
        public float AngularVelocity { get; private set; }
        internal Matrix4x4 SceneToOcean { get; set; } = Matrix4x4.identity;
        public Vector2 WorldPosition => SceneToOcean.MultiplyPoint3x4(transform.position);
        public Vector2 Forward => SceneToOcean.MultiplyVector(transform.up).normalized;
        private float OceanAngle => Mathf.Atan2(-Forward.x, Forward.y) * Mathf.Rad2Deg;
        private void OnEnable() => ResetMotion();

        public void ResetMotion()
        {
            previousPosition = WorldPosition;
            previousAngle = OceanAngle;
            motionInitialized = true;
            Velocity = Vector2.zero;
            AngularVelocity = 0f;
        }

        public void SyncMotion(float deltaTime)
        {
            if (!motionInitialized)
                ResetMotion();
            Vector2 position = WorldPosition;
            float angle = OceanAngle;
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
    }
}
