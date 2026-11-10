using System;
using UnityEngine;

namespace GreatVoyage.Ocean
{
    [Serializable]
    public struct OceanTileCell
    {
        public Vector2 MainDirection;
        public float Speed;
        public float NoiseStrength;

        public OceanTileCell(Vector2 mainDirection, float speed, float noiseStrength)
        {
            MainDirection = mainDirection;
            Speed = Mathf.Max(0f, speed);
            NoiseStrength = Mathf.Max(0f, noiseStrength);
        }
    }

    [CreateAssetMenu(fileName = "OceanDataMap", menuName = "Great Voyage/Ocean Data Map")]
    public sealed class OceanDataMap : ScriptableObject
    {
        [SerializeField] private Vector2 worldOrigin = Vector2.zero;
        [SerializeField, Min(1f)] private float tileWorldSize = 100f;
        [SerializeField] private Vector2Int dimensions = new Vector2Int(4, 4);
        [SerializeField] private OceanTileCell[] cells;

        [NonSerialized] private int version;

        public Vector2 WorldOrigin => worldOrigin;
        public float TileWorldSize => tileWorldSize;
        public Vector2Int Dimensions => dimensions;
        public int Version => version;

        private void OnEnable()
        {
            EnsureStorage();
        }

        private void OnValidate()
        {
            dimensions.x = Mathf.Max(1, dimensions.x);
            dimensions.y = Mathf.Max(1, dimensions.y);
            tileWorldSize = Mathf.Max(1f, tileWorldSize);
            EnsureStorage();
            version++;
        }

        public void Configure(Vector2 origin, Vector2Int size, float worldSizePerTile)
        {
            worldOrigin = origin;
            dimensions = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            tileWorldSize = Mathf.Max(1f, worldSizePerTile);
            cells = new OceanTileCell[dimensions.x * dimensions.y];
            Fill(new OceanTileCell(Vector2.up, 1f, 0.18f));
        }

        public void Fill(OceanTileCell cell)
        {
            EnsureStorage();
            for (int i = 0; i < cells.Length; i++)
                cells[i] = cell;
            version++;
        }

        public OceanTileCell GetCell(int x, int y)
        {
            EnsureStorage();
            int ix = Mathf.Clamp(x, 0, dimensions.x - 1);
            int iy = Mathf.Clamp(y, 0, dimensions.y - 1);
            return cells[iy * dimensions.x + ix];
        }

        public void SetCell(int x, int y, OceanTileCell cell)
        {
            if (x < 0 || y < 0 || x >= dimensions.x || y >= dimensions.y)
                throw new ArgumentOutOfRangeException(nameof(x), "Ocean tile coordinates are outside the data map.");
            EnsureStorage();
            cells[y * dimensions.x + x] = cell;
            version++;
        }

        public OceanTileCell SampleInterpolated(Vector2 worldPosition)
        {
            Vector2 grid = (worldPosition - worldOrigin) / tileWorldSize - Vector2.one * 0.5f;
            int x = Mathf.FloorToInt(grid.x);
            int y = Mathf.FloorToInt(grid.y);
            float tx = Mathf.Clamp01(grid.x - x);
            float ty = Mathf.Clamp01(grid.y - y);
            OceanTileCell a = GetCell(x, y);
            OceanTileCell b = GetCell(x + 1, y);
            OceanTileCell c = GetCell(x, y + 1);
            OceanTileCell d = GetCell(x + 1, y + 1);
            Vector2 dir = Vector2.Lerp(Vector2.Lerp(UnitDirection(a.MainDirection), UnitDirection(b.MainDirection), tx),
                Vector2.Lerp(UnitDirection(c.MainDirection), UnitDirection(d.MainDirection), tx), ty);
            float speed = Mathf.Lerp(Mathf.Lerp(a.Speed, b.Speed, tx),
                Mathf.Lerp(c.Speed, d.Speed, tx), ty);
            float noise = Mathf.Lerp(Mathf.Lerp(a.NoiseStrength, b.NoiseStrength, tx),
                Mathf.Lerp(c.NoiseStrength, d.NoiseStrength, tx), ty);
            return new OceanTileCell(dir, speed, noise);
        }

        public Color[] BuildGpuPayload()
        {
            EnsureStorage();
            var payload = new Color[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                OceanTileCell cell = cells[i];
                Vector2 dir = cell.MainDirection.sqrMagnitude > 0.000001f
                    ? cell.MainDirection.normalized : Vector2.zero;
                payload[i] = new Color(dir.x, dir.y, Mathf.Max(0f, cell.Speed),
                    Mathf.Max(0f, cell.NoiseStrength));
            }
            return payload;
        }

        private static Vector2 UnitDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.zero;
        }

        private void EnsureStorage()
        {
            int count = Mathf.Max(1, dimensions.x) * Mathf.Max(1, dimensions.y);
            if (cells != null && cells.Length == count)
                return;
            OceanTileCell[] old = cells;
            cells = new OceanTileCell[count];
            for (int i = 0; i < count; i++)
                cells[i] = old != null && i < old.Length
                    ? old[i] : new OceanTileCell(Vector2.up, 1f, 0.18f);
        }
    }
}
