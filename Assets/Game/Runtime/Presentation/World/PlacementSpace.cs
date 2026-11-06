using System.Collections.Generic;
using System.Linq;
using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    [DisallowMultipleComponent]
    public sealed class PlacementSpace : MonoBehaviour
    {
        [SerializeField] private string spaceId = "captain-cabin";
        [SerializeField] private GridSettings settings;
        [SerializeField] private Vector2 origin = new Vector2(-5.5f, -3);
        [SerializeField] private List<Vector2Int> excludedCells = new List<Vector2Int>();
        private PlacementGrid cached;
        private float cachedCellSize;
        public string SpaceId => spaceId;
        public GridSettings Settings => settings;
        public Vector2 Origin => origin;
        public IReadOnlyList<Vector2Int> ExcludedCells => excludedCells;
        public void Invalidate() => cached = null;
        private void OnEnable() => Invalidate();
        private void OnValidate() => Invalidate();
        private void OnTransformChildrenChanged() => Invalidate();

        public PlacementGrid GetGrid()
        {
            if (settings == null) throw new System.InvalidOperationException("Missing global GridSettings.");
            if (cached != null && cachedCellSize == settings.CellWorldSize) return cached;
            var areas = GetComponentsInChildren<GridArea>(true).Where(a => a.enabled && a.Space == this)
                .Select(a => new GridRect(a.Bounds.x, a.Bounds.y, a.Bounds.width, a.Bounds.height));
            cached = new PlacementGrid(settings.CellWorldSize, origin.x, origin.y, areas,
                excludedCells.Select(c => new GridCell(c.x, c.y)));
            cachedCellSize = settings.CellWorldSize;
            return cached;
        }

        public bool Contains(CellSize furniture)
        {
            if (furniture == null || !furniture.IsConfigured || furniture.Space != this || furniture.Settings != settings
                || furniture.CellArea == null || furniture.CellArea.transform.parent != furniture.transform
                || Mathf.Abs(transform.lossyScale.x - 1) > .0001f || Mathf.Abs(transform.lossyScale.y - 1) > .0001f
                || (furniture.transform.localScale - Vector3.one).sqrMagnitude > .0001f
                || (furniture.CellArea.transform.localScale - Vector3.one).sqrMagnitude > .0001f) return false;
            var p = transform.InverseTransformPoint(furniture.CellArea.transform.position);
            return GetGrid().TryGetFootprint(p.x, p.y, furniture.Cells.x, furniture.Cells.y, out _, out _);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (settings == null) return;
            PlacementGrid grid;
            try { grid = GetGrid(); } catch (System.Exception) { return; }
            var matrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.3f, .8f, 1, .4f);
            foreach (var c in grid.Cells)
                Gizmos.DrawWireCube(new Vector3(origin.x + (c.X + .5f) * grid.CellSize,
                    origin.y + (c.Y + .5f) * grid.CellSize), new Vector3(grid.CellSize, grid.CellSize));
            Gizmos.color = new Color(1, .3f, .2f, .8f);
            foreach (var c in excludedCells)
                Gizmos.DrawWireCube(new Vector3(origin.x + (c.x + .5f) * grid.CellSize,
                    origin.y + (c.y + .5f) * grid.CellSize), new Vector3(grid.CellSize, grid.CellSize));
            Gizmos.matrix = matrix;
        }
#endif
    }
}
