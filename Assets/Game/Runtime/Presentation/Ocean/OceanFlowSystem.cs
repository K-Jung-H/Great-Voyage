using System;
using System.Collections.Generic;
using UnityEngine;
using GreatVoyage.Application;
using GreatVoyage.Domain;

namespace GreatVoyage.Ocean
{
    public sealed class OceanFlowSystem : MonoBehaviour
    {
        [Header("Fixed data source")]
        [SerializeField] private OceanDataMap dataMap;
        [SerializeField] private ComputeShader flowCompute;
        [SerializeField] private Renderer oceanSurfaceRenderer;

        [Header("Session projection")]
        [SerializeField] private Transform deckAnchor;
        [SerializeField] private OceanShip navigationShip;
        [SerializeField] private float deckBowDegrees = 90f;
        [SerializeField, Min(0.01f)] private float fieldUnitsPerNauticalMile = 10f;

        [Header("World-aligned FlowMap window")]
        [SerializeField] private Vector2 fieldWorldOrigin = new Vector2(-100f, -100f);
        [SerializeField] private Vector2 fieldWorldSize = new Vector2(400f, 400f);
        [SerializeField, Min(8)] private int fieldWidth = 512;
        [SerializeField, Min(8)] private int fieldHeight = 512;
        [SerializeField] private int worldNoiseSeed = 1337;
        [SerializeField, Min(0.0001f)] private float noiseFrequency = 0.025f;
        [SerializeField, Min(0f)] private float foamDecay = 0.55f;
        [SerializeField] private List<OceanShip> initialShips = new List<OceanShip>();

        private readonly List<OceanShip> ships = new List<OceanShip>();
        private Texture2D lowResolutionData;
        private RenderTexture baseFlow;
        private RenderTexture shipDelta;
        private RenderTexture finalFlow;
        private RenderTexture lightRead;
        private RenderTexture lightWrite;
        private RenderTexture waveRead;
        private RenderTexture waveWrite;
        private int waveKernel;
        [SerializeField, Min(0f)] private float waveFlowResponse = 0.22f;
        internal float WaveFlowResponse
        {
            get => waveFlowResponse;
            set => waveFlowResponse = Mathf.Max(0, value);
        }
        private double waveCycle;
        private RenderTexture foamA;
        private RenderTexture foamB;
        private MaterialPropertyBlock materialProperties;
        private bool initialized;
        private bool baseDirty = true;
        private OceanDataMap knownDataMap;
        private int knownSeed;
        private float knownNoiseFrequency;
        private int knownDataVersion = int.MinValue;
        private int buildBaseKernel;
        private int clearShipKernel;
        private int emitShipKernel;
        private int composeKernel;
        private int foamKernel;
        private int clearFoamKernel;

        private GameSession session;
        private double lastGameTime;
        private double oceanTime;
        private bool frameReady;

        public void BindSession(GameSession value)
        {
            session = value ?? throw new ArgumentNullException(nameof(value));
            lastGameTime = session.State.Clock.TotalGameSeconds;
            if (navigationShip != null)
            {
                navigationShip.ProjectNavigation(session.State.Navigation, fieldUnitsPerNauticalMile);
                navigationShip.ResetMotion();
            }
        }

        public RenderTexture BaseFlowMap => baseFlow;
        public RenderTexture FinalFlowMap => finalFlow;
        public RenderTexture FoamHistory => foamA;

        private void OnEnable()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            if (!initialized || dataMap == null)
                return;

            if (knownDataMap != dataMap || knownDataVersion != dataMap.Version)
                UploadDataMap();

            SetCommonParameters();
            bool rebuildBase = baseDirty || knownSeed != worldNoiseSeed || knownNoiseFrequency != noiseFrequency;
            if (rebuildBase)
            {
                flowCompute.SetTexture(buildBaseKernel, "_LowResolutionData", lowResolutionData);
                flowCompute.SetTexture(buildBaseKernel, "_BaseFlowWrite", baseFlow);
                DispatchFull(buildBaseKernel);
                knownSeed = worldNoiseSeed;
                knownNoiseFrequency = noiseFrequency;
                baseDirty = false;
            }

