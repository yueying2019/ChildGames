using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public JieqiRookCaptureFx CaptureFx;
        public JieqiHorseCaptureFx HorseCaptureFx;
        public JieqiRoleCaptureFx RoleCaptureFx;
        public Button EditStartButton;
        public Button RevealAllButton;
        public Button CaptureButton;
        public Button MoveButton;
        public Button RestoreButton;
        public Button UndoEditButton;
        public JieqiBoardClick BoardClick;

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
        bool _editor;
        bool _editing;
        bool _skillCapture;
        bool _editorBound;
        EditorTool _tool;
        Text _editLabel;

        public bool Editing => _editing;

        Coroutine _ai;
        Coroutine _hold;
        bool _holding;

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
            CancelHold();
            StopCaptureFx();
            Refresh();
        }

        /// <summary>残局编辑。必须在 <see cref="Play"/> 之后调用，棋盘先按正常开局摆好。技能象棋吃子是选中后再吃对方。</summary>
        public void BeginEditor(bool skillCapture = false)
        {
            _editor = true;
            _editing = false;
            _skillCapture = skillCapture;
            _tool = EditorTool.None;
            if (RevealAllButton != null)
                RevealAllButton.gameObject.SetActive(!skillCapture);
            if (skillCapture)
                LayoutSkillButtons();
            if (EditStartButton != null)
                EditStartButton.transform.parent.gameObject.SetActive(true);
            if (!_editorBound)
            {
                _editorBound = true;
                WireButton(EditStartButton, OnStartEdit);
                WireButton(RevealAllButton, OnRevealAll);
                WireButton(CaptureButton, OnCaptureTool);
                WireButton(MoveButton, OnMoveTool);
                WireButton(RestoreButton, OnRestore);
                WireButton(UndoEditButton, OnUndoEdit);
                if (BoardClick != null)
                    BoardClick.Clicked += OnBoardClick;
            }

            if (_match != null)
                _match.Engine.EditorCaptureOrigin();
            ShowEditLabel(false);
            SetTool(EditorTool.None);
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
            StopCaptureFx();
            if (!_match.TryUndo())
                return false;

            CancelHold();

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
            if (!_editing)
            {
                engine.CollectMoves(_moves);
                MarkLastMove();
            }
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

            if (_fromX >= 0 && !_editing)
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
            if (_editor && _editing)
            {
                OnEditorSquare(x, y);
                return;
            }

            if (_locked || _thinking || _holding || _match == null || !_match.HumanToMove)
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
                FinishMove(step, false);
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
            if (_ai != null || _locked || _holding || _match == null || !_match.AiToMove)
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
                FinishMove(step, true);
                yield break;
            }

            Refresh();
            Changed?.Invoke();
        }

        /// <summary>吃子且动效开着时，锁到这段动画播完。对局分出胜负就等同一段时间后再打开结算。</summary>
        void FinishMove(JieqiStep step, bool fromAi)
        {
            if (_editor && _match != null)
                _match.Engine.EditorClearUndo();
            RememberMove(step);
            PlayMoveSounds(step);
            var seconds = step.Captured ? PlayCaptureFx(step) : 0f;
            Refresh();
            var over = _match != null && _match.Engine.Outcome != JieqiOutcome.Playing;
            if (seconds > 0f)
            {
                BeginHold(over, seconds);
                if (!over)
                    Changed?.Invoke();
                return;
            }

            Changed?.Invoke();
            if (!fromAi)
                ScheduleAi();
        }

        void BeginHold(bool openResult, float seconds)
        {
            if (_hold != null)
                StopCoroutine(_hold);
            _hold = StartCoroutine(HoldCapture(openResult, seconds));
        }

        IEnumerator HoldCapture(bool openResult, float seconds)
        {
            _holding = true;
            yield return new WaitForSeconds(seconds);
            _holding = false;
            _hold = null;
            HideSand();
            if (openResult)
                Changed?.Invoke();
            else
                ScheduleAi();
        }

        void CancelHold()
        {
            if (_hold != null)
            {
                StopCoroutine(_hold);
                _hold = null;
            }

            _holding = false;
        }

        void HideSand()
        {
            if (CaptureFx != null && CaptureFx.Backdrop != null)
                CaptureFx.Backdrop.Hide();
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

        /// <summary>吃子已经写入棋盘。按走出这一步的走法播对应动画；吃掉将或帅只播城旗。返回这段动画的秒数，0 表示没播。</summary>
        float PlayCaptureFx(JieqiStep step)
        {
            if (SettingsRepository.Current != null && SettingsRepository.Current.RemoveXiangqiFx)
                return 0f;

            if (step.CapturedRole == JieqiRole.King)
            {
                StopPieceFx();
                if (RoleCaptureFx == null)
                    return 0f;
                RoleCaptureFx.PlayFlag();
                return JieqiRoleCaptureFx.Duration;
            }

            var horse = step.MoverRole == JieqiRole.Horse || step.MoverHorseSkill;
            var rook = step.MoverRole == JieqiRole.Rook || step.MoverRookSkill;
            var cannon = step.MoverRole == JieqiRole.Cannon || step.MoverCannonSkill;
            var elephant = step.MoverRole == JieqiRole.Elephant || step.MoverElephantSkill;
            var advisor = step.MoverRole == JieqiRole.Advisor || step.MoverAdvisorSkill;
            var pawn = step.MoverRole == JieqiRole.Pawn || step.MoverPawnSkill;
            var dx = step.Move.ToX - step.Move.FromX;
            var dy = step.Move.ToY - step.Move.FromY;
            if (dx < 0)
                dx = -dx;
            if (dy < 0)
                dy = -dy;

            if (horse && ((dx == 1 && dy == 2) || (dx == 2 && dy == 1)))
                return PlayHorse();

            if (dx == 2 && dy == 2 && elephant)
                return PlayRole(2);

            if ((dx == 2 && dy == 2 && advisor) || (dx == 1 && dy == 1 && advisor))
                return PlayRole(3);

            var orthogonal = (dx == 0) != (dy == 0);
            if (!orthogonal)
                return 0f;

            var screens = CountScreens(step.Move);
            var dist = dx + dy;
            if (cannon && screens >= 1)
                return PlayRole(1);

            if (rook && screens >= 1)
                return PlayRook();

            if (rook && screens == 0 && step.MoverRole == JieqiRole.Rook)
                return PlayRook();

            if (step.MoverRole == JieqiRole.King && dist == 1)
                return PlayRole(5);

            if (pawn && dist == 1)
                return PlayRole(4);

            if (rook && screens == 0)
                return PlayRook();
            return 0f;
        }

        float PlayHorse()
        {
            if (HorseCaptureFx == null)
                return 0f;
            if (CaptureFx != null)
                CaptureFx.Stop();
            if (RoleCaptureFx != null)
                RoleCaptureFx.Stop();
            HorseCaptureFx.Play();
            return JieqiHorseCaptureFx.Duration;
        }

        float PlayRook()
        {
            if (CaptureFx == null)
                return 0f;
            if (HorseCaptureFx != null)
                HorseCaptureFx.Stop();
            if (RoleCaptureFx != null)
                RoleCaptureFx.Stop();
            CaptureFx.Play();
            return JieqiRookCaptureFx.Duration;
        }

        float PlayRole(int kind)
        {
            if (RoleCaptureFx == null)
                return 0f;
            if (CaptureFx != null)
                CaptureFx.Stop();
            if (HorseCaptureFx != null)
                HorseCaptureFx.Stop();
            if (kind == 1)
                RoleCaptureFx.PlayCannon();
            else if (kind == 2)
                RoleCaptureFx.PlayElephant();
            else if (kind == 3)
                RoleCaptureFx.PlayAdvisor();
            else if (kind == 4)
            {
                RoleCaptureFx.PlayPawn();
                return JieqiRoleCaptureFx.PawnDuration;
            }
            else if (kind == 5)
                RoleCaptureFx.PlaySeal();
            else
                return 0f;
            return JieqiRoleCaptureFx.Duration;
        }

        void StopPieceFx()
        {
            if (CaptureFx != null)
                CaptureFx.Stop();
            if (HorseCaptureFx != null)
                HorseCaptureFx.Stop();
        }

        void StopCaptureFx()
        {
            StopPieceFx();
            if (RoleCaptureFx != null)
                RoleCaptureFx.Stop();
        }

        int CountScreens(JieqiMove move)
        {
            if (_match == null)
                return 0;
            if (move.FromX != move.ToX && move.FromY != move.ToY)
                return -1;
            var sx = move.ToX == move.FromX ? 0 : (move.ToX > move.FromX ? 1 : -1);
            var sy = move.ToY == move.FromY ? 0 : (move.ToY > move.FromY ? 1 : -1);
            if (sx == 0 && sy == 0)
                return -1;
            var count = 0;
            var x = move.FromX + sx;
            var y = move.FromY + sy;
            var guard = 0;
            while ((x != move.ToX || y != move.ToY) && guard < 16)
            {
                if (_match.Engine.TryView(x, y, out var view) && view.Occupied)
                    count++;
                x += sx;
                y += sy;
                guard++;
            }

            return count;
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

        enum EditorTool
        {
            None = 0,
            Capture = 1,
            Move = 2
        }

        void ShowEditLabel(bool editing)
        {
            if (_editLabel == null && EditStartButton != null)
                _editLabel = EditStartButton.GetComponentInChildren<Text>(true);
            if (_editLabel != null)
                _editLabel.text = editing ? "退出编辑" : "开始编辑";
        }

        void ClearSelection()
        {
            _fromX = -1;
            _fromY = -1;
            _lastFromX = -1;
            _lastFromY = -1;
            _lastToX = -1;
            _lastToY = -1;
            _popGlow = false;
        }

        void OnStartEdit()
        {
            if (_locked)
                return;
            _editing = !_editing;
            ShowEditLabel(_editing);
            ClearSelection();
            SetTool(EditorTool.None);
            if (!_editing && _match != null)
                _match.Engine.RefreshTerminal();
            Changed?.Invoke();
        }

        void OnRestore()
        {
            if (!EnsureEditing() || _match == null)
                return;
            ClearSelection();
            SetTool(EditorTool.None);
            if (!_match.Engine.EditorRestoreOrigin())
                return;
            _match.AbandonHistory();
            Refresh();
            Changed?.Invoke();
        }

        void OnRevealAll()
        {
            if (!EnsureEditing() || _match == null)
                return;
            SetTool(EditorTool.None);
            if (!_match.Engine.EditorRevealAll())
            {
                UIManager.Instance?.ShowToast("没有暗棋");
                return;
            }

            Refresh();
            Changed?.Invoke();
        }

        void OnCaptureTool()
        {
            if (!EnsureEditing())
                return;
            SetTool(_tool == EditorTool.Capture ? EditorTool.None : EditorTool.Capture);
        }

        void OnMoveTool()
        {
            if (!EnsureEditing())
                return;
            SetTool(_tool == EditorTool.Move ? EditorTool.None : EditorTool.Move);
        }

        void OnUndoEdit()
        {
            if (!EnsureEditing() || _match == null)
                return;
            SetTool(EditorTool.None);
            if (!_match.Engine.EditorUndo())
            {
                UIManager.Instance?.ShowToast("没有可撤销的操作");
                return;
            }

            Refresh();
            Changed?.Invoke();
        }

        bool EnsureEditing()
        {
            if (_locked)
                return false;
            return _editing;
        }

        void SetTool(EditorTool tool)
        {
            _tool = tool;
            _fromX = -1;
            _fromY = -1;
            Latch(CaptureButton, tool == EditorTool.Capture);
            Latch(MoveButton, tool == EditorTool.Move);
            if (BoardClick != null)
                BoardClick.SetListening(tool == EditorTool.Move);
            if (_editor)
                Refresh();
        }

        void LayoutSkillButtons()
        {
            Place(EditStartButton, 168f);
            Place(CaptureButton, 84f);
            Place(MoveButton, 0f);
            Place(RestoreButton, -84f);
            Place(UndoEditButton, -168f);
        }

        static void Place(Button button, float y)
        {
            var rect = button != null ? button.transform as RectTransform : null;
            if (rect != null)
                rect.anchoredPosition = new Vector2(0f, y);
        }

        void OnSkillCaptureSquare(int x, int y)
        {
            var occupied = _match.Engine.TryView(x, y, out var view) && view.Occupied;
            if (_fromX < 0)
            {
                if (!occupied)
                    return;
                _fromX = x;
                _fromY = y;
                Refresh();
                return;
            }

            if (!occupied || (x == _fromX && y == _fromY))
                return;
            if (!_match.Engine.TryView(_fromX, _fromY, out var from) || !from.Occupied)
            {
                _fromX = x;
                _fromY = y;
                Refresh();
                return;
            }

            if (view.Controller == from.Controller)
            {
                _fromX = x;
                _fromY = y;
                Refresh();
                return;
            }

            if (!_match.Engine.EditorTake(_fromX, _fromY, x, y, out var message))
            {
                if (!string.IsNullOrEmpty(message))
                    UIManager.Instance?.ShowToast(message);
                return;
            }

            _fromX = -1;
            _fromY = -1;
            Refresh();
            Changed?.Invoke();
        }

        void OnEditorSquare(int x, int y)
        {
            if (_locked || !_editing || _match == null)
                return;
            if (_tool == EditorTool.Capture)
            {
                if (_skillCapture)
                {
                    OnSkillCaptureSquare(x, y);
                    return;
                }

                if (!_match.Engine.EditorCapture(x, y, out var message))
                {
                    if (!string.IsNullOrEmpty(message))
                        UIManager.Instance?.ShowToast(message);
                    return;
                }

                Refresh();
                Changed?.Invoke();
                return;
            }

            if (_tool != EditorTool.Move)
                return;

            var occupied = _match.Engine.TryView(x, y, out var view) && view.Occupied;
            if (_fromX < 0 || occupied)
            {
                if (!occupied)
                    return;
                _fromX = x;
                _fromY = y;
                Refresh();
                return;
            }

            if (!_match.Engine.EditorMove(_fromX, _fromY, x, y, out var moveMessage))
            {
                if (!string.IsNullOrEmpty(moveMessage))
                    UIManager.Instance?.ShowToast(moveMessage);
                return;
            }

            _fromX = -1;
            _fromY = -1;
            Refresh();
            Changed?.Invoke();
        }

        void OnBoardClick(PointerEventData eventData)
        {
            if (!_editor || !_editing || _tool != EditorTool.Move || PieceLayer == null || eventData == null)
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    PieceLayer, eventData.position, eventData.pressEventCamera, out var local))
                return;

            var rect = PieceLayer.rect;
            if (rect.width < 1f || rect.height < 1f)
                return;
            var nx = (local.x - rect.xMin) / rect.width;
            var ny = (local.y - rect.yMin) / rect.height;
            var designX = nx * BoardWidth;
            var designY = (1f - ny) * BoardHeight;
            var best = 70f * 70f;
            var bx = -1;
            var by = -1;
            for (var y = 0; y < JieqiEngine.Ranks; y++)
            {
                var dy = designY - RankY[y];
                for (var x = 0; x < JieqiEngine.Files; x++)
                {
                    var dx = designX - FileX[x];
                    var dist = dx * dx + dy * dy;
                    if (dist >= best)
                        continue;
                    best = dist;
                    bx = x;
                    by = y;
                }
            }

            if (bx < 0)
                return;
            OnEditorSquare(bx, by);
        }

        static void WireButton(Button button, UnityEngine.Events.UnityAction click)
        {
            if (button == null || click == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(click);
        }

        static void Latch(Button button, bool down)
        {
            if (button == null)
                return;
            button.transform.localScale = down ? new Vector3(0.94f, 0.94f, 1f) : Vector3.one;
            if (button.targetGraphic == null)
                return;
            if (down)
            {
                button.transition = Selectable.Transition.None;
                button.targetGraphic.color = button.colors.pressedColor;
            }
            else
            {
                button.targetGraphic.color = Color.white;
                button.transition = Selectable.Transition.ColorTint;
            }
        }
    }
}
