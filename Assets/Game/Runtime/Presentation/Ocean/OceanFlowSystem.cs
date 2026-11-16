using System;
using System.Collections.Generic;
using UnityEngine;
using GreatVoyage.Application;
using GreatVoyage.Domain;

namespace GreatVoyage.Ocean
{
    public enum OceanSpaceMode { LocalFixed, WorldDynamic }

    public sealed class OceanFlowSystem : MonoBehaviour
    {
        [Header("Fixed data source")]
        [SerializeField] private OceanDataMap dataMap;
        [SerializeField] private ComputeShader flowCompute;
        [SerializeField] private Renderer oceanSurfaceRenderer;

        [Header("Session projection")]
        [SerializeField] private OceanSpaceMode spaceMode;
        [SerializeField] private Transform deckAnchor;
        [SerializeField, Range(0f, 2f)] private float lightPatternTravel = 1.5f;

        private Vector2 reviewLightCurrent;
        private double reviewLightCurrentStart;

        internal void SetReviewLightCurrent(Vector2 velocity)
        {
            reviewLightCurrent = velocity;
            reviewLightCurrentStart = oceanTime;
        }
        internal float LightPatternTravel
        {
            get => lightPatternTravel;
            set => lightPatternTravel = Mathf.Clamp(value, 0f, 2f);
        }

        [Serializable]
        private sealed class OceanVisual
        {
            public Transform oceanPose;
            public Transform visual;
        }

