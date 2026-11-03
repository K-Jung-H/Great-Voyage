using System;
using System.IO;
using GreatVoyage.Application;
using GreatVoyage.Domain;
using GreatVoyage.Infrastructure;
using GreatVoyage.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreatVoyage.Composition
{
    [DefaultExecutionOrder(-100)]
    public sealed class CabinSessionRoot : MonoBehaviour
    {
        [SerializeField] private PlayerMotor player;
        [SerializeField] private CharacterView character;
        [SerializeField] private JournalDesk desk;
        [SerializeField] private CabinHud hud;
        [SerializeField] private InputActionAsset controls;
        [SerializeField] private string saveDirectoryName = "P3Cabin";
        [SerializeField] private int rootSeed = 1717;
        [SerializeField] private double firstSliceGameSeconds = 28800;
        private PcInputSource input;
        private bool initialized, quitAccepted;
        public GameSession Session { get; private set; }
        public string SavePath { get; private set; }

        // Used by isolated product-scene validation; consumed once, never changes the product save path.
        public static string NextLoadSaveDirectory { private get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLaunchOverrides() => NextLoadSaveDirectory = null;

        private void Awake()
        {
            try
            {
                var directory = NextLoadSaveDirectory;
                NextLoadSaveDirectory = null;
                if (string.IsNullOrEmpty(directory))
                    directory = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves", saveDirectoryName);
                var store = new JsonFileSessionStore(directory);
                SavePath = store.FilePath;
                Physics2D.SyncTransforms();
                var state = store.Load(s => desk.PlacementIsClear(s.Player.Position));
                if (state == null)
                {
                    var position = new WorldPosition("captain-cabin", player.transform.position.x, player.transform.position.y);
                    state = new GameSessionState(Guid.NewGuid().ToString("N"), rootSeed,
                        new CharacterState("player", position, Facing.Front), new GameClock(firstSliceGameSeconds));
                }
                Session = new GameSession(state, desk, new SaveCoordinator(store));
                player.Bind(Session);
                character.Bind(Session, player);
                hud.Bind(Session, RequestExit, store.RecoveredBackup ? "이전 정상 저장을 복구했습니다." : "");
                input = new PcInputSource(controls, Session);
                UnityEngine.Application.wantsToQuit += WantsToQuit;
                initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError("Session startup failed; save files preserved. " + ex);
                player.gameObject.SetActive(false);
                hud.ShowStartupFailure("저장 파일을 불러올 수 없습니다. 기존 파일은 보존했습니다.", QuitWithoutSaving);
            }
        }

        private void Update()
        {
            if (!initialized) return;
            input.Poll();
            Session.Tick(Time.unscaledDeltaTime);
        }

        public bool PrepareExit()
        {
            if (!initialized) return false;
            player.CapturePose();
            return Session.PrepareNormalExit();
        }

        public void RequestExit()
        {
            if (!PrepareExit()) return;
            quitAccepted = true;
            QuitWithoutSaving();
        }

        private bool WantsToQuit()
        {
            if (!initialized || quitAccepted) return true;
            return PrepareExit();
        }

        private static void QuitWithoutSaving()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }

        private void OnApplicationQuit()
        {
            if (initialized && !Session.ExitPrepared && !PrepareExit())
                Debug.LogError("Normal-exit save failed; existing file preserved.");
        }

        private void OnDestroy()
        {
            UnityEngine.Application.wantsToQuit -= WantsToQuit;
            input?.Dispose(); input = null;
            Session?.Dispose();
            initialized = false;
        }
    }
}
