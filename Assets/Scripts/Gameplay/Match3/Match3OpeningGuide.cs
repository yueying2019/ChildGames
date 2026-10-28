using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 三消开场讲解和教学关强制引导。棋盘点击和倒计时仍在界面上。
    /// </summary>
    public sealed class Match3OpeningGuide
    {
        private readonly Match3GameView _view;

        public Match3OpeningGuide(Match3GameView view)
        {
            _view = view;
        }

        /// <summary>进关名牌关掉之后由界面启动。教学关不走这条。</summary>
        public IEnumerator PlayOpening()
        {
            while (UIManager.Instance != null && UIManager.Instance.IsOpen(UIPanelId.ModeRules))
                yield return null;

            var keys = CollectOpeningGuides();
            if (keys.Count == 0)
                yield break;

            _view._busy = true;
            _view.InputLocked = true;
            for (var i = 0; i < keys.Count; i++)
                yield return ShowIfNeeded(keys[i]);

            if (_view._ended)
                yield break;
            _view._busy = false;
            _view.InputLocked = false;
        }

        /// <summary>教学关强制引导。棋盘建好之后调用，才能拿到牌面的 RectTransform。</summary>
        public void StartForced()
        {
            TutorialDirector.Start(GameModeType.Match3, ResolveTargets, ResolveGraphics);
        }

        /// <summary>消除过程中首次出现特殊棋子时，由播放器转来。</summary>
        public IEnumerator ShowIfNeeded(string key)
        {
            yield return GuideDirector.Play(GameModeType.Match3, key, GuideIcon(key), ResolveGuideTarget);
        }

        private List<string> CollectOpeningGuides()
        {
            var keys = new List<string>();
            if (_view._engine == null)
                return keys;

            TryAddOpening(keys, Match3CellKind.Ice);
            TryAddOpening(keys, Match3CellKind.Stone);
            TryAddOpening(keys, Match3CellKind.Jelly);
            TryAddOpening(keys, Match3CellKind.Cage);
            TryAddOpening(keys, Match3Drop.ExtraMoves);
            TryAddOpening(keys, Match3Drop.GoldBean);
            return keys;
        }

        private void TryAddOpening(List<string> keys, Match3CellKind kind)
        {
            if (!Match3GuideCatalog.TryFromOverlay(kind, out var id))
                return;
            var key = GuideCatalog.KeyFor(id);
            if (GuideRepository.HasSeen(GameModeType.Match3, key) || keys.Contains(key))
                return;
            if (!BoardHasOverlay(kind))
                return;
            keys.Add(key);
        }

        private void TryAddOpening(List<string> keys, Match3Drop drop)
        {
            if (!Match3GuideCatalog.TryFromDrop(drop, out var id))
                return;
            var key = GuideCatalog.KeyFor(id);
            if (GuideRepository.HasSeen(GameModeType.Match3, key) || keys.Contains(key))
                return;
            if (!BoardHasDrop(drop))
                return;
            keys.Add(key);
        }

        private bool BoardHasOverlay(Match3CellKind kind)
        {
            var engine = _view._engine;
            for (var y = 0; y < engine.Height; y++)
            {
                for (var x = 0; x < engine.Width; x++)
                {
                    if (engine.GetKind(x, y) == kind && engine.GetLayers(x, y) > 0)
                        return true;
                }
            }

            return false;
        }

        private bool BoardHasDrop(Match3Drop drop)
        {
            var engine = _view._engine;
            for (var y = 0; y < engine.Height; y++)
            {
                for (var x = 0; x < engine.Width; x++)
                {
                    var tile = engine.GetTile(x, y);
                    if (tile != null && tile.Drop == drop)
                        return true;
                }
            }

            return false;
        }

        private Sprite GuideIcon(string key)
        {
            return Match3GuideCatalog.TryFromKey(key, out var id)
                ? Match3GuideCatalog.Icon(id, _view.IconSprites)
                : null;
        }

        private RectTransform ResolveGuideTarget(string targetId)
        {
            switch (targetId)
            {
                case GuideTargets.Moves:
                    return _view.MovesText != null ? _view.MovesText.rectTransform : _view.BoardRoot;
                case GuideTargets.Goal:
                    return _view.GoalText != null ? _view.GoalText.rectTransform : _view.BoardRoot;
                case GuideTargets.Tools:
                    return _view.HintButton != null ? _view.HintButton.transform as RectTransform : _view.BoardRoot;
                case GuideTargets.Pause:
                    return _view.PauseButton != null ? _view.PauseButton.transform as RectTransform : _view.BoardRoot;
                default:
                    return _view.BoardRoot;
            }
        }

        private RectTransform[] ResolveTargets(int[] cells)
        {
            var list = new List<RectTransform>(4);
            for (var i = 0; i + 1 < cells.Length; i += 2)
            {
                var view = _view.ViewAt(cells[i], cells[i + 1]);
                if (view != null)
                    list.Add(view.transform as RectTransform);
            }

            return list.Count > 0 ? list.ToArray() : null;
        }

        private Graphic[] ResolveGraphics(int[] cells)
        {
            var list = new List<Graphic>(4);
            for (var i = 0; i + 1 < cells.Length; i += 2)
            {
                var view = _view.ViewAt(cells[i], cells[i + 1]);
                if (view != null && view.Icon != null)
                    list.Add(view.Icon);
            }

            return list.Count > 0 ? list.ToArray() : null;
        }
    }
}
