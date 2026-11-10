using GreatVoyage.Application;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage.Presentation
{
    public sealed class NavigationView : MonoBehaviour
    {
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text chartReadout;
        [SerializeField] private RectTransform chart;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform mapArtwork;
        [SerializeField] private Button zoomIn, zoomOut, locate;
        private GameSession session;
        private float span = 40;
        private double centerX, centerY;
        private float nextText;
        private float mapAspect;
        public RectTransform Marker => marker;
        public void Bind(GameSession value)
        {
            session = value;
            var mapSprite = mapArtwork.GetComponent<Image>().sprite; mapAspect = mapSprite.rect.width / mapSprite.rect.height;
            zoomIn.onClick.AddListener(ZoomIn); zoomOut.onClick.AddListener(ZoomOut); locate.onClick.AddListener(Locate);
        }
        private void ZoomIn() => span = Mathf.Max(5, span * .5f);
        private void ZoomOut() => span = Mathf.Min(40, span * 2);
        private void Locate() { centerX = session.State.Navigation.X; centerY = session.State.Navigation.Y; }

        private void LateUpdate()
        {
            if (session == null) return;
            var state = session.State.Navigation;
            var scale = Mathf.Min(chart.rect.width, chart.rect.height) / span;
            mapArtwork.sizeDelta = new Vector2(chart.rect.height * mapAspect, chart.rect.height);
            mapArtwork.localScale = Vector3.one * (40 / span);
            mapArtwork.anchoredPosition = new Vector2((float)-centerX * scale, (float)-centerY * scale);
            var x = (float)(state.X - centerX) * scale;
            var y = (float)(state.Y - centerY) * scale;
            var visible = Mathf.Abs(x) < chart.rect.width * .5f - 24 && Mathf.Abs(y) < chart.rect.height * .5f - 24;
            marker.gameObject.SetActive(visible);
            marker.anchoredPosition = new Vector2(x, y);
            marker.localRotation = Quaternion.Euler(0, 0, (float)state.HeadingDegrees - 180);
            if (Time.unscaledTime < nextText) return;
            nextText = Time.unscaledTime + .2f;
            var bearing = (90 - state.HeadingDegrees + 360) % 360;
            status.text = state.IsUnderway
                ? $"항해 {state.VoyageSequence}  ·  {state.Speed * 3600:0.0} kn  ·  방위 {bearing:000}°  ·  {(session.IsSteering ? "수동 조타" : "직진")}" : "정박 중";
            chartReadout.text = $"현재 위치  {state.X:0.00}, {state.Y:0.00} 해리    방위 {bearing:000}°\n근해도 · 위쪽 북쪽 · 세로 범위 {span:0} 해리"
                + (visible ? "" : "\n현재 위치가 범위 밖입니다. [내 위치]로 이동하세요.");
        }
        private void OnDestroy()
        {
            if (zoomIn != null) zoomIn.onClick.RemoveListener(ZoomIn);
            if (zoomOut != null) zoomOut.onClick.RemoveListener(ZoomOut);
            if (locate != null) locate.onClick.RemoveListener(Locate);
            session = null;
        }
    }
}
