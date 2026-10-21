using System;
using System.Collections.Generic;
using UnityEngine;

public enum EarlyTutorialStep
{
    Move,
    ReachFirstLake,
    StartFishing,
    CatchFirstFish,
    OpenInventory,
    ReachFishMarket,
    SellFish,
    ReachRodShop,
    BuyUpgradeRod,
    Complete
}

public enum EarlyQuestGoal
{
    CatchAtFirstLake,
    SellFish,
    PurchaseRod,
    CatchWithUpgradedRod
}

[Serializable]
public sealed class EarlyQuestDefinition
{
    public string Id;
    public string Title;
    public string Description;
    public EarlyQuestGoal Goal;
    public int TargetAmount;
    public int RewardGold;
    public string RequiredId;
    public string NextQuestId;

    public EarlyQuestDefinition(string id, string title, string description, EarlyQuestGoal goal,
        int targetAmount, int rewardGold, string requiredId, string nextQuestId)
    {
        Id = id;
        Title = title;
        Description = description;
        Goal = goal;
        TargetAmount = Mathf.Max(1, targetAmount);
        RewardGold = Mathf.Max(0, rewardGold);
        RequiredId = requiredId;
        NextQuestId = nextQuestId;
    }
}

[Serializable]
public sealed class EarlyGameProgressSaveData
{
    public int TutorialStep;
    public string ActiveQuestId = "first_catch";
    public int QuestProgress;
    public List<string> CompletedQuestIds = new();
    public bool NextRegionReady;
    public bool OpeningSeen;
}

/// <summary>Connects the existing fishing, shop, inventory, and wallet systems into the first-area progression.</summary>
public sealed class EarlyGameQuestSystem : MonoBehaviour
{
    private enum CenterNoticeKind { Objective, Information, Completion }

    private sealed class CenterNotice
    {
        public CenterNoticeKind Kind;
        public string Title;
        public string Message;
        public int Reward;
        public float Duration;
    }

    public const string FirstLakeId = "village_pond";
    public const string UpgradeRodId = "sturdy_rod";

    private readonly List<EarlyQuestDefinition> quests = new();
    private readonly List<string> completedQuestIds = new();
    private Transform player;
    private Transform fishMarket;
    private Transform rodShop;
    private PlayerWallet wallet;
    private FishingSpotDefinition firstLake;
    private Vector2 movementStart;
    private EarlyTutorialStep tutorialStep;
    private int activeQuestIndex;
    private int questProgress;
    private bool nextRegionReady;
    private Func<bool> uiBlocked;
    private readonly Queue<CenterNotice> completionNotices = new();
    private readonly Queue<CenterNotice> objectiveNotices = new();
    private readonly Queue<CenterNotice> informationNotices = new();
    private CenterNotice activeNotice;
    private float activeNoticeTimer;
    private float activeNoticeDuration;
    private float centerNoticeDelay;
    private bool openingSeen;

    public event Action ProgressChanged;
    public event Action<string, int> QuestCompleted;
    public event Action TutorialAdvanced;

    public EarlyTutorialStep TutorialStep => tutorialStep;
    public EarlyQuestDefinition ActiveQuest => activeQuestIndex >= 0 && activeQuestIndex < quests.Count ? quests[activeQuestIndex] : null;
    public int QuestProgress => questProgress;
    public bool NextRegionReady => nextRegionReady;
    public bool OpeningSeen => openingSeen;
    public IReadOnlyList<EarlyQuestDefinition> Quests => quests;
    public IReadOnlyList<string> CompletedQuestIds => completedQuestIds;

    public void Setup(Transform playerTransform, PlayerWallet playerWallet, FishingSpotDefinition beginnerLake,
        Transform marketTarget, Transform shopTarget)
    {
        player = playerTransform;
        wallet = playerWallet;
        firstLake = beginnerLake;
        fishMarket = marketTarget;
        rodShop = shopTarget;
        movementStart = player != null ? (Vector2)player.position : Vector2.zero;
        BuildQuestLine();
        ClearCenterNotices();
    }

    public void SetUiBlocker(Func<bool> blocker) => uiBlocked = blocker;

