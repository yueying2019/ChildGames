using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 按引擎事件播放三消动画。胜负只转回界面上的事件，不改规则。
    /// </summary>
    public sealed class Match3Playback
    {
        private readonly Match3GameView _view;

        public Match3Playback(Match3GameView view)
        {
            _view = view;
        }

        public IEnumerator Play(Match3ResolveResult result)
        {
            _view._busy = true;
            HintFlash.Stop();
            if (HasClear(result))
            {
                _view.ResetIdle();
                TutorialDirector.NotifyCurrent();
                if (!result.Won && _view._mascot != null)
                    _view._mascot.PlayEat();
            }

            _view._poppedIds.Clear();
            yield return PlayEventList(result.Events, false);
            _view.SyncFromEngine();
            _view.RefreshHud();
            _view.ApplyBonuses(result);

            if (_view._ended)
            {
                _view._busy = false;
                _view._playRoutine = null;
                yield break;
            }

            if (result.WaveCleared)
            {
                _view._audio?.PlayWin();
                _view.RaiseWaveCleared();
            }
            else if (result.Won)
            {
                _view._ended = true;
                yield return PlayLeftoverBonus();
                _view._audio?.PlayWin();
                _view.RaiseWon();
            }
            else if (result.Lost)
            {
                _view._ended = true;
                _view._audio?.PlayLose();
                _view.RaiseLost();
            }

            _view._busy = false;
            _view._playRoutine = null;
            if (!_view.Tutorial && !_view._ended && !result.Won && !result.Lost && !result.WaveCleared
                && HasClear(result))
                _view.TryChargeAfterClear();
        }

        private IEnumerator PlayLeftoverBonus()
        {
            if (_view._engine == null || _view._engine.Endless || _view._engine.MovesLeft <= 0)
                yield break;

            _view._animSpeed = 3f;
            try
            {
                var convert = _view._engine.ConvertLeftoverMoves();
                for (var i = 0; i < convert.Events.Count; i++)
                {
                    var e = convert.Events[i];
                    if (e.Kind != Match3EventKind.SpawnSpecial)
                        continue;
                    yield return AnimateLeftoverConvert(e);
                    _view._engine.SetMovesLeft(_view._engine.MovesLeft - 1);
                    _view.RefreshHud();
                }

                _view._engine.SetMovesLeft(0);
                _view.RefreshHud();
                var boom = _view._engine.DetonateLeftoverSpecials();
                if (boom.Events.Count == 0)
                    yield break;

                _view._poppedIds.Clear();
                yield return PlayEventList(boom.Events, true);
                _view.SyncFromEngine();
                _view.RefreshHud();
                _view.ApplyBonuses(boom);
            }
            finally
            {
                _view._animSpeed = 1f;
            }
        }

        private IEnumerator PlayEventList(List<Match3BoardEvent> events, bool skipGuides)
        {
            if (events == null || events.Count == 0)
                yield break;

            var i = 0;
            var wave = 0;
            while (i < events.Count)
            {
                var kind = events[i].Kind;
                if (kind == Match3EventKind.Blast)
                {
                    var blast = events[i++];
                    _view._audio?.PlaySpecial(blast.Special);
                    yield return AnimateBlast(blast);
                    continue;
                }

                if (kind == Match3EventKind.Remove)
                {
                    var batch = Collect(events, ref i, Match3EventKind.Remove);
                    wave++;
                    var label = ComboCatalog.Match3Label(wave);
                    if (!string.IsNullOrEmpty(label))
                        _view._combo?.ShowCombo(wave, label);
                    PlayRemoveSfx(batch);
                    yield return AnimateRemoves(batch);
                    continue;
                }

                if (kind == Match3EventKind.Move || kind == Match3EventKind.Spawn)
                {
                    var start = i;
                    while (i < events.Count && (events[i].Kind == Match3EventKind.Move || events[i].Kind == Match3EventKind.Spawn))
                        i++;
                    _view._audio?.PlayFall();
                    yield return AnimateMoves(events, start, i);
                    continue;
                }

                if (kind == Match3EventKind.CellHit)
                {
                    var hits = Collect(events, ref i, Match3EventKind.CellHit);
                    PlayOverlaySfx(hits);
                    for (var h = 0; h < hits.Count; h++)
                        _view.ApplyOverlay(hits[h].FromX, hits[h].FromY);
                    continue;
                }

                var e = events[i++];
                if (e.Kind == Match3EventKind.Swap || e.Kind == Match3EventKind.SwapRevert)
                {
                    _view._audio?.PlaySwap();
                    yield return AnimateSwap(e);
                }
                else if (e.Kind == Match3EventKind.SpawnSpecial)
                {
                    AnimateSpawnSpecial(e);
                    if (!skipGuides && Match3GuideCatalog.TryFromSpecial(e.Special, out var guideId))
                        yield return _view.Guides.ShowIfNeeded(GuideCatalog.KeyFor(guideId));
                }
                else if (e.Kind == Match3EventKind.Shuffle)
                {
                    _view.RebuildTiles();
                    if (e.ScoreDelta > 0)
                        UIManager.Instance?.ShowToast("没有可消步，已自动重排");
                }
            }
        }

        private IEnumerator AnimateLeftoverConvert(Match3BoardEvent e)
        {
            _view._audio?.PlayMatch();
            yield return FlyHudToCell(e);
            AnimateSpawnSpecial(e);
            yield return PunchTile(e.Id);
        }

        private IEnumerator FlyHudToCell(Match3BoardEvent e)
        {
            if (_view.MovesText == null || _view.BoardRoot == null)
                yield break;

            var sprite = LeftoverFlySprite(e.Special);
            var start = _view.MovesText.rectTransform.position;
            var end = _view.BoardRoot.TransformPoint(_view.CellPos(e.ToX, e.ToY));
            var rect = UIViewFactory.CreateRect("LeftoverFly", _view.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(56f, 56f);
            rect.position = start;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : UIStyle.WhiteSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            const float duration = 0.28f;
            var flying = UITween.Custom(rect, _view.Dur(duration), u =>
            {
                if (rect == null)
                    return;
                rect.position = Vector3.LerpUnclamped(start, end, u);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.45f, 1f, u);
            }, EaseKind.InOutQuad);

            if (flying != null)
                yield return flying.WaitForCompletion();

            DestroyFx(rect);
        }

        private Sprite LeftoverFlySprite(Match3Special special)
        {
            switch (special)
            {
                case Match3Special.LineH:
                    return ArrowSprite(true);
                case Match3Special.LineV:
                    return ArrowSpriteUp();
                case Match3Special.Bomb:
                    return Match3Icons.FromFile(_view.IconSprites, Match3Icons.BombFile);
                default:
                    return Match3Icons.FromFile(_view.IconSprites, Match3Icons.BombFile);
            }
        }

        private IEnumerator PunchTile(int id)
        {
            if (!_view._views.TryGetValue(id, out var view) || view == null)
                yield break;

            var rect = view.GetComponent<RectTransform>();
            if (rect == null)
                yield break;

            UITween.KillTarget(view);
            const float duration = 0.16f;
            var punch = UITween.Custom(view, _view.Dur(duration), u =>
            {
                if (rect != null)
                    rect.localScale = Vector3.one * Overshoot(u);
            }, EaseKind.Linear);

            if (punch != null)
                yield return punch.WaitForCompletion();
        }

        private static float Overshoot(float u)
        {
            const float peak = 1.22f;
            const float rise = 0.45f;
            return u < rise
                ? Mathf.LerpUnclamped(1f, peak, u / rise)
                : Mathf.LerpUnclamped(peak, 1f, (u - rise) / (1f - rise));
        }

        private void PlayRemoveSfx(List<Match3BoardEvent> batch)
        {
            if (_view._audio == null || batch == null || batch.Count == 0)
                return;
            _view._audio.PlayMatch();
        }

        private void PlayOverlaySfx(List<Match3BoardEvent> hits)
        {
            if (_view._audio == null || hits == null)
                return;
            var ice = false;
            var stone = false;
            var jelly = false;
            var cage = false;
            for (var i = 0; i < hits.Count; i++)
            {
                var kind = (Match3CellKind)hits[i].Color;
                if (kind == Match3CellKind.Ice)
                    ice = true;
                else if (kind == Match3CellKind.Stone)
                    stone = true;
                else if (kind == Match3CellKind.Jelly)
                    jelly = true;
                else if (kind == Match3CellKind.Cage)
                    cage = true;
            }

            if (ice)
                _view._audio.PlayOverlay(Match3CellKind.Ice);
            if (stone)
                _view._audio.PlayOverlay(Match3CellKind.Stone);
            if (jelly)
                _view._audio.PlayOverlay(Match3CellKind.Jelly);
            if (cage)
                _view._audio.PlayOverlay(Match3CellKind.Cage);
        }

        private static List<Match3BoardEvent> Collect(List<Match3BoardEvent> events, ref int i, Match3EventKind kind)
        {
            var batch = new List<Match3BoardEvent>();
            while (i < events.Count && events[i].Kind == kind)
                batch.Add(events[i++]);
            return batch;
        }

        private IEnumerator AnimateSwap(Match3BoardEvent e)
        {
            _view._views.TryGetValue(e.Id, out var a);
            _view._views.TryGetValue(e.Color, out var b);
            if (a == null || b == null)
                yield break;

            var aRect = a.GetComponent<RectTransform>();
            var bRect = b.GetComponent<RectTransform>();
            var aPos = aRect.anchoredPosition;
            var bPos = bRect.anchoredPosition;
            yield return LerpPair(aRect, bPos, bRect, aPos, 0.14f);
            if (e.Kind == Match3EventKind.SwapRevert)
            {
                yield return LerpPair(aRect, aPos, bRect, bPos, 0.12f);
                yield break;
            }

            a.SetCell(e.ToX, e.ToY);
            b.SetCell(e.FromX, e.FromY);
        }

        private IEnumerator AnimateRemoves(List<Match3BoardEvent> batch)
        {
            _view.StartCoroutine(ShakeBoard(0.12f, 7f));
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                SpawnFlash(_view.CellPos(e.FromX, e.FromY));
                if (e.Drop == Match3Drop.ExtraMoves || e.Drop == Match3Drop.GoldBean)
                    _view.StartCoroutine(FlyDropToHud(e.Drop, _view.CellPos(e.FromX, e.FromY)));
                if (!_view._views.TryGetValue(e.Id, out var view) || view == null)
                    continue;
                if (e.Drop == Match3Drop.None && _view._mascot != null)
                    _view._mascot.Feed(view.Icon);
            }

            for (var i = 0; i < batch.Count; i++)
            {
                if (!_view._views.TryGetValue(batch[i].Id, out var view))
                    continue;
                _view._views.Remove(batch[i].Id);
                _view._poppedIds.Remove(batch[i].Id);
                _view.RecycleTile(view);
            }

            yield break;
        }

        private IEnumerator AnimateBlast(Match3BoardEvent e)
        {
            _view.StartCoroutine(ShakeBoard(0.2f, e.Special == Match3Special.Bomb || e.Special == Match3Special.ColorBomb ? 14f : 9f));
            switch (e.Special)
            {
                case Match3Special.LineH:
                case Match3Special.LineV:
                    yield return AnimateLineBlast(e);
                    break;
                case Match3Special.Bomb:
                    yield return AnimateBombBlast(e);
                    break;
                case Match3Special.ColorBomb:
                    yield return AnimateRainbowBlast(e);
                    break;
            }
        }

        private IEnumerator AnimateLineBlast(Match3BoardEvent e)
        {
            var x = e.FromX;
            var y = e.FromY;
            var horizontal = e.Special == Match3Special.LineH;
            var width = _view._engine != null ? _view._engine.Width : 8;
            var height = _view._engine != null ? _view._engine.Height : 8;
            var max = horizontal ? Mathf.Max(x, width - 1 - x) : Mathf.Max(y, height - 1 - y);
            if (horizontal && (y < 0 || y >= height) || !horizontal && (x < 0 || x >= width))
                yield break;
            var size = new Vector2(_view._cellSize * 0.78f, _view._cellSize * 0.78f);
            var left = SpawnFx(horizontal ? ArrowSprite(false) : ArrowSpriteUp(), _view.CellPos(x, y), size);
            var right = SpawnFx(horizontal ? ArrowSprite(true) : ArrowSpriteDown(), _view.CellPos(x, y), size);
            for (var step = 0; step <= max; step++)
            {
                if (horizontal)
                {
                    PopCell(x - step, y);
                    PopCell(x + step, y);
                    MoveFx(left, _view.CellPos(x - step, y));
                    MoveFx(right, _view.CellPos(x + step, y));
                }
                else
                {
                    PopCell(x, y - step);
                    PopCell(x, y + step);
                    MoveFx(left, _view.CellPos(x, y - step));
                    MoveFx(right, _view.CellPos(x, y + step));
                }

                yield return Wait(0.05f);
            }

            DestroyFx(left);
            DestroyFx(right);
        }

        private IEnumerator AnimateBombBlast(Match3BoardEvent e)
        {
            var x = e.FromX;
            var y = e.FromY;
            var radius = Mathf.Max(1, e.ScoreDelta);
            var span = radius * 2 + 1;
            var sprite = Match3Icons.FromFile(_view.IconSprites, Match3Icons.BombFile);
            var fx = SpawnFx(sprite, _view.CellPos(x, y), new Vector2(_view._cellSize, _view._cellSize));
            var from = _view._cellSize;
            var to = _view._cellSize * span;
            const float grow = 0.22f;
            if (fx != null)
            {
                var growing = UITween.Custom(fx, _view.Dur(grow), u => fx.sizeDelta = Vector2.one * Mathf.LerpUnclamped(from, to, u), EaseKind.Linear);
                if (growing != null)
                    yield return growing.WaitForCompletion();
            }

            for (var cy = y - radius; cy <= y + radius; cy++)
            {
                for (var cx = x - radius; cx <= x + radius; cx++)
                    PopCell(cx, cy);
            }

            const float fade = 0.1f;
            var image = fx != null ? fx.GetComponent<Image>() : null;
            if (image != null)
            {
                var color = image.color;
                var fading = UITween.Custom(image, _view.Dur(fade), u => image.color = new Color(color.r, color.g, color.b, 1f - u), EaseKind.Linear);
                if (fading != null)
                    yield return fading.WaitForCompletion();
            }

            DestroyFx(fx);
        }

        private IEnumerator AnimateRainbowBlast(Match3BoardEvent e)
        {
            var rainbow = Match3Icons.FromFile(_view.IconSprites, 19);
            var color = e.Color;
            var targets = new List<Match3TileView>(16);
            foreach (var view in _view._views.Values)
            {
                if (view == null)
                    continue;
                var hit = color <= 0 || view.ColorId == color || view.TileId == e.Id;
                if (!hit)
                    continue;
                if (view.Icon != null)
                {
                    view.Icon.sprite = rainbow;
                    view.Icon.enabled = rainbow != null;
                    view.Icon.preserveAspect = true;
                }

                ShowGo(view.SpecialArrowL, false);
                ShowGo(view.SpecialArrowR, false);
                ShowGo(view.SpecialArrowU, false);
                ShowGo(view.SpecialArrowD, false);
                targets.Add(view);
            }

            yield return Wait(0.28f);
            for (var i = 0; i < targets.Count; i++)
                PopView(targets[i]);
            yield return Wait(0.08f);
        }

        private IEnumerator AnimateMoves(List<Match3BoardEvent> events, int start, int end)
        {
            var items = new List<(RectTransform rect, Vector2 from, Vector2 to, Match3TileView view, Match3BoardEvent e)>();
            for (var i = start; i < end; i++)
            {
                var e = events[i];
                if (e.Kind == Match3EventKind.Spawn)
                {
                    var tile = new Match3Tile { Id = e.Id, Color = e.Color, Special = e.Special };
                    var view = _view.CreateView(tile, e.ToX, e.ToY, _view.CellPos(e.FromX, e.FromY));
                    if (view == null)
                        continue;
                    items.Add((view.GetComponent<RectTransform>(), _view.CellPos(e.FromX, e.FromY), _view.CellPos(e.ToX, e.ToY), view, e));
                    continue;
                }

                if (_view._views.TryGetValue(e.Id, out var existing))
                    items.Add((existing.GetComponent<RectTransform>(), _view.CellPos(e.FromX, e.FromY), _view.CellPos(e.ToX, e.ToY), existing, e));
            }

            const float duration = 0.16f;
            var moving = UITween.Custom(_view, _view.Dur(duration), u =>
            {
                for (var n = 0; n < items.Count; n++)
                {
                    var item = items[n];
                    if (item.rect != null)
                        item.rect.anchoredPosition = Vector2.LerpUnclamped(item.from, item.to, u);
                }
            }, EaseKind.InOutQuad);

            if (moving != null)
                yield return moving.WaitForCompletion();

            for (var n = 0; n < items.Count; n++)
            {
                var item = items[n];
                if (item.rect != null)
                    item.rect.anchoredPosition = item.to;
                if (item.view != null)
                    item.view.SetCell(item.e.ToX, item.e.ToY);
            }
        }

        private void AnimateSpawnSpecial(Match3BoardEvent e)
        {
            var tile = new Match3Tile { Id = e.Id, Color = e.Color, Special = e.Special };
            if (_view._views.TryGetValue(e.Id, out var existing) && existing != null)
            {
                existing.Bind(e.ToX, e.ToY, tile, _view.IconOf(tile));
                return;
            }

            _view.CreateView(tile, e.ToX, e.ToY, _view.CellPos(e.ToX, e.ToY));
        }

        private RectTransform SpawnFx(Sprite sprite, Vector2 pos, Vector2 size)
        {
            _view.EnsureFxLayer();
            var rect = UIViewFactory.CreateRect("BlastFx", _view.FxLayer);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : UIStyle.WhiteSprite;
            image.color = sprite != null ? Color.white : UIStyle.Accent;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        private static void MoveFx(RectTransform rect, Vector2 pos)
        {
            if (rect != null)
                rect.anchoredPosition = pos;
        }

        private static void DestroyFx(RectTransform rect)
        {
            if (rect != null)
                UnityEngine.Object.Destroy(rect.gameObject);
        }

        private void SpawnFlash(Vector2 pos)
        {
            var fx = SpawnFx(UIStyle.WhiteSprite, pos, new Vector2(_view._cellSize * 0.92f, _view._cellSize * 0.92f));
            if (fx == null)
                return;
            var image = fx.GetComponent<Image>();
            if (image != null)
                image.color = new Color(1f, 0.98f, 0.82f, 0.9f);
            _view.StartCoroutine(FadeFx(fx, 0.16f));
        }

        private IEnumerator FadeFx(RectTransform rect, float duration)
        {
            if (rect == null)
                yield break;

            var image = rect.GetComponent<Image>();
            var color = image != null ? image.color : Color.white;
            var fading = UITween.Custom(rect, _view.Dur(duration), u =>
            {
                if (rect == null)
                    return;
                if (image != null)
                    image.color = new Color(color.r, color.g, color.b, 1f - u);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(1.15f, 0.4f, u);
            }, EaseKind.Linear);

            if (fading != null)
                yield return fading.WaitForCompletion();

            DestroyFx(rect);
        }

        private IEnumerator ShakeBoard(float duration, float amplitude)
        {
            if (_view.BoardRoot == null)
                yield break;

            var board = _view.BoardRoot;
            var origin = board.anchoredPosition;
            UITween.KillTarget(board);
            var shaking = UITween.Custom(board, _view.Dur(duration), u =>
            {
                var falloff = 1f - u;
                board.anchoredPosition = origin + UnityEngine.Random.insideUnitCircle * (amplitude * falloff);
            }, EaseKind.Linear);

            if (shaking != null)
                yield return shaking.WaitForCompletion();

            board.anchoredPosition = origin;
        }

        private IEnumerator FlyDropToHud(Match3Drop drop, Vector2 fromBoard)
        {
            var sprite = drop == Match3Drop.ExtraMoves
                ? Match3Icons.FromFile(_view.IconSprites, Match3Icons.ExtraMovesFile)
                : Match3Icons.FromFile(_view.IconSprites, Match3Icons.GoldBeanFile);
            var hud = drop == Match3Drop.ExtraMoves
                ? (_view.MovesText != null ? _view.MovesText.rectTransform : null)
                : (_view.LevelText != null ? _view.LevelText.rectTransform : _view.GoalText != null ? _view.GoalText.rectTransform : null);
            if (hud == null || _view.BoardRoot == null)
                yield break;

            var start = _view.BoardRoot.TransformPoint(fromBoard);
            var rect = UIViewFactory.CreateRect("DropFly", _view.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(56f, 56f);
            rect.position = start;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : UIStyle.WhiteSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var end = hud.position;
            const float duration = 0.48f;
            var flying = UITween.Custom(rect, _view.Dur(duration), u =>
            {
                if (rect == null)
                    return;
                rect.position = Vector3.LerpUnclamped(start, end, u);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.35f, u);
            }, EaseKind.InOutQuad);

            if (flying != null)
                yield return flying.WaitForCompletion();

            DestroyFx(rect);
        }

        private Sprite ArrowSprite(bool right)
        {
            return ArrowFromPrefab(right ? _view.TilePrefab?.SpecialArrowR : _view.TilePrefab?.SpecialArrowL);
        }

        private Sprite ArrowSpriteUp()
        {
            return ArrowFromPrefab(_view.TilePrefab?.SpecialArrowU);
        }

        private Sprite ArrowSpriteDown()
        {
            return ArrowFromPrefab(_view.TilePrefab?.SpecialArrowD);
        }

        private static Sprite ArrowFromPrefab(GameObject go)
        {
            if (go == null)
                return null;
            var image = go.GetComponent<Image>();
            return image != null ? image.sprite : null;
        }

        private void PopCell(int x, int y)
        {
            PopView(_view.ViewAt(x, y));
        }

        private void PopView(Match3TileView view)
        {
            if (view == null || !_view._poppedIds.Add(view.TileId))
                return;
            view.SetSelected(false);
            ShowGo(view.SpecialArrowL, false);
            ShowGo(view.SpecialArrowR, false);
            ShowGo(view.SpecialArrowU, false);
            ShowGo(view.SpecialArrowD, false);
            ShowGo(view.SpecialBoom, false);
            view.transform.localScale = Vector3.one * 0.08f;
        }

        private static void ShowGo(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on)
                go.SetActive(on);
        }

        private IEnumerator Wait(float seconds)
        {
            if (seconds > 0f)
                yield return new WaitForSeconds(_view.Dur(seconds));
        }

        private IEnumerator LerpPair(RectTransform a, Vector2 aTo, RectTransform b, Vector2 bTo, float duration)
        {
            if (a == null || b == null)
                yield break;

            var aFrom = a.anchoredPosition;
            var bFrom = b.anchoredPosition;
            var lerp = UITween.Custom(a, _view.Dur(duration), u =>
            {
                if (a != null)
                    a.anchoredPosition = Vector2.LerpUnclamped(aFrom, aTo, u);
                if (b != null)
                    b.anchoredPosition = Vector2.LerpUnclamped(bFrom, bTo, u);
            }, EaseKind.Linear);

            if (lerp != null)
                yield return lerp.WaitForCompletion();

            if (a != null)
                a.anchoredPosition = aTo;
            if (b != null)
                b.anchoredPosition = bTo;
        }

        private static bool HasClear(Match3ResolveResult result)
        {
            if (result.Events == null)
                return false;
            for (var i = 0; i < result.Events.Count; i++)
            {
                var kind = result.Events[i].Kind;
                if (kind == Match3EventKind.Remove || kind == Match3EventKind.Blast)
                    return true;
            }

            return false;
        }
    }
}
