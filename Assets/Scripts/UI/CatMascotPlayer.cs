using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmallGames
{
    /// <summary>
    /// 局内右侧猫猫序列帧。挠头 8 帧播一轮后停 3 秒再循环；消除 24 帧播一遍；胜利 18 帧连播 3 遍再回调结算。
    /// 过关连播期间任意点击可跳过，立刻弹出结算。同一段还在播时再次触发，不从头打断。
    /// </summary>
    public sealed class CatMascotPlayer
    {
        public const int FrameCount = 18;
        public const int SkillFrameCount = 24;
        public const float ScratchFrameSeconds = 1f / 8f;
        public const float EatFrameSeconds = 1f / 24f;
        public const float SkillFrameSeconds = 1f / 24f;
        public const float ExcitedFrameSeconds = 1f / 18f;
        public const int ExcitedRepeats = 3;
        public const float ScratchHoldSeconds = 3f;
        public const float DisplayWidth = 280f;
        public const float DisplayHeight = 360f;
        public const float EdgeInset = 24f;
        public const int LianEnergyMax = 4;
        public const int SheepEnergyMax = 3;
        public const int Match3EnergyMax = 4;
        public const int Match3SkillMoves = 3;

        /// <summary>居中棋盘为右侧猫留出的左移量。</summary>
        public const float BoardShift = 164f;

        private static CatMascotPlayer _shown;

        private readonly Image _image;
        private readonly RectTransform _rect;
        private Transform _home;
        private int _homeSibling;
        private bool _lifted;
        private readonly Sprite[] _scratch = new Sprite[FrameCount];
        private readonly Sprite[] _eat = new Sprite[FrameCount];
        private readonly Sprite[] _excited = new Sprite[FrameCount];
        private readonly Sprite[] _skill1 = new Sprite[SkillFrameCount];
        private readonly Sprite[] _skill2 = new Sprite[SkillFrameCount];
        private Sprite[] _clip;
        private int _frame;
        private int _cyclesLeft;
        private float _timer;
        private float _frameSeconds;
        private float _holdLeft;
        private bool _loop;
        private bool _holding;
        private bool _playing;
        private bool _cheering;
        private Action _onDone;
        private readonly CatBiteFlight _bites;
        private readonly CatEnergyBar _bar;
        private readonly CatSkillCharge _charge;

        private CatMascotPlayer(Image image, CatEnergyBar bar, GameModeType mode)
        {
            _image = image;
            _bar = bar;
            _charge = new CatSkillCharge(EnergyMax(mode));
            _rect = image != null ? image.rectTransform : null;
            if (_shown != null && _shown != this)
                _shown.Drop();
            _shown = this;
            _bites = CatBiteFlight.Create(image);
            var folder = Folder(mode);
            if (folder != null)
            {
                LoadClip(folder, "Scratch", _scratch);
                LoadClip(folder, "Eat", _eat);
                LoadClip(folder, "Excited", _excited);
                LoadClip(folder, "Skill1", _skill1);
                LoadClip(folder, "Skill2", _skill2);
            }

            if (HasFrame(_scratch))
                Begin(_scratch, true, ScratchFrameSeconds, 0);
            else if (_image != null)
                _image.enabled = false;
            RefreshBar();
        }

        /// <summary>
        /// 在界面右侧垂直居中挂一只猫。board 用来插到棋盘后面、底栏前面。
        /// </summary>
        public static CatMascotPlayer Attach(Transform parent, RectTransform board, GameModeType mode)
        {
            if (parent == null)
                return null;

            var prefab = AssetHub.Load<GameObject>(UiPrefabs.CatMascot);
            if (prefab == null)
                return null;

            var go = UnityEngine.Object.Instantiate(prefab, parent, false);
            go.name = "CatMascot";
            var image = go.GetComponent<Image>();
            var bar = go.GetComponent<CatEnergyBar>();
            if (image != null)
            {
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.color = Color.white;
            }

            var rect = go.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(DisplayWidth, DisplayHeight);
                rect.anchoredPosition = new Vector2(-(EdgeInset + DisplayWidth * 0.5f), 36f);
                if (board != null && board.parent == parent)
                    rect.SetSiblingIndex(board.GetSiblingIndex() + 1);
                else
                    rect.SetAsLastSibling();
            }

            return new CatMascotPlayer(image, bar, mode);
        }

        /// <summary>
        /// 成功消除后加能量。到第一段放技能一，到第二段放技能二并清空。放不出来就停在当前段。
        /// </summary>
        public bool TryCharge(int gain, Func<int, bool> cast)
        {
            if (_charge == null)
                return false;
            var fired = _charge.Add(gain, cast);
            RefreshBar();
            return fired;
        }

        /// <summary>
        /// 教学遮罩几乎不透明，盖在弹窗层。猫若留在棋盘层，引导关看起来就像没猫。
        /// 抬到遮罩同一层的最前，世界坐标不变。
        /// </summary>
        public static void LiftAbove(RectTransform layer)
        {
            if (_shown != null)
                _shown.Lift(layer);
        }

        /// <summary>引导结束，把猫放回原来的界面层级。</summary>
        public static void DropAbove()
        {
            if (_shown != null)
                _shown.Drop();
        }

        public void Tick(float deltaTime)
        {
            if (_cheering && Input.GetMouseButtonDown(0))
            {
                SkipCheer();
                return;
            }

            if (!_playing || _clip == null)
                return;

            if (_holding)
            {
                _holdLeft -= deltaTime;
                if (_holdLeft > 0f)
                    return;
                _holding = false;
                _frame = 0;
                _timer = 0f;
                Show(0);
                return;
            }

            _timer += deltaTime;
            if (_timer < _frameSeconds)
                return;

            _timer -= _frameSeconds;
            if (_timer < 0f)
                _timer = 0f;
            var next = _frame + 1;
            if (next >= _clip.Length || _clip[next] == null)
            {
                if (_loop)
                {
                    _holding = true;
                    _holdLeft = ScratchHoldSeconds;
                    return;
                }

                if (_cyclesLeft > 1)
                {
                    _cyclesLeft--;
                    _frame = 0;
                    Show(0);
                    return;
                }

                _playing = false;
                Complete();
                return;
            }

            _frame = next;
            Show(next);
        }

        /// <summary>把这张格子图沿随机弧线送进猫嘴。调用方负责把原格子收掉。</summary>
        public void Feed(Image source)
        {
            if (_bites != null)
                _bites.Launch(source);
        }

        /// <summary>技能动作 24 帧播一遍。同一段还在播时不重头开始，也不被吃东西打断。</summary>
        public void PlaySkill(int index)
        {
            if (_cheering)
                return;
            var clip = index == 0 ? _skill1 : _skill2;
            if (_playing && _clip == clip)
                return;
            _onDone = null;
            Begin(clip, false, SkillFrameSeconds, 1);
        }

        public void PlayEat()
        {
            if (_cheering)
                return;
            if (_playing && (_clip == _eat || _clip == _skill1 || _clip == _skill2))
                return;
            _onDone = null;
            Begin(_eat, false, EatFrameSeconds, 1);
        }

        /// <summary>兴奋按 18 帧连播 3 遍再回调。帧没加载到时立刻回调，避免结算被卡住。</summary>
        public void PlayExcited(Action onDone)
        {
            if (_playing && _clip == _excited)
                return;
            _cheering = true;
            _onDone = onDone;
            Begin(_excited, false, ExcitedFrameSeconds, ExcitedRepeats);
        }

        /// <summary>过关连播中点击跳过，直接走结算回调。</summary>
        public void SkipCheer()
        {
            if (!_cheering)
                return;
            _playing = false;
            _holding = false;
            Complete();
        }

        /// <summary>界面被关掉时把还没触发的过关回调放出去，避免结算丢失。</summary>
        public void Abort()
        {
            Drop();
            if (_shown == this)
                _shown = null;
            if (_bites != null)
                _bites.Stop();
            _playing = false;
            _cheering = false;
            var done = _onDone;
            _onDone = null;
            if (done != null)
                done();
        }

        private void Lift(RectTransform layer)
        {
            if (_lifted || _rect == null || layer == null)
                return;
            _home = _rect.parent;
            _homeSibling = _rect.GetSiblingIndex();
            _rect.SetParent(layer, true);
            _rect.SetAsLastSibling();
            _lifted = true;
        }

        private void Drop()
        {
            if (!_lifted || _rect == null)
                return;
            _lifted = false;
            if (_home == null)
                return;
            _rect.SetParent(_home, true);
            var index = _homeSibling;
            if (index < 0)
                index = 0;
            if (index > _home.childCount - 1)
                index = _home.childCount - 1;
            _rect.SetSiblingIndex(index);
        }

        private void Begin(Sprite[] clip, bool loop, float frameSeconds, int cycles)
        {
            if (!HasFrame(clip))
            {
                if (loop)
                {
                    _playing = false;
                    return;
                }

                _playing = false;
                Complete();
                return;
            }

            _clip = clip;
            _loop = loop;
            _holding = false;
            _holdLeft = 0f;
            _cyclesLeft = cycles;
            _frameSeconds = frameSeconds > 0f ? frameSeconds : ScratchFrameSeconds;
            _frame = 0;
            _timer = 0f;
            _playing = true;
            Show(0);
        }

        private void Complete()
        {
            var done = _onDone;
            _onDone = null;
            _cheering = false;
            if (done != null)
            {
                done();
                return;
            }

            Begin(_scratch, true, ScratchFrameSeconds, 0);
        }

        private void Show(int index)
        {
            if (_image == null || _clip == null || index < 0 || index >= _clip.Length)
                return;
            _image.enabled = true;
            _image.sprite = _clip[index];
        }

        private void RefreshBar()
        {
            if (_bar != null && _charge != null)
                _bar.SetCharge(_charge.Energy, _charge.Stage);
        }

        private static int EnergyMax(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Sheep: return SheepEnergyMax;
                case GameModeType.Match3: return Match3EnergyMax;
                default: return LianEnergyMax;
            }
        }

        private static bool HasFrame(Sprite[] clip)
        {
            return clip != null && clip.Length > 0 && clip[0] != null;
        }

        private static void LoadClip(string folder, string clip, Sprite[] dest)
        {
            for (var i = 0; i < dest.Length; i++)
            {
                var index = i.ToString("00");
                dest[i] = AssetHub.Load<Sprite>("UI/Cats/" + folder + "/" + clip + "/frame_" + index);
            }
        }

        private static string Folder(GameModeType mode)
        {
            switch (mode)
            {
                case GameModeType.Lianliankan: return "Lianliankan";
                case GameModeType.Sheep: return "Sheep";
                case GameModeType.Match3: return "Match3";
                default: return null;
            }
        }
    }
}