        [SerializeField] private List<OceanVisual> oceanVisuals = new List<OceanVisual>();
        private OceanShip reviewStageShip;
        private Matrix4x4 reviewProjection = Matrix4x4.identity;
        public Matrix4x4 SceneToOcean { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 OceanToScene { get; private set; } = Matrix4x4.identity;

        internal void SetReviewSpace(OceanSpaceMode mode, OceanShip stageShip, Matrix4x4 projection)
        {
            if (spaceMode != mode)
            {
                wakeHeads.Clear();
                frameReady = false;
            }
            spaceMode = mode;
            reviewStageShip = stageShip;
            reviewProjection = projection;
            UpdateProjection();
            if (stageShip != null) stageShip.SceneToOcean = SceneToOcean;
        }

        private void UpdateProjection()
        {
            SceneToOcean = Matrix4x4.identity;
            if (spaceMode == OceanSpaceMode.LocalFixed)
            {
                if (reviewStageShip != null)
                    SceneToOcean = reviewProjection;
                else if (session != null && deckAnchor != null)
                {
                    var navigation = session.State.Navigation;
                    var position = new Vector3((float)(navigation.X * fieldUnitsPerNauticalMile),
                        (float)(navigation.Y * fieldUnitsPerNauticalMile));
                    var rotation = Quaternion.Euler(0, 0, (float)navigation.HeadingDegrees - deckBowDegrees);
                    Matrix4x4 stagePose = Matrix4x4.TRS(deckAnchor.position, deckAnchor.rotation, Vector3.one);
                    SceneToOcean = Matrix4x4.TRS(position, rotation, Vector3.one) * stagePose.inverse;
                }
            }
            OceanToScene = SceneToOcean.inverse;
        }

        private void ProjectVisuals()
        {
            foreach (var entry in oceanVisuals)
            {
                if (entry == null || !entry.oceanPose || !entry.visual || entry.visual == entry.oceanPose
                    || entry.oceanPose.IsChildOf(entry.visual)) continue;
                entry.visual.SetPositionAndRotation(OceanToScene.MultiplyPoint3x4(entry.oceanPose.position),
                    OceanToScene.rotation * entry.oceanPose.rotation);
            }
        }
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

        [SerializeField] private List<OceanSurfaceInteractor> standaloneInteractors = new List<OceanSurfaceInteractor>();

        private readonly List<OceanShip> ships = new List<OceanShip>();
        private Texture2D lowResolutionData;
        private RenderTexture baseFlow;
        private RenderTexture boatFlow;
        private RenderTexture finalFlow;
        private OceanLightArea lightArea;
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
        private struct WakePoint
        {
            public Vector2 Position;
            public Vector4[] Edges, Strengths, Radii;
            public int EdgeCount, Topology;
            public Vector2 Minimum, Maximum;
            public double Time;
        }
        private struct WakeSegment
        {
            public WakePoint Start, End;
            public float Amplitude, Wavelength, Travel;
        }
        private readonly Dictionary<MonoBehaviour, WakePoint> wakeHeads = new Dictionary<MonoBehaviour, WakePoint>();
        private readonly List<WakeSegment> wakeSegments = new List<WakeSegment>(256);
        private const float WakeLifetime = 8f;
        private int wakeKernel;
        private const int MaxWakeEdges = 16;
        private const int MaxContactShapes = 8;
        private readonly Vector4[] contactEdges = new Vector4[MaxContactShapes * OceanSurfaceInteractor.MaxEdges];
        private readonly Vector4[] contactShapes = new Vector4[MaxContactShapes];
        private readonly Vector4[] contactBounds = new Vector4[MaxContactShapes];
        private int contactCount;
        private readonly Vector4[] emitterEdges = new Vector4[OceanSurfaceInteractor.MaxEdges];
        private readonly List<OceanSurfaceInteractor> wakeEmitters = new List<OceanSurfaceInteractor>();

        internal void DrawWakeSources(OceanShip ship, bool showEmitters, bool showHistory,
            Action<Vector2, Vector2, Color> draw)
        {
            if (showHistory)
            {
                for (int i = Mathf.Max(0, wakeSegments.Count - 12); i < wakeSegments.Count; i += 2)
                    DrawShape(wakeSegments[i].End, new Color(1f, 0.55f, 0.15f), draw);
            }
            if (showEmitters)
            {
                var current = new WakePoint { Edges = new Vector4[MaxWakeEdges], Strengths = new Vector4[4], Radii = new Vector4[4] };
                ReadWakeShape(ship, ref current);
                DrawShape(current, Color.magenta, draw);
            }
        }

        private static void DrawShape(WakePoint point, Color color, Action<Vector2, Vector2, Color> draw)
        {
            for (int i = 0; i < point.EdgeCount; i++)
            {
                Vector4 edge = point.Edges[i];
                OceanSurfaceInteractor.DrawOutline(edge, point.Radii[i / 4][i % 4],
                    (a, b) => draw(a, b, color));
            }
        }

        public void BindSession(GameSession value)
        {
            session = value ?? throw new ArgumentNullException(nameof(value));
            lastGameTime = session.State.Clock.TotalGameSeconds;
            if (navigationShip != null)
            {
                UpdateProjection();
                if (spaceMode == OceanSpaceMode.WorldDynamic)
                    navigationShip.ProjectNavigation(session.State.Navigation, fieldUnitsPerNauticalMile);
                navigationShip.SceneToOcean = SceneToOcean;
                navigationShip.ResetMotion();
            }
        }

        internal int ActiveWakeSegments => wakeSegments.Count;
        public RenderTexture BaseFlowMap => baseFlow;
        public RenderTexture BoatFlowMap => boatFlow;
        public RenderTexture FinalFlowMap => finalFlow;
        public RenderTexture FoamHistory => foamA;

        public void ClearFoamHistory()
        {
            if (foamA == null || foamB == null) return;
            ClearFoam(foamA);
            ClearFoam(foamB);
        }

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
            UpdateProjection();
            ProjectVisuals();
            oceanTime += dt;
            contactCount = 0;
            flowCompute.SetTexture(clearShipKernel, "_BoatFlowWrite", boatFlow);
            DispatchFull(clearShipKernel);
            for (int i = 0; i < ships.Count; i++)
            {
                OceanShip ship = ships[i];
                if (ship == null || !ship.isActiveAndEnabled)
                    continue;
                bool stageShip = ship == (reviewStageShip != null ? reviewStageShip : navigationShip);
                ship.SceneToOcean = stageShip ? SceneToOcean : Matrix4x4.identity;
                if (stageShip && ship == navigationShip && session != null &&
                    spaceMode == OceanSpaceMode.WorldDynamic)
                    ship.ProjectNavigation(session.State.Navigation, fieldUnitsPerNauticalMile);
                if (dt > 0) ship.SyncMotion(dt);
                EmitShipInteractions(ship);
                RecordWake(ship);
            }

            foreach (var interactor in standaloneInteractors)
            {
                if (interactor == null || interactor.GetComponentInParent<OceanShip>() != null) continue;
                ProcessInteraction(interactor, null);
                if (interactor.Strength > 0f && interactor.HasActiveCollider)
                    RecordWake(null, interactor);
                else
                    wakeHeads.Remove(interactor);
            }

            RenderWake();

            flowCompute.SetTexture(composeKernel, "_BaseFlowRead", baseFlow);
            flowCompute.SetTexture(composeKernel, "_BoatFlowRead", boatFlow);
            flowCompute.SetTexture(composeKernel, "_FinalFlowWrite", finalFlow);
            DispatchFull(composeKernel);

            if (dt > 0)
            {
                TransportWave(dt);
                AdvanceFoam(dt);
            }
            frameReady = true;

            BindRenderer(dt);
        }

