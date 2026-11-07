using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
namespace GreatVoyage.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ShipWaterSurface : MonoBehaviour
    {
        [SerializeField] private List<WaterContactSource> sources = new List<WaterContactSource>();
        [SerializeField, Range(64, 1024)] private int longestEdge = 512;
        [SerializeField, Min(.01f)] private float padding = 1;
        private WaterlineGenerator generator;
        private uint revision = 1, generatedRevision;
        private int lastFailedFrame = -1;
        private static readonly ProfilerMarker generateMarker = new ProfilerMarker("GreatVoyage.Waterline.Generate");
        public IReadOnlyList<WaterContactSource> Sources => sources;
        public int LongestEdge => longestEdge;
        public float Padding => padding;
        public bool IsDirty => revision != generatedRevision;
        public Texture2D Sdf => generator?.Texture;
        public Rect LocalBounds => generator?.Bounds ?? default;
        public int Generation => generator?.Generation ?? 0;
        public string LastError
        {
            get; private set;
        }
        public WaterlineGenerator Generator => generator;
        public void Register(WaterContactSource source)
        {
            if (source == null || sources.Contains(source))
                return;
            if (!source.transform.IsChildOf(transform))
                throw new ArgumentException("Source belongs to a different Ship.");
            sources.Add(source);
            NotifyGeometryChanged();
        }
        public void Unregister(WaterContactSource source)
        {
            if (sources.Remove(source))
                NotifyGeometryChanged();
        }
        public void NotifyGeometryChanged()
        {
            unchecked
            {
                revision++;
            }
            lastFailedFrame = -1;
        }
        public bool RefreshIfNeeded(bool force = false)
        {
            if (!force && !IsDirty && Sdf != null)
                return true;
            if (UnityEngine.Application.isPlaying && !force && lastFailedFrame == Time.frameCount)
                return false;
            generator ??= new WaterlineGenerator();
            var captured = revision;
            try
            {
                using (generateMarker.Auto())
                    generator.Generate(transform, sources, longestEdge, padding);
                generatedRevision = captured;
                LastError = null;
                lastFailedFrame = -1;
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; lastFailedFrame = Time.frameCount; return false; }
        }
        private void LateUpdate()
        {
            if (IsDirty && lastFailedFrame < 0 && !RefreshIfNeeded())
                Debug.LogError("Waterline: " + LastError, this);
        }
        private void OnDestroy()
        {
            generator?.Dispose();
            generator = null;
        }
    }
}
