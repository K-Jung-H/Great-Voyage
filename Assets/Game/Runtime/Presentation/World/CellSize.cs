using UnityEngine;

namespace GreatVoyage.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CellSize : MonoBehaviour
    {
        [SerializeField] private GridSettings settings;
        [SerializeField] private FurnitureDefinition definition;
        [SerializeField] private Vector2Int cells;
        [SerializeField] private FurnitureCellArea cellArea;
        public GridSettings Settings => settings;
        public FurnitureDefinition Definition => definition;
        public Vector2Int Cells => cells;
        public FurnitureCellArea CellArea => cellArea;
        public bool IsConfigured => cells.x > 0 && cells.y > 0;
        public Vector2 WorldSize => settings == null ? Vector2.zero : new Vector2(cells.x, cells.y) * settings.CellWorldSize;
        public PlacementSpace Space => GetComponentInParent<PlacementSpace>(true);
        public bool IsPlacementValid => IsConfigured && Space != null && Space.Contains(this);
    }
}