        private void RecordWake(OceanShip ship, OceanSurfaceInteractor interactor = null)
        {
            MonoBehaviour owner = ship != null ? ship : (MonoBehaviour)interactor;
            Vector2 position = ship != null ? ship.WorldPosition : interactor.SourceCollider.bounds.center;
            if (interactor != null && interactor.CoordinateMode == OceanSpaceMode.LocalFixed)
                position = SceneToOcean.MultiplyPoint3x4(position);
            var current = new WakePoint { Position = position, Time = oceanTime };
            bool hasPrevious = wakeHeads.TryGetValue(owner, out var previous);
            float distance = hasPrevious ? Vector2.Distance(previous.Position, current.Position) : 0f;
            if (hasPrevious && distance < 0.25f && oceanTime - previous.Time < 0.2) return;
            current.Edges = new Vector4[MaxWakeEdges];
            current.Strengths = new Vector4[4];
            current.Radii = new Vector4[4];
            if (ship != null)
                ReadWakeShape(ship, ref current);
            else
            {
                AppendWakeShape(interactor, ref current);
                SetWakeBounds(ref current);
            }
            if (hasPrevious && current.EdgeCount > 0 && current.Topology == previous.Topology &&
                current.EdgeCount == previous.EdgeCount)
            {
                // Measure the emitting outline's travel so turning in place can leave a wake.
                float travel = 0f;
                for (int i = 0; i < current.EdgeCount; i++)
                {
                    if (current.Strengths[i / 4][i % 4] <= 0f) continue;
                    Vector4 delta = current.Edges[i] - previous.Edges[i];
                    travel = Mathf.Max(travel, new Vector2(delta.x, delta.y).magnitude);
                    travel = Mathf.Max(travel, new Vector2(delta.z, delta.w).magnitude);
                }
                // A reset/teleport must not draw a segment across the field.
                if (travel > 0.01f && travel < 10f && distance < 10f)
                {
                    if (wakeSegments.Count == 256) wakeSegments.RemoveAt(0);
                    float speed = travel / Mathf.Max(0.001f, (float)(current.Time - previous.Time));
                    wakeSegments.Add(new WakeSegment {
                        Start = previous, End = current,
                        Amplitude = (ship != null ? ship.HeightAmplitude : .12f) * Mathf.Min(speed, 4f),
                        Wavelength = ship != null ? ship.Wavelength : 2f, Travel = travel
                    });
                }
            }
            wakeHeads[owner] = current;
        }

