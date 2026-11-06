using GreatVoyage.Application;
using GreatVoyage.Domain;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    public sealed class OceanNavigationBinding : MonoBehaviour
    {
        [SerializeField] private OceanAmbientDriver ocean;
        [SerializeField, Min(.001f)] private float unitsPerNauticalMile = 4;
        [SerializeField, Min(0)] private float responseSeconds = .75f;
        private GameSession session;
        private OceanRelativeFlow flow;
        private double previousX, previousY, previousClock;
        public OceanRelativeFlow Flow => flow;
        public OceanAmbientDriver Ocean => ocean;
        public void Bind(GameSession value)
        {
            session = value;
            flow = new OceanRelativeFlow();
            previousX = value.State.Navigation.X; previousY = value.State.Navigation.Y;
            previousClock = value.State.Clock.TotalGameSeconds;
            ocean.UseExternalClock = true;
        }
        private void LateUpdate()
        {
            if (session == null) return;
            var n = session.State.Navigation;
            var clock = session.State.Clock.TotalGameSeconds;
            var dt = (clock - previousClock) / GameClock.GameSecondsPerRealSecond;
            if (dt > 0 && !ocean.Paused)
            {
                flow.Advance(n.X-previousX,n.Y-previousY,dt,unitsPerNauticalMile,responseSeconds);
                ocean.AdvanceExternal(dt,flow.SamplingOffset);
            }
            previousX=n.X; previousY=n.Y; previousClock=clock;
        }
    }
}
