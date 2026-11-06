using System;

namespace GreatVoyage.Domain
{
    public sealed class NavigationState
    {
        public string ShipId { get; }
        public string DeparturePortId { get; private set; }
        public string VoyageId { get; private set; }
        public int VoyageSequence { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double HeadingDegrees { get; private set; }
        public double Speed { get; private set; }
        public double Distance { get; private set; }
        public bool IsUnderway => !string.IsNullOrEmpty(VoyageId);

        public NavigationState(string shipId, string departurePortId, string voyageId, int voyageSequence,
            double x, double y, double headingDegrees, double speed, double distance)
        {
            if (string.IsNullOrWhiteSpace(shipId) || string.IsNullOrWhiteSpace(departurePortId)
                || voyageSequence < 0 || !Finite(x) || !Finite(y) || !Finite(headingDegrees)
                || !Finite(speed) || speed < 0 || !Finite(distance) || distance < 0
                || (!string.IsNullOrEmpty(voyageId) && (voyageSequence == 0 || speed <= 0))
                || (string.IsNullOrEmpty(voyageId) && speed != 0))
                throw new ArgumentException("Invalid navigation state.");
            ShipId = shipId; DeparturePortId = departurePortId; VoyageId = voyageId ?? "";
            VoyageSequence = voyageSequence; X = x; Y = y;
            HeadingDegrees = Normalize(headingDegrees); Speed = speed; Distance = distance;
        }

        public static NavigationState Docked() => new NavigationState("starting-ship", "starting-port", "", 0, 0, 0, 180, 0, 0);

        public bool Depart(string playId, double cruiseSpeed)
        {
            if (string.IsNullOrWhiteSpace(playId) || !Finite(cruiseSpeed) || cruiseSpeed <= 0)
                throw new ArgumentException("Invalid departure.");
            if (IsUnderway) return false;
            VoyageSequence = checked(VoyageSequence + 1);
            VoyageId = playId + ":voyage:" + VoyageSequence;
            Speed = cruiseSpeed;
            return true;
        }

        public void Advance(double gameSeconds, double steering, double turnDegreesPerGameSecond)
        {
            if (!Finite(gameSeconds) || gameSeconds < 0 || !Finite(steering) || Math.Abs(steering) > 1
                || !Finite(turnDegreesPerGameSecond) || turnDegreesPerGameSecond <= 0)
                throw new ArgumentException("Invalid navigation step.");
            if (!IsUnderway || gameSeconds == 0) return;
            var heading = HeadingDegrees * Math.PI / 180;
            var rate = -steering * turnDegreesPerGameSecond * Math.PI / 180;
            var angle = rate * gameSeconds;
            var half = angle * .5;
            var arc = Math.Abs(half) < 1e-8 ? 1 - half * half / 6 : Math.Sin(half) / half;
            var distance = Speed * gameSeconds;
            X += distance * arc * Math.Cos(heading + half);
            Y += distance * arc * Math.Sin(heading + half);
            HeadingDegrees = Normalize(HeadingDegrees + angle * 180 / Math.PI);
            Distance += distance;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static double Normalize(double value) => (value % 360 + 360) % 360;
    }

    public sealed class NavigationTuning
    {
        public double CruiseSpeed { get; }
        public double TurnDegreesPerGameSecond { get; }
        public NavigationTuning(double cruiseSpeed = .002, double turnDegreesPerGameSecond = .2)
        {
            if (double.IsNaN(cruiseSpeed) || double.IsInfinity(cruiseSpeed) || cruiseSpeed <= 0
                || double.IsNaN(turnDegreesPerGameSecond) || double.IsInfinity(turnDegreesPerGameSecond) || turnDegreesPerGameSecond <= 0)
                throw new ArgumentException("Invalid navigation tuning.");
            CruiseSpeed = cruiseSpeed; TurnDegreesPerGameSecond = turnDegreesPerGameSecond;
        }
    }
}