        private void RenderWake()
        {
            for (int i = wakeSegments.Count - 1; i >= 0; i--)
                if (oceanTime - wakeSegments[i].End.Time >= WakeLifetime)
                    wakeSegments.RemoveAt(i);
            flowCompute.SetTexture(wakeKernel, "_BoatFlowWrite", boatFlow);
            foreach (var segment in wakeSegments)
            {
                float ageStart = (float)(oceanTime - segment.Start.Time);
                float ageEnd = (float)(oceanTime - segment.End.Time);
                float radius = 2f + Mathf.Min(ageStart, WakeLifetime) * 0.6f;
                Vector2 minimum = Vector2.Min(segment.Start.Minimum, segment.End.Minimum) - Vector2.one * radius;
                Vector2 maximum = Vector2.Max(segment.Start.Maximum, segment.End.Maximum) + Vector2.one * radius;
                int x = Mathf.Clamp(Mathf.FloorToInt((minimum.x - fieldWorldOrigin.x) * fieldWidth / fieldWorldSize.x), 0, fieldWidth);
                int y = Mathf.Clamp(Mathf.FloorToInt((minimum.y - fieldWorldOrigin.y) * fieldHeight / fieldWorldSize.y), 0, fieldHeight);
                int right = Mathf.Clamp(Mathf.CeilToInt((maximum.x - fieldWorldOrigin.x) * fieldWidth / fieldWorldSize.x), 0, fieldWidth);
                int top = Mathf.Clamp(Mathf.CeilToInt((maximum.y - fieldWorldOrigin.y) * fieldHeight / fieldWorldSize.y), 0, fieldHeight);
                if (right <= x || top <= y) continue;

                flowCompute.SetInts("_ShipDispatchOffset", x, y);
                flowCompute.SetInts("_ShipDispatchSize", right - x, top - y);
                flowCompute.SetVector("_WakeStart", new Vector4(segment.Start.Position.x, segment.Start.Position.y, ageStart, segment.Amplitude));
                flowCompute.SetVector("_WakeEnd", new Vector4(segment.End.Position.x, segment.End.Position.y, ageEnd, segment.Wavelength));
                flowCompute.SetVectorArray("_WakeStartEdges", segment.Start.Edges);
                flowCompute.SetVectorArray("_WakeEndEdges", segment.End.Edges);
                flowCompute.SetInt("_WakeEdgeCount", segment.Start.EdgeCount);
                flowCompute.SetFloat("_WakeTravel", segment.Travel);
                flowCompute.SetVectorArray("_WakeStrengths", segment.End.Strengths);
                flowCompute.SetVectorArray("_WakeStartRadii", segment.Start.Radii);
                flowCompute.SetVectorArray("_WakeEndRadii", segment.End.Radii);
                DispatchRect(wakeKernel, right - x, top - y);
            }
        }

        private void ReadWakeShape(OceanShip ship, ref WakePoint point)
        {
            ship.GetComponentsInChildren(true, wakeEmitters);
            foreach (var emitter in wakeEmitters)
                if (emitter.GetComponentInParent<OceanShip>() == ship)
                    AppendWakeShape(emitter, ref point);
            SetWakeBounds(ref point);
        }

        private void AppendWakeShape(OceanSurfaceInteractor emitter, ref WakePoint point)
        {
            if (!emitter.HasActiveCollider) return;
            int budget = Mathf.Min(OceanSurfaceInteractor.MaxEdges, MaxWakeEdges - point.EdgeCount);
            int count = emitter.ReadEdges(emitterEdges, out float radius, budget);
            if (count == 0) return;
            ProjectInteractorEdges(emitter, count);
            Array.Copy(emitterEdges, 0, point.Edges, point.EdgeCount, count);
            for (int i = point.EdgeCount; i < point.EdgeCount + count; i++)
            {
                point.Strengths[i / 4][i % 4] = emitter.Strength;
                point.Radii[i / 4][i % 4] = radius;
            }
            point.EdgeCount += count;
            point.Topology = unchecked(((point.Topology * 397 ^ emitter.GetEntityId().GetHashCode()) * 397 ^ count)
                * 397 ^ (int)emitter.CoordinateMode);
        }

        private static void SetWakeBounds(ref WakePoint point)
        {
            point.Minimum = Vector2.one * float.PositiveInfinity;
            point.Maximum = Vector2.one * float.NegativeInfinity;
            for (int i = 0; i < point.EdgeCount; i++)
            {
                Vector4 edge = point.Edges[i];
                Vector2 a = new Vector2(edge.x, edge.y);
                Vector2 b = new Vector2(edge.z, edge.w);
                Vector2 padding = Vector2.one * point.Radii[i / 4][i % 4];
                point.Minimum = Vector2.Min(point.Minimum, Vector2.Min(a, b) - padding);
                point.Maximum = Vector2.Max(point.Maximum, Vector2.Max(a, b) + padding);
            }
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
                flowCompute.SetTexture(waveKernel, "_FinalFlowRead", baseFlow);
                flowCompute.SetTexture(waveKernel, "_WaveRead", waveRead);
                flowCompute.SetTexture(waveKernel, "_WaveWrite", waveWrite);
                flowCompute.SetFloat("_WaveStep", (float)step);
                flowCompute.SetInt("_WaveReset", reset ? (boundary == 4 ? 1 : 2) : 0);
                DispatchFull(waveKernel);
                (waveRead, waveWrite) = (waveWrite, waveRead);

                waveCycle = reset ? boundary % 8 : waveCycle + step;
                remaining -= step;
            }
        }

