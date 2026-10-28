using System.Collections;
using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 运行时入口。无需在场景中摆物体，Play 后自动拉起管理器与主界面。
    /// </summary>
    public static class GameEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindObjectOfType<GameBootstrap>() != null)
                return;

            var root = new GameObject("[SmallGames]");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<GameBootstrap>();
        }
    }

    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        public const string AppName = "咔滋盒";
        public const string AppVersion = "0.1.0";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(BootRoutine());
        }

        private IEnumerator BootRoutine()
        {
            TintMainCamera();
            AssetHub.Initialize();
            yield return LoadChineseFontRoutine();

            BuildManagers();
            if (UIManager.Instance == null)
                yield break;
            BeginUiFlow();
        }

        private static IEnumerator LoadChineseFontRoutine()
        {
#if UNITY_EDITOR
            yield break;
#else
            var font = AssetHub.Load<Font>(UiPrefabs.CjkFont);
            if (font == null)
            {
                Debug.LogWarning("缺少中文字体 " + UiPrefabs.CjkFont);
                yield break;
            }

            UIStyle.SetRuntimeFont(font);
            yield break;
#endif
        }

        private static void BeginUiFlow()
        {
            Match3LevelCatalog.TryStartEditorPlay();
            LianliankanLevelCatalog.TryStartEditorPlay();
            SheepLevelCatalog.TryStartEditorPlay();
            if (GameManager.Instance != null && GameManager.Instance.IsInGame)
                return;
            UIManager.Instance.Open(UIPanelId.Splash);
        }

        public static void ContinueFromSplash()
        {
            EnterMainMenu(true);
        }

        public static void EnterMainMenu(bool promptDaily)
        {
            if (UIManager.Instance == null)
                return;
            AdDirector.Initialize();
            UIManager.Instance.Close(UIPanelId.Legal);
            UIManager.Instance.Close(UIPanelId.Privacy);
            UIManager.Instance.Close(UIPanelId.Splash);
            UIManager.Instance.Close(UIPanelId.Loading);
            UIManager.Instance.Open(UIPanelId.MainMenu);
            // 主菜单已经出来了，借这段空闲把大面板建好
            UIManager.Instance.WarmupHeavyPanels();
            if (promptDaily && DailyLoginRepository.CanClaim)
                UIManager.Instance.Open(UIPanelId.DailyLogin);
        }

        public static void OpenLegal(LegalDocId doc)
        {
            if (UIManager.Instance == null)
                return;
            UIManager.Instance.Open(UIPanelId.Legal, new LegalPayload
            {
                Doc = doc
            });
        }

        private void Update()
        {
            EnergyRepository.Tick();
            if (Input.GetKeyDown(KeyCode.Escape))
                GameManager.Instance.HandleBack();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void BuildManagers()
        {
            var settings = SettingsRepository.Load();
            CoinWallet.Load();
            ProgressRepository.Load();
            EnergyRepository.Load();
            DailyLoginRepository.Load();
            DailyTaskRepository.Load();
            GuideRepository.Load();
            ModeRulesRepository.Load();
            Analytics.Load();
            Analytics.AppLaunch();

            var audioGo = new GameObject("AudioManager");
            audioGo.transform.SetParent(transform, false);
            audioGo.AddComponent<AudioManager>().Initialize(settings);

            var uiGo = new GameObject("UIManager");
            uiGo.transform.SetParent(transform, false);
            uiGo.AddComponent<UIManager>().Initialize();

            var gameGo = new GameObject("GameManager");
            gameGo.transform.SetParent(transform, false);
            gameGo.AddComponent<GameManager>().Initialize();
        }

        private static void TintMainCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UIStyle.CameraBackground;
            camera.orthographic = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
        }

        public static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
