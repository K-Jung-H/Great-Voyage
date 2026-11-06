using System;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class OceanAmbientDriver : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] surfaces;
        [SerializeField] private Material sourceMaterial;
        [SerializeField] private Vector4 angularRates = new Vector4(.18f, .12f, .95f, .07f);
        [Tooltip("Natural large-wave domain velocity in world units per visual second; independent of ship movement.")]
        [SerializeField] private Vector2 largeWaveVelocity = new Vector2(.13f, -.05f);
        [Tooltip("Natural ripple domain velocity in world units per visual second.")]
        [SerializeField] private Vector2 rippleVelocity = new Vector2(-.04f, .09f);
        [SerializeField, Range(0,1)] private float shapeEvolution = .35f;
        [SerializeField, Min(0)] private float playbackSpeed = 1;
        [SerializeField] private bool paused;
        private Material runtimeMaterial;
        private Material[] previousMaterials;
        private double elapsed;
        private Vector2 relativeOffset;
        public bool UseExternalClock { get; set; }
        public Vector2 RelativeOffset => relativeOffset;
        private static readonly int PhaseProperty = Shader.PropertyToID("_OceanPhase");
        private static readonly int TravelProperty = Shader.PropertyToID("_OceanTravel");
        private static readonly int EvolutionProperty = Shader.PropertyToID("_ShapeEvolution");
        private static readonly int RelativeProperty = Shader.PropertyToID("_RelativeOceanOffset");
        public double ElapsedSeconds => elapsed;
        public bool Paused { get => paused; set => paused = value; }
        public Vector4 Phase => EvaluatePhase(elapsed, angularRates);

        private void OnEnable()
        {
            if (sourceMaterial == null || surfaces == null || surfaces.Length == 0)
                throw new InvalidOperationException("Ocean surfaces and source material are required.");
            runtimeMaterial = new Material(sourceMaterial) { name = sourceMaterial.name + " (Runtime)", hideFlags = HideFlags.DontSave };
            previousMaterials = new Material[surfaces.Length];
            for (var i=0;i<surfaces.Length;i++)
            {
                if (surfaces[i] == null) throw new InvalidOperationException("Missing ocean surface.");
                previousMaterials[i] = surfaces[i].sharedMaterial;
                surfaces[i].sharedMaterial = runtimeMaterial;
            }
            ApplyPhase();
        }
        private void Update()
        {
            if (!paused && !UseExternalClock) elapsed += Time.deltaTime * playbackSpeed;
            ApplyPhase();
        }
        public void AdvanceExternal(double visualSeconds, Vector2 samplingOffset)
        {
            if (double.IsNaN(visualSeconds) || double.IsInfinity(visualSeconds) || visualSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(visualSeconds));
            if (paused) return;
            elapsed += visualSeconds * playbackSpeed;
            relativeOffset = samplingOffset;
            ApplyPhase();
        }
        public void SetPreviewTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            elapsed = seconds; ApplyPhase();
        }
        private void ApplyPhase()
        {
            if (runtimeMaterial == null) return;
            runtimeMaterial.SetVector(PhaseProperty,Phase);
            // Domain transport is continuous: wrapping a noise coordinate at 2*pi would cause a visible jump.
            runtimeMaterial.SetVector(TravelProperty,new Vector4((float)(elapsed*largeWaveVelocity.x),
                (float)(elapsed*largeWaveVelocity.y),(float)(elapsed*rippleVelocity.x),(float)(elapsed*rippleVelocity.y)));
            runtimeMaterial.SetFloat(EvolutionProperty,shapeEvolution);
            runtimeMaterial.SetVector(RelativeProperty,new Vector4(relativeOffset.x,relativeOffset.y,0,0));
        }
        public static Vector4 EvaluatePhase(double seconds, Vector4 rates)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            return new Vector4(Wrap(seconds,rates.x),Wrap(seconds,rates.y),Wrap(seconds,rates.z),Wrap(seconds,rates.w));
        }
        private static float Wrap(double seconds, float rate) => (float)((seconds * rate) % (Math.PI * 2));
        private void OnDisable()
        {
            if (previousMaterials != null)
                for(var i=0;i<surfaces.Length;i++)
                    if(surfaces[i]!=null && surfaces[i].sharedMaterial==runtimeMaterial) surfaces[i].sharedMaterial=previousMaterials[i];
            previousMaterials = null;
            if(runtimeMaterial != null) Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
