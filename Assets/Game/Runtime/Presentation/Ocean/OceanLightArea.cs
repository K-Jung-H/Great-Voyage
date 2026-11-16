using System;
using UnityEngine;

namespace GreatVoyage.Ocean
{
    // Fixed pattern cache only. No control mesh, per-frame compute or displacement history.
    internal sealed class OceanLightArea : IDisposable
    {
        private readonly RenderTexture pattern;
        internal OceanLightArea(ComputeShader compute)
        {
            pattern = new RenderTexture(512,512,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
            {
                name="Ocean.LightAreaPattern", enableRandomWrite=true, useMipMap=true,
                autoGenerateMips=false, filterMode=FilterMode.Trilinear, wrapMode=TextureWrapMode.Repeat
            };
            pattern.Create();
            int bake=compute.FindKernel("BakeLightArea");
            compute.SetTexture(bake,"_LightPatternWrite",pattern);
            compute.Dispatch(bake,64,64,1);
            pattern.GenerateMips();
        }
        internal void Bind(MaterialPropertyBlock properties)
        {
            properties.SetTexture("_OceanLightPattern",pattern);
            properties.SetFloat("_OceanLightReady",1);
        }
        public void Dispose()
        {
            pattern.Release();
            UnityEngine.Object.Destroy(pattern);
        }
    }
}
