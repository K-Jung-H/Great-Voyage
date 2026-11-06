using System;
using UnityEngine;

namespace GreatVoyage.Presentation
{
    // Presentation only. Positive sampling offset makes the visible water travel opposite the ship.
    public sealed class OceanRelativeFlow
    {
        private double x, y, vx, vy;
        public Vector2 SamplingOffset => new Vector2((float)x, (float)y);
        public Vector2 WaterVelocity => new Vector2((float)-vx, (float)-vy);

        public void Advance(double shipDeltaX, double shipDeltaY, double visualSeconds,
            double unitsPerNauticalMile, double responseSeconds)
        {
            if (!Finite(shipDeltaX) || !Finite(shipDeltaY) || !Finite(visualSeconds) || visualSeconds < 0
                || !Finite(unitsPerNauticalMile) || unitsPerNauticalMile <= 0
                || !Finite(responseSeconds) || responseSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(visualSeconds));
            if (visualSeconds == 0) return;
            var tx = shipDeltaX * unitsPerNauticalMile / visualSeconds;
            var ty = shipDeltaY * unitsPerNauticalMile / visualSeconds;
            if (responseSeconds == 0)
            { x += shipDeltaX * unitsPerNauticalMile; y += shipDeltaY * unitsPerNauticalMile; vx = tx; vy = ty; return; }
            var decay = Math.Exp(-visualSeconds / responseSeconds);
            var integral = responseSeconds * (1 - decay);
            x += tx * visualSeconds + (vx - tx) * integral;
            y += ty * visualSeconds + (vy - ty) * integral;
            vx = tx + (vx - tx) * decay;
            vy = ty + (vy - ty) * decay;
        }
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
