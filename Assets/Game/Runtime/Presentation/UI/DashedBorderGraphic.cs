using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage.Presentation.UI
{
    [AddComponentMenu("Great Voyage/UI/Dashed Border")]
    public sealed class DashedBorderGraphic : MaskableGraphic
    {
        [SerializeField, Min(0.5f)] private float thickness = 2f;
        [SerializeField, Min(1f)] private float dashLength = 8f;
        [SerializeField, Min(0.5f)] private float gapLength = 5f;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            if (bounds.width <= 0 || bounds.height <= 0) return;
            var stroke = Mathf.Clamp(thickness, 0.5f, Mathf.Min(bounds.width, bounds.height) / 2);
            Edge(mesh, new Vector2(bounds.xMin, bounds.yMin), Vector2.right, bounds.width, stroke, true);
            Edge(mesh, new Vector2(bounds.xMin, bounds.yMax - stroke), Vector2.right, bounds.width, stroke, true);
            Edge(mesh, new Vector2(bounds.xMin, bounds.yMin + stroke), Vector2.up, bounds.height - 2 * stroke, stroke, false);
            Edge(mesh, new Vector2(bounds.xMax - stroke, bounds.yMin + stroke), Vector2.up, bounds.height - 2 * stroke, stroke, false);
        }

        private void Edge(VertexHelper mesh, Vector2 start, Vector2 direction, float length, float stroke, bool horizontal)
        {
            if (length <= 0) return;
            var dash = Mathf.Min(length, Mathf.Max(1, dashLength));
            var gap = Mathf.Max(0.5f, gapLength);
            var count = Mathf.Max(1, Mathf.FloorToInt((length + gap) / (dash + gap)));
            var inset = (length - (count * dash + (count - 1) * gap)) / 2;
            for (var i = 0; i < count; i++)
            {
                var position = start + direction * (inset + i * (dash + gap));
                var size = horizontal ? new Vector2(Mathf.Min(dash, length), stroke) : new Vector2(stroke, Mathf.Min(dash, length));
                var index = mesh.currentVertCount;
                var vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = position;
                mesh.AddVert(vertex);
                vertex.position = position + new Vector2(0, size.y);
                mesh.AddVert(vertex);
                vertex.position = position + size;
                mesh.AddVert(vertex);
                vertex.position = position + new Vector2(size.x, 0);
                mesh.AddVert(vertex);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index + 2, index + 3, index);
            }
        }
    }
}
