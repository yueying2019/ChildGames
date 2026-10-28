using UnityEngine;

namespace SmallGames
{
    public interface IGameSession
    {
        GameModeType Mode { get; }
        bool IsPaused { get; }
        bool HasNextLevel { get; }
        bool IsTutorial { get; }

        void StartSession();
        void Pause();
        void Resume();
        void Restart();
        void StartNextLevel();
        void QuitToMenu();
        void Dispose();
    }

    /// <summary>
    /// 玩法会话基类。具体模式继承后实现棋盘/卡牌逻辑。
    /// </summary>
    public abstract class GameSessionBase : MonoBehaviour, IGameSession
    {
        public abstract GameModeType Mode { get; }
        public bool IsPaused { get; private set; }
        public virtual bool HasNextLevel => false;
        public int LevelIndex { get; protected set; }
        public bool IsEndless { get; protected set; }

        /// <summary>本局是每日挑战：关卡数据来自当天抽中的正式关，但不把「下一关」接到后面的正式关。</summary>
        public bool IsDaily { get; protected set; }

        public int Wave { get; protected set; } = 1;

        /// <summary>本局是教学关（第 0 关）：不计星级、不计解锁、失败不弹续关。</summary>
        public bool IsTutorial { get; protected set; }

        protected RectTransform Root { get; private set; }
        protected int ContinuesUsed { get; private set; }
        protected string LastLoseMessage { get; private set; }
        protected virtual int CurrentLevelId => IsTutorial
            ? TutorialCatalog.LevelId
            : IsEndless ? EndlessCatalog.LevelId : LevelIndex + 1;

        public void Attach(RectTransform root)
        {
            Root = root;
        }

        public void JumpToLevel(int index)
        {
            LevelIndex = Mathf.Max(0, index);
            IsEndless = EndlessCatalog.IsIndex(Mode, LevelIndex);
            IsTutorial = false;
            IsDaily = false;
            Wave = 1;
        }

        /// <summary>
        /// 切到教学关。LevelIndex 故意取 -1：这样「下一关」走 LevelIndex+1 正好落到正式第 1 关，
        /// 不必为教学关单独写一套跳转。关卡数据由 TutorialCatalog 提供，不走正式关目录。
        /// </summary>
        public void JumpToTutorial()
        {
            LevelIndex = -1;
            IsEndless = false;
            IsTutorial = true;
            IsDaily = false;
            Wave = 1;
        }

        /// <summary>
        /// 切到今日挑战。LevelIndex 换成当天抽中的正式关，关卡数据仍走正式目录。
        /// IsDaily 挡住「下一关」，选关高亮留在每日格子上。
        /// </summary>
        public void JumpToDaily()
        {
            LevelIndex = DailyCatalog.TodayIndex(Mode);
            IsEndless = false;
            IsTutorial = false;
            IsDaily = true;
            Wave = 1;
        }

        /// <summary>每日挑战再来一局：换成另一关，进关名和局内标题会跟着新关卡走。</summary>
        public void RollNextDaily()
        {
            if (!IsDaily)
                return;
            LevelIndex = DailyCatalog.NextIndex(Mode, LevelIndex);
        }

        public virtual void StartSession()
        {
            IsPaused = false;
            ContinuesUsed = 0;
            LastLoseMessage = null;
            gameObject.SetActive(true);
            OnStart();
            RefreshHud();
        }

        public virtual void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            OnPauseChanged(true);
            RefreshHud();
        }

