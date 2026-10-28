using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    internal enum Match3Tool
    {
        None = 0,
        Hammer = 1,
        LineH = 2,
        LineV = 3,
        Bomb = 4,
        ColorBomb = 5,
        ColorClear = 6
    }

    /// <summary>
    /// 三消道具：选中、锤子、赋特效、清一色、加步、重排和提示。
    /// 点在棋子上仍由界面收下，再转给这里。
    /// </summary>
    public sealed class Match3Tools
    {
        private static readonly Color ArmedIconColor = new Color(1f, 0.9f, 0.45f);

        private readonly Match3GameView _view;
        private Match3Tool _armed;

        public Match3Tools(Match3GameView view)
        {
            _view = view;
        }

        internal Match3Tool Armed => _armed;

        internal void Arm(Match3Tool tool, ShopItemId id, string hint)
        {
            if (_view._busy || _view._ended || _view.InputLocked)
                return;
            if (CoinWallet.Count(id) <= 0)
            {
                UIManager.Instance?.ShowToast("道具次数用完了，去商城看看");
                return;
            }

            if (_armed == tool)
            {
                SetArmed(Match3Tool.None);
                UIManager.Instance?.ShowToast("已取消");
                return;
            }

            SetArmed(tool);
            UIManager.Instance?.ShowToast(hint);
        }

        public void UseArmed(Match3TileView view)
        {
            switch (_armed)
            {
                case Match3Tool.Hammer:
                    UseHammer(view);
                    break;
                case Match3Tool.LineH:
                    UsePaint(view, Match3Special.LineH, ShopItemId.Match3LineH);
                    break;
                case Match3Tool.LineV:
                    UsePaint(view, Match3Special.LineV, ShopItemId.Match3LineV);
                    break;
                case Match3Tool.Bomb:
                    UsePaint(view, Match3Special.Bomb, ShopItemId.Match3Bomb);
                    break;
                case Match3Tool.ColorBomb:
                    UsePaint(view, Match3Special.ColorBomb, ShopItemId.Match3ColorBomb);
                    break;
                case Match3Tool.ColorClear:
                    UseColorClear(view);
                    break;
            }
        }

        public void AddMoves()
        {
            if (_view._busy || _view._ended || _view.InputLocked || _view._engine == null)
                return;
            if (!CoinWallet.TryUse(ShopItemId.Match3Moves))
            {
                UIManager.Instance?.ShowToast("加步次数用完了，去商城看看");
                return;
            }

            SetArmed(Match3Tool.None);
            _view._engine.AddMoves(ShopCatalog.Match3ExtraMoves);
            _view.RefreshHud();
            RefreshCounts();
            UIManager.Instance?.ShowToast($"步数 +{ShopCatalog.Match3ExtraMoves}");
        }

        public void ShowHint()
        {
            if (_view._busy || _view._ended || _view.InputLocked || _view._engine == null)
                return;
            SetArmed(Match3Tool.None);
            if (CoinWallet.Count(ShopItemId.Match3Hint) <= 0)
            {
                UIManager.Instance?.ShowToast("提示次数用完了，去商城看看");
                return;
            }

            if (!_view._engine.TryGetHint(out var a, out var b))
            {
                UIManager.Instance?.ShowToast("当前没有可消除的交换");
                return;
            }

            CoinWallet.TryUse(ShopItemId.Match3Hint);
            RefreshCounts();
            foreach (var tile in _view._views.Values)
            {
                var hit = tile.X == a.X && tile.Y == a.Y || tile.X == b.X && tile.Y == b.Y;
                tile.SetSelected(hit);
            }
        }

        public void RequestShuffle()
        {
            if (_view._busy || _view._ended || _view.InputLocked || _view._engine == null)
                return;
            SetArmed(Match3Tool.None);
            if (!CoinWallet.TryUse(ShopItemId.Match3Shuffle))
            {
                UIManager.Instance?.ShowToast("重排次数用完了，去商城看看");
                return;
            }

            RefreshCounts();
            _view.StartPlay(_view._engine.Shuffle());
        }

        public void RefreshCounts()
        {
            SetToolButton(_view.HintButton, ShopItemId.Match3Hint, Match3Tool.None);
            SetToolButton(_view.ShuffleButton, ShopItemId.Match3Shuffle, Match3Tool.None);
            SetToolButton(_view.MovesButton, ShopItemId.Match3Moves, Match3Tool.None);
            SetToolButton(_view.HammerButton, ShopItemId.Match3Hammer, Match3Tool.Hammer);
            SetToolButton(_view.LineHButton, ShopItemId.Match3LineH, Match3Tool.LineH);
            SetToolButton(_view.LineVButton, ShopItemId.Match3LineV, Match3Tool.LineV);
            SetToolButton(_view.BombButton, ShopItemId.Match3Bomb, Match3Tool.Bomb);
            SetToolButton(_view.ColorBombButton, ShopItemId.Match3ColorBomb, Match3Tool.ColorBomb);
            SetToolButton(_view.ColorClearButton, ShopItemId.Match3ColorClear, Match3Tool.ColorClear);
        }

        private void SetArmed(Match3Tool tool)
        {
            _armed = tool;
            RefreshCounts();
        }

        private void UseHammer(Match3TileView view)
        {
            if (CoinWallet.Count(ShopItemId.Match3Hammer) <= 0)
            {
                UIManager.Instance?.ShowToast("道具次数用完了，去商城看看");
                SetArmed(Match3Tool.None);
                return;
            }

            var result = _view._engine.TryRemoveAt(view.X, view.Y);
            if (!result.Applied)
            {
                UIManager.Instance?.ShowToast("这里不能消除");
                return;
            }

            CoinWallet.TryUse(ShopItemId.Match3Hammer);
            SetArmed(Match3Tool.None);
            _view.StartPlay(result);
        }

        private void UsePaint(Match3TileView view, Match3Special special, ShopItemId id)
        {
            if (CoinWallet.Count(id) <= 0)
            {
                UIManager.Instance?.ShowToast("道具次数用完了，去商城看看");
                SetArmed(Match3Tool.None);
                return;
            }

            var tile = _view._engine.GetTile(view.X, view.Y);
            if (tile == null)
            {
                UIManager.Instance?.ShowToast("这里没有棋子");
                return;
            }

            if (!_view._engine.CanSwapAt(view.X, view.Y))
            {
                UIManager.Instance?.ShowToast("先解开这颗棋子");
                return;
            }

            if (tile.Special == Match3Special.ColorBomb)
            {
                UIManager.Instance?.ShowToast("彩球不能再赋特效");
                return;
            }

            var result = _view._engine.TryPaintSpecial(view.X, view.Y, special);
            if (!result.Applied)
            {
                UIManager.Instance?.ShowToast("这颗棋子不能赋特效");
                return;
            }

            CoinWallet.TryUse(id);
            SetArmed(Match3Tool.None);
            _view.StartPlay(result);
        }

        private void UseColorClear(Match3TileView view)
        {
            if (CoinWallet.Count(ShopItemId.Match3ColorClear) <= 0)
            {
                UIManager.Instance?.ShowToast("道具次数用完了，去商城看看");
                SetArmed(Match3Tool.None);
                return;
            }

            var tile = _view._engine.GetTile(view.X, view.Y);
            if (tile == null || tile.Color <= 0)
            {
                UIManager.Instance?.ShowToast("这颗棋子没有颜色");
                return;
            }

            var result = _view._engine.TryClearColorAt(view.X, view.Y);
            if (!result.Applied)
            {
                UIManager.Instance?.ShowToast("这里不能消除");
                return;
            }

            CoinWallet.TryUse(ShopItemId.Match3ColorClear);
            SetArmed(Match3Tool.None);
            _view.StartPlay(result);
        }

        private void SetToolButton(Button button, ShopItemId id, Match3Tool tool)
        {
            if (button == null)
                return;
            var countNode = button.transform.Find("Label");
            var count = countNode != null ? countNode.GetComponent<Text>() : null;
            if (count != null)
            {
                count.text = CoinWallet.Count(id).ToString();
                if (CoinWallet.ConsumePendingGrant(id))
                {
                    UITween.KillTarget(countNode);
                    UITween.Pop(countNode, 1f, 1.4f, 0.28f);
                }
            }
            var iconNode = button.transform.Find("Icon");
            var icon = iconNode != null ? iconNode.GetComponent<Image>() : null;
            if (icon != null)
                icon.color = tool != Match3Tool.None && _armed == tool ? ArmedIconColor : Color.white;
        }
    }
}
