using GreatVoyage.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace GreatVoyage.Tests
{
    public sealed class OceanRelativeFlowTests
    {
        [Test] public void FlowOpposesDisplacementAndDoesNotJumpWhenDirectionChanges()
        {
            var f=new OceanRelativeFlow();f.Advance(2,0,1,4,.75);
            Assert.That(f.WaterVelocity.x,Is.LessThan(0));var before=f.SamplingOffset;
            f.Advance(0,2*.001,.001,4,.75);
            Assert.That(Vector2.Distance(before,f.SamplingOffset),Is.LessThan(.02));
            Assert.That(f.WaterVelocity.y,Is.LessThan(0));
        }
        [Test] public void ConstantInputIsIndependentOfFramePartitionAndPauseHasNoCatchup()
        {
            var one=new OceanRelativeFlow();var many=new OceanRelativeFlow();
            one.Advance(20,-10,10,4,.75);
            for(var i=0;i<1000;i++)many.Advance(.02,-.01,.01,4,.75);
            Assert.That(Vector2.Distance(one.SamplingOffset,many.SamplingOffset),Is.LessThan(.0001));
            var offset=many.SamplingOffset;var velocity=many.WaterVelocity;
            many.Advance(0,0,0,4,.75);
            Assert.That(many.SamplingOffset,Is.EqualTo(offset));Assert.That(many.WaterVelocity,Is.EqualTo(velocity));
        }
        [Test] public void DecelerationSettlesWithoutReversingOrResettingAccumulatedOffset()
        {
            var f=new OceanRelativeFlow();f.Advance(2,0,1,4,.75);var before=f.SamplingOffset.x;
            f.Advance(0,0,20,4,.75);
            Assert.That(f.SamplingOffset.x,Is.GreaterThan(before));Assert.That(f.SamplingOffset.x,Is.EqualTo(8).Within(.0001));
            Assert.That(f.WaterVelocity.magnitude,Is.LessThan(.0001));
        }
    }
}
