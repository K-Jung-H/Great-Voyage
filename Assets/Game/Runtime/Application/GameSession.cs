using System;
using GreatVoyage.Domain;

namespace GreatVoyage.Application
{
    public interface IGameActions
    {
        void Move(float x, float y);
        bool Interact();
        void Back();
    }

    public interface IInteractionQueries
    {
        bool CanOpenJournal(WorldPosition player);
    }

    public interface ISessionStore
    {
        bool TrySave(GameSessionState state, out string error);
    }

    public readonly struct MoveIntent
    {
        public float X { get; }
        public float Y { get; }
        public MoveIntent(float x, float y)
        {
            if (!WorldPosition.Finite(x) || !WorldPosition.Finite(y)) throw new ArgumentException("Invalid movement.");
            var magnitude = Math.Sqrt(x * x + y * y);
            var divisor = Math.Max(1.0, magnitude);
            X = (float)(x / divisor); Y = (float)(y / divisor);
        }
    }

    public sealed class SaveCoordinator
    {
        private readonly ISessionStore store;
        public SaveCoordinator(ISessionStore store) => this.store = store ?? throw new ArgumentNullException(nameof(store));
        public bool SaveNormalExit(GameSessionState state, out string error) => store.TrySave(state, out error);
    }

    public sealed class GameSession : IGameActions, IDisposable
    {
        private readonly IInteractionQueries interactions;
        private readonly SaveCoordinator saves;
        private IDisposable journalPause;
        private IDisposable menuPause;
        private bool disposed;

        public GameSessionState State { get; }
        public PauseReasons Pauses { get; } = new PauseReasons();
        public MoveIntent Movement { get; private set; }
        public bool JournalOpen { get; private set; }
        public bool MenuOpen { get; private set; }
        public bool ExitPrepared { get; private set; }
        public string Error { get; private set; } = "";
        public bool WorldInputAllowed => !disposed && !ExitPrepared && !Pauses.IsPaused;
        public event Action Changed;

        public GameSession(GameSessionState state, IInteractionQueries interactions, SaveCoordinator saves)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
            Pauses.Changed += OnPauseChanged;
        }

        private void OnPauseChanged(bool paused)
        {
            if (paused) Movement = default;
            Changed?.Invoke();
        }

        public void Tick(double realSeconds)
        {
            if (!disposed && !ExitPrepared && !Pauses.IsPaused) State.Clock.Advance(realSeconds);
        }

        public void Move(float x, float y) => Movement = WorldInputAllowed ? new MoveIntent(x, y) : default;
        public bool CanInteract => WorldInputAllowed && interactions.CanOpenJournal(State.Player.Position);

        public bool Interact()
        {
            if (!CanInteract) return false;
            JournalOpen = true;
            journalPause = Pauses.Acquire("Journal");
            Changed?.Invoke();
            return true;
        }

        public void CloseJournal()
        {
            if (!JournalOpen || MenuOpen) return;
            JournalOpen = false;
            journalPause?.Dispose(); journalPause = null;
            Changed?.Invoke();
        }

        public void OpenMenu()
        {
            if (disposed || ExitPrepared || MenuOpen) return;
            MenuOpen = true;
            menuPause = Pauses.Acquire("SessionMenu");
            Changed?.Invoke();
        }

        public void CloseMenu()
        {
            if (!MenuOpen) return;
            MenuOpen = false;
            menuPause?.Dispose(); menuPause = null;
            Changed?.Invoke();
        }

        public void Back()
        {
            if (disposed || ExitPrepared) return;
            if (MenuOpen) CloseMenu();
            else if (JournalOpen) CloseJournal();
            else OpenMenu();
        }

        public bool PrepareNormalExit()
        {
            if (disposed) return false;
            if (ExitPrepared) return true;
            using (Pauses.Acquire("SavingExit"))
            {
                if (!saves.SaveNormalExit(State, out var error))
                {
                    Error = "저장하지 못했습니다.\n기존 저장은 보존됩니다. 다시 시도해 주세요.";
                    OpenMenu();
                    Changed?.Invoke();
                    return false;
                }
                ExitPrepared = true;
                Movement = default;
                Error = "";
                Changed?.Invoke();
                return true;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Pauses.Changed -= OnPauseChanged;
            journalPause?.Dispose(); menuPause?.Dispose();
            journalPause = menuPause = null;
            Pauses.Dispose();
            Changed = null;
        }
    }
}
