using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 统一配色与字体。
    /// </summary>
    public static class UIStyle
    {
        public static readonly Color CameraBackground = Hex("#1B2838");
        public static readonly Color BgDeep = Hex("#15202C");
        public static readonly Color BgPanel = Hex("#243447");
        public static readonly Color BgCard = Hex("#2E445A");
        public static readonly Color BgCardSoft = Hex("#36506A");
        public static readonly Color Overlay = Hex("#0B121AEE");
        public static readonly Color Accent = Hex("#FF4D00");
        public static readonly Color AccentGreen = Hex("#66FF79");
        public static readonly Color AccentBlue = Hex("#5BA3E8");
        public static readonly Color Danger = Hex("#E57373");
        public static readonly Color TextMain = Hex("#F4F1EA");
        public static readonly Color TextMuted = Hex("#A8B4C0");
        public static readonly Color SlotEmpty = Hex("#1A2836");
        public static readonly Color Line = Hex("#4A6278");
        public static readonly Color TabSelect = Hex("#929292");
        public static readonly Color None = Hex("#FFFFFF");

        public const int TitleSize = 72;
        public const int SubtitleSize = 28;
        public const int ButtonSize = 36;
        public const int BodySize = 26;
        public const int CaptionSize = 22;

        /// <summary>中文大字号下行高要留余量，避免裁切。</summary>
        public static float TextBoxHeight(int fontSize, int lines = 1)
        {
            return Mathf.Ceil(fontSize * 1.5f * Mathf.Max(1, lines) + 20f);
        }

        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public const string PrefabFontAssetPath = "Assets/Font/NotoSansSC-UI.otf";
        public const string PrefabFontName = "NotoSansSC-UI";
        public const FontStyle TextFontStyle = FontStyle.Bold;
        public const float TextLineSpacing = 1f;

        private static Font _prefabFont;
        private static Sprite _whiteSprite;

        public static Font PrefabFont
        {
            get
            {
                if (_prefabFont != null)
                    return _prefabFont;

#if UNITY_EDITOR
                _prefabFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(PrefabFontAssetPath);
#endif
                if (_prefabFont == null)
                {
                    var fonts = Resources.FindObjectsOfTypeAll<Font>();
                    for (var i = 0; i < fonts.Length; i++)
                    {
                        if (fonts[i] != null && fonts[i].name == PrefabFontName)
                        {
                            _prefabFont = fonts[i];
                            break;
                        }
                    }
                }

                if (_prefabFont == null)
                    _prefabFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _prefabFont;
            }
        }

        public static void SetRuntimeFont(Font font)
        {
            if (font == null)
                return;
            _prefabFont = font;
        }

        public static Font Font => PrefabFont;

        public static void ApplyTextStyle(Text text)
        {
            if (text == null)
                return;
            text.font = PrefabFont;
            text.fontStyle = TextFontStyle;
            text.lineSpacing = TextLineSpacing;
        }

        public static void ApplyTextStyle(Component root)
        {
            if (root == null)
                return;
            var labels = root.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
                ApplyTextStyle(labels[i]);
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null)
                    return _whiteSprite;

                var tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _whiteSprite.name = "UIWhite";
                return _whiteSprite;
            }
        }

        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var color))
                return color;
            return Color.magenta;
        }

    }

    public enum UILayer
    {
        Background = 0,
        Gameplay = 1,
        Normal = 2,
        Hud = 3,
        Popup = 4,
        Toast = 5
    }

    public enum UIPanelId
    {
        MainMenu = 0,
        Settings = 1,
        GameHud = 2,
        Pause = 3,
        Result = 4,
        Shop = 5,
        LevelSelect = 6,
        Continue = 7,
        DailyLogin = 8,
        Energy = 9,
        Guide = 10,
        Splash = 11,
        Privacy = 12,
        Legal = 13,
        Loading = 14,
        ModeRules = 15,
        DailyTask = 17,
        TestWindow = 18,
        DevWindow = 19,
        Codex = 20,
        LevelIntro = 21
    }

    public sealed class ResultPayload
    {
        public bool IsWin;
        public GameModeType Mode;
        public string Message;
        public bool HasNextLevel;
        public int CoinsGained;
        public bool FirstClear;
        public bool IsEndless;
        public int Wave;
        public int Stars;
        /// <summary>教学关结算：不显示星级，按钮语义走「去玩第 1 关」。</summary>
        public bool IsTutorial;
        /// <summary>三星条件文案，失败 / 未满星时展示，告诉玩家差在哪。</summary>
        public string StarCondition;
        /// <summary>本局在评星口径上的实际值，例如「剩余 4 步」。</summary>
        public string StarProgress;
        /// <summary>本关首次拿到三星：已发放满体力奖励。</summary>
        public bool PerfectReward;
        public bool IsDaily;
    }

    public sealed class ContinuePayload
    {
        public GameModeType Mode;
        public string Message;
        public int Price;
        public string Benefit;
        public int ContinuesLeft;
        /// <summary>true 表示续关次数已用尽，本次只能看广告复活（不扣金币）。</summary>
        public bool UseAd;
    }

    public sealed class GuidePayload
    {
        /// <summary>数据驱动主键，新的逻辑只认它。</summary>
        public string StepKey;

        public GameModeType Mode;

        /// <summary>高亮目标（可选）。为空就退化成原来的纯文案引导。</summary>
        public RectTransform Target;

        public GuideHighlightShape Shape = GuideHighlightShape.None;

        /// <summary>兼容旧字段：旧的 GuidePanel / 图标走英文名解析。</summary>
        public Match3GuideId Id;

        public Sprite Icon;
        public string Title;
        public string Desc;
        public bool ShowLineArrows;
        public System.Action Closed;
    }

    public sealed class LegalPayload
    {
        public LegalDocId Doc;
    }

    public sealed class ModeRulesPayload
    {
        public GameModeType Mode;
        public bool ResumeOnClose = true;
    }
}
