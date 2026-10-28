using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames.Editor
{
    /// <summary>
    /// 生成全屏图鉴预制体，并在主菜单复制一个「图鉴」按钮。
    /// 菜单：SmallGames/生成图鉴面板
    /// </summary>
    public static class CodexPanelBuilder
    {
        const string PrefabPath = "Assets/Content/UI/handbook.prefab";
        const string PrefabAddress = "UI/handbook";
        const string MainMenuPath = "Assets/Content/UI/MainMenu.prefab";
        const string Yellow = "Assets/UI/PNG/Yellow/Default/button_rectangle_border.png";
        const string Green = "Assets/UI/PNG/Green/Default/button_rectangle_border.png";
        const string Red = "Assets/UI/PNG/Red/Default/button_rectangle_border.png";
        const string Background = "Assets/UI/PNG/bg.png";

        [MenuItem("SmallGames/生成图鉴面板")]
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
            Debug.Log("[图鉴] 预制体已生成。主菜单按钮：" + (patched ? "已添加" : "已存在或跳过"));
        }

        static GameObject BuildPanelPrefab()
        {
            var root = new GameObject("handbook", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            UIViewFactory.StretchFull(rootRect);
            var panel = root.AddComponent<CodexPanel>();
            panel.TabNormal = LoadSprite(Yellow);
            panel.TabSelected = LoadSprite(Green);

            var bg = MakeImage("Background", rootRect, Background, false);
            UIViewFactory.StretchFull(bg.rectTransform);
            bg.raycastTarget = true;

            var title = Label(rootRect, "Title", "图鉴", 44, UIStyle.TextMain,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(240f, UIStyle.TextBoxHeight(44)), new Vector2(36f, -28f), TextAnchor.MiddleLeft);

            var modeRoot = UIViewFactory.CreateRect("ModeTabs", rootRect);
            modeRoot.anchorMin = new Vector2(0f, 0f);
            modeRoot.anchorMax = new Vector2(0f, 1f);
            modeRoot.pivot = new Vector2(0f, 0.5f);
            modeRoot.offsetMin = new Vector2(28f, 36f);
            modeRoot.offsetMax = new Vector2(300f, -120f);
            var modeLayout = modeRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            modeLayout.spacing = 16f;
            modeLayout.childAlignment = TextAnchor.UpperCenter;
            modeLayout.childControlWidth = true;
            modeLayout.childControlHeight = false;
            modeLayout.childForceExpandWidth = true;
            modeLayout.childForceExpandHeight = false;
            panel.ModeTabRoot = modeRoot;
            panel.ModeTabTemplate = MakeTab(modeRoot, "ModeTab", "玩法", Yellow);

            var right = UIViewFactory.CreateRect("Right", rootRect);
            right.anchorMin = new Vector2(0f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.offsetMin = new Vector2(328f, 28f);
            right.offsetMax = new Vector2(-28f, -28f);

            var window = MakeImage("Window", right, null, false);
            window.color = UIStyle.BgPanel;
            UIViewFactory.StretchFull(window.rectTransform);

            var categoryRoot = UIViewFactory.CreateRect("CategoryTabs", right);
            categoryRoot.anchorMin = new Vector2(0f, 1f);
            categoryRoot.anchorMax = new Vector2(1f, 1f);
            categoryRoot.pivot = new Vector2(0.5f, 1f);
            categoryRoot.sizeDelta = new Vector2(-48f, 74f);
            categoryRoot.anchoredPosition = new Vector2(0f, -24f);
            var categoryLayout = categoryRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            categoryLayout.spacing = 16f;
            categoryLayout.childAlignment = TextAnchor.MiddleLeft;
            categoryLayout.childControlWidth = false;
            categoryLayout.childControlHeight = true;
            categoryLayout.childForceExpandWidth = false;
            categoryLayout.childForceExpandHeight = true;
            panel.CategoryTabRoot = categoryRoot;
            panel.CategoryTabTemplate = MakeTab(categoryRoot, "CategoryTab", "分类", Yellow);
            panel.CategoryTabTemplate.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 62f);

            BuildList(right, panel);

            var close = MakeImage("Close", rootRect, Red, true);
            var closeRect = close.rectTransform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(180f, 74f);
            closeRect.anchoredPosition = new Vector2(-28f, -24f);
            panel.CloseButton = close.gameObject.AddComponent<Button>();
            panel.CloseButton.targetGraphic = close;
            Label(closeRect, "Label", "关闭", 36, UIStyle.SlotEmpty,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            title.raycastTarget = false;
            EditorUtility.SetDirty(root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void BuildList(RectTransform parent, CodexPanel panel)
        {
            var listRect = UIViewFactory.CreateRect("List", parent);
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(24f, 24f);
            listRect.offsetMax = new Vector2(-24f, -168f);
            panel.ListRoot = listRect;

            var scroll = listRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 40f;
            panel.List = scroll;

            var viewport = UIViewFactory.CreateRect("Viewport", listRect);
            UIViewFactory.StretchFull(viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = UIStyle.WhiteSprite;
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UIViewFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            panel.ListContent = content;
            panel.RowTemplate = BuildRow(listRect);
        }

        static CodexRowView BuildRow(RectTransform parent)
        {
            var go = new GameObject("RowTemplate", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 132f);

            var view = go.AddComponent<CodexRowView>();
            var bg = MakeImage("Bg", rect, null, false);
            bg.color = UIStyle.BgCard;
            UIViewFactory.StretchFull(bg.rectTransform);
            bg.raycastTarget = false;

            var icon = MakeImage("Icon", rect, null, false);
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
            iconRect.anchoredPosition = new Vector2(18f, 0f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            view.IconImage = icon;

            view.NameText = Label(rect, "Name", "名称", 36, UIStyle.TextMain,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(720f, UIStyle.TextBoxHeight(36)), new Vector2(132f, -8f), TextAnchor.MiddleLeft);

            var viewport = UIViewFactory.CreateRect("BodyViewport", rect);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 0f);
            viewport.pivot = new Vector2(0f, 0f);
            viewport.offsetMin = new Vector2(132f, 14f);
            viewport.offsetMax = new Vector2(-20f, 14f + UIStyle.TextBoxHeight(26));
            viewport.gameObject.AddComponent<RectMask2D>();
            view.BodyViewport = viewport;

            view.BodyText = Label(viewport, "Body", "描述", 26, UIStyle.TextMain,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(800f, UIStyle.TextBoxHeight(26)), Vector2.zero, TextAnchor.MiddleLeft);
            view.BodyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            view.BodyText.verticalOverflow = VerticalWrapMode.Overflow;

            go.SetActive(false);
            return view;
        }

        static Button MakeTab(RectTransform parent, string name, string caption, string spritePath)
        {
            var image = MakeImage(name, parent, spritePath, true);
            var rect = image.rectTransform;
            rect.sizeDelta = new Vector2(240f, 74f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 74f;
            layout.minHeight = 74f;
            Label(rect, "Label", caption, 36, UIStyle.SlotEmpty,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            image.gameObject.SetActive(false);
            return button;
        }

        static Image MakeImage(string name, RectTransform parent, string spritePath, bool sliced)
        {
            var rect = UIViewFactory.CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            var sprite = LoadSprite(spritePath);
            image.sprite = sprite != null ? sprite : UIStyle.WhiteSprite;
            image.type = sliced && sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            return image;
        }

        static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Text Label(
            RectTransform parent,
            string name,
            string content,
            int size,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPos,
            TextAnchor align)
        {
            var rect = UIViewFactory.CreateRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPos;
            var text = rect.gameObject.AddComponent<Text>();
            UIStyle.ApplyTextStyle(text);
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        static bool PatchMainMenu()
        {
            if (!File.Exists(MainMenuPath))
                return false;
            var root = PrefabUtility.LoadPrefabContents(MainMenuPath);
            var panel = root.GetComponent<MainMenuPanel>();
            if (panel == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return false;
            }

            if (panel.CodexButton == null)
            {
                var source = panel.TaskButton != null ? panel.TaskButton : panel.ShopButton;
                if (source == null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    Debug.LogWarning("[图鉴] 主菜单没有可复制的按钮。");
                    return false;
                }

                var copy = Object.Instantiate(source, source.transform.parent);
                copy.name = "CodexButton";
                copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
                var label = copy.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = "图鉴";
                panel.CodexButton = copy;
            }

            PlaceCodexButton(panel);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, MainMenuPath);
            PrefabUtility.UnloadPrefabContents(root);
            return true;
        }

        /// <summary>
        /// 图鉴放在任务按钮左侧。任务和设置之间塞不下一颗按钮，往右偏会盖住设置。
        /// </summary>
        static void PlaceCodexButton(MainMenuPanel panel)
        {
            var rect = panel.CodexButton.GetComponent<RectTransform>();
            var anchorButton = panel.TaskButton != null ? panel.TaskButton : panel.DailyButton;
            if (anchorButton == null)
                anchorButton = panel.SettingsButton;
            var anchor = anchorButton != null ? anchorButton.GetComponent<RectTransform>() : null;
            if (rect == null || anchor == null || rect == anchor)
                return;

            rect.anchorMin = anchor.anchorMin;
            rect.anchorMax = anchor.anchorMax;
            rect.pivot = anchor.pivot;
            rect.anchoredPosition = anchor.anchoredPosition - new Vector2(anchor.sizeDelta.x + 16f, 0f);
        }

        static void MarkAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
                return;
            var group = settings.FindGroup("core_ui");
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
            AddressableContentBuilder.MarkCodexIcons(settings, group);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }
    }
}
