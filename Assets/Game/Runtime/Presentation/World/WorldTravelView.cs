using System;
using System.Collections;
using System.Linq;
using GreatVoyage.Application;
using GreatVoyage.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage.Presentation
{
    public sealed class WorldTravelView : MonoBehaviour, ITravelMap
    {
        [SerializeField] private WorldArea[] areas;
        [SerializeField] private WorldPortal[] portals;
        [SerializeField] private CabinCamera cameraRig;
        [SerializeField] private CanvasGroup cover;
        [SerializeField] private Image illustration;
        [SerializeField] private TMP_Text destinationLabel;
        [SerializeField] private TMP_Text areaLabel;
        private GameSession session;
        private bool running;
        private IDisposable coverPause;
        public bool IsTransitioning => running;
        public WorldArea ActiveArea { get; private set; }
        public WorldPortal[] Portals => portals;
        public WorldArea[] Areas => areas;

        public bool TryResolve(string id, WorldPosition source, out TravelLink link)
        {
            link = null;
            foreach (var p in portals)
                if (p.TargetId == id && p.TryQuery(source, out _)) { link = p.Link(); return true; }
            return false;
        }

        public bool PrepareLoad(WorldPosition position)
        {
            var area = areas.SingleOrDefault(a => a.AreaId == position.AreaId);
            if (area == null) return false;
            Activate(area); Physics2D.SyncTransforms();
            return area.PlacementIsClear(position);
        }

        public void Bind(GameSession value)
        {
            session = value; session.Changed += Refresh;
            cover.alpha = 0; cover.blocksRaycasts = false; Refresh();
        }

        private void Activate(WorldArea selected)
        {
            foreach (var a in areas) if (a.gameObject.activeSelf != (a == selected)) a.gameObject.SetActive(a == selected);
            ActiveArea = selected;
        }

        private void Refresh()
        {
            if (session == null) return;
            if (session.PendingTravel != null)
            {
                if (!running) StartCoroutine(Transition(session.PendingTravel));
                return;
            }
            var selected = areas.Single(a => a.AreaId == session.State.Player.Position.AreaId);
            if (ActiveArea != selected) Activate(selected);
            areaLabel.text = selected.DisplayName;
            cameraRig.SetArea(selected.CameraBounds, session.State.Player.Position, false);
        }

        private IEnumerator Transition(TravelLink request)
        {
            running = true;
            coverPause = session.Pauses.Acquire("TransitionCover");
            var previous = ActiveArea;
            var destination = areas.SingleOrDefault(a => a.AreaId == request.Destination.AreaId);
            destinationLabel.text = destination != null ? destination.DisplayName : "이동 준비";
            illustration.sprite = destination != null ? destination.Illustration : null;
            illustration.enabled = illustration.sprite != null;
            cover.blocksRaycasts = true;
            yield return Fade(0, 1, .18f);
            var valid = false;
            if (destination != null)
            {
                Activate(destination); Physics2D.SyncTransforms();
                valid = destination.PlacementIsClear(request.Destination);
                if (!valid) { Activate(previous); Physics2D.SyncTransforms(); }
            }
            session.CompleteTravel(request, valid);
            cameraRig.SetArea(ActiveArea.CameraBounds, session.State.Player.Position, true);
            yield return new WaitForSecondsRealtime(.16f);
            // Cover retains input/time ownership through fade-in, independently of session's commit pause.
            yield return Fade(1, 0, .18f);
            cover.blocksRaycasts = false; running = false;
            coverPause?.Dispose(); coverPause = null;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration) { cover.alpha = Mathf.Lerp(from, to, elapsed / duration);
                elapsed += Time.unscaledDeltaTime; yield return null; }
            cover.alpha = to;
        }

        private void OnDestroy() { if (session != null) session.Changed -= Refresh;
            coverPause?.Dispose(); coverPause = null; }
    }
}
