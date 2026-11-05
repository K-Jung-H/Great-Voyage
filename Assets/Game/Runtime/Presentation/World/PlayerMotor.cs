using GreatVoyage.Application;
using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 2.4f;
        [SerializeField, Min(0.1f)] private float acceleration = 18;
        [SerializeField, Min(0.1f)] private float deceleration = 26;
        private Rigidbody2D body;
        private GameSession session;
        private Facing facing;
        private string areaId;
        public Vector2 Velocity => body != null && body.simulated ? body.linearVelocity : Vector2.zero;
        public Rigidbody2D Body => body;

        public void Bind(GameSession value)
        {
            body = GetComponent<Rigidbody2D>();
            session = value;
            areaId = value.State.Player.Position.AreaId;
            facing = value.State.Player.Facing;
            var p = value.State.Player.Position;
            // Loading is an explicit pose transition: initialize both the scene transform and physics pose
            // before interpolation/scene-load synchronization can restore the authored starting transform.
            transform.position = new Vector3(p.X, p.Y, transform.position.z);
            body.position = new Vector2(p.X, p.Y);
            body.linearVelocity = Vector2.zero;
            session.Changed += OnSessionChanged;
            OnSessionChanged();
        }

        private void OnSessionChanged()
        {
            if (session == null || body == null) return;
            if (areaId != session.State.Player.Position.AreaId)
            {
                var p = session.State.Player.Position; areaId = p.AreaId;
                facing = session.State.Player.Facing;
                transform.position = new Vector3(p.X, p.Y, transform.position.z);
                body.position = new Vector2(p.X, p.Y); body.linearVelocity = Vector2.zero;
            }
            if (!session.WorldInputAllowed)
            {
                CapturePose();
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            else body.simulated = true;
        }

        private void FixedUpdate()
        {
            if (session == null || !session.WorldInputAllowed) return;
            CapturePose();
            var intent = session.Movement;
            var target = new Vector2(intent.X, intent.Y) * speed;
            var rate = target.sqrMagnitude > 0.001f ? acceleration : deceleration;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target, rate * Time.fixedDeltaTime);
            if (target.sqrMagnitude > 0.001f)
            {
                if (Mathf.Abs(intent.Y) > Mathf.Abs(intent.X) && intent.Y > 0) facing = Facing.Back;
                else if (Mathf.Abs(intent.X) > 0.1f) facing = intent.X < 0 ? Facing.Left : Facing.Right;
                else facing = Facing.Front;
            }
        }

        private void LateUpdate() => CapturePose();

        public void CapturePose()
        {
            if (session == null || body == null) return;
            session.State.Player.CommitResolvedPose(new WorldPosition(session.State.Player.Position.AreaId,
                body.position.x, body.position.y), facing);
        }

        private void OnDestroy()
        {
            if (session != null) session.Changed -= OnSessionChanged;
            session = null;
        }
    }
}
