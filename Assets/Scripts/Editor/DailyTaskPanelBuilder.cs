using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames.Editor
{
    /// <summary>
    /// 生成每日任务面板预制体（内置一个隐藏的任务行模板），并给主菜单补一个「任务」入口按钮。
    /// 菜单：SmallGames/生成每日任务面板
    /// </summary>
    public static class DailyTaskPanelBuilder
    {
        private const string PrefabPath = "Assets/Content/UI/DailyTask.prefab";
        private const string PrefabAddress = "UI/DailyTask";
        private const string MainMenuPath = "Assets/Content/UI/MainMenu.prefab";
        private const string CoreGroup = "core_ui";

        [MenuItem("SmallGames/生成每日任务面板")]
        public static void BuildFromMenu()
        {
            Directory.CreateDirectory("Assets/Content/UI");
            var prefab = BuildPanelPrefab();
            var patched = PatchMainMenu();
            MarkAddressable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[DailyTask] 面板预制体：{PrefabPath}；主菜单按钮：{(patched ? "已添加" : "已存在或跳过")}");
        }

        private static GameObject BuildPanelPrefab()
        {
            var root = new GameObject("DailyTask", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            UIViewFactory.StretchFull(rootRect);
            var panel = root.AddComponent<DailyTaskPanel>();

            // 背景遮罩兼点击关闭
            var scrimImage = UIViewFactory.CreateImage("Scrim", rootRect, UIStyle.Overlay);
            var scrimButton = scrimImage.gameObject.AddComponent<Button>();
            panel.DimButton = scrimButton;

            // 主窗口
            var windowRect = UIViewFactory.CreateRect("Window", rootRect);
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(1080f, 860f);
            windowRect.anchoredPosition = Vector2.zero;
            windowRect.localScale = Vector3.one;
            var windowImage = windowRect.gameObject.AddComponent<Image>();
            windowImage.sprite = UIStyle.WhiteSprite;
            windowImage.color = UIStyle.BgPanel;

            panel.TitleText = Label(windowRect, "Title", "每日任务", 48, UIStyle.TextMain,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(640f, 72f), new Vector2(40f, -32f), TextAnchor.MiddleLeft);

            panel.SummaryText = Label(windowRect, "Summary", "已完成 0/9", UIStyle.BodySize, UIStyle.TextMuted,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(640f, 48f), new Vector2(40f, -108f), TextAnchor.MiddleLeft);

            BuildScrollList(windowRect, out var listRoot, out var contentRoot);
            panel.ListRoot = listRoot;
            // 行模板挂在 Content 下并保持隐藏：和 LevelSelectPanel.CellPrefab 一个套路，
            // 运行时靠 Instantiate 复制，改外观直接编辑 DailyTask.prefab 里的这个节点即可。
            panel.CellPrefab = BuildCellTemplate(contentRoot);

            var closeRect = UIViewFactory.CreateRect("Close", windowRect);
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.sizeDelta = new Vector2(260f, 84f);
            closeRect.anchoredPosition = new Vector2(0f, 28f);
            closeRect.localScale = Vector3.one;
            var closeImage = closeRect.gameObject.AddComponent<Image>();
            closeImage.sprite = UIStyle.WhiteSprite;
            closeImage.color = UIStyle.BgCardSoft;
            panel.CloseButton = closeRect.gameObject.AddComponent<Button>();
            Label(closeRect, "Label", "关闭", UIStyle.ButtonSize, UIStyle.TextMain,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            EditorUtility.SetDirty(root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 任务行模板：挂在 Content 下、保持隐藏，面板运行时 Instantiate 它。
        /// 结构和 LevelSelectPanel.CellPrefab 一致，运行时只 Bind 数据。
        /// </summary>
        private static DailyTaskCellView BuildCellTemplate(RectTransform contentParent)
        {
            var go = new GameObject("TaskCellTemplate", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(contentParent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, DailyTaskCellView.CellHeight);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            var view = go.AddComponent<DailyTaskCellView>();
            view.Background = UIViewFactory.CreateImage("Bg", rect, UIStyle.BgCardSoft);
            view.TitleText = Label(rect, "Title", "任务标题", UIStyle.BodySize + 4, UIStyle.TextMain,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(560f, 46f), new Vector2(32f, -18f), TextAnchor.MiddleLeft);
            view.DescText = Label(rect, "Desc", "任务描述", UIStyle.CaptionSize, UIStyle.TextMuted,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(560f, 40f), new Vector2(32f, -70f), TextAnchor.MiddleLeft);
            view.ProgressText = Label(rect, "Progress", "0/1", UIStyle.CaptionSize, UIStyle.TextMuted,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(190f, 44f), new Vector2(-300f, 0f), TextAnchor.MiddleRight);
            view.RewardText = Label(rect, "Reward", "+0", UIStyle.CaptionSize, UIStyle.AccentGreen,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(110f, 44f), new Vector2(-180f, 0f), TextAnchor.MiddleRight);

            var claimRect = UIViewFactory.CreateRect("Claim", rect);
            claimRect.anchorMin = new Vector2(1f, 0.5f);
            claimRect.anchorMax = new Vector2(1f, 0.5f);
            claimRect.pivot = new Vector2(1f, 0.5f);
            claimRect.sizeDelta = new Vector2(150f, 68f);
            claimRect.anchoredPosition = new Vector2(-24f, 0f);
            claimRect.localScale = Vector3.one;
            var claimImage = claimRect.gameObject.AddComponent<Image>();
            claimImage.sprite = UIStyle.WhiteSprite;
            claimImage.color = UIStyle.Accent;
            view.ClaimButton = claimRect.gameObject.AddComponent<Button>();
            view.ClaimLabel = Label(claimRect, "Label", "领取", UIStyle.BodySize, UIStyle.TextMain,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);

            // 模板自身不参与布局、不显示，只作为 Instantiate 的蓝本
            go.SetActive(false);
            return view;
        }

        private static void BuildScrollList(RectTransform parent, out RectTransform listRoot, out RectTransform contentRoot)
        {
            var listRect = UIViewFactory.CreateRect("TaskList", parent);
            listRect.anchorMin = new Vector2(0f, 1f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.pivot = new Vector2(0.5f, 1f);
            listRect.sizeDelta = new Vector2(-80f, 620f);
            listRect.anchoredPosition = new Vector2(0f, -172f);
            listRect.localScale = Vector3.one;

            var scroll = listRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.scrollSensitivity = 40f;

            var viewportRect = UIViewFactory.CreateRect("Viewport", listRect);
            UIViewFactory.StretchFull(viewportRect);
            var viewportImage = viewportRect.gameObject.AddComponent<Image>();
            viewportImage.sprite = UIStyle.WhiteSprite;
            viewportImage.color = UIStyle.BgDeep;
            viewportRect.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var contentRect = UIViewFactory.CreateRect("Content", viewportRect);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.localScale = Vector3.one;

            var layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            listRoot = listRect;
            contentRoot = contentRect;
        }

        private static Text Label(
            RectTransform parent,
            string name,
            string content,
            int size,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size2,
            Vector2 anchoredPos,
            TextAnchor align)
        {
            var rect = UIViewFactory.CreateRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size2;
            rect.anchoredPosition = anchoredPos;
            rect.localScale = Vector3.one;
            var text = rect.gameObject.AddComponent<Text>();
            UIStyle.ApplyTextStyle(text);
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>复制主菜单的「每日」按钮，改成「任务」按钮并挂到 MainMenuPanel.TaskButton。</summary>
        private static bool PatchMainMenu()
        {
            if (!File.Exists(MainMenuPath))
            {
                Debug.LogWarning($"[DailyTask] 没找到 {MainMenuPath}，任务入口未添加。");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(MainMenuPath);
            var panel = root.GetComponent<MainMenuPanel>();
            if (panel == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning("[DailyTask] MainMenu 预制体上没有 MainMenuPanel，跳过。");
                return false;
            }

            if (panel.TaskButton != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return false;
            }

            var daily = panel.DailyButton;
            if (daily == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning("[DailyTask] MainMenuPanel.DailyButton 为空，无法复制按钮。");
                return false;
            }

            var copy = Object.Instantiate(daily, daily.transform.parent);
            copy.name = "TaskButton";
            copy.transform.SetSiblingIndex(daily.transform.GetSiblingIndex() + 1);
            var label = copy.GetComponentInChildren<Text>();
            if (label != null)
                label.text = "任务";
            panel.TaskButton = copy;

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, MainMenuPath);
            PrefabUtility.UnloadPrefabContents(root);
            return true;
        }

        private static void MarkAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
                return;

            var group = settings.FindGroup(CoreGroup);
            if (group == null)
                return;

            var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            if (string.IsNullOrEmpty(guid))
                return;

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
                return;
            entry.address = PrefabAddress;
            entry.SetLabel(AssetLabels.Core, true, true);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }
    }
}
