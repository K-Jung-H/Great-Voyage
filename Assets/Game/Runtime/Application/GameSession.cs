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
        public bool SaveAtBoundary(GameSessionState state, out string error) => store.TrySave(state, out error);
    }

    public sealed class GameSession : IGameActions, IDisposable
    {
        private readonly IInteractionQueries interactions;
        private readonly SaveCoordinator saves;
        private readonly ITravelMap travelMap;
        private IDisposable travelPause;
        public TravelLink PendingTravel { get; private set; }
        public bool BoundarySavePending { get; private set; }
        private IDisposable journalPause;
        private IDisposable menuPause;
        private bool disposed;
        private readonly NavigationTuning navigationTuning;
        private float steeringInput;
        public bool IsSteering { get; private set; }
        public bool CharacterMovementAllowed => WorldInputAllowed && !IsSteering;

        public GameSessionState State { get; }
        public PauseReasons Pauses { get; } = new PauseReasons();
        public MoveIntent Movement { get; private set; }
        public bool JournalOpen { get; private set; }
        public bool MenuOpen { get; private set; }
        public bool ExitPrepared { get; private set; }
        public InteractionSelection? FocusedInteraction { get; private set; }
        public string Error { get; private set; } = "";
        public bool WorldInputAllowed => !disposed && !ExitPrepared && !Pauses.IsPaused;
        public event Action Changed;

        public GameSession(GameSessionState state, IInteractionQueries interactions, SaveCoordinator saves, ITravelMap travelMap = null,
            NavigationTuning navigationTuning = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
            this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
            this.travelMap = travelMap;
            this.navigationTuning = navigationTuning ?? new NavigationTuning();
            Pauses.Changed += OnPauseChanged;
            RefreshInteraction();
        }

        private void OnPauseChanged(bool paused)
        {
            if (paused) { Movement = default; steeringInput = 0; }
            RefreshInteraction();
            Changed?.Invoke();
        }

        public void Tick(double realSeconds)
        {
            if (!disposed && !ExitPrepared && !Pauses.IsPaused)
            {
                State.Clock.Advance(realSeconds);
                State.Navigation.Advance(realSeconds * GameClock.GameSecondsPerRealSecond,
                    IsSteering ? steeringInput : 0, navigationTuning.TurnDegreesPerGameSecond);
            }
            RefreshInteraction();
        }

        public void Move(float x, float y)
        {
            var intent = new MoveIntent(x, y);
            steeringInput = WorldInputAllowed && IsSteering ? Math.Max(-1f, Math.Min(1f, x)) : 0;
            Movement = CharacterMovementAllowed ? intent : default;
        }
        public bool CanInteract => WorldInputAllowed && (IsSteering || FocusedInteraction.HasValue);

        public void RefreshInteraction()
        {
            var next = WorldInputAllowed && !IsSteering ? interactions.Resolve(State.Player.Position, FocusedInteraction?.TargetId) : null;
            var changed = next?.TargetId != FocusedInteraction?.TargetId || next?.Prompt != FocusedInteraction?.Prompt
                || next?.Action != FocusedInteraction?.Action;
            FocusedInteraction = next;
            if (changed) Changed?.Invoke();
        }

        public bool Interact()
        {
            if (!CanInteract) return false;
            if (IsSteering) { EndSteering(); return true; }
            var selected = FocusedInteraction.Value;
            if (!interactions.IsStillValid(State.Player.Position, selected))
            {
                FocusedInteraction = null; Changed?.Invoke();
                return false;
            }
            if (selected.Action == InteractionAction.UseHelm)
            {
                if (State.Player.Position.AreaId != "upper-deck") return false;
                State.Navigation.Depart(State.PlayId, navigationTuning.CruiseSpeed);
                IsSteering = true; Movement = default; steeringInput = 0;
                RefreshInteraction(); Changed?.Invoke(); return true;
            }
            if (selected.Action == InteractionAction.Travel)
            {
                if (travelMap == null || !travelMap.TryResolve(selected.TargetId, State.Player.Position, out var link)
                    || link.SourceArea != State.Player.Position.AreaId || link.Id != selected.TargetId) return false;
                if (link.Kind == TravelKind.Disembark && State.Navigation.IsUnderway) return false;
                PendingTravel = link;
                travelPause = Pauses.Acquire("WorldTransition");
                Changed?.Invoke();
                return true;
            }
            if (selected.Action != InteractionAction.OpenJournal) return false;
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
            if (disposed || ExitPrepared || MenuOpen || PendingTravel != null) return;
            MenuOpen = true;
            menuPause = Pauses.Acquire("SessionMenu");
            Changed?.Invoke();
        }

        public void CloseMenu()
        {
            if (!MenuOpen || BoundarySavePending) return;
            MenuOpen = false;
            menuPause?.Dispose(); menuPause = null;
            Changed?.Invoke();
        }

        public void Back()
        {
            if (disposed || ExitPrepared || PendingTravel != null) return;
            if (MenuOpen) CloseMenu();
            else if (JournalOpen) CloseJournal();
            else if (IsSteering) EndSteering();
            else OpenMenu();
        }

        private void EndSteering()
        {
            IsSteering = false; steeringInput = 0; Movement = default;
            RefreshInteraction(); Changed?.Invoke();
        }

        public bool PrepareNormalExit()
        {
            if (disposed || PendingTravel != null) return false;
            if (ExitPrepared) return true;
            using (Pauses.Acquire("SavingExit"))
            {
                if (!saves.SaveAtBoundary(State, out var error))
                {
                    Error = "저장하지 못했습니다.\n기존 저장은 보존됩니다. 다시 시도해 주세요.";
                    OpenMenu();
                    Changed?.Invoke();
                    return false;
                }
                ExitPrepared = true;
                BoundarySavePending = false;
                Movement = default;
                Error = "";
                Changed?.Invoke();
                return true;
            }
        }

        public bool CompleteTravel(TravelLink expected, bool destinationReady)
        {
            if (disposed || expected == null || !ReferenceEquals(PendingTravel, expected)) return false;
            if (destinationReady) { State.Player.TransferTo(expected.Destination, expected.Facing); Error = ""; }
            PendingTravel = null;
            // While still paused, adapters synchronize the one committed pose and active view.
            Changed?.Invoke();
            if (destinationReady && (expected.Kind == TravelKind.Board || expected.Kind == TravelKind.Disembark))
            {
                BoundarySavePending = !saves.SaveAtBoundary(State, out _);
                if (BoundarySavePending) Error = "이동은 완료됐지만 저장하지 못했습니다. 재개 버튼으로 저장을 재시도하세요.";
                else Error = "";
            }
            else if (!destinationReady) Error = "이동할 공간을 준비하지 못했습니다. 원래 위치를 유지합니다.";
            travelPause?.Dispose(); travelPause = null;
            if (!destinationReady || BoundarySavePending) OpenMenu();
            Changed?.Invoke();
            return destinationReady;
        }

        public bool RetryBoundarySave()
        {
            if (!BoundarySavePending || PendingTravel != null || disposed) return false;
            if (!saves.SaveAtBoundary(State, out _)) return false;
            BoundarySavePending = false; Error = ""; Changed?.Invoke(); return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            FocusedInteraction = null;
            Pauses.Changed -= OnPauseChanged;
            PendingTravel = null;
            travelPause?.Dispose(); travelPause = null;
            journalPause?.Dispose(); menuPause?.Dispose();
            journalPause = menuPause = null;
            Pauses.Dispose();
            Changed = null;
        }
    }
}
