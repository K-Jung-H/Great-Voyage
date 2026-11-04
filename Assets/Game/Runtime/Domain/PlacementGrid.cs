using System;
using System.Collections.Generic;

namespace GreatVoyage.Domain
{
    // Local placement definition only. It neither moves entities nor discretizes their world positions.
    public sealed class PlacementGrid
    {
        public float CellSize { get; }
        public float OriginX { get; }
        public float OriginY { get; }
        public int Columns { get; }
        public int Rows { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int CellCount => allowed.Count;
        private readonly HashSet<GridCell> allowed = new HashSet<GridCell>();
        public IEnumerable<GridCell> Cells { get { foreach (var cell in allowed) yield return cell; } }

        public PlacementGrid(float cellSize, float originX, float originY, int columns, int rows,
            IEnumerable<int> blockedCells = null)
            : this(cellSize, originX, originY, new[] { new GridRect(0, 0, columns, rows) },
                ConvertBlocked(columns, rows, blockedCells)) { }

        private static IEnumerable<GridCell> ConvertBlocked(int columns, int rows, IEnumerable<int> blocked)
        {
            if (blocked == null) yield break;
            foreach (var index in blocked)
            {
                if (index < 0 || index >= (long)columns * rows) throw new ArgumentException("Blocked cell outside grid.");
                yield return new GridCell(index % columns, index / columns);
            }
        }

        public PlacementGrid(float cellSize, float originX, float originY, IEnumerable<GridRect> areas,
            IEnumerable<GridCell> excludedCells = null)
        {
            if (!WorldPosition.Finite(cellSize) || cellSize <= 0 || !WorldPosition.Finite(originX)
                || !WorldPosition.Finite(originY) || areas == null)
                throw new ArgumentException("Invalid local placement grid.");
            CellSize = cellSize; OriginX = originX; OriginY = originY;
            var first = true; var minX = 0; var minY = 0; var maxX = 0; var maxY = 0;
            foreach (var area in areas)
            {
                if (area.Width <= 0 || area.Height <= 0) throw new ArgumentException("Empty placement rectangle.");
                minX = first ? area.X : Math.Min(minX, area.X); minY = first ? area.Y : Math.Min(minY, area.Y);
                maxX = first ? area.X + area.Width : Math.Max(maxX, area.X + area.Width);
                maxY = first ? area.Y + area.Height : Math.Max(maxY, area.Y + area.Height); first = false;
                for (var y = area.Y; y < area.Y + area.Height; y++)
                    for (var x = area.X; x < area.X + area.Width; x++) allowed.Add(new GridCell(x, y));
                if (allowed.Count > 1000000) throw new ArgumentException("Placement space is too large.");
            }
            if ((long)maxX - minX > int.MaxValue || (long)maxY - minY > int.MaxValue)
                throw new ArgumentException("Placement span is too large.");
            MinX = minX; MinY = minY; Columns = maxX - minX; Rows = maxY - minY;
            if (excludedCells != null) foreach (var cell in excludedCells) allowed.Remove(cell);
        }

        public bool IsCellAllowed(int x, int y) => allowed.Contains(new GridCell(x, y));

        public bool IsFootprintAllowed(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0 || (long)width * height > allowed.Count || x < MinX || y < MinY
                || (long)x + width > (long)MinX + Columns || (long)y + height > (long)MinY + Rows) return false;
            for (var row = y; row < y + height; row++)
                for (var column = x; column < x + width; column++)
                    if (!IsCellAllowed(column, row)) return false;
            return true;
        }

        public bool TryGetFootprint(float centerX, float centerY, int width, int height, out int x, out int y)
        {
            x = y = 0;
            if (!WorldPosition.Finite(centerX) || !WorldPosition.Finite(centerY) || width <= 0 || height <= 0) return false;
            var column = (centerX - OriginX) / CellSize - width * 0.5;
            var row = (centerY - OriginY) / CellSize - height * 0.5;
            if (column < MinX - .001 || row < MinY - .001 || column > (long)MinX + Columns || row > (long)MinY + Rows) return false;
            x = (int)Math.Round(column); y = (int)Math.Round(row);
            return Math.Abs(column - x) < 0.001 && Math.Abs(row - y) < 0.001
                && IsFootprintAllowed(x, y, width, height);
        }
    }
}
