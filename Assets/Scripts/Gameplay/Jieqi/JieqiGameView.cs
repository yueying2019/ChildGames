using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 揭棋棋盘。红方在下。暗子只显示背面，翻开后才换成真子图。
    /// </summary>
    public class JieqiGameView : MonoBehaviour
    {
        public const int BoardWidth = 1080;
        public const int BoardHeight = 1200;

        static readonly int[] FileX = { 61, 181, 300, 420, 540, 660, 780, 900, 1020 };
        static readonly int[] RankY = { 60, 180, 300, 420, 540, 660, 779, 899, 1019, 1139 };

        public RectTransform PieceLayer;
        public JieqiPieceView PiecePrefab;
        public Text StatusText;
        public Text RedHeldText;
        public Text BlackHeldText;
        public RectTransform RedHeldIcons;
        public RectTransform BlackHeldIcons;
        public Sprite Back;
        public Sprite RedShuai;
        public Sprite RedShi;
        public Sprite RedXiang;
        public Sprite RedMa;
        public Sprite RedJu;
        public Sprite RedPao;
        public Sprite RedBing;
        public Sprite BlackJiang;
        public Sprite BlackShi;
        public Sprite BlackXiang;
        public Sprite BlackMa;
        public Sprite BlackJu;
        public Sprite BlackPao;
        public Sprite BlackZu;
        public Sprite MoveGlow;
        public AudioClip CaptureClip;
        public AudioClip CheckClip;

        readonly List<JieqiPieceView> _pool = new List<JieqiPieceView>(48);
        readonly List<JieqiPieceView> _heldPool = new List<JieqiPieceView>(16);
        readonly List<JieqiMove> _moves = new List<JieqiMove>(128);
        readonly List<JieqiCapturedView> _held = new List<JieqiCapturedView>(16);

        JieqiMatch _match;
        int _cursor;
        int _heldCursor;
        int _fromX = -1;
        int _fromY = -1;
        int _lastFromX = -1;
        int _lastFromY = -1;
        int _lastToX = -1;
        int _lastToY = -1;
        bool _popGlow;
        bool _locked;
        bool _thinking;
        Coroutine _ai;

        public event Action Changed;

        public void Play(JieqiMatch match)
        {
            _match = match;
            _fromX = -1;
            _fromY = -1;
            _lastFromX = -1;
            _lastFromY = -1;
            _lastToX = -1;
            _lastToY = -1;
            _popGlow = false;
            _thinking = false;
            Refresh();
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            if (!locked)
                ScheduleAi();
        }

        /// <summary>退回上一步。电脑正在想也会停掉，悔完不再自动替电脑走。</summary>
        public bool TryUndo()
        {
            if (_match == null)
                return false;
            if (_ai != null)
            {
                StopCoroutine(_ai);
                _ai = null;
            }

            _thinking = false;
            if (!_match.TryUndo())
                return false;

            _locked = false;
            _fromX = -1;
            _fromY = -1;
            _lastFromX = -1;
            _lastFromY = -1;
            _lastToX = -1;
            _lastToY = -1;
            _popGlow = false;
            Refresh();
            Changed?.Invoke();
            return true;
        }

        public void SetStatus(string text)
        {
            if (StatusText != null)
                StatusText.text = text;
        }

        void OnDisable()
        {
            if (_ai != null)
                StopCoroutine(_ai);
            _ai = null;
            _thinking = false;
        }

        void Refresh()
        {
            _cursor = 0;
            _heldCursor = 0;
            if (_match == null || PieceLayer == null || PiecePrefab == null)
            {
                HideUnused();
                HideHeld();
                return;
            }

            var engine = _match.Engine;
            engine.CollectMoves(_moves);
            MarkLastMove();
            for (var y = 0; y < JieqiEngine.Ranks; y++)
            {
                for (var x = 0; x < JieqiEngine.Files; x++)
                {
                    if (!engine.TryView(x, y, out var view) || !view.Occupied)
                        continue;
                    var piece = Rent(x, y, 72f);
                    if (view.HasSkill)
                        piece.ShowPair(
                            SpriteOf(view.Printed, view.Role),
                            Color.white,
                            SpriteOf(view.SkillPrinted, view.SkillRole),
                            Color.white);
                    else
                        piece.Show(SpriteOf(view), Color.white);
                    var capturedX = x;
                    var capturedY = y;
                    Wire(piece, () => OnSquare(capturedX, capturedY));
                    if (_fromX == x && _fromY == y)
                        piece.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
                }
            }

            if (_fromX >= 0)
            {
                for (var i = 0; i < _moves.Count; i++)
                {
                    var move = _moves[i];
                    if (move.FromX != _fromX || move.FromY != _fromY)
                        continue;
                    var hint = Rent(move.ToX, move.ToY, 44f);
                    hint.Show(Back, new Color(0.92f, 0.08f, 0.08f, 1f));
                    hint.SetHitPadding(new Vector4(-18f, -18f, -18f, -18f));
                    var toX = move.ToX;
                    var toY = move.ToY;
                    Wire(hint, () => OnSquare(toX, toY));
                }
            }

            HideUnused();
            WriteHeld(RedHeldIcons, JieqiCamp.Red);
            WriteHeld(BlackHeldIcons, JieqiCamp.Black);
            HideHeld();
        }

        void OnSquare(int x, int y)
        {
            if (_locked || _thinking || _match == null || !_match.HumanToMove)
                return;

            if (_fromX >= 0 && IsLegal(_fromX, _fromY, x, y))
            {
                if (!_match.PlayHuman(_fromX, _fromY, x, y, out var step))
                {
                    if (!string.IsNullOrEmpty(step.Message))
                        UIManager.Instance?.ShowToast(step.Message);
                    return;
                }

                _fromX = -1;
                _fromY = -1;
                RememberMove(step);
                PlayMoveSounds(step);
                Refresh();
                Changed?.Invoke();
                ScheduleAi();
                return;
            }

            if (!OwnPiece(x, y, out var canMove))
            {
                _fromX = -1;
                _fromY = -1;
                Refresh();
                return;
            }

            if (!canMove)
            {
                _fromX = -1;
                _fromY = -1;
                Refresh();
                if (_match.Engine.InCheck())
                    UIManager.Instance?.ShowToast("被将军了，这一步不能应将");
                return;
            }

            _fromX = x;
            _fromY = y;
            Refresh();
        }

        bool OwnPiece(int x, int y, out bool canMove)
        {
            canMove = false;
            if (!_match.Engine.TryView(x, y, out var view) || !view.Occupied)
                return false;
            if (view.Controller != _match.Engine.Turn)
                return false;
            for (var i = 0; i < _moves.Count; i++)
            {
                if (_moves[i].FromX == x && _moves[i].FromY == y)
                {
                    canMove = true;
                    break;
                }
            }

            return true;
        }

        bool IsLegal(int fromX, int fromY, int toX, int toY)
        {
            for (var i = 0; i < _moves.Count; i++)
            {
                var move = _moves[i];
                if (move.FromX == fromX && move.FromY == fromY && move.ToX == toX && move.ToY == toY)
                    return true;
            }

            return false;
        }

        void ScheduleAi()
        {
            if (_ai != null || _locked || _match == null || !_match.AiToMove)
                return;
            _ai = StartCoroutine(PlayAi());
        }

        IEnumerator PlayAi()
        {
            _thinking = true;
            _fromX = -1;
            _fromY = -1;
            Refresh();
            if (StatusText != null)
                StatusText.text = "电脑在想";
            yield return new WaitForSeconds(0.35f);
            _thinking = false;
            _ai = null;
            if (_match != null && _match.AiToMove && _match.PlayAi(out var step))
            {
                RememberMove(step);
                PlayMoveSounds(step);
            }
            Refresh();
            Changed?.Invoke();
        }

        void MarkLastMove()
        {
            if (MoveGlow == null || _lastToX < 0)
                return;
            if (_lastFromX >= 0)
                ShowGlow(_lastFromX, _lastFromY, 78f, new Color(1f, 1f, 1f, 0.72f), false);
            ShowGlow(_lastToX, _lastToY, 116f, Color.white, _popGlow);
            _popGlow = false;
        }

        void ShowGlow(int x, int y, float size, Color color, bool pop)
        {
            var mark = Rent(x, y, size);
            mark.Show(MoveGlow, color);
            if (mark.Icon != null)
                mark.Icon.raycastTarget = false;
            if (mark.Button != null)
            {
                mark.Button.onClick.RemoveAllListeners();
                mark.Button.interactable = false;
            }

            if (!pop)
                return;
            UITween.KillTarget(mark.transform);
            UITween.Pop(mark.transform, 0.7f, 1.05f, 0.22f);
        }

        void PlayMoveSounds(JieqiStep step)
        {
            if (!step.Ok || _match == null)
                return;
            if (step.Captured)
                AudioManager.Instance?.PlaySfx(CaptureClip);
            if (_match.Engine.DefenderInCheck())
                AudioManager.Instance?.PlaySfx(CheckClip);
        }

        void RememberMove(JieqiStep step)
        {
            if (!step.Ok)
                return;
            _lastFromX = step.Move.FromX;
            _lastFromY = step.Move.FromY;
            _lastToX = step.Move.ToX;
            _lastToY = step.Move.ToY;
            _popGlow = true;
        }

        void WriteHeld(RectTransform layer, JieqiCamp side)
        {
            if (layer == null || _match == null)
                return;
            _match.Engine.CopyCaptured(_held);
            var shown = 0;
            for (var i = 0; i < _held.Count; i++)
            {
                var captured = _held[i];
                if (captured.CapturedBy != side)
                    continue;
                var icon = RentHeld(layer, shown);
                icon.Show(SpriteOf(captured.Printed, captured.Role), Color.white);
                shown++;
            }
        }

        Sprite SpriteOf(JieqiViewSquare view)
        {
            if (view.FaceDown)
                return Back;
            return SpriteOf(view.Printed, view.Role);
        }

        Sprite SpriteOf(JieqiCamp printed, JieqiRole role)
        {
            var red = printed == JieqiCamp.Red;
            switch (role)
            {
                case JieqiRole.King: return red ? RedShuai : BlackJiang;
                case JieqiRole.Advisor: return red ? RedShi : BlackShi;
                case JieqiRole.Elephant: return red ? RedXiang : BlackXiang;
                case JieqiRole.Horse: return red ? RedMa : BlackMa;
                case JieqiRole.Rook: return red ? RedJu : BlackJu;
                case JieqiRole.Cannon: return red ? RedPao : BlackPao;
                default: return red ? RedBing : BlackZu;
            }
        }

        JieqiPieceView Rent(int x, int y, float size)
        {
            JieqiPieceView view;
            if (_cursor < _pool.Count)
            {
                view = _pool[_cursor];
            }
            else
            {
                view = Instantiate(PiecePrefab, PieceLayer);
                _pool.Add(view);
            }

            _cursor++;
            UITween.KillTarget(view.transform);
            var rect = view.transform as RectTransform;
            if (rect != null)
            {
                var nx = FileX[x] / (float)BoardWidth;
                var ny = 1f - RankY[y] / (float)BoardHeight;
                rect.anchorMin = new Vector2(nx, ny);
                rect.anchorMax = new Vector2(nx, ny);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(size, size);
                rect.localScale = Vector3.one;
            }

            view.SetHitPadding(Vector4.zero);
            view.SetHitEnabled(true);
            if (view.Button != null)
                view.Button.interactable = true;
            view.transform.SetAsLastSibling();
            return view;
        }

        JieqiPieceView RentHeld(RectTransform layer, int index)
        {
            JieqiPieceView view;
            if (_heldCursor < _heldPool.Count)
            {
                view = _heldPool[_heldCursor];
            }
            else
            {
                view = Instantiate(PiecePrefab, layer);
                _heldPool.Add(view);
            }

            _heldCursor++;
            if (view.transform.parent != layer)
                view.transform.SetParent(layer, false);
            var rect = view.transform as RectTransform;
            if (rect != null)
            {
                var col = index % 2;
                var row = index / 2;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(4f + col * 78f, -4f - row * 78f);
                rect.sizeDelta = new Vector2(72f, 72f);
                rect.localScale = Vector3.one;
            }

            if (view.Button != null)
            {
                view.Button.onClick.RemoveAllListeners();
                view.Button.interactable = false;
            }

            view.SetHitEnabled(false);
            view.transform.SetAsLastSibling();
            return view;
        }

        void HideUnused()
        {
            for (var i = _cursor; i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                    _pool[i].gameObject.SetActive(false);
            }
        }

        void HideHeld()
        {
            for (var i = _heldCursor; i < _heldPool.Count; i++)
            {
                if (_heldPool[i] != null)
                    _heldPool[i].gameObject.SetActive(false);
            }
        }

        static void Wire(JieqiPieceView view, UnityEngine.Events.UnityAction click)
        {
            if (view.Button == null)
                return;
            view.Button.onClick.RemoveAllListeners();
            view.Button.onClick.AddListener(click);
        }
    }
}