    private void BuildQuestLine()
    {
        quests.Clear();
        EarlyGameQuestConfig config = Resources.Load<EarlyGameQuestConfig>("Data/EarlyGameQuestConfig");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<EarlyGameQuestConfig>();
            config.ApplyDefaults();
        }
        else config.EnsureValid();
        if (config.Quests != null) quests.AddRange(config.Quests);
    }

    private void Update()
    {
        bool visualsBlocked = uiBlocked != null && uiBlocked();
        if (!visualsBlocked) UpdateCenterNoticeQueue();
        if (player == null || tutorialStep == EarlyTutorialStep.Complete) return;

        if (tutorialStep == EarlyTutorialStep.Move && Vector2.Distance(movementStart, player.position) >= 1.35f)
            AdvanceTutorial(EarlyTutorialStep.ReachFirstLake);
        else if (tutorialStep == EarlyTutorialStep.ReachFirstLake && firstLake != null &&
                 Vector2.Distance(player.position, firstLake.ShorePosition) <= 2.6f)
            AdvanceTutorial(EarlyTutorialStep.StartFishing);
        else if (tutorialStep == EarlyTutorialStep.ReachFishMarket && fishMarket != null &&
                 Vector2.Distance(player.position, fishMarket.position) <= 2.5f)
            AdvanceTutorial(EarlyTutorialStep.SellFish);
        else if (tutorialStep == EarlyTutorialStep.ReachRodShop && rodShop != null &&
                 Vector2.Distance(player.position, rodShop.position) <= 2.5f)
            AdvanceTutorial(EarlyTutorialStep.BuyUpgradeRod);
    }

    public void NotifyFishingStarted(string spotId)
    {
        if (tutorialStep == EarlyTutorialStep.StartFishing && spotId == FirstLakeId)
            AdvanceTutorial(EarlyTutorialStep.CatchFirstFish);
    }

    public void NotifyFishCaught(string spotId, string rodId)
    {
        if (tutorialStep == EarlyTutorialStep.CatchFirstFish)
            AdvanceTutorial(EarlyTutorialStep.ReachFishMarket);

        EarlyQuestDefinition quest = ActiveQuest;
        if (quest == null) return;
        bool matches = quest.Goal == EarlyQuestGoal.CatchAtFirstLake && spotId == quest.RequiredId;
        if (quest.Goal == EarlyQuestGoal.CatchWithUpgradedRod)
            matches = spotId == quest.RequiredId && !string.IsNullOrWhiteSpace(rodId) && rodId != FishingRodCatalog.DefaultRodId;
        if (matches) AddQuestProgress(1);
    }

    public void NotifyInventoryOpened()
    {
        if (tutorialStep == EarlyTutorialStep.OpenInventory)
            AdvanceTutorial(EarlyTutorialStep.ReachFishMarket);
    }

    public void NotifyFishSold(int soldCount)
    {
        if (soldCount <= 0) return;
        if (tutorialStep == EarlyTutorialStep.SellFish)
        {
            AdvanceTutorial(EarlyTutorialStep.ReachRodShop);
        }
        if (ActiveQuest?.Goal == EarlyQuestGoal.SellFish) AddQuestProgress(1);
    }

    public void NotifyRodPurchased(string rodId)
    {
        if (string.IsNullOrWhiteSpace(rodId)) return;
        if (tutorialStep == EarlyTutorialStep.BuyUpgradeRod && rodId == UpgradeRodId)
            AdvanceTutorial(EarlyTutorialStep.Complete);
        EarlyQuestDefinition quest = ActiveQuest;
        if (quest != null && quest.Goal == EarlyQuestGoal.PurchaseRod && rodId == quest.RequiredId)
            AddQuestProgress(1);
    }

    private void AddQuestProgress(int amount)
    {
        EarlyQuestDefinition quest = ActiveQuest;
        if (quest == null || amount <= 0) return;
        questProgress = Mathf.Clamp(questProgress + amount, 0, quest.TargetAmount);
        ProgressChanged?.Invoke();
        if (questProgress >= quest.TargetAmount) CompleteActiveQuest();
    }

    private void CompleteActiveQuest()
    {
        EarlyQuestDefinition completed = ActiveQuest;
        if (completed == null) return;
        if (!completedQuestIds.Contains(completed.Id)) completedQuestIds.Add(completed.Id);
        wallet?.AddMoney(completed.RewardGold);
        QuestCompleted?.Invoke(completed.Title, completed.RewardGold);

        int nextIndex = string.IsNullOrWhiteSpace(completed.NextQuestId)
            ? -1
            : quests.FindIndex(candidate => candidate.Id == completed.NextQuestId);
        activeQuestIndex = nextIndex;
        questProgress = 0;
        if (activeQuestIndex < 0)
        {
            nextRegionReady = true;
            EnqueueCompletion("첫걸음 완료", "낚시와 판매 방법을 익혔습니다.", completed.RewardGold, 1.6f);
        }
        else EnqueueCompletion("퀘스트 완료", completed.Title, completed.RewardGold, 1.45f);
        ProgressChanged?.Invoke();
    }

    private void AdvanceTutorial(EarlyTutorialStep next)
    {
        if (next <= tutorialStep) return;
        tutorialStep = next;
        EnqueueObjective(TutorialMessage, .3f);
        TutorialAdvanced?.Invoke();
        ProgressChanged?.Invoke();
    }

    public void MarkOpeningSeen()
    {
        if (openingSeen) return;
        openingSeen = true;
        EnqueueObjective(TutorialMessage, .35f);
        ProgressChanged?.Invoke();
    }

    private void QueueSystemMessages(params string[] messages)
    {
        informationNotices.Clear();
        if (messages != null)
            foreach (string message in messages)
                if (!string.IsNullOrWhiteSpace(message)) informationNotices.Enqueue(new CenterNotice
                {
                    Kind = CenterNoticeKind.Information,
                    Title = "안내",
                    Message = message,
                    Duration = 1.45f
                });
        if (activeNotice == null) centerNoticeDelay = Mathf.Max(centerNoticeDelay, .2f);
    }

    private void EnqueueObjective(string message, float delay)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        objectiveNotices.Clear();
        objectiveNotices.Enqueue(new CenterNotice
        {
            Kind = CenterNoticeKind.Objective,
            Title = "새로운 목표",
            Message = message,
            Duration = 1.35f
        });
        if (activeNotice == null) centerNoticeDelay = Mathf.Max(centerNoticeDelay, delay);
    }

    private void EnqueueCompletion(string title, string message, int reward, float duration)
    {
        completionNotices.Enqueue(new CenterNotice
        {
            Kind = CenterNoticeKind.Completion,
            Title = title,
            Message = message,
            Reward = reward,
            Duration = duration
        });
        if (activeNotice == null) centerNoticeDelay = Mathf.Max(centerNoticeDelay, .2f);
    }

    private void UpdateCenterNoticeQueue()
    {
        float delta = Time.unscaledDeltaTime;
        if (activeNotice != null)
        {
            activeNoticeTimer -= delta;
            if (activeNoticeTimer <= 0f)
            {
                activeNotice = null;
                activeNoticeTimer = 0f;
                centerNoticeDelay = .3f;
            }
            return;
        }

        if (centerNoticeDelay > 0f)
        {
            centerNoticeDelay -= delta;
            return;
        }

        if (completionNotices.Count > 0) activeNotice = completionNotices.Dequeue();
        else if (objectiveNotices.Count > 0) activeNotice = objectiveNotices.Dequeue();
        else if (informationNotices.Count > 0) activeNotice = informationNotices.Dequeue();
        if (activeNotice == null) return;
        activeNoticeDuration = Mathf.Max(.6f, activeNotice.Duration);
        activeNoticeTimer = activeNoticeDuration;
    }

    private void ClearCenterNotices()
    {
        completionNotices.Clear();
        objectiveNotices.Clear();
        informationNotices.Clear();
        activeNotice = null;
        activeNoticeTimer = 0f;
        activeNoticeDuration = 0f;
        centerNoticeDelay = 0f;
    }

    public EarlyGameProgressSaveData CreateSnapshot()
    {
        return new EarlyGameProgressSaveData
        {
            TutorialStep = (int)tutorialStep,
            ActiveQuestId = ActiveQuest?.Id ?? "",
            QuestProgress = questProgress,
            CompletedQuestIds = new List<string>(completedQuestIds),
            NextRegionReady = nextRegionReady,
            OpeningSeen = openingSeen
        };
    }

    public void Restore(EarlyGameProgressSaveData saved)
    {
        if (quests.Count == 0) BuildQuestLine();
        completedQuestIds.Clear();
        if (saved == null)
        {
            ResetProgress();
            return;
        }

        tutorialStep = (EarlyTutorialStep)Mathf.Clamp(saved.TutorialStep, 0, (int)EarlyTutorialStep.Complete);
        if (saved.CompletedQuestIds != null)
            foreach (string id in saved.CompletedQuestIds)
                if (!string.IsNullOrWhiteSpace(id) && quests.Exists(candidate => candidate.Id == id) && !completedQuestIds.Contains(id))
                    completedQuestIds.Add(id);

        activeQuestIndex = string.IsNullOrWhiteSpace(saved.ActiveQuestId)
            ? -1
            : quests.FindIndex(candidate => candidate.Id == saved.ActiveQuestId);
        questProgress = ActiveQuest != null ? Mathf.Clamp(saved.QuestProgress, 0, ActiveQuest.TargetAmount) : 0;
        nextRegionReady = saved.NextRegionReady ||
            (activeQuestIndex < 0 && completedQuestIds.Contains("use_new_rod"));
        openingSeen = saved.OpeningSeen || tutorialStep > EarlyTutorialStep.Move || completedQuestIds.Count > 0;
        movementStart = player != null ? (Vector2)player.position : Vector2.zero;
        ClearCenterNotices();
        ProgressChanged?.Invoke();
    }

    public void ResetProgress()
    {
        tutorialStep = EarlyTutorialStep.Move;
        activeQuestIndex = quests.Count > 0 ? 0 : -1;
        questProgress = 0;
        completedQuestIds.Clear();
        nextRegionReady = false;
        openingSeen = false;
        movementStart = player != null ? (Vector2)player.position : Vector2.zero;
        ClearCenterNotices();
        ProgressChanged?.Invoke();
    }

    private string TutorialMessage => tutorialStep switch
    {
        EarlyTutorialStep.Move => "마을을 둘러보세요.",
        EarlyTutorialStep.ReachFirstLake => "연못으로 이동하세요.",
        EarlyTutorialStep.StartFishing => "낚시를 시작하세요.",
        EarlyTutorialStep.CatchFirstFish => "첫 물고기를 잡아보세요.",
        EarlyTutorialStep.OpenInventory => "잡은 물고기를 확인하세요.",
        EarlyTutorialStep.ReachFishMarket => "물고기 판매소로 이동하세요.",
        EarlyTutorialStep.SellFish => "잡은 물고기를 판매하세요.",
        EarlyTutorialStep.ReachRodShop => "낚시 상점으로 이동하세요.",
        EarlyTutorialStep.BuyUpgradeRod => "튼튼한 낚싯대를 구매하세요.",
        _ => "튜토리얼 완료 · 현재 퀘스트를 진행하세요."
    };

    private void OnGUI()
    {
        if (uiBlocked != null && uiBlocked()) return;
        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        FishingUiTheme.Ensure();

        FishingHudSettings hud = FishingUiTheme.Settings;
        float sideWidth = Mathf.Min(hud.SidePanelWidth, (width - hud.ScreenMargin * 4f) * .25f);

        if (!CenterNoticeSequenceActive) DrawQuestPanel(width, sideWidth, hud);
        DrawCenterNotice(width, hud);
        DrawWorldMarker(scale);
        GUI.matrix = previous;
    }

    private bool CenterNoticeSequenceActive => activeNotice != null || centerNoticeDelay > 0f ||
        completionNotices.Count > 0 || objectiveNotices.Count > 0 || informationNotices.Count > 0;

    private void DrawQuestPanel(float width, float sideWidth, FishingHudSettings hud)
    {
        Rect questPanel = new(width - hud.ScreenMargin - sideWidth, hud.ScreenMargin, sideWidth, hud.TopPanelHeight);
        FishingUiTheme.DrawHudPanel(questPanel,
            tutorialStep != EarlyTutorialStep.Complete ? "현재 목표" : "현재 퀘스트", FishingUiTheme.AccentBright);
        if (tutorialStep != EarlyTutorialStep.Complete)
        {
            GUI.Label(new Rect(questPanel.x + 16f, questPanel.y + 31f, questPanel.width - 32f, 48f),
                CondensedTutorialMessage, FishingUiTheme.Heading);
            GUI.Label(new Rect(questPanel.x + 16f, questPanel.yMax - 31f, questPanel.width - 32f, 22f),
                "첫 플레이 가이드", FishingUiTheme.Small);
        }
        else if (ActiveQuest != null)
        {
            GUI.Label(new Rect(questPanel.x + 16f, questPanel.y + 27f, questPanel.width - 32f, 26f), ActiveQuest.Title, FishingUiTheme.Heading);
            GUI.Label(new Rect(questPanel.x + 16f, questPanel.y + 52f, questPanel.width - 32f, 24f), ActiveQuest.Description, FishingUiTheme.Small);
            FishingUiTheme.DrawBar(new Rect(questPanel.x + 16f, questPanel.yMax - 34f, questPanel.width - 32f, 24f),
                (float)questProgress / ActiveQuest.TargetAmount, FishingUiTheme.AccentBright,
                $"진행 {questProgress} / {ActiveQuest.TargetAmount}   ·   보상 {ActiveQuest.RewardGold:N0} G");
        }
        else
        {
            GUI.Label(new Rect(questPanel.x + 16f, questPanel.y + 31f, questPanel.width - 32f, 27f), "첫 지역 퀘스트 완료", FishingUiTheme.Heading);
            GUI.Label(new Rect(questPanel.x + 16f, questPanel.y + 59f, questPanel.width - 32f, 26f),
                "더 좋은 낚시터에 갈 준비가 되었습니다.", FishingUiTheme.Small);
        }
    }

    private string CondensedTutorialMessage => tutorialStep switch
    {
        EarlyTutorialStep.Move => "마을을 둘러보기",
        EarlyTutorialStep.ReachFirstLake => "첫 연못으로 이동",
        EarlyTutorialStep.StartFishing => "연못에서 낚시 시작",
        EarlyTutorialStep.CatchFirstFish => "첫 물고기 낚기",
        EarlyTutorialStep.OpenInventory => "잡은 물고기 확인",
        EarlyTutorialStep.ReachFishMarket => "판매소로 이동",
        EarlyTutorialStep.SellFish => "물고기 판매하기",
        EarlyTutorialStep.ReachRodShop => "낚시 상점으로 이동",
        EarlyTutorialStep.BuyUpgradeRod => "튼튼한 낚싯대 구매",
        _ => "자유롭게 낚시하기"
    };

    private void DrawCenterNotice(float width, FishingHudSettings hud)
    {
        if (activeNotice == null || activeNoticeTimer <= 0f) return;
        float elapsed = activeNoticeDuration - activeNoticeTimer;
        float alpha = Mathf.Clamp01(Mathf.Min(elapsed / .2f, activeNoticeTimer / .22f));
        float yOffset = Mathf.Lerp(-12f, 0f, Mathf.Clamp01(elapsed / .2f));
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        if (activeNotice.Kind == CenterNoticeKind.Completion)
        {
            Rect card = new((width - 560f) * .5f, 318f + yOffset, 560f, 132f);
            FishingUiTheme.DrawPanel(card);
            FishingUiTheme.ColoredLabel(new Rect(card.x + 24f, card.y + 15f, card.width - 48f, 34f),
                activeNotice.Title, FishingUiTheme.Title, FishingUiTheme.Gold);
            GUI.Label(new Rect(card.x + 24f, card.y + 51f, card.width - 48f, 42f), activeNotice.Message, FishingUiTheme.BodyCentered);
            if (activeNotice.Reward > 0)
                GUI.Label(new Rect(card.x + 24f, card.yMax - 31f, card.width - 48f, 22f),
                    $"보상  +{activeNotice.Reward:N0} G", FishingUiTheme.SmallCentered);
        }
        else
        {
            Rect card = new((width - 520f) * .5f, hud.ScreenMargin + 142f + yOffset, 520f, 72f);
            FishingUiTheme.DrawPanel(card, true);
            FishingUiTheme.ColoredLabel(new Rect(card.x + 18f, card.y + 8f, card.width - 36f, 20f),
                activeNotice.Title, FishingUiTheme.SmallCentered,
                activeNotice.Kind == CenterNoticeKind.Objective ? FishingUiTheme.AccentBright : FishingUiTheme.Gold);
            GUI.Label(new Rect(card.x + 18f, card.y + 29f, card.width - 36f, 34f), activeNotice.Message, FishingUiTheme.BodyCentered);
        }
        GUI.color = old;
    }

    private void DrawWorldMarker(float uiScale)
    {
        Vector3? target = tutorialStep switch
        {
            EarlyTutorialStep.ReachFishMarket or EarlyTutorialStep.SellFish when fishMarket != null => fishMarket.position,
            EarlyTutorialStep.ReachRodShop or EarlyTutorialStep.BuyUpgradeRod when rodShop != null => rodShop.position,
            EarlyTutorialStep.ReachFirstLake or EarlyTutorialStep.StartFishing when firstLake != null => firstLake.ShorePosition,
            _ => null
        };
        Camera camera = Camera.main;
        if (target == null || camera == null) return;
        Vector3 screen = camera.WorldToScreenPoint(target.Value + Vector3.up * .75f);
        if (screen.z <= 0f) return;
        float x = screen.x / uiScale;
        float y = (Screen.height - screen.y) / uiScale;
        float logicalWidth = Screen.width / uiScale;
        float logicalHeight = Screen.height / uiScale;
        x = Mathf.Clamp(x, 34f, logicalWidth - 34f);
        y = Mathf.Clamp(y, 150f, logicalHeight - 82f);
        float bob = Mathf.Sin(Time.unscaledTime * 4f) * 5f;
        FishingUiTheme.ColoredLabel(new Rect(x - 24f, y - 45f + bob, 48f, 36f), "▼",
            FishingUiTheme.Title, FishingUiTheme.Gold);
    }
}
