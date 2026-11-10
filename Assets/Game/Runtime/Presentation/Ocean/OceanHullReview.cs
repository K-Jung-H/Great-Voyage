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
        private static readonly string[] Views = { "Water", "BaseFlow", "ShipDelta", "FinalFlow" };
        private Material material;
        private Material originalMaterial;
        private OceanDataMap runtimeData;
        private int scenario = 1;
        private int view;

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
            ResetScenario();
        }

        private void Update()
        {
            var hull = ship.transform;
            if (scenario == 1 || scenario == 2)
            {
                float direction = scenario == 1 ? 1 : -1;
                if (direction * hull.position.y >= 8)
                    ResetScenario();
                else
                    hull.position += Vector3.up * (direction * 4 * Time.deltaTime);
            }
            else if (scenario == 3)
            {
                hull.Rotate(0, 0, 40 * Time.deltaTime);
            }
        }

        private void ResetScenario()
        {
            ship.transform.SetPositionAndRotation(
                Vector3.up * (scenario == 1 ? -8 : scenario == 2 ? 8 : 0), Quaternion.identity);
            ship.ResetMotion();
            runtimeData.Fill(new OceanTileCell(Vector2.right, scenario == 4 ? 2 : 0, 0));
            flow.enabled = false;
            flow.enabled = true;
        }

        private void OnGUI()
        {
            if (material == null) return;
            GUILayout.BeginArea(new Rect(12, 12, 445, 175), GUI.skin.box);
            GUILayout.Label("W4 Hull Flow Review");
            int nextScenario = GUILayout.SelectionGrid(scenario, Scenarios, Scenarios.Length);
            if (nextScenario != scenario)
            {
                scenario = nextScenario;
                ResetScenario();
            }
            view = GUILayout.SelectionGrid(view, Views, Views.Length);
            material.SetFloat("_DebugMode", view);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Hull flow ×{ship.FlowStrength:F2}", GUILayout.Width(108));
            float strength = GUILayout.HorizontalSlider(ship.FlowStrength, 0, 4);
            if (GUILayout.Button("Off", GUILayout.Width(45))) strength = 0;
            if (GUILayout.Button("1×", GUILayout.Width(45))) strength = 1;
            if (GUILayout.Button("4×", GUILayout.Width(45))) strength = 4;
            ship.FlowStrength = strength;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Surface flow ×{flow.WaveFlowResponse:F2}", GUILayout.Width(140));
            flow.WaveFlowResponse = GUILayout.HorizontalSlider(flow.WaveFlowResponse, 0, 2);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Restart comparison")) ResetScenario();
            GUILayout.EndArea();

            // Draw the actual contact polygon independently of the water material.
            var hull = ship.GetComponent<PolygonCollider2D>();
            var vertices = hull.GetPath(0);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 a = reviewCamera.WorldToScreenPoint(hull.transform.TransformPoint(vertices[i] + hull.offset));
                Vector3 b = reviewCamera.WorldToScreenPoint(hull.transform.TransformPoint(vertices[(i + 1) % vertices.Length] + hull.offset));
                DrawLine(new Vector2(a.x, Screen.height - a.y), new Vector2(b.x, Screen.height - b.y));
            }
        }

        private static void DrawLine(Vector2 a, Vector2 b)
        {
            var direction = b - a;
            Matrix4x4 previous = GUI.matrix;
            Color color = GUI.color;
            GUI.color = new Color(1, .88f, .35f);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - 1, direction.magnitude, 2), Texture2D.whiteTexture);
            GUI.matrix = previous;
            GUI.color = color;
        }

        private void OnDestroy()
        {
            if (surface != null && originalMaterial != null) surface.sharedMaterial = originalMaterial;
            if (material != null) Destroy(material);
            if (runtimeData != null) Destroy(runtimeData);
        }
    }
}
