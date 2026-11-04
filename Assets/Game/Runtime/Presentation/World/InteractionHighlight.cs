using GreatVoyage.Application;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class InteractionHighlight : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Color outlineColor = new Color(1, .85f, .45f, 1);
        [SerializeField, Range(.5f, 3)] private float outlinePixels = 1.25f;
        private GameSession session;
        private string targetId;
        private Material originalMaterial;
        private MaterialPropertyBlock originalProperties, highlightedProperties;
        public bool IsHighlighted { get; private set; }

        public void Bind(GameSession value, string id)
        {
            if (session != null) session.Changed -= Refresh;
            Restore();
            if (artwork == null || outlineMaterial == null) throw new System.InvalidOperationException("Missing highlight renderer/material.");
            session = value; targetId = id;
            originalMaterial = artwork.sharedMaterial;
            originalProperties = new MaterialPropertyBlock(); artwork.GetPropertyBlock(originalProperties);
            highlightedProperties = new MaterialPropertyBlock(); artwork.GetPropertyBlock(highlightedProperties);
            var rect = artwork.sprite.rect; var texture = artwork.sprite.texture;
            highlightedProperties.SetVector("_SpriteUvRect", new Vector4(rect.xMin / texture.width, rect.yMin / texture.height,
                rect.xMax / texture.width, rect.yMax / texture.height));
            highlightedProperties.SetColor("_OutlineColor", outlineColor);
            highlightedProperties.SetFloat("_OutlinePixels", outlinePixels);
            session.Changed += Refresh; Refresh();
        }

        private void Refresh()
        {
            var active = isActiveAndEnabled && session != null && session.WorldInputAllowed
                && session.FocusedInteraction?.TargetId == targetId;
            if (active == IsHighlighted) return;
            if (active)
            {
                artwork.sharedMaterial = outlineMaterial;
                artwork.SetPropertyBlock(highlightedProperties); IsHighlighted = true;
            }
            else Restore();
        }

        private void Restore()
        {
            if (IsHighlighted && artwork != null)
            {
                artwork.sharedMaterial = originalMaterial;
                artwork.SetPropertyBlock(originalProperties);
            }
            IsHighlighted = false;
        }
        private void OnEnable() { if (session != null) Refresh(); }
        private void OnDisable() => Restore();
        private void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            Restore(); session = null;
        }
    }
}
