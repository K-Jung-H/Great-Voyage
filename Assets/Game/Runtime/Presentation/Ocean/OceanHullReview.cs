using UnityEngine;

namespace GreatVoyage.Ocean
{
    public sealed class OceanHullReview : MonoBehaviour
    {
        [SerializeField] private OceanFlowSystem flow;
        [SerializeField] private OceanDataMap data;
        [SerializeField] private OceanShip ship;
        [SerializeField] private Renderer surface;
        [SerializeField] private Camera reviewCamera;

        private static readonly string[] Scenarios = { "Still", "Ahead", "Astern", "Turn", "Cross current" };
        private static readonly string[] Views = { "Water", "Wake height", "Foam" };
        private static readonly int[] ViewModes = { 0, 5, 4 };
        private Material material;
        private Material originalMaterial;
        private OceanDataMap runtimeData;
        private int scenario = 1;
        private int view;
        private bool holdShip;
        private bool loopMotion = true;
        private bool lockShipInView;
        private bool fixedStage;
        private Vector3 voyagePosition;
        private Quaternion voyageRotation;
        private static readonly string[] SpaceModes = { "World (Dynamic)", "Local (Fixed)" };
        private Vector3 cameraWorldPosition;
        private Quaternion cameraWorldRotation;
        private Vector3 cameraHullOffset;
        private Quaternion cameraHullRotation;
        private bool showHull = true;
        private bool showEmitters = true;
        private bool showWakeHistory;

        private void Start()
        {
            originalMaterial = surface.sharedMaterial;
            material = new Material(originalMaterial);
            surface.sharedMaterial = material;
            // The review scene owns a dedicated data asset. Override it with a
            // transient copy so changing the scenario cannot alter that asset.
            runtimeData = Instantiate(data);
            flow.SetReviewData(runtimeData);
            flow.WaveFlowResponse = 1f;
            ship.InteractionRadius = 8f;
            ship.FullInfluenceRadius = 2f;
            ResetScenario();
            cameraWorldPosition = reviewCamera.transform.position;
            cameraWorldRotation = reviewCamera.transform.rotation;
            Quaternion inverseHullRotation = Quaternion.Inverse(ship.transform.rotation);
            cameraHullOffset = inverseHullRotation * (cameraWorldPosition - ship.transform.position);
            cameraHullRotation = inverseHullRotation * cameraWorldRotation;
        }

        private void LateUpdate()
        {
            if (!lockShipInView || fixedStage) return;
            var hull = ship.transform;
            reviewCamera.transform.SetPositionAndRotation(
                hull.position + hull.rotation * cameraHullOffset,
                hull.rotation * cameraHullRotation);
        }

        private void Update()
        {
            if (holdShip) return;
            if (scenario == 1 || scenario == 2)
            {
                float direction = scenario == 1 ? 1 : -1;
                if (direction * voyagePosition.y >= 8)
                {
                    if (loopMotion) ResetScenario();
                    else holdShip = true;
                }
                else
                    voyagePosition += Vector3.up * (direction * 4 * Time.deltaTime);
            }
            else if (scenario == 3)
            {
                voyageRotation *= Quaternion.Euler(0, 0, 40 * Time.deltaTime);
            }
            ApplyVoyagePose();
        }

        private void ApplyVoyagePose()
        {
            if (!fixedStage)
                ship.transform.SetPositionAndRotation(voyagePosition, voyageRotation);
            Matrix4x4 projection = fixedStage
                ? Matrix4x4.TRS(voyagePosition, voyageRotation, Vector3.one) *
                    Matrix4x4.TRS(ship.transform.position, ship.transform.rotation, Vector3.one).inverse
                : Matrix4x4.identity;
            flow.SetReviewSpace(fixedStage ? OceanSpaceMode.LocalFixed : OceanSpaceMode.WorldDynamic, ship, projection);
        }

        private void ResetScenario()
        {
            holdShip = false;
            voyagePosition = Vector3.up * (scenario == 1 ? -8 : scenario == 2 ? 8 : 0);
            voyageRotation = Quaternion.identity;
            if (fixedStage)
                ship.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ApplyVoyagePose();
            ship.ResetMotion();
            float currentSpeed = scenario == 4 ? 2f : 0f;
            runtimeData.Fill(new OceanTileCell(Vector2.right, currentSpeed, 0));
            flow.SetReviewLightCurrent(Vector2.right * currentSpeed);
            flow.enabled = false;
            flow.enabled = true;
        }

