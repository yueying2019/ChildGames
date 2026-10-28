using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    public class ShopItemView : MonoBehaviour
    {
        /// <summary>Item 预制体实例里的 Icon 图片。没拖引用时按名字自动找（见 FindIconImage）。</summary>
        public Image IconImage;
        public Text DescText;
        public Text PriceText;
        public Text OwnedText;
        public Button BuyButton;

        public ShopItemId ItemId { get; private set; }

        private void Awake()
        {
            if (IconImage == null)
                IconImage = FindIconImage();
        }

        public void Bind(ShopItemInfo info, Sprite icon)
        {
            ItemId = info.Id;
            gameObject.SetActive(info.Id != ShopItemId.None);
            if (info.Id == ShopItemId.None)
                return;

            if (IconImage != null)
            {
                IconImage.enabled = true;
                IconImage.sprite = icon;
            }
            if (DescText != null)
                DescText.text = info.Desc;
            if (PriceText != null)
                PriceText.text = info.Price + " 金币";
            SetBuyVisible(true);
            SetBuyLabel("购买");
            if (BuyButton != null)
                BuyButton.interactable = true;
            RefreshOwned();
        }

        /// <summary>星星兑换的一行：价格是星星，按钮是兑换。</summary>
        public void BindStar(ShopItemInfo info, Sprite icon, int starCost, int owned, bool canBuy)
        {
            ItemId = info.Id;
            gameObject.SetActive(info.Id != ShopItemId.None);
            if (info.Id == ShopItemId.None)
                return;

            if (IconImage != null)
            {
                IconImage.enabled = icon != null;
                IconImage.sprite = icon;
            }
            if (DescText != null)
                DescText.text = info.Desc;
            if (PriceText != null)
                PriceText.text = starCost + " 星";
            if (OwnedText != null)
                OwnedText.text = "已有 " + owned;
            SetBuyVisible(true);
            SetBuyLabel("兑换");
            if (BuyButton != null)
                BuyButton.interactable = canBuy;
        }

        /// <summary>一种模式的分组标题，写出这个模式还能兑多少星。</summary>
        public void BindSection(string title)
        {
            ItemId = ShopItemId.None;
            gameObject.SetActive(true);
            if (IconImage != null)
                IconImage.enabled = false;
            if (DescText != null)
                DescText.text = title;
            if (PriceText != null)
                PriceText.text = string.Empty;
            if (OwnedText != null)
                OwnedText.text = string.Empty;
            SetBuyVisible(false);
        }

        private void SetBuyVisible(bool visible)
        {
            if (BuyButton != null)
                BuyButton.gameObject.SetActive(visible);
        }

        private void SetBuyLabel(string text)
        {
            if (BuyButton == null)
                return;
            var label = BuyButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = text;
        }

        public void RefreshOwned()
        {
            if (OwnedText != null)
                OwnedText.text = ItemId == ShopItemId.None ? string.Empty : $"已有 {CoinWallet.Count(ItemId)}";
        }

        /// <summary>兜底：模板里没拖 IconImage 时，找 Item 实例下名为 Icon 的图片节点。</summary>
        private Image FindIconImage()
        {
            var images = GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].gameObject.name == "Icon")
                    return images[i];
            }

            return null;
        }
    }
}
