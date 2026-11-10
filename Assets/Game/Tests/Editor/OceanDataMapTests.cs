using NUnit.Framework;
using UnityEngine;
using GreatVoyage.Ocean;

public sealed class OceanDataMapTests
{
    [Test]
    public void TileCenterSamplesExactValue()
    {
        var map = ScriptableObject.CreateInstance<OceanDataMap>();
        map.Configure(Vector2.zero, new Vector2Int(2, 1), 100f);
        map.SetCell(0, 0, new OceanTileCell(Vector2.right, 2f, 0f));
        map.SetCell(1, 0, new OceanTileCell(Vector2.up, 4f, 0f));

        OceanTileCell a = map.SampleInterpolated(new Vector2(50f, 50f));
        OceanTileCell b = map.SampleInterpolated(new Vector2(150f, 50f));

        Assert.That(a.MainDirection.x, Is.EqualTo(1f).Within(0.00001f));
        Assert.That(a.Speed, Is.EqualTo(2f).Within(0.00001f));
        Assert.That(b.MainDirection.y, Is.EqualTo(1f).Within(0.00001f));
        Assert.That(b.Speed, Is.EqualTo(4f).Within(0.00001f));
        Object.DestroyImmediate(map);
    }

    [Test]
    public void SharedTileBoundaryIsContinuous()
    {
        var map = ScriptableObject.CreateInstance<OceanDataMap>();
        map.Configure(Vector2.zero, new Vector2Int(2, 1), 100f);
        map.SetCell(0, 0, new OceanTileCell(Vector2.right, 2f, 0.2f));
        map.SetCell(1, 0, new OceanTileCell(Vector2.up, 4f, 0.8f));
        OceanTileCell a = map.SampleInterpolated(new Vector2(99.999f, 50f));
        OceanTileCell b = map.SampleInterpolated(new Vector2(100.001f, 50f));
        Assert.That(Vector2.Distance(a.MainDirection, b.MainDirection), Is.LessThan(0.0001f));
        Assert.That(Mathf.Abs(a.Speed - b.Speed), Is.LessThan(0.0001f));
        Object.DestroyImmediate(map);
    }

    [Test]
    public void SameWorldCoordinateReturnsSameCoarseInput()
    {
        var map = ScriptableObject.CreateInstance<OceanDataMap>();
        map.Configure(new Vector2(-300f, -100f), new Vector2Int(3, 3), 100f);
        OceanTileCell a = map.SampleInterpolated(new Vector2(-126.789f, 83.456f));
        OceanTileCell b = map.SampleInterpolated(new Vector2(-126.789f, 83.456f));
        Assert.That(a.MainDirection, Is.EqualTo(b.MainDirection));
        Assert.That(a.Speed, Is.EqualTo(b.Speed));
        Object.DestroyImmediate(map);
    }
}
