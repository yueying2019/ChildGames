using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class ShopPanel : UIPanel
    {
        public Button DimButton;
        public Button CloseButton;
        public Text CoinText;
        public Button TabLianButton;
        public Button TabSheepButton;
        public Button TabMatch3Button;
        public Button TabStarButton;
        public ShopItemView ItemTemplate;
        public RectTransform ItemContent;
        public ShopItemView Item0;
        public ShopItemView Item1;
        public ShopItemView Item2;

        /// <summary>道具图标表。由菜单「SmallGames/绑定商店道具图标」按 Assets/UI/&lt;玩法&gt;/&lt;ShopItemId&gt;.png 自动填。</summary>
        public ShopIconEntry[] Icons;

        /// <summary>一个道具对应一张图标；Id 唯一，Icon 允许为空（缺图时不显示）。</summary>
        [System.Serializable]
        public struct ShopIconEntry
        {
            public ShopItemId Id;
            public Sprite Icon;
        }

        private int _page;
        private bool _starMode;
        private Text _titleText;
        private readonly List<ShopItemView> _rows = new List<ShopItemView>();

        public override void BindListeners()
        {
            base.BindListeners();
            Wire(DimButton, () => UIManager.Instance.Close(UIPanelId.Shop));
            Wire(CloseButton, () => UIManager.Instance.Close(UIPanelId.Shop));
            Wire(TabLianButton, () => ShowPage(0));
            Wire(TabSheepButton, () => ShowPage(1));
            Wire(TabMatch3Button, () => ShowPage(2));
            Wire(TabStarButton, ShowStars);
        }

        public override void OnOpen(object payload)
        {
            base.OnOpen(payload);
            CoinWallet.Changed += Refresh;
            var page = payload is GameModeType mode && mode != GameModeType.None
                ? ShopCatalog.PageOf(mode)
                : _page;
            ShowPage(page);
            Analytics.ShopOpen(payload is GameModeType openMode ? openMode : GameModeType.None);
        }

        public override void OnClose()
        {
            CoinWallet.Changed -= Refresh;
            base.OnClose();
        }

        private void OnDestroy()
        {
            CoinWallet.Changed -= Refresh;
        }

        private void ShowPage(int page)
        {
            _page = Mathf.Clamp(page, 0, ShopCatalog.PageCount - 1);
            _starMode = false;
            EnsureItemList();
            RebuildRows();
            HighlightTab(TabLianButton, _page == 0);
            HighlightTab(TabSheepButton, _page == 1);
            HighlightTab(TabMatch3Button, _page == 2);
            HighlightTab(TabStarButton, false);
            SetTitle("道具商城");
            RefreshCoins();
        }

        private void Refresh()
        {
            RebuildRows();
            if (_starMode)
                ShowStars();
            else
                RefreshCoins();
        }

        private void RefreshCoins()
        {
            if (CoinText != null)
                CoinText.text = $"金币 {CoinWallet.Coins}";
        }

        private ShopItemView Template => ItemTemplate != null ? ItemTemplate : Item0;

        private void EnsureItemList()
        {
            if (ItemTemplate != null && ItemContent != null)
                return;
            var template = Template;
            if (template == null)
                return;

            var window = template.transform.parent as RectTransform;
            if (window == null)
                return;

            var scroll = UIViewFactory.CreateRect("ItemScroll", window);
            UIViewFactory.StretchFull(scroll);
            scroll.offsetMin = new Vector2(40f, 28f);
            scroll.offsetMax = new Vector2(-40f, -196f);
            var scrollImg = scroll.gameObject.AddComponent<Image>();
            scrollImg.sprite = UIStyle.WhiteSprite;
            scrollImg.color = Color.clear;
            scrollImg.raycastTarget = true;
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 40f;

            var viewport = UIViewFactory.CreateRect("Viewport", scroll);
            UIViewFactory.StretchFull(viewport);
            var viewportImg = viewport.gameObject.AddComponent<Image>();
            viewportImg.sprite = UIStyle.WhiteSprite;
            viewportImg.color = Color.white;
            viewportImg.raycastTarget = true;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = UIViewFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 4, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            template.transform.SetParent(content, false);
            var row = template.GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(0f, 130f);
            if (template.GetComponent<LayoutElement>() == null)
            {
                var itemLayout = template.gameObject.AddComponent<LayoutElement>();
                itemLayout.minHeight = 130f;
                itemLayout.preferredHeight = 130f;
                itemLayout.flexibleWidth = 1f;
            }

            ItemTemplate = template;
            ItemContent = content;
        }

        private RectTransform ContentRoot
        {
            get
            {
                if (ItemContent != null)
                    return ItemContent;
                var template = Template;
                return template != null ? template.transform.parent as RectTransform : null;
            }
        }

        private void RebuildRows()
        {
            var template = Template;
            var content = ContentRoot;
            if (template == null || content == null)
            {
                BindFallback();
                return;
            }

            HideLegacySlot(Item0, template);
            HideLegacySlot(Item1, template);
            HideLegacySlot(Item2, template);
            template.gameObject.SetActive(false);

            if (_starMode)
            {
                RebuildStarRows(template, content);
                return;
            }

            var count = ShopCatalog.CountOnPage(_page);
            while (_rows.Count < count)
            {
                var row = Instantiate(template, content);
                row.gameObject.SetActive(true);
                WireBuy(row);
                _rows.Add(row);
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (i >= count)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                row.gameObject.SetActive(true);
                var info = ShopCatalog.GetOnPage(_page, i);
                row.Bind(info, IconOf(info.Id));
            }
        }

        private void BindFallback()
        {
            BindItem(Item0, 0);
            BindItem(Item1, 1);
            BindItem(Item2, 2);
        }

        private static void HideLegacySlot(ShopItemView view, ShopItemView template)
        {
            if (view == null || view == template)
                return;
            view.gameObject.SetActive(false);
        }

        private void BindItem(ShopItemView view, int index)
        {
            if (view == null)
                return;
            if (index >= ShopCatalog.CountOnPage(_page))
            {
                view.Bind(default, null);
                return;
            }

            var info = ShopCatalog.GetOnPage(_page, index);
            view.Bind(info, IconOf(info.Id));
        }

        /// <summary>按道具取图标，表里没有就返回 null（行内图标会留空）。</summary>
        private Sprite IconOf(ShopItemId id)
        {
            if (Icons == null)
                return null;
            for (var i = 0; i < Icons.Length; i++)
            {
                if (Icons[i].Id == id)
                    return Icons[i].Icon;
            }

            return null;
        }

        private void WireBuy(ShopItemView view)
        {
            if (view == null || view.BuyButton == null)
                return;
            view.BuyButton.onClick.RemoveAllListeners();
            view.BuyButton.onClick.AddListener(() => Buy(view));
        }

        private void RebuildStarRows(ShopItemView template, RectTransform content)
        {
            var count = StarRowCount();
            while (_rows.Count < count)
            {
                var row = Instantiate(template, content);
                row.gameObject.SetActive(true);
                WireBuy(row);
                _rows.Add(row);
            }

            var index = 0;
            index = FillStarMode(GameModeType.Lianliankan, index);
            index = FillStarMode(GameModeType.Sheep, index);
            index = FillStarMode(GameModeType.Match3, index);
            for (var i = index; i < _rows.Count; i++)
                _rows[i].gameObject.SetActive(false);
        }

        private int FillStarMode(GameModeType mode, int index)
        {
            var row = RowAt(index);
            index++;
            if (row != null)
            {
                var info = GameModeCatalog.Get(mode);
                row.BindSection($"{info.Title}  可兑 {ProgressRepository.AvailableStars(mode)} 星");
            }

            var page = ShopCatalog.PageOf(mode);
            var count = ShopCatalog.CountOnPage(page);
            for (var i = 0; i < count; i++)
            {
                var itemRow = RowAt(index);
                index++;
                if (itemRow == null)
                    continue;
                var item = ShopCatalog.GetOnPage(page, i);
                var cost = ShopCatalog.StarCost(item);
                itemRow.BindStar(
                    item,
                    IconOf(item.Id),
                    cost,
                    CoinWallet.Count(item.Id),
                    ProgressRepository.AvailableStars(mode) >= cost);
            }

            return index;
        }

        private ShopItemView RowAt(int index)
        {
            if (index < 0 || index >= _rows.Count)
                return null;
            var row = _rows[index];
            row.gameObject.SetActive(true);
            return row;
        }

        private static int StarRowCount()
        {
            return 3 + ShopCatalog.All.Length;
        }

        private void ShowStars()
        {
            _starMode = true;
            EnsureItemList();
            RebuildRows();
            HighlightTab(TabLianButton, false);
            HighlightTab(TabSheepButton, false);
            HighlightTab(TabMatch3Button, false);
            HighlightTab(TabStarButton, true);
            SetTitle("星星兑换");
        }

        private void SetTitle(string text)
        {
            if (_titleText == null)
            {
                var node = transform.Find("Window/Title");
                if (node != null)
                    _titleText = node.GetComponent<Text>();
            }

            if (_titleText != null)
                _titleText.text = text;
        }

        private void Buy(ShopItemView view)
        {
            if (view == null || view.ItemId == ShopItemId.None)
                return;
            if (!ShopCatalog.TryGet(view.ItemId, out var info))
                return;
            if (_starMode)
            {
                var cost = ShopCatalog.StarCost(info);
                if (!ProgressRepository.TrySpendStars(info.Mode, cost))
                {
                    UIManager.Instance?.ShowToast("这个模式的星星不够");
                    return;
                }

                CoinWallet.GrantItem(info.Id, 1);
                UIManager.Instance?.ShowToast($"兑换了 {info.Title}");
                Refresh();
                return;
            }

            if (CoinWallet.TryBuy(view.ItemId))
            {
                UIManager.Instance?.ShowToast($"已购买 {info.Title}");
                return;
            }

            UIManager.Instance?.ShowToast("金币不够，过关可获得金币");
        }

        private static void HighlightTab(Button button, bool on)
        {
            if (button == null)
                return;
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = on ? UIStyle.TabSelect : UIStyle.None;
        }
    }
}
