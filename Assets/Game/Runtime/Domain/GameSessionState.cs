using System;

namespace GreatVoyage.Domain
{
    public readonly struct WorldPosition
    {
        public string AreaId { get; }
        public float X { get; }
        public float Y { get; }

        public WorldPosition(string areaId, float x, float y)
        {
            if (string.IsNullOrWhiteSpace(areaId) || !Finite(x) || !Finite(y))
                throw new ArgumentException("A finite position and stable area ID are required.");
            AreaId = areaId; X = x; Y = y;
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum Facing { Front, Back, Left, Right }

    public sealed class CharacterState
    {
        public string Id { get; }
        public WorldPosition Position { get; private set; }
        public Facing Facing { get; private set; }

        public CharacterState(string id, WorldPosition position, Facing facing)
        {
            if (string.IsNullOrWhiteSpace(id) || !Enum.IsDefined(typeof(Facing), facing))
                throw new ArgumentException("Invalid character identity or facing.");
            Id = id; Position = position; Facing = facing;
        }

        // The physics adapter reports its resolved pose here; views and saving only read this state.
        public void CommitResolvedPose(WorldPosition position, Facing facing)
        {
            if (position.AreaId != Position.AreaId || !Enum.IsDefined(typeof(Facing), facing))
                throw new ArgumentException("Ordinary movement cannot change area.");
            Position = position; Facing = facing;
        }
    }

    public sealed class GameClock
    {
        public const double GameSecondsPerRealSecond = 86400.0 / 600.0;
        public double TotalGameSeconds { get; private set; }

        public GameClock(double totalGameSeconds)
        {
            if (double.IsNaN(totalGameSeconds) || double.IsInfinity(totalGameSeconds) || totalGameSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(totalGameSeconds));
            TotalGameSeconds = totalGameSeconds;
        }

        public void Advance(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));
            TotalGameSeconds += realSeconds * GameSecondsPerRealSecond;
        }
    }

    public sealed class GameSessionState
    {
        public string PlayId { get; }
        public int RootSeed { get; }
        public CharacterState Player { get; }
        public GameClock Clock { get; }

        public GameSessionState(string playId, int rootSeed, CharacterState player, GameClock clock)
        {
            if (string.IsNullOrWhiteSpace(playId)) throw new ArgumentException("A play ID is required.");
            PlayId = playId; RootSeed = rootSeed;
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }
    }
}
