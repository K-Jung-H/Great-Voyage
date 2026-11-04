using UnityEngine;

namespace GreatVoyage.Presentation
{
    // Editor-only visualization behavior; the data remains in the shared furniture/grid definitions.
    public sealed class FurnitureCellArea : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var furniture = GetComponentInParent<CellSize>();
            if (furniture == null || furniture.Settings == null) return;
            var oldMatrix = UnityEditor.Handles.matrix;
            var oldColor = UnityEditor.Handles.color;
            var oldDepth = UnityEditor.Handles.zTest;
            UnityEditor.Handles.matrix = transform.localToWorldMatrix;
            UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            try
            {
                if (!furniture.IsConfigured)
                {
                    UnityEditor.Handles.color = new Color(1, .65f, .15f);
                    UnityEditor.Handles.DrawLine(new Vector3(-.15f, 0), new Vector3(.15f, 0));
                    UnityEditor.Handles.DrawLine(new Vector3(0, -.15f), new Vector3(0, .15f));
                    UnityEditor.Handles.Label(Vector3.up * .2f, furniture.name + " | Cell 미정 (표준 " + furniture.Settings.CellWorldSize + " unit)");
                    return;
                }
                var cell = furniture.Settings.CellWorldSize;
                var cells = furniture.Cells;
                var size = new Vector2(cells.x * cell, cells.y * cell);
                var valid = furniture.Space == null || furniture.IsPlacementValid;
                var color = valid ? new Color(.25f, 1, .55f, .85f) : new Color(1, .3f, .2f, .9f);
                var corners = new Vector3[4];
                for (var y = 0; y < cells.y; y++) for (var x = 0; x < cells.x; x++)
                {
                    var left = -size.x / 2 + x * cell;
                    var bottom = -size.y / 2 + y * cell;
                    corners[0] = new Vector3(left, bottom);
                    corners[1] = new Vector3(left, bottom + cell);
                    corners[2] = new Vector3(left + cell, bottom + cell);
                    corners[3] = new Vector3(left + cell, bottom);
                    UnityEditor.Handles.DrawSolidRectangleWithOutline(corners,
                        new Color(color.r, color.g, color.b, .045f), color);
                }
                UnityEditor.Handles.Label(new Vector3(-size.x / 2, size.y / 2 + .15f),
                    furniture.Definition.KindId + " | " + cells.x + "×" + cells.y + " Cell | " + cell + " unit/Cell");
            }
            finally
            {
                UnityEditor.Handles.matrix = oldMatrix;
                UnityEditor.Handles.color = oldColor;
                UnityEditor.Handles.zTest = oldDepth;
            }
        }
#endif
    }
}