        public virtual void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            OnPauseChanged(false);
            RefreshHud();
        }

        public virtual void Restart()
        {
            IsPaused = false;
            ContinuesUsed = 0;
            LastLoseMessage = null;
            if (IsEndless)
                Wave = 1;
            ClearRoot();
            OnRestart();
            RefreshHud();
        }

        public virtual void StartNextLevel()
        {
        }

        protected void ClearRoot()
        {
            if (Root == null)
                return;

            for (var i = Root.childCount - 1; i >= 0; i--)
                Destroy(Root.GetChild(i).gameObject);
        }

        public virtual void QuitToMenu()
        {
            GameManager.Instance.ReturnToMenu();
        }

        public virtual void Dispose()
        {
            LevelIntroPanel.Dismiss();
            // 引导覆盖层挂在 Popup 层，不撤掉会挡住后面的结算面板。
            TutorialDirector.Stop();
            OnDispose();
            AudioManager.Instance?.PlayDefaultBgm();
            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        protected virtual void OnStart()
        {
        }

        protected virtual void OnRestart()
        {
            OnStart();
        }

        protected virtual void OnPauseChanged(bool paused)
        {
        }

        protected virtual void OnDispose()
        {
        }

        protected virtual void RefreshHud()
        {
            var hud = UIManager.Instance.Get<GameHudPanel>(UIPanelId.GameHud);
            if (hud == null)
                return;

            var info = GameModeCatalog.Get(Mode);
            hud.SetTitle(info.Title);
            hud.SetStatus(GetStatusText());
        }

        /// <summary>进关名牌上的关序号。教学关是第 0 关，其余用关卡自己的 LevelId。</summary>
        protected int IntroLevelNumber(int levelId)
        {
            if (IsTutorial)
                return 0;
            return levelId > 0 ? levelId : Mathf.Max(1, LevelIndex + 1);
        }

        protected virtual string GetStatusText()
        {
            return "准备开始";
        }

        protected void OfferContinue(string message)
        {
            LastLoseMessage = message;
            // 教学关不弹续关：新手不该在这里被收费面板拦一下，直接重来更顺。
            if (IsTutorial)
            {
                ReportResult(false, message, CurrentLevelId);
                return;
            }

            if (ContinuesUsed >= ContinueCatalog.MaxPerAttempt)
            {
                // 续关次数用尽：还有激励视频就再给一次「看广告复活」，没有才结算。
                if (AdDirector.RewardedReady)
                {
                    Analytics.ContinueShow(Mode, CurrentLevelId, message);
                    UIManager.Instance.Open(UIPanelId.Continue, new ContinuePayload
                    {
                        Mode = Mode,
                        Message = message,
                        Price = 0,
                        Benefit = ContinueCatalog.Benefit(Mode),
                        ContinuesLeft = 0,
                        UseAd = true
                    });
                    return;
                }

                ReportResult(false, message, CurrentLevelId);
                return;
            }

            Analytics.ContinueShow(Mode, CurrentLevelId, message);
            UIManager.Instance.Open(UIPanelId.Continue, new ContinuePayload
            {
                Mode = Mode,
                Message = message,
                Price = ContinueCatalog.Price(Mode),
                Benefit = ContinueCatalog.Benefit(Mode),
                ContinuesLeft = ContinueCatalog.MaxPerAttempt - ContinuesUsed
            });
        }

        public bool TryContinue()
        {
            var price = ContinueCatalog.Price(Mode);
            if (CoinWallet.Coins < price)
                return false;

            CoinWallet.AddCoins(-price);
            ContinuesUsed++;
            Analytics.ContinuePay(Mode, price);
            ApplyContinue();
            return true;
        }

        /// <summary>
        /// 看广告复活。不扣金币，但同样计入续关次数——降星规则看的是 ContinuesUsed，
        /// 不计的话可以靠刷广告保住三星。
        /// </summary>
        public void AdContinue()
        {
            ContinuesUsed++;
            Analytics.ContinueAd(Mode);
            ApplyContinue();
        }

        public void GiveUp()
        {
            Analytics.ContinueGiveUp(Mode, CurrentLevelId);
            ReportResult(false, LastLoseMessage, CurrentLevelId);
        }

        protected virtual void ApplyContinue()
        {
        }

        protected string EndlessTitle => EndlessCatalog.Title(Wave);

        protected void AnnounceWave()
        {
            UIManager.Instance?.ShowToast(EndlessTitle);
        }

        /// <summary>
        /// 会话收尾时把本局统计报给每日任务（得分、特效数等）。默认不上报，玩法按需重写。
        /// 通关计数由 ReportResult 统一处理，玩法只要关心自己的特色指标。
        /// </summary>
        protected virtual void ReportDailyStats()
        {
        }

        /// <summary>
        /// 通关评级。只在 ReportResult 确认通关后调用，玩法不用自己判输赢。
        /// 默认 1 星；子类按各自的资源口径（剩余步数 / 剩余时间 / 槽位空余）给到 3 星。
        /// </summary>
        protected virtual int ComputeStars()
        {
            return 1;
        }

        /// <summary>三星条件文案，失败和未满星时给玩家看。玩法按自己的评星口径重写。</summary>
        protected virtual string StarConditionText()
        {
            return string.Empty;
        }

        /// <summary>本局在评星口径上的实际达成值。</summary>
        protected virtual string StarProgressText()
        {
            return string.Empty;
        }

        /// <summary>
        /// 每关首次拿到三星：体力回满。同一关重复打三星不再发，靠 HasPerfectReward 去重。
        /// 必须在 RecordStars 之前调用（要拿旧纪录做「首次」判断）。
        /// </summary>
        private bool TryGrantPerfectReward(int levelId, int stars)
        {
            if (stars < StarCatalog.MaxStars)
                return false;
            if (ProgressRepository.Stars(Mode, levelId) >= StarCatalog.MaxStars)
                return false;
            if (ProgressRepository.HasPerfectReward(Mode, levelId))
                return false;

            ProgressRepository.MarkPerfectReward(Mode, levelId);
            var lack = EnergyCatalog.Max - EnergyRepository.Energy;
            if (lack > 0)
                EnergyRepository.Add(lack);
            UIManager.Instance?.ShowToast("首次满星，体力已回满");
            return true;
        }

        protected void ReportResult(bool isWin, string message = null, int levelId = 1)
        {
            // 结算面板要能点，先把引导覆盖层撤掉（没在引导中时是无操作）。
            TutorialDirector.Stop();
            ReportDailyStats();
            if (isWin)
                DailyTaskDirector.ReportClear(Mode);

            if (IsEndless)
            {
                ReportEndlessFail(message);
                return;
            }

            if (IsTutorial)
            {
                ReportTutorialResult(isWin, message);
                return;
            }

            var coins = 0;
            var first = false;
            var stars = 0;
            var perfect = false;
            if (isWin)
            {
                // 每日挑战的金币、满星体力和道具一天只发一次。星级和解锁仍按这一关记。
                var pay = !IsDaily || !ProgressRepository.HasDailyReward(Mode);
                first = ProgressRepository.MarkCleared(Mode, levelId, IsDaily);
                stars = Mathf.Clamp(ComputeStars(), 0, StarCatalog.MaxStars);
                // 用过续关就不给满级：金币能救命，但买不到评级。保底仍留 1 星。
                if (ContinuesUsed > 0 && stars > 1)
                    stars--;

                // 先取旧纪录再写新纪录：RecordStars 只升不降，写完就分不清是不是首次了。
                if (pay)
                    perfect = TryGrantPerfectReward(levelId, stars);
                ProgressRepository.RecordStars(Mode, levelId, stars);
                if (pay)
                {
                    coins = first ? ShopCatalog.CoinsForClear(levelId) : ShopCatalog.ReplayClearCoins;
                    if (coins > 0)
                        CoinWallet.AddCoins(coins);
                    CoinWallet.GrantItem(ShopCatalog.RandomItem(Mode), 1);
                    if (IsDaily)
                        ProgressRepository.MarkDailyReward(Mode);
                }
            }
            else
            {
                if (!IsDaily)
                    ProgressRepository.SetLastPlayed(Mode, levelId);
            }

            if (isWin)
                Analytics.LevelWin(Mode, levelId, coins, first);
            else
                Analytics.LevelFail(Mode, levelId, message);

            var payload = new ResultPayload
            {
                IsWin = isWin,
                Mode = Mode,
                Message = message,
                HasNextLevel = isWin && HasNextLevel,
                CoinsGained = coins,
                FirstClear = first,
                Stars = stars,
                StarCondition = StarConditionText(),
                StarProgress = StarProgressText(),
                PerfectReward = perfect,
                IsDaily = IsDaily
            };
            UIManager.Instance.Open(UIPanelId.Result, payload);
        }

        protected void ReportEndlessFail(string message)
        {
            var wave = Mathf.Max(1, Wave);
            ProgressRepository.SetLastPlayed(Mode, EndlessCatalog.LevelId);
            var record = ProgressRepository.RecordEndlessBest(Mode, wave);
            var coins = EndlessCatalog.CoinsForWaves(wave);
            if (coins > 0)
                CoinWallet.AddCoins(coins);

            Analytics.LevelFail(Mode, EndlessCatalog.LevelId, $"wave:{wave} {message}");
            var body = record
                ? $"坚持了 {wave} 波 · 新纪录"
                : $"坚持了 {wave} 波";
            if (!string.IsNullOrEmpty(message))
                body += $"\n{message}";

            UIManager.Instance.Open(UIPanelId.Result, new ResultPayload
            {
                IsWin = false,
                Mode = Mode,
                Message = body,
                HasNextLevel = false,
                CoinsGained = coins,
                FirstClear = false,
                IsEndless = true,
                Wave = wave
            });
        }

        /// <summary>
        /// 教学关结算：只记「学会了没」，不记星级、不动解锁进度。
        /// 通关后 HasNextLevel 给 true，结算页的「下一关」正好接到正式第 1 关。
        /// </summary>
        private void ReportTutorialResult(bool isWin, string message)
        {
            var coins = 0;
            var learned = false;
            if (isWin)
            {
                learned = !TutorialCatalog.IsCleared(Mode);
                TutorialCatalog.MarkCleared(Mode);
                coins = ShopCatalog.ReplayClearCoins;
                if (coins > 0)
                    CoinWallet.AddCoins(coins);
                CoinWallet.GrantItem(ShopCatalog.RandomItem(Mode), 1);
            }

            UIManager.Instance.Open(UIPanelId.Result, new ResultPayload
            {
                IsWin = isWin,
                Mode = Mode,
                Message = message,
                HasNextLevel = isWin,
                CoinsGained = coins,
                FirstClear = learned,
                IsTutorial = true,
                Stars = 0
            });
        }
    }
}