        private void OnGUI()
        {
            if (material == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 460, 308), GUI.skin.box);
            GUILayout.Label("W4 Hull Flow Review");
            bool nextFixed = GUILayout.SelectionGrid(fixedStage ? 1 : 0, SpaceModes, 2) == 1;
            if (nextFixed != fixedStage)
            {
                fixedStage = nextFixed;
                if (fixedStage)
                    reviewCamera.transform.SetPositionAndRotation(cameraWorldPosition, cameraWorldRotation);
                ResetScenario();
            }
            GUILayout.Label("Motion");
            int nextScenario = GUILayout.SelectionGrid(scenario, Scenarios, Scenarios.Length);
            if (nextScenario != scenario)
            {
                scenario = nextScenario;
                ResetScenario();
            }
            GUILayout.Space(4);
            GUILayout.Label($"Water diagnostics | Wake segments: {flow.ActiveWakeSegments}");
            view = GUILayout.SelectionGrid(view, Views, Views.Length);
            material.SetFloat("_DebugMode", ViewModes[view]);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Wake height {ship.HeightAmplitude:F2}", GUILayout.Width(140));
            ship.HeightAmplitude = GUILayout.HorizontalSlider(ship.HeightAmplitude, 0, 0.5f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            ship.FoamEmissionEnabled = GUILayout.Toggle(ship.FoamEmissionEnabled, "Emission On");
            holdShip = GUILayout.Toggle(holdShip, "Pause motion");
            loopMotion = GUILayout.Toggle(loopMotion, "Loop");
            if (GUILayout.Button("Restart")) ResetScenario();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !fixedStage;
            bool nextLock = GUILayout.Toggle(!fixedStage && lockShipInView, "Lock ship on screen");
            GUI.enabled = guiEnabled;
            if (lockShipInView && !nextLock)
                reviewCamera.transform.SetPositionAndRotation(cameraWorldPosition, cameraWorldRotation);
            lockShipInView = nextLock;
            if (fixedStage)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Light travel ×{flow.LightPatternTravel:F2}", GUILayout.Width(160));
                flow.LightPatternTravel = GUILayout.HorizontalSlider(flow.LightPatternTravel, 0f, 2f);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Overlays");
            GUILayout.BeginHorizontal();
            showHull = GUILayout.Toggle(showHull, "Hull");
            showEmitters = GUILayout.Toggle(showEmitters, "Emitters");
            showWakeHistory = GUILayout.Toggle(showWakeHistory, "Wake history");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            if (Event.current.type != EventType.Repaint) return;

            if (showHull && ship.TryGetComponent<PolygonCollider2D>(out var hull))
            {
                for (int path = 0; path < hull.pathCount; path++)
                {
                    var vertices = hull.GetPath(path);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector2 a = hull.transform.TransformPoint(vertices[i] + hull.offset);
                        Vector2 b = hull.transform.TransformPoint(vertices[(i + 1) % vertices.Length] + hull.offset);
                        DrawWorldLine(a, b, new Color(1, .88f, .35f));
                    }
                }
            }
            if (showEmitters || showWakeHistory)
                flow.DrawWakeSources(ship, showEmitters, showWakeHistory, DrawOceanLine);
        }

        private void DrawOceanLine(Vector2 a, Vector2 b, Color tint)
        {
            DrawWorldLine(flow.OceanToScene.MultiplyPoint3x4(a),
                flow.OceanToScene.MultiplyPoint3x4(b), tint);
        }

        private void DrawWorldLine(Vector2 a, Vector2 b, Color tint)
        {
            Vector3 start = reviewCamera.WorldToScreenPoint(a);
            Vector3 end = reviewCamera.WorldToScreenPoint(b);
            DrawLine(new Vector2(start.x, Screen.height - start.y), new Vector2(end.x, Screen.height - end.y), tint);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterScenePreview()
        {
            UnityEditor.SceneView.duringSceneGui -= DrawScenePreviews;
            UnityEditor.SceneView.duringSceneGui += DrawScenePreviews;
        }

        private static void DrawScenePreviews(UnityEditor.SceneView view)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (var review in FindObjectsByType<OceanHullReview>(FindObjectsSortMode.None))
                if (review.isActiveAndEnabled) review.DrawScenePreview();
        }

        private void DrawScenePreview()
        {
            if (!ship || !ship.TryGetComponent<PolygonCollider2D>(out var hull)) return;
            for (int path = 0; path < hull.pathCount; path++)
            {
                if (!showHull) break;
                var points = hull.GetPath(path);
                for (int i = 0; i < points.Length; i++)
                    OceanSurfaceInteractor.DrawSceneLine(hull.transform.TransformPoint(points[i] + hull.offset),
                        hull.transform.TransformPoint(points[(i + 1) % points.Length] + hull.offset), Color.yellow);
            }
            foreach (var emitter in ship.GetComponentsInChildren<OceanSurfaceInteractor>())
                if (showEmitters && emitter.isActiveAndEnabled) emitter.DrawSceneOutline();
        }
#endif

        private static void DrawLine(Vector2 a, Vector2 b, Color tint)
        {
            var direction = b - a;
            Matrix4x4 previous = GUI.matrix;
            Color color = GUI.color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, a);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(a.x - 1, a.y - 3, direction.magnitude + 2, 6), Texture2D.whiteTexture);
            GUI.color = tint;
            GUI.DrawTexture(new Rect(a.x, a.y - 1.5f, direction.magnitude, 3), Texture2D.whiteTexture);
            GUI.matrix = previous;
            GUI.color = color;
        }

        private void OnDestroy()
        {
            if (flow != null) flow.SetReviewLightCurrent(Vector2.zero);
            if (lockShipInView && reviewCamera != null)
                reviewCamera.transform.SetPositionAndRotation(cameraWorldPosition, cameraWorldRotation);
            if (surface != null && originalMaterial != null) surface.sharedMaterial = originalMaterial;
            if (material != null) Destroy(material);
            if (runtimeData != null) Destroy(runtimeData);
        }
    }
}
