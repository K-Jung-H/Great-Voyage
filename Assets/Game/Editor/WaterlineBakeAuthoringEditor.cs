using System;
using GreatVoyage.Presentation;
using UnityEditor;
using UnityEngine;
namespace GreatVoyage.Editor
{
    [CustomEditor(typeof(WaterlineBakeAuthoring))]
    public sealed class WaterlineBakeAuthoringEditor : UnityEditor.Editor
    {
        private Texture2D distance;
        private string error;
        private UnityEngine.Object errorTarget;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var a = (WaterlineBakeAuthoring)target;
            EditorGUILayout.HelpBox("버튼을 눌렀을 때만 검증·생성합니다. SceneView 카메라와 무관한 Collider 로컬 SDF이며 Asset 저장/자동 변경 감지는 없습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Generate / Review Waterline SDF", GUILayout.Height(34)))
                    Generate(a);
            if (error != null)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                if (errorTarget != null && GUILayout.Button("문제 객체 선택"))
                {
                    Selection.activeObject = errorTarget;
                    EditorGUIUtility.PingObject(errorTarget);
                }
            }
            if (a.Review?.Texture == null)
            {
                EditorGUILayout.LabelField("아직 메모리 검토 결과가 없습니다.");
                return;
            }
            EditorGUILayout.HelpBox("마지막 수동 생성 결과입니다. 이후 변경 여부를 자동 판정하지 않습니다.", MessageType.Info);
            if (a.ReviewOverlay != null)
                a.ReviewOverlay.enabled = EditorGUILayout.Toggle("포함 영역 표시", a.ReviewOverlay.enabled);
            if (distance != null)
                GUI.DrawTexture(GUILayoutUtility.GetAspectRect((float)distance.width / distance.height), distance, ScaleMode.ScaleToFit);
        }
        private void Generate(WaterlineBakeAuthoring a)
        {
            try
            {
                BakeAction(a);
                error = null;
                errorTarget = null;
                if (distance != null)
                    DestroyImmediate(distance);
                distance = ColliderWaterlineBaker.DistancePreview(a.Review);
            }
            catch (Exception ex) { error = ex.Message; errorTarget = (ex as WaterlineInputException)?.Target; }
        }
        public static void BakeAction(WaterlineBakeAuthoring a) => ColliderWaterlineBaker.Generate(a);
        [MenuItem("Great Voyage/Water/Bake Selected Waterline")]
        private static void BakeSelected() => BakeAction(Selection.activeGameObject.GetComponent<WaterlineBakeAuthoring>());
        [MenuItem("Great Voyage/Water/Bake Selected Waterline", true)]
        private static bool CanBakeSelected() => !EditorApplication.isPlayingOrWillChangePlaymode && Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<WaterlineBakeAuthoring>() != null;
        private void OnDisable()
        {
            if (distance != null)
                DestroyImmediate(distance);
        }
    }
    [CustomEditor(typeof(WaterContactSource))]
    public sealed class WaterContactSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("같은 객체의 수면 접촉 Collider만 지정하세요. 일반 Collider는 체크를 끄며 Rigidbody가 필요하지 않습니다. Composite만 비시뮬레이션 Rigidbody를 사용합니다. 런타임에서 형상을 변경한 코드는 NotifyGeometryChanged()를 호출해야 합니다.", MessageType.Info);
        }
    }
}
