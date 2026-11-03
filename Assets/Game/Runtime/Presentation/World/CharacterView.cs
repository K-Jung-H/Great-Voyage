using GreatVoyage.Application;
using GreatVoyage.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace GreatVoyage.Presentation
{
    public sealed class CharacterView : MonoBehaviour
    {
        [SerializeField] private Transform visuals;
        [SerializeField] private Transform frontHead;
        [SerializeField] private Transform frontBody;
        [SerializeField] private Transform backParts;
        [SerializeField] private Transform quarterParts;
        [SerializeField] private Transform closedParts;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private SortingGroup sorting;
        private GameSession session;
        private PlayerMotor motor;
        private Vector3 leftBase, rightBase;
        private float phase, idleTime;
        private Facing? lastFacing;
        private bool lastBlink;

        public void Bind(GameSession value, PlayerMotor movement)
        {
            session = value; motor = movement;
            leftBase = leftHand.localPosition; rightBase = rightHand.localPosition;
            RefreshFacing(false);
        }

        private void LateUpdate()
        {
            if (session == null) return;
            sorting.sortingOrder = 100 - Mathf.RoundToInt((transform.position.y - 0.2f) * 20);
            if (session.Pauses.IsPaused || session.ExitPrepared) return;
            var moving = motor.Velocity.sqrMagnitude > 0.015f;
            if (moving) { phase += Time.deltaTime * 11; idleTime = 0; }
            else idleTime += Time.deltaTime;
            var bob = moving ? Mathf.Sin(phase) * 0.012f : 0;
            visuals.localPosition = new Vector3(0, bob, 0);
            var handSwing = moving ? Mathf.Sin(phase) * 0.035f : 0;
            leftHand.localPosition = leftBase + Vector3.up * handSwing;
            rightHand.localPosition = rightBase - Vector3.up * handSwing;
            RefreshFacing(!moving && idleTime % 4.8f > 4.65f);
        }

        private void RefreshFacing(bool blink)
        {
            var facing = session.State.Player.Facing;
            blink = blink && facing == Facing.Front;
            if (lastFacing == facing && lastBlink == blink) return;
            frontHead.gameObject.SetActive(facing == Facing.Front && !blink);
            frontBody.gameObject.SetActive(facing == Facing.Front);
            closedParts.gameObject.SetActive(blink);
            backParts.gameObject.SetActive(facing == Facing.Back);
            quarterParts.gameObject.SetActive(facing == Facing.Left || facing == Facing.Right);
            visuals.localScale = facing == Facing.Left ? new Vector3(-1, 1, 1) : Vector3.one;
            lastFacing = facing; lastBlink = blink;
        }
    }
}