            double now = session == null ? 0 : session.State.Clock.TotalGameSeconds;
            float dt = session == null ? Time.deltaTime : session.Pauses.IsPaused ? 0 :
                (float)Math.Max(0, (now - lastGameTime) / GameClock.GameSecondsPerRealSecond);
            lastGameTime = now;
            if (dt <= 0 && frameReady && !rebuildBase)
                return;
            oceanTime += dt;
            flowCompute.SetTexture(clearShipKernel, "_ShipDeltaWrite", shipDelta);
            DispatchFull(clearShipKernel);
            for (int i = 0; i < ships.Count; i++)
            {
                OceanShip ship = ships[i];
                if (ship == null || !ship.isActiveAndEnabled)
                    continue;
                if (ship == navigationShip && session != null)
                    ship.ProjectNavigation(session.State.Navigation, fieldUnitsPerNauticalMile);
                if (dt > 0) ship.SyncMotion(dt);
                if (!ship.PrepareHull())
                    continue;
                EmitHullFlow(ship);
            }

            flowCompute.SetTexture(composeKernel, "_BaseFlowRead", baseFlow);
            flowCompute.SetTexture(composeKernel, "_ShipDeltaRead", shipDelta);
            flowCompute.SetTexture(composeKernel, "_FinalFlowWrite", finalFlow);
            DispatchFull(composeKernel);

            if (dt > 0)
            {
                TransportWave(dt);
                AdvanceFoam(dt);
            }
            frameReady = true;
            BindRenderer();
        }

        private void TransportWave(double remaining)
        {
            flowCompute.SetFloat("_WaveFlowResponse", waveFlowResponse);
            while (remaining > 0)
            {
                double boundary = waveCycle < 4 ? 4 : 8;
                double distance = boundary - waveCycle;
                double step = Math.Min(Math.Min(remaining, distance), 1.0 / 60.0);
                bool reset = step == distance;
                flowCompute.SetTexture(waveKernel, "_FinalFlowRead", finalFlow);
                flowCompute.SetTexture(waveKernel, "_WaveRead", waveRead);
                flowCompute.SetTexture(waveKernel, "_WaveWrite", waveWrite);
                flowCompute.SetFloat("_WaveStep", (float)step);
                flowCompute.SetInt("_WaveReset", reset ? (boundary == 4 ? 1 : 2) : 0);
                DispatchFull(waveKernel);
                (waveRead, waveWrite) = (waveWrite, waveRead);

                // Connected lines retain their coordinate history across wave resets.
                flowCompute.SetTexture(waveKernel, "_WaveRead", lightRead);
                flowCompute.SetTexture(waveKernel, "_WaveWrite", lightWrite);
                flowCompute.SetInt("_WaveReset", 0);
                DispatchFull(waveKernel);
                (lightRead, lightWrite) = (lightWrite, lightRead);
                waveCycle = reset ? boundary % 8 : waveCycle + step;
                remaining -= step;
            }
        }

        private void AdvanceFoam(float dt)
        {
            flowCompute.SetTexture(foamKernel, "_FinalFlowRead", finalFlow);
            flowCompute.SetTexture(foamKernel, "_ShipDeltaRead", shipDelta);
            flowCompute.SetTexture(foamKernel, "_FoamRead", foamA);
            flowCompute.SetTexture(foamKernel, "_FoamWrite", foamB);
            flowCompute.SetFloat("_DeltaTime", dt);
            flowCompute.SetFloat("_FoamDecay", foamDecay);
            DispatchFull(foamKernel);

            RenderTexture swap = foamA;
            foamA = foamB;
            foamB = swap;
        }

        public void RegisterShip(OceanShip ship)
        {
            if (ship != null && !ships.Contains(ship))
                ships.Add(ship);
        }

        public void UnregisterShip(OceanShip ship)
        {
            ships.Remove(ship);
        }

        internal void SetReviewData(OceanDataMap value)
        {
            dataMap = value;
            MarkOceanDataDirty();
        }

        public void MarkOceanDataDirty()
        {
            knownDataVersion = int.MinValue;
            baseDirty = true;
        }

