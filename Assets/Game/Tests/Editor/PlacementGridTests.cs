using System;
using GreatVoyage.Domain;
using NUnit.Framework;

namespace GreatVoyage.Tests
{
    public sealed class PlacementGridTests
    {
        [Test] public void WholeFootprintMustFitZoneAndAvoidForbiddenCells()
        {
            var grid = new PlacementGrid(.5f, -1, -1, 4, 4, new[] { 5 });
            Assert.That(grid.IsCellAllowed(-1, 0), Is.False);
            Assert.That(grid.IsCellAllowed(4, 0), Is.False);
            Assert.That(grid.IsFootprintAllowed(0, 1, 2, 1), Is.False);
            Assert.That(grid.IsFootprintAllowed(3, 2, 2, 1), Is.False);
            Assert.That(grid.IsFootprintAllowed(0, 2, 2, 1), Is.True);
        }

        [Test] public void ContinuousCentersAreValidatedWithoutMovingOrRoundingThem()
        {
            var grid = new PlacementGrid(1, -5.5f, -3, 11, 6);
            Assert.That(grid.TryGetFootprint(2.5f, 1.5f, 2, 1, out var x, out var y), Is.True);
            Assert.That(x, Is.EqualTo(7)); Assert.That(y, Is.EqualTo(4));
            Assert.That(grid.TryGetFootprint(2.6f, 1.5f, 2, 1, out _, out _), Is.False);
            Assert.That(grid.TryGetFootprint(0, 0, 0, 0, out _, out _), Is.False);
            Assert.That(grid.TryGetFootprint(float.NaN, 0, 2, 1, out _, out _), Is.False);
        }

        [Test] public void InvalidOrOutsideDefinitionsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new PlacementGrid(0, 0, 0, 2, 2));
            Assert.Throws<ArgumentException>(() => new PlacementGrid(.5f, 0, 0, 2, 2, new[] { 4 }));
        }

        [Test] public void RectanglesUnionAcrossSeamsAndExclusionsAlwaysWin()
        {
            var grid = new PlacementGrid(1, 0, 0,
                new[] { new GridRect(0, 0, 4, 2), new GridRect(2, 1, 2, 3) }, new[] { new GridCell(3, 2) });
            Assert.That(grid.CellCount, Is.EqualTo(11)); // overlap counted once, then one hole removed
            Assert.That(grid.IsFootprintAllowed(2, 0, 1, 3), Is.True);
            Assert.That(grid.IsFootprintAllowed(3, 0, 1, 3), Is.False);
            Assert.That(grid.IsCellAllowed(0, 3), Is.False); // inside bounding rectangle, outside union
        }

        [Test] public void AdjacentAreasShareCellsOnlyWithinTheirOwnSpace()
        {
            var a = new GridRect(0, 0, 2, 2); var b = new GridRect(2, 0, 2, 2);
            var combined = new PlacementGrid(1, 0, 0, new[] { a, b });
            Assert.That(combined.IsFootprintAllowed(1, 0, 2, 1), Is.True);
            Assert.That(new PlacementGrid(1, 0, 0, new[] { a }).IsFootprintAllowed(1, 0, 2, 1), Is.False);
        }

        [Test] public void NegativeCellCoordinatesAndEmptySpaceAreSupported()
        {
            var grid = new PlacementGrid(1, 0, 0,
                new[] { new GridRect(-2, -1, 2, 2), new GridRect(0, -1, 1, 2) });
            Assert.That(grid.TryGetFootprint(-.5f, 0, 3, 2, out var x, out var y), Is.True);
            Assert.That(x, Is.EqualTo(-2)); Assert.That(y, Is.EqualTo(-1));
            Assert.That(new PlacementGrid(1, 0, 0, new GridRect[0]).IsCellAllowed(0, 0), Is.False);
        }
    }
}
