using System;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public static class WaterlineDistanceField
    {
        private const double Far = 1e30;
        public sealed class Workspace
        {
            private double[] insideDistances, outsideDistances, f, d, breaks;
            private int[] sites;
            public void Calculate(bool[] mask,int width,int height,Vector2 size,float[] output)
            {
                if(mask==null || output==null || mask.Length!=(long)width*height || output.Length!=mask.Length || width<2 || height<2 || !Finite(size.x) || !Finite(size.y) || size.x<=0 || size.y<=0)throw new ArgumentException("Invalid SDF buffers.");
                var insideCount=0;foreach(var bit in mask)if(bit)insideCount++;
                if(insideCount==0 || insideCount==mask.Length)throw new ArgumentException("SDF requires both contact and exterior samples.");
                if(insideDistances==null || insideDistances.Length!=mask.Length){insideDistances=new double[mask.Length];outsideDistances=new double[mask.Length];}
                var limit=Math.Max(width,height);
                if(f==null || f.Length<limit){f=new double[limit];d=new double[limit];sites=new int[limit];breaks=new double[limit+1];}
                TransformInto(mask,width,height,size,true,insideDistances);TransformInto(mask,width,height,size,false,outsideDistances);
                var half=Math.Min(size.x,size.y)*.5;
                for(var i=0;i<output.Length;i++)output[i]=(float)(Math.Max(0,Math.Sqrt(mask[i]?outsideDistances[i]:insideDistances[i])-half)*(mask[i]?-1:1));
            }
            private void TransformInto(bool[] mask,int w,int h,Vector2 size,bool feature,double[] data)
            {
                for(var y=0;y<h;y++)
                {for(var x=0;x<w;x++)f[x]=mask[y*w+x]==feature?0:Far;Line(f,d,w,(double)size.x*size.x,sites,breaks);for(var x=0;x<w;x++)data[y*w+x]=d[x];}
                for(var x=0;x<w;x++)
                {for(var y=0;y<h;y++)f[y]=data[y*w+x];Line(f,d,h,(double)size.y*size.y,sites,breaks);for(var y=0;y<h;y++)data[y*w+x]=d[y];}
            }
        }
        private static void Line(double[] f,double[] d,int n,double spacing,int[] sites,double[] breaks)
        {
            var k=-1;
            for(var q=0;q<n;q++)
            {
                if(f[q]>=Far*.5)continue;
                double crossing=double.NegativeInfinity;
                while(k>=0)
                {
                    var v=sites[k];crossing=((f[q]-f[v])/spacing+(double)q*q-(double)v*v)/(2*(q-v));
                    if(crossing>breaks[k])break;k--;
                }
                k++;sites[k]=q;breaks[k]=k==0 ? double.NegativeInfinity : crossing;breaks[k+1]=double.PositiveInfinity;
            }
            if(k<0){for(var q=0;q<n;q++)d[q]=Far;return;}
            var at=0;
            for(var q=0;q<n;q++)
            {while(breaks[at+1]<q)at++;var delta=q-sites[at];d[q]=spacing*delta*delta+f[sites[at]];}
        }
        private static bool Finite(float v)=>!float.IsNaN(v) && !float.IsInfinity(v);
    }
}
