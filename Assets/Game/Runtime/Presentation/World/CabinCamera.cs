using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class CabinCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 roomCenter;
        [SerializeField] private Vector2 roomSize = new Vector2(12.2f, 7);
        [SerializeField, Min(0.01f)] private float smoothing = 0.12f;
        private Camera view;
        private Vector3 velocity;

        private void Awake() => view = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (target == null) return;
            var halfHeight = view.orthographicSize;
            var halfWidth = halfHeight * view.aspect;
            var dx = Mathf.Max(0, roomSize.x * 0.5f - halfWidth);
            var dy = Mathf.Max(0, roomSize.y * 0.5f - halfHeight);
            var goal = new Vector3(Mathf.Clamp(target.position.x, roomCenter.x - dx, roomCenter.x + dx),
                Mathf.Clamp(target.position.y, roomCenter.y - dy, roomCenter.y + dy), transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothing,
                Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}
