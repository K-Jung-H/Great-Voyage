using System;
using UnityEngine;
namespace GreatVoyage.Presentation
{
    public sealed class WaterlineBakeAuthoring : MonoBehaviour
    {
        [SerializeField] private ShipWaterSurface surface;
        [SerializeField] private SpriteRenderer reviewOverlay;
        [NonSerialized] private WaterlineGenerator review;
        public ShipWaterSurface Surface => surface;
        public Transform ShipRoot => surface == null ? null : surface.transform;
        public SpriteRenderer ReviewOverlay => reviewOverlay;
        public WaterlineGenerator Review => review;
        public void GenerateReview()
        {
            if (UnityEngine.Application.isPlaying)
                throw new InvalidOperationException("Editor 검토는 Play 종료 후 실행하세요.");
            if (surface == null)
                throw new InvalidOperationException("ShipWaterSurface를 지정하세요.");
            review ??= new WaterlineGenerator();
            review.Generate(surface.transform, surface.Sources, surface.LongestEdge, surface.Padding);
        }
        public void ReleaseReview()
        {
            review?.Dispose();
            review = null;
        }
        private void OnDestroy() => ReleaseReview();
    }
}