        private void Initialize()
        {
            if (initialized)
                return;
            if (dataMap == null || flowCompute == null || oceanSurfaceRenderer == null)
            {
                Debug.LogError("OceanFlowSystem requires a Data Map, Compute Shader, and Surface Renderer.", this);
                return;
            }
            if (!SystemInfo.supportsComputeShaders ||
                !SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                Debug.LogError("The graphics device does not support the required compute and ARGBHalf UAV features.", this);
                return;
            }

            fieldWidth = Mathf.Max(8, fieldWidth);
            fieldHeight = Mathf.Max(8, fieldHeight);
            fieldWorldSize.x = Mathf.Max(0.01f, fieldWorldSize.x);
            fieldWorldSize.y = Mathf.Max(0.01f, fieldWorldSize.y);
            buildBaseKernel = flowCompute.FindKernel("BuildBaseFlow");
            clearShipKernel = flowCompute.FindKernel("ClearShipDelta");
            emitShipKernel = flowCompute.FindKernel("EmitHullFlow");
            composeKernel = flowCompute.FindKernel("ComposeFinalFlow");
            foamKernel = flowCompute.FindKernel("AdvanceFoam");
            clearFoamKernel = flowCompute.FindKernel("ClearFoam");

            baseFlow = MakeField("Ocean.BaseFlow");
            shipDelta = MakeField("Ocean.ShipDelta");
            finalFlow = MakeField("Ocean.FinalFlow");
            foamA = MakeField("Ocean.FoamA");
            foamB = MakeField("Ocean.FoamB");
            materialProperties = new MaterialPropertyBlock();
            ships.Clear();
            foreach (OceanShip ship in initialShips)
                RegisterShip(ship);
            RegisterShip(navigationShip);
            initialized = true;
            UploadDataMap();
            SetCommonParameters();
            ClearFoam(foamA);
            ClearFoam(foamB);
            var lightFormat = SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.RGFloat)
                ? RenderTextureFormat.RGFloat : RenderTextureFormat.ARGBHalf;
            lightRead = MakeField("Ocean.LightRead", lightFormat);
            lightWrite = MakeField("Ocean.LightWrite", lightFormat);
            waveRead = MakeField("Ocean.WaveRead");
            waveWrite = MakeField("Ocean.WaveWrite");
            waveCycle = 0;
            waveKernel = flowCompute.FindKernel("TransportWave");
            int clearWave = flowCompute.FindKernel("ClearWave");
            flowCompute.SetTexture(clearWave, "_WaveWrite", lightRead);
            DispatchFull(clearWave);
            flowCompute.SetTexture(clearWave, "_WaveWrite", waveRead);
            DispatchFull(clearWave);
        }

        private RenderTexture MakeField(string textureName, RenderTextureFormat format = RenderTextureFormat.ARGBHalf)
        {
            var texture = new RenderTexture(fieldWidth, fieldHeight, 0, format,
                RenderTextureReadWrite.Linear);
            texture.name = textureName;
            texture.enableRandomWrite = true;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.useMipMap = false;
            texture.Create();
            return texture;
        }

        private void UploadDataMap()
        {
            Vector2Int size = dataMap.Dimensions;
            if (lowResolutionData == null || lowResolutionData.width != size.x ||
                lowResolutionData.height != size.y)
            {
                if (lowResolutionData != null)
                    Destroy(lowResolutionData);
                lowResolutionData = new Texture2D(size.x, size.y, TextureFormat.RGBAHalf,
                    false, true);
                lowResolutionData.name = "Ocean.CoarseData";
                lowResolutionData.filterMode = FilterMode.Point;
                lowResolutionData.wrapMode = TextureWrapMode.Clamp;
            }
            lowResolutionData.SetPixels(dataMap.BuildGpuPayload());
            lowResolutionData.Apply(false, false);
            knownDataMap = dataMap;
            knownDataVersion = dataMap.Version;
            baseDirty = true;
        }

        private void SetCommonParameters()
        {
            flowCompute.SetInts("_FieldResolution", fieldWidth, fieldHeight);
            flowCompute.SetVector("_FieldOrigin", fieldWorldOrigin);
            flowCompute.SetVector("_FieldWorldSize", fieldWorldSize);
            flowCompute.SetVector("_DataOrigin", dataMap.WorldOrigin);
            flowCompute.SetInts("_DataResolution", dataMap.Dimensions.x, dataMap.Dimensions.y);
            flowCompute.SetFloat("_TileWorldSize", dataMap.TileWorldSize);
            flowCompute.SetFloat("_NoiseFrequency", noiseFrequency);
            flowCompute.SetInt("_NoiseSeed", worldNoiseSeed);
        }

        private void EmitHullFlow(OceanShip ship)
        {
            Rect r = ship.WorldBounds;
            float pad = ship.InteractionRadius;
            r.xMin -= pad;
            r.xMax += pad;
            r.yMin -= pad;
            r.yMax += pad;

            Vector2 texelsPerWorld = new Vector2(fieldWidth / fieldWorldSize.x,
                fieldHeight / fieldWorldSize.y);
            int minX = Mathf.Clamp(Mathf.FloorToInt((r.xMin - fieldWorldOrigin.x) * texelsPerWorld.x), 0, fieldWidth);
            int minY = Mathf.Clamp(Mathf.FloorToInt((r.yMin - fieldWorldOrigin.y) * texelsPerWorld.y), 0, fieldHeight);
            int maxX = Mathf.Clamp(Mathf.CeilToInt((r.xMax - fieldWorldOrigin.x) * texelsPerWorld.x), 0, fieldWidth);
            int maxY = Mathf.Clamp(Mathf.CeilToInt((r.yMax - fieldWorldOrigin.y) * texelsPerWorld.y), 0, fieldHeight);
            if (maxX <= minX || maxY <= minY)
                return;

            flowCompute.SetInts("_ShipDispatchOffset", minX, minY);
            flowCompute.SetInts("_ShipDispatchSize", maxX - minX, maxY - minY);
            flowCompute.SetInt("_HullVertexCount", ship.VertexCount);
            flowCompute.SetVector("_ShipPosition", ship.WorldPosition);
            flowCompute.SetVector("_ShipVelocity", ship.Velocity);
            flowCompute.SetFloat("_ShipAngularVelocity", ship.AngularVelocity);
            flowCompute.SetFloat("_ShipRadius", ship.InteractionRadius);
            flowCompute.SetFloat("_SideFlowGain", ship.SideFlowGain);
            flowCompute.SetFloat("_ShipFlowStrength", ship.FlowStrength);
            flowCompute.SetFloat("_ShipFoamEmission", ship.FoamEmission);
            flowCompute.SetBuffer(emitShipKernel, "_HullWorldVertices", ship.VertexBuffer);
            flowCompute.SetTexture(emitShipKernel, "_BaseFlowRead", baseFlow);
            flowCompute.SetTexture(emitShipKernel, "_ShipDeltaWrite", shipDelta);
            DispatchRect(emitShipKernel, maxX - minX, maxY - minY);
        }

        private void ClearFoam(RenderTexture target)
        {
            flowCompute.SetTexture(clearFoamKernel, "_FoamWrite", target);
            DispatchFull(clearFoamKernel);
        }

        private void DispatchFull(int kernel)
        {
            DispatchRect(kernel, fieldWidth, fieldHeight);
        }

        private void DispatchRect(int kernel, int width, int height)
        {
            flowCompute.Dispatch(kernel, Mathf.CeilToInt(width / 8f),
                Mathf.CeilToInt(height / 8f), 1);
        }

        private void BindRenderer()
        {
            oceanSurfaceRenderer.GetPropertyBlock(materialProperties);
            float blend = (float)(1 - Math.Abs(waveCycle / 4 - 1));
            materialProperties.SetFloat("_OceanWaveBlend", blend * blend * (3 - 2 * blend));
            materialProperties.SetTexture("_OceanLightOffsets", lightRead);
            materialProperties.SetTexture("_OceanWaveOffsets", waveRead);
            materialProperties.SetFloat("_OceanTime", (float)oceanTime);
            var projection = Matrix4x4.identity;
            if (session != null && deckAnchor != null)
            {
                var navigation = session.State.Navigation;
                var position = new Vector3((float)(navigation.X * fieldUnitsPerNauticalMile),
                    (float)(navigation.Y * fieldUnitsPerNauticalMile));
                var rotation = Quaternion.Euler(0, 0, (float)navigation.HeadingDegrees - deckBowDegrees);
                projection = Matrix4x4.TRS(position, rotation, Vector3.one) * deckAnchor.worldToLocalMatrix;
            }
            materialProperties.SetMatrix("_OceanWorldToField", projection);
            materialProperties.SetTexture("_OceanBaseFlowMap", baseFlow);
            materialProperties.SetTexture("_OceanShipDeltaMap", shipDelta);
            materialProperties.SetTexture("_OceanFinalFlowMap", finalFlow);
            materialProperties.SetTexture("_OceanFoamMap", foamA);
            materialProperties.SetVector("_FieldOrigin", fieldWorldOrigin);
            materialProperties.SetVector("_FieldWorldSize", fieldWorldSize);
            oceanSurfaceRenderer.SetPropertyBlock(materialProperties);
        }

        private void OnDisable()
        {
            initialized = false;
            frameReady = false;
            ReleaseTexture(ref baseFlow);
            ReleaseTexture(ref shipDelta);
            ReleaseTexture(ref finalFlow);
            ReleaseTexture(ref lightRead);
            ReleaseTexture(ref lightWrite);
            ReleaseTexture(ref waveRead);
            ReleaseTexture(ref waveWrite);
            ReleaseTexture(ref foamA);
            ReleaseTexture(ref foamB);
            if (lowResolutionData != null)
                Destroy(lowResolutionData);
            lowResolutionData = null;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }
}
