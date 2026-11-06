using System;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class OceanAmbientDriver : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] surfaces;
        [SerializeField] private Material sourceMaterial;
        [SerializeField] private Vector4 angularRates = new Vector4(.18f, .12f, .95f, .07f);
        [SerializeField, Min(0)] private float playbackSpeed = 1;
        [SerializeField] private bool paused;
        private Material runtimeMaterial;
        private Material[] previousMaterials;
        private double elapsed;
        private static readonly int PhaseProperty = Shader.PropertyToID("_OceanPhase");
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
            if (!paused) elapsed += Time.deltaTime * playbackSpeed;
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
            if (runtimeMaterial != null) runtimeMaterial.SetVector(PhaseProperty,Phase);
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
