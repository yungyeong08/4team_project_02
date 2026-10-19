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
}

/// <summary>Connects the existing fishing, shop, inventory, and wallet systems into the first-area progression.</summary>
public sealed class EarlyGameQuestSystem : MonoBehaviour
{
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
    private string toast = "";
    private float toastTimer;

    public event Action ProgressChanged;

    public EarlyTutorialStep TutorialStep => tutorialStep;
    public EarlyQuestDefinition ActiveQuest => activeQuestIndex >= 0 && activeQuestIndex < quests.Count ? quests[activeQuestIndex] : null;
    public int QuestProgress => questProgress;
    public bool NextRegionReady => nextRegionReady;
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
        if (toastTimer > 0f) toastTimer -= Time.unscaledDeltaTime;
        if (player == null || tutorialStep == EarlyTutorialStep.Complete) return;

        if (tutorialStep == EarlyTutorialStep.Move && Vector2.Distance(movementStart, player.position) >= .8f)
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
            AdvanceTutorial(EarlyTutorialStep.OpenInventory);

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
            AdvanceTutorial(EarlyTutorialStep.ReachRodShop);
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
        toast = $"퀘스트 완료  +{completed.RewardGold:N0} G";
        toastTimer = 3.5f;

        int nextIndex = string.IsNullOrWhiteSpace(completed.NextQuestId)
            ? -1
            : quests.FindIndex(candidate => candidate.Id == completed.NextQuestId);
        activeQuestIndex = nextIndex;
        questProgress = 0;
        if (activeQuestIndex < 0)
        {
            nextRegionReady = true;
            toast = "첫 지역 완료! 더 좋은 낚시터에 갈 준비가 되었습니다.";
            toastTimer = 6f;
        }
        ProgressChanged?.Invoke();
    }

    private void AdvanceTutorial(EarlyTutorialStep next)
    {
        if (next <= tutorialStep) return;
        tutorialStep = next;
        ProgressChanged?.Invoke();
    }

    public EarlyGameProgressSaveData CreateSnapshot()
    {
        return new EarlyGameProgressSaveData
        {
            TutorialStep = (int)tutorialStep,
            ActiveQuestId = ActiveQuest?.Id ?? "",
            QuestProgress = questProgress,
            CompletedQuestIds = new List<string>(completedQuestIds),
            NextRegionReady = nextRegionReady
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
        nextRegionReady = saved.NextRegionReady || completedQuestIds.Contains("use_new_rod");
        movementStart = player != null ? (Vector2)player.position : Vector2.zero;
        ProgressChanged?.Invoke();
    }

    public void ResetProgress()
    {
        tutorialStep = EarlyTutorialStep.Move;
        activeQuestIndex = quests.Count > 0 ? 0 : -1;
        questProgress = 0;
        completedQuestIds.Clear();
        nextRegionReady = false;
        movementStart = player != null ? (Vector2)player.position : Vector2.zero;
        ProgressChanged?.Invoke();
    }

    private string TutorialMessage => tutorialStep switch
    {
        EarlyTutorialStep.Move => "WASD 또는 방향키로 이동해보세요.",
        EarlyTutorialStep.ReachFirstLake => "왼쪽 길을 따라 첫 번째 호수로 이동하세요.",
        EarlyTutorialStep.StartFishing => "초록 표식 근처에서 F키로 낚시를 시작하세요.",
        EarlyTutorialStep.CatchFirstFish => "SPACE를 눌렀다 놓으며 물고기를 흰색 바 안에 유지하세요.",
        EarlyTutorialStep.OpenInventory => "I키를 눌러 잡은 물고기를 확인하세요.",
        EarlyTutorialStep.ReachFishMarket => "마을의 물고기 판매소로 이동하세요.",
        EarlyTutorialStep.SellFish => "판매소에서 F키를 누르고 물고기를 판매하세요.",
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

        if (tutorialStep != EarlyTutorialStep.Complete)
        {
            Rect tutorial = new(18f, 18f, Mathf.Min(530f, width - 36f), 70f);
            FishingUiTheme.DrawPanel(tutorial, true);
            GUI.Label(new Rect(tutorial.x + 14f, tutorial.y + 6f, tutorial.width - 28f, 22f), "튜토리얼", FishingUiTheme.Small);
            GUI.Label(new Rect(tutorial.x + 14f, tutorial.y + 27f, tutorial.width - 28f, 34f), TutorialMessage, FishingUiTheme.Body);
        }

        Rect questPanel = new(width - 368f, 18f, 350f, ActiveQuest != null ? 112f : 90f);
        FishingUiTheme.DrawPanel(questPanel, true);
        if (ActiveQuest != null)
        {
            GUI.Label(new Rect(questPanel.x + 14f, questPanel.y + 8f, questPanel.width - 28f, 28f), ActiveQuest.Title, FishingUiTheme.Heading);
            GUI.Label(new Rect(questPanel.x + 14f, questPanel.y + 37f, questPanel.width - 28f, 28f), ActiveQuest.Description, FishingUiTheme.Small);
            FishingUiTheme.DrawBar(new Rect(questPanel.x + 14f, questPanel.y + 74f, questPanel.width - 28f, 25f),
                (float)questProgress / ActiveQuest.TargetAmount, FishingUiTheme.AccentBright,
                $"{questProgress} / {ActiveQuest.TargetAmount}    보상 {ActiveQuest.RewardGold:N0} G");
        }
        else
        {
            GUI.Label(new Rect(questPanel.x + 14f, questPanel.y + 10f, questPanel.width - 28f, 27f), "첫 지역 퀘스트 완료", FishingUiTheme.Heading);
            GUI.Label(new Rect(questPanel.x + 14f, questPanel.y + 40f, questPanel.width - 28f, 34f),
                "더 좋은 낚시터에 갈 준비가 되었습니다.", FishingUiTheme.Small);
        }

        if (toastTimer > 0f && !string.IsNullOrWhiteSpace(toast))
            FishingUiTheme.DrawNotice(new Rect((width - 620f) * .5f, 88f, 620f, 48f), toast, FishingUiTheme.Gold);
        GUI.matrix = previous;
    }
}