        private void AdvanceFoam(float dt)
        {
            flowCompute.SetTexture(foamKernel, "_FinalFlowRead", finalFlow);
            flowCompute.SetTexture(foamKernel, "_BoatFlowRead", boatFlow);
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
            wakeHeads.Remove(ship);
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
            clearShipKernel = flowCompute.FindKernel("ClearBoatFlow");
            emitShipKernel = flowCompute.FindKernel("EmitBoatFlow");
            wakeKernel = flowCompute.FindKernel("EmitWake");
            composeKernel = flowCompute.FindKernel("ComposeFinalFlow");
            foamKernel = flowCompute.FindKernel("AdvanceFoam");
            clearFoamKernel = flowCompute.FindKernel("ClearFoam");

            baseFlow = MakeField("Ocean.BaseFlow");
            boatFlow = MakeField("Ocean.BoatFlow");
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
            if (lightArea == null) lightArea = new OceanLightArea(flowCompute);
            waveRead = MakeField("Ocean.WaveRead");
            waveWrite = MakeField("Ocean.WaveWrite");
            waveCycle = 0;
            waveKernel = flowCompute.FindKernel("TransportWave");
            int clearWave = flowCompute.FindKernel("ClearWave");
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

        private void EmitBoatFlow(OceanSurfaceInteractor interactor, OceanShip ship, Rect r, int count, float radius)
        {
            float influenceRadius = ship != null ? ship.InteractionRadius : 1f;
            float pad = influenceRadius + radius;
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
            flowCompute.SetInt("_InteractorEdgeCount", count);
            flowCompute.SetVectorArray("_InteractorEdges", emitterEdges);
            flowCompute.SetFloat("_InteractorRadius", radius);
            flowCompute.SetInt("_InteractorClosed", interactor.SourceCollider is PolygonCollider2D ? 1 : 0);
            flowCompute.SetVector("_ShipPosition", ship != null ? ship.WorldPosition : Vector2.zero);
            flowCompute.SetVector("_ShipVelocity", ship != null ? ship.Velocity : Vector2.zero);
            flowCompute.SetFloat("_ShipAngularVelocity", ship != null ? ship.AngularVelocity : 0f);
            flowCompute.SetFloat("_ShipRadius", influenceRadius);
            flowCompute.SetFloat("_ShipInnerRadius", ship != null ? ship.FullInfluenceRadius : 0f);
            flowCompute.SetFloat("_SideFlowGain", ship != null ? ship.SideFlowGain : .65f);
            flowCompute.SetFloat("_ShipFlowStrength", interactor.FlowStrength * (ship != null ? ship.FlowStrength : 1f));
            flowCompute.SetFloat("_ShipFoamEmission", interactor.FoamStrength * (ship != null ? ship.FoamEmission : .6f));
            flowCompute.SetFloat("_OceanTime", (float)oceanTime);
            flowCompute.SetTexture(emitShipKernel, "_BaseFlowRead", baseFlow);
            flowCompute.SetTexture(emitShipKernel, "_BoatFlowWrite", boatFlow);
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

        private void ProjectInteractorEdges(OceanSurfaceInteractor interactor, int count)
        {
            if (interactor.CoordinateMode != OceanSpaceMode.LocalFixed) return;
            Matrix4x4 projection = SceneToOcean;
            for (int i = 0; i < count; i++)
            {
                Vector4 edge = emitterEdges[i];
                emitterEdges[i] = OceanSurfaceInteractor.Pack(
                    projection.MultiplyPoint3x4(new Vector3(edge.x, edge.y)),
                    projection.MultiplyPoint3x4(new Vector3(edge.z, edge.w)));
            }
        }

        private void EmitShipInteractions(OceanShip ship)
        {
            ship.GetComponentsInChildren(false, wakeEmitters);
            foreach (var emitter in wakeEmitters)
                if (emitter.GetComponentInParent<OceanShip>() == ship)
                    ProcessInteraction(emitter, ship);
        }

        private void ProcessInteraction(OceanSurfaceInteractor emitter, OceanShip ship)
        {
            if (!emitter.HasActiveCollider) return;
            float width = emitter.ContactWidth;
            bool drawContact = width > 0f && contactCount < MaxContactShapes;
            bool emitFlow = emitter.FlowStrength > 0f || emitter.FoamStrength > 0f;
            if (!drawContact && !emitFlow) return;
            int count = emitter.ReadEdges(emitterEdges, out float radius);
            if (count == 0) return;
            ProjectInteractorEdges(emitter, count);
            var min = Vector2.one * float.PositiveInfinity;
            var max = Vector2.one * float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Vector4 edge = emitterEdges[i];
                var a = new Vector2(edge.x, edge.y);
                var b = new Vector2(edge.z, edge.w);
                min = Vector2.Min(min, Vector2.Min(a, b));
                max = Vector2.Max(max, Vector2.Max(a, b));
            }
            if (emitFlow)
                EmitBoatFlow(emitter, ship, Rect.MinMaxRect(min.x, min.y, max.x, max.y), count, radius);
            if (!drawContact) return;
            Array.Copy(emitterEdges, 0, contactEdges, contactCount * OceanSurfaceInteractor.MaxEdges, count);
            float pad = radius + width;
            contactBounds[contactCount] = new Vector4(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
            bool closed = emitter.SourceCollider is PolygonCollider2D;
            contactShapes[contactCount++] = new Vector4(count, radius, width, closed ? 1 : 0);
        }

        private void BindRenderer(float dt)
        {
            oceanSurfaceRenderer.GetPropertyBlock(materialProperties);
            float blend = (float)(1 - Math.Abs(waveCycle / 4 - 1));
            materialProperties.SetFloat("_OceanWaveBlend", blend * blend * (3 - 2 * blend));
            materialProperties.SetTexture("_OceanWaveOffsets", waveRead);
            materialProperties.SetFloat("_OceanTime", (float)oceanTime);
            materialProperties.SetMatrix("_OceanWorldToField", SceneToOcean);
            Vector2 lightTravel = Vector2.zero;
            Transform stage = reviewStageShip != null ? reviewStageShip.transform : deckAnchor;
            if (spaceMode == OceanSpaceMode.LocalFixed && stage != null)
            {
                Vector3 origin = SceneToOcean.MultiplyPoint3x4(stage.position);
                lightTravel = (Vector2)(origin - stage.position) * (lightPatternTravel - 1f);
            }
            // Backtrace the uniform review current in ocean space.
            lightTravel -= reviewLightCurrent * (float)(oceanTime - reviewLightCurrentStart);
            materialProperties.SetVector("_OceanLightTravel", new Vector4(lightTravel.x, lightTravel.y, 0f, 0f));
            materialProperties.SetTexture("_OceanBaseFlowMap", baseFlow);
            materialProperties.SetTexture("_OceanBoatFlowMap", boatFlow);
            materialProperties.SetTexture("_OceanFinalFlowMap", finalFlow);
            materialProperties.SetTexture("_OceanFoamMap", foamA);
            materialProperties.SetVector("_FieldOrigin", fieldWorldOrigin);
            materialProperties.SetVector("_FieldWorldSize", fieldWorldSize);
            materialProperties.SetInt("_OceanContactCount", contactCount);
            if (contactCount > 0)
            {
                materialProperties.SetVectorArray("_OceanContactEdges", contactEdges);
                materialProperties.SetVectorArray("_OceanContactShapes", contactShapes);
                materialProperties.SetVectorArray("_OceanContactBounds", contactBounds);
            }
            lightArea.Bind(materialProperties);
            oceanSurfaceRenderer.SetPropertyBlock(materialProperties);

        }

        private void OnDisable()
        {
            wakeSegments.Clear();
            wakeHeads.Clear();
            initialized = false;
            frameReady = false;
            ReleaseTexture(ref baseFlow);
            ReleaseTexture(ref boatFlow);
            ReleaseTexture(ref finalFlow);

            ReleaseTexture(ref waveRead);
            ReleaseTexture(ref waveWrite);
            ReleaseTexture(ref foamA);
            ReleaseTexture(ref foamB);
            if (lowResolutionData != null)
                Destroy(lowResolutionData);
            lowResolutionData = null;
        }

        private void OnDestroy()
        {
            lightArea?.Dispose();
            lightArea = null;
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
