using System;
using GreatVoyage.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace GreatVoyage.Tests
{
    public sealed class WaterlineDistanceFieldTests
    {
        private static float[] Calculate(bool[] mask, int width, int height, Vector2 cellSize)
        {
            var result = new float[mask?.Length ?? 0];
            new WaterlineDistanceField.Workspace().Calculate(mask, width, height, cellSize, result);
            return result;
        }
        [Test] public void InsideIsNegativeOutsidePositiveAndResolutionUsesLocalUnits()
        {
            var mask=new bool[49];for(var y=2;y<=4;y++)for(var x=2;x<=4;x++)mask[y*7+x]=true;
            var a=Calculate(mask,7,7,Vector2.one);
            var b=Calculate(mask,7,7,Vector2.one*.5f);
            Assert.That(a[3*7+3],Is.EqualTo(-1.5f).Within(.00001));
            Assert.That(a[3*7+1],Is.EqualTo(.5f).Within(.00001));
            Assert.That(b[3*7+3],Is.EqualTo(a[3*7+3]*.5f).Within(.00001));
        }
        [Test] public void HolesAndDisconnectedIslandsRemainExteriorAndInteriorRespectively()
        {
            var mask=new bool[81];for(var y=1;y<=5;y++)for(var x=1;x<=5;x++)mask[y*9+x]=true;
            mask[3*9+3]=false;mask[7*9+7]=true;
            var field=Calculate(mask,9,9,Vector2.one);
            Assert.That(field[3*9+3],Is.GreaterThan(0));Assert.That(field[7*9+7],Is.LessThan(0));
            Assert.That(field[7*9+6],Is.GreaterThan(0));
        }
        [Test] public void RectangularPixelSpacingIsRespected()
        {
            var mask=new bool[25];mask[2*5+2]=true;
            var field=Calculate(mask,5,5,new Vector2(2,1));
            Assert.That(field[2*5+3],Is.EqualTo(1.5f).Within(.00001));
            Assert.That(field[3*5+2],Is.EqualTo(.5f).Within(.00001));
        }
        [Test] public void UnionHasNoInternalPartBoundary()
        {
            var mask=new bool[63];for(var y=1;y<6;y++)for(var x=1;x<8;x++)mask[y*9+x]=true;
            var field=Calculate(mask,9,7,Vector2.one);
            Assert.That(field[3*9+4],Is.EqualTo(-2.5f).Within(.00001));
        }
        [Test] public void EmptyFullAndInvalidRastersAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>Calculate(new bool[4],2,2,Vector2.one));
            Assert.Throws<ArgumentException>(()=>Calculate(new[]{true,true,true,true},2,2,Vector2.one));
            Assert.Throws<ArgumentException>(()=>Calculate(new[]{true,false,false,false},2,2,Vector2.zero));
        }
        [Test] public void SeparableTransformMatchesIndependentBruteForceReference()
        {
            var random=new System.Random(27017);var cell=new Vector2(.3f,.7f);const int width=11,height=9;
            for(var test=0;test<12;test++)
            {
                var mask=new bool[width*height];for(var i=0;i<mask.Length;i++)mask[i]=random.Next(4)==0;
                mask[0]=true;mask[mask.Length-1]=false;
                var actual=Calculate(mask,width,height,cell);
                for(var y=0;y<height;y++)for(var x=0;x<width;x++)
                {
                    var at=y*width+x;var best=double.MaxValue;
                    for(var j=0;j<mask.Length;j++)if(mask[j]!=mask[at])
                    {var dx=(x-j%width)*cell.x;var dy=(y-j/width)*cell.y;best=Math.Min(best,(double)dx*dx+(double)dy*dy);}
                    var expected=(Math.Sqrt(best)-.15)*(mask[at] ? -1 : 1);
                    Assert.That(actual[at],Is.EqualTo(expected).Within(.00001),"sample "+test+":"+at);
                }
            }
        }
    }
}
