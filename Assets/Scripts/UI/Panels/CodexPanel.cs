using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 全屏图鉴。左侧玩法页签、右侧分类页签和列表都按 Codex.json 生成。
    /// </summary>
    public class CodexPanel : UIPanel
    {
        public Button CloseButton;
        public RectTransform ModeTabRoot;
        public Button ModeTabTemplate;
        public RectTransform CategoryTabRoot;
        public Button CategoryTabTemplate;
        public ScrollRect List;
        public RectTransform ListContent;
        public RectTransform ListRoot;
        public CodexRowView RowTemplate;
        public Sprite TabNormal;
        public Sprite TabSelected;

        public float RowHeight = 132f;
        public float RowSpacing = 12f;

        readonly List<Button> _modeTabs = new List<Button>();
        readonly List<Button> _categoryTabs = new List<Button>();
        readonly List<CodexRowView> _rows = new List<CodexRowView>();
        readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();

        CodexCatalog.FileData _file;
        CodexCatalog.Entry[] _entries = System.Array.Empty<CodexCatalog.Entry>();
        int _mode;
        int _tab;
        int _first = -1;

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(CloseButton, () => UIManager.Instance.Close(UIPanelId.Codex));
            if (List != null)
            {
                List.onValueChanged.RemoveListener(OnScroll);
                List.onValueChanged.AddListener(OnScroll);
            }
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            _file = CodexCatalog.Load();
            _mode = 0;
            _tab = 0;
            if (payload is string modeId)
                _mode = IndexOfMode(modeId);
            BuildModeTabs();
            ShowMode(_mode);
        }

        public override void OnClose()
        {
            _first = -1;
            base.OnClose();
        }

        void OnScroll(Vector2 _)
        {
            RefreshRows(false);
        }

        int IndexOfMode(string modeId)
        {
            var modes = Modes();
            for (var i = 0; i < modes.Length; i++)
            {
                if (modes[i] != null && modes[i].id == modeId)
                    return i;
            }

            return 0;
        }

        CodexCatalog.Mode[] Modes()
        {
            return _file != null && _file.modes != null
                ? _file.modes
                : System.Array.Empty<CodexCatalog.Mode>();
        }

        void BuildModeTabs()
        {
            var modes = Modes();
            EnsureTabs(_modeTabs, ModeTabTemplate, ModeTabRoot, modes.Length);
            for (var i = 0; i < _modeTabs.Count; i++)
            {
                var button = _modeTabs[i];
                var visible = i < modes.Length && modes[i] != null;
                button.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                SetLabel(button, modes[i].name);
                var index = i;
                Wire(button, () => ShowMode(index));
            }
        }

        void ShowMode(int mode)
        {
            var modes = Modes();
            if (modes.Length == 0)
            {
                _entries = System.Array.Empty<CodexCatalog.Entry>();
                if (CategoryTabRoot != null)
                    CategoryTabRoot.gameObject.SetActive(false);
                RefreshRows(true);
                return;
            }

            _mode = Mathf.Clamp(mode, 0, modes.Length - 1);
            Highlight(_modeTabs, _mode);
            var tabs = modes[_mode] != null ? modes[_mode].tabs : null;
            var count = CountTabs(tabs);
            var showBar = count > 1;
            if (CategoryTabRoot != null)
                CategoryTabRoot.gameObject.SetActive(showBar);
            LayoutList(showBar);
            EnsureTabs(_categoryTabs, CategoryTabTemplate, CategoryTabRoot, count);
            var slot = 0;
            var length = tabs != null ? tabs.Length : 0;
            for (var i = 0; i < length; i++)
            {
                if (!HasEntries(tabs[i]))
                    continue;
                if (slot >= _categoryTabs.Count)
                    break;
                var button = _categoryTabs[slot];
                button.gameObject.SetActive(true);
                SetLabel(button, tabs[i].name);
                var index = i;
                Wire(button, () => ShowTab(index));
                slot++;
            }

            for (var i = slot; i < _categoryTabs.Count; i++)
                _categoryTabs[i].gameObject.SetActive(false);

            if (_tab >= length || !HasEntries(tabs != null && _tab < length ? tabs[_tab] : null))
                _tab = FirstTab(tabs);
            ShowTab(_tab);
        }

        void ShowTab(int tab)
        {
            var modes = Modes();
            var tabs = _mode < modes.Length && modes[_mode] != null ? modes[_mode].tabs : null;
            var length = tabs != null ? tabs.Length : 0;
            _tab = Mathf.Clamp(tab, 0, Mathf.Max(0, length - 1));
            var selectedSlot = 0;
            var slot = 0;
            for (var i = 0; i < length; i++)
            {
                if (!HasEntries(tabs[i]))
                    continue;
                if (i == _tab)
                    selectedSlot = slot;
                slot++;
            }

            Highlight(_categoryTabs, selectedSlot);
            var entries = length > 0 && HasEntries(tabs[_tab]) ? tabs[_tab].entries : null;
            _entries = entries ?? System.Array.Empty<CodexCatalog.Entry>();
            RefreshRows(true);
        }

        void LayoutList(bool showCategories)
        {
            if (ListRoot == null)
                return;
            var top = showCategories ? -168f : -96f;
            ListRoot.offsetMax = new Vector2(ListRoot.offsetMax.x, top);
        }

        void RefreshRows(bool reset)
        {
            if (List == null || ListContent == null || RowTemplate == null)
                return;
            if (reset)
            {
                var origin = ListContent.anchoredPosition;
                origin.y = 0f;
                ListContent.anchoredPosition = origin;
                List.verticalNormalizedPosition = 1f;
                _first = -1;
            }

            var stride = RowHeight + RowSpacing;
            var count = _entries.Length;
            var height = count <= 0 ? 0f : count * stride - RowSpacing;
            var size = ListContent.sizeDelta;
            size.y = height;
            ListContent.sizeDelta = size;

            var view = List.viewport != null ? List.viewport.rect.height : 0f;
            if (view < 1f)
                view = 640f;
            var need = Mathf.CeilToInt(view / stride) + 2;
            if (need < 1)
                need = 1;
            while (_rows.Count < need)
            {
                var row = Instantiate(RowTemplate, ListContent);
                row.gameObject.SetActive(true);
                row.name = "Row" + _rows.Count;
                UIStyle.ApplyTextStyle(row);
                _rows.Add(row);
            }

            var scroll = ListContent.anchoredPosition.y;
            if (scroll < 0f)
                scroll = 0f;
            var first = stride > 0f ? Mathf.FloorToInt(scroll / stride) : 0;
            if (first < 0)
                first = 0;
            if (first > count)
                first = count;
            if (!reset && first == _first)
                return;
            _first = first;

            for (var i = 0; i < _rows.Count; i++)
            {
                var index = first + i;
                var row = _rows[i];
                if (index >= count)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                row.gameObject.SetActive(true);
                var rect = (RectTransform)row.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, RowHeight);
                rect.anchoredPosition = new Vector2(0f, -index * stride);
                var entry = _entries[index];
                row.Bind(entry != null ? entry.name : string.Empty, BodyOf(entry), IconOf(entry));
            }
        }

        Sprite IconOf(CodexCatalog.Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.icon))
                return null;
            if (_icons.TryGetValue(entry.icon, out var sprite))
                return sprite;
            sprite = AssetHub.Load<Sprite>(entry.icon);
            _icons[entry.icon] = sprite;
            return sprite;
        }

        static string BodyOf(CodexCatalog.Entry entry)
        {
            if (entry == null)
                return string.Empty;
            var desc = entry.desc ?? string.Empty;
            var obtain = entry.obtain ?? string.Empty;
            if (obtain.Length == 0)
                return desc;
            if (desc.Length == 0)
                return obtain;
            return desc + "    " + obtain;
        }

        static int CountTabs(CodexCatalog.Tab[] tabs)
        {
            var count = 0;
            if (tabs == null)
                return 0;
            for (var i = 0; i < tabs.Length; i++)
            {
                if (HasEntries(tabs[i]))
                    count++;
            }

            return count;
        }

        static int FirstTab(CodexCatalog.Tab[] tabs)
        {
            if (tabs == null)
                return 0;
            for (var i = 0; i < tabs.Length; i++)
            {
                if (HasEntries(tabs[i]))
                    return i;
            }

            return 0;
        }

        static bool HasEntries(CodexCatalog.Tab tab)
        {
            return tab != null && tab.entries != null && tab.entries.Length > 0;
        }

        void EnsureTabs(List<Button> pool, Button template, RectTransform parent, int count)
        {
            if (template == null || parent == null)
                return;
            template.gameObject.SetActive(false);
            while (pool.Count < count)
            {
                var button = Instantiate(template, parent);
                button.gameObject.SetActive(true);
                pool.Add(button);
            }
        }

        void Highlight(List<Button> tabs, int selected)
        {
            for (var i = 0; i < tabs.Count; i++)
            {
                var image = tabs[i] != null ? tabs[i].targetGraphic as Image : null;
                if (image == null)
                    continue;
                if (i == selected && TabSelected != null)
                    image.sprite = TabSelected;
                else if (TabNormal != null)
                    image.sprite = TabNormal;
                image.color = Color.white;
            }
        }

        static void SetLabel(Button button, string text)
        {
            if (button == null)
                return;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = text ?? string.Empty;
        }
    }
}
