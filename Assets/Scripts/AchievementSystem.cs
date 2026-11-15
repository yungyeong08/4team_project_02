using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AchievementStateData
{
    public string Id;
    public int Progress;
    public bool Completed;
    public bool RewardGranted;
}

[Serializable]
public sealed class AchievementMetricsData
{
    public int PerfectCatches;
    public int SuccessfulSaleTransactions;
    public int TotalSaleGold;
    public int SellAllTransactions;
    public int FishCaughtAfterFirstRodPurchase;
    public int SuccessfulDebtRepayments;
    public int LargestSingleSale;
    public int ConsecutiveRareCatches;
    public int ConsecutiveSuccessfulCatches;
    public bool AdditionalRodPurchased;
}

[Serializable]
public sealed class AchievementSaveData
{
    public List<AchievementStateData> States = new();
    public AchievementMetricsData Metrics = new();
}

public enum FishingResolution { Success, Failure, Cancelled }

/// <summary>Evaluates the configured achievements from existing gameplay events and grants each reward once.</summary>
public sealed class AchievementSystem : MonoBehaviour
{
    private sealed class Notice
    {
        public string Title;
        public int Reward;
    }

    private AchievementConfig config;
    private PlayerWallet wallet;
    private FishInventory fishInventory;
    private FishingRodInventory rodInventory;
    private PlayerDebt debt;
    private IReadOnlyList<FishDefinition> fishCatalog;
    private readonly Dictionary<string, AchievementStateData> states = new();
    private AchievementMetricsData metrics = new();
    private readonly Queue<Notice> notices = new();
    private Notice activeNotice;
    private float noticeTimer;

    public event Action ProgressChanged;
    public event Action<AchievementDefinition> AchievementCompleted;
    public IReadOnlyList<AchievementDefinition> Definitions => config?.Achievements;
    public AchievementMetricsData Metrics => metrics;

    public void Setup(AchievementConfig achievementConfig, PlayerWallet playerWallet, FishInventory inventory,
        FishingRodInventory rods, PlayerDebt playerDebt, IReadOnlyList<FishDefinition> availableFish)
    {
        config = achievementConfig != null ? achievementConfig : AchievementConfig.LoadOrCreateRuntimeDefault();
        wallet = playerWallet;
        fishInventory = inventory;
        rodInventory = rods;
        debt = playerDebt;
        fishCatalog = availableFish;
        EnsureStates();
        if (fishInventory != null) fishInventory.FishAcquired += OnFishAcquired;
        if (debt != null)
        {
            debt.DebtRepaid += OnDebtRepaid;
            debt.DebtFullyRepaid += OnDebtFullyRepaid;
            debt.GameCleared += OnGameCleared;
        }
    }

    public AchievementStateData GetState(string id)
    {
        states.TryGetValue(id ?? "", out AchievementStateData state);
        return state;
    }

    public void NotifyFishingResolved(FishingResolution result, FishDefinition fish)
    {
        if (result == FishingResolution.Success)
        {
            metrics.ConsecutiveSuccessfulCatches = SafeAdd(metrics.ConsecutiveSuccessfulCatches, 1);
            metrics.ConsecutiveRareCatches = fish != null && fish.Rarity == FishRarity.Rare
                ? SafeAdd(metrics.ConsecutiveRareCatches, 1)
                : 0;
            if (metrics.AdditionalRodPurchased)
                metrics.FishCaughtAfterFirstRodPurchase = SafeAdd(metrics.FishCaughtAfterFirstRodPurchase, 1);
        }
        else if (result == FishingResolution.Failure)
        {
            metrics.ConsecutiveSuccessfulCatches = 0;
            metrics.ConsecutiveRareCatches = 0;
        }
        EvaluateAll(true);
    }

    /// <summary>Hook for a future, explicitly defined PERFECT rule. No current minigame path calls it.</summary>
    public void NotifyPerfectCatch()
    {
        metrics.PerfectCatches = SafeAdd(metrics.PerfectCatches, 1);
        EvaluateAll(true);
    }

    public void NotifySaleCompleted(int soldCount, int earnedGold, bool soldAll)
    {
        if (soldCount <= 0 || earnedGold <= 0) return;
        metrics.SuccessfulSaleTransactions = SafeAdd(metrics.SuccessfulSaleTransactions, 1);
        metrics.TotalSaleGold = SafeAdd(metrics.TotalSaleGold, earnedGold);
        metrics.LargestSingleSale = Mathf.Max(metrics.LargestSingleSale, earnedGold);
        if (soldAll) metrics.SellAllTransactions = SafeAdd(metrics.SellAllTransactions, 1);
        EvaluateAll(true);
    }

    public void NotifyRodPurchased(string rodId)
    {
        if (string.IsNullOrWhiteSpace(rodId) || rodId == FishingRodCatalog.DefaultRodId) return;
        metrics.AdditionalRodPurchased = true;
        EvaluateAll(true);
    }

    private void OnFishAcquired(string fishId, int amount, bool isNewDiscovery) => EvaluateAll(true);

    private void OnDebtRepaid(int amount)
    {
        if (amount <= 0) return;
        metrics.SuccessfulDebtRepayments = SafeAdd(metrics.SuccessfulDebtRepayments, 1);
        EvaluateAll(true);
    }

    private void OnDebtFullyRepaid() => EvaluateAll(true);
    private void OnGameCleared() => EvaluateAll(true);

    private void EnsureStates()
    {
        if (config?.Achievements == null) return;
        foreach (AchievementDefinition definition in config.Achievements)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || states.ContainsKey(definition.Id)) continue;
            states.Add(definition.Id, new AchievementStateData { Id = definition.Id });
        }
    }

    private void EvaluateAll(bool allowCompletion)
    {
        EnsureStates();
        if (config?.Achievements == null) return;
        bool changed = false;
        foreach (AchievementDefinition definition in config.Achievements)
        {
            if (definition == null || !states.TryGetValue(definition.Id, out AchievementStateData state)) continue;
            int progress = CalculateProgress(definition);
            int displayTarget = GetTarget(definition);
            if (state.Progress != Mathf.Clamp(progress, 0, displayTarget))
            {
                state.Progress = Mathf.Clamp(progress, 0, displayTarget);
                changed = true;
            }
            if (allowCompletion && !state.Completed && progress >= displayTarget)
            {
                Complete(definition, state);
                changed = true;
            }
        }
        if (changed) ProgressChanged?.Invoke();
    }

    private int CalculateProgress(AchievementDefinition definition)
    {
        switch (definition.Condition)
        {
            case AchievementConditionType.TotalFishCaught: return fishInventory?.TotalLifetimeCaught ?? 0;
            case AchievementConditionType.PerfectCatches: return metrics.PerfectCatches;
            case AchievementConditionType.UniqueFishDiscovered:
            case AchievementConditionType.AllFishDiscovered: return CountDiscoveredFish();
            case AchievementConditionType.FirstRarityCaught:
                return HasCaughtRarity(definition.RequiredRarity) ? 1 : 0;
            case AchievementConditionType.TotalSaleGold: return metrics.TotalSaleGold;
            case AchievementConditionType.SuccessfulSales: return metrics.SuccessfulSaleTransactions;
            case AchievementConditionType.SellAllTransactions: return metrics.SellAllTransactions;
            case AchievementConditionType.UniqueRodsOwned: return rodInventory?.OwnedRodCount ?? 0;
            case AchievementConditionType.FishCaughtAfterFirstRodPurchase: return metrics.FishCaughtAfterFirstRodPurchase;
            case AchievementConditionType.SuccessfulDebtRepayments: return metrics.SuccessfulDebtRepayments;
            case AchievementConditionType.DebtRepaidRatio:
                if (debt == null || debt.InitialDebt <= 0) return 0;
                return Mathf.FloorToInt((float)debt.LifetimeRepaid / debt.InitialDebt * 100f);
            case AchievementConditionType.DebtFullyRepaid: return debt != null && debt.IsFullyRepaid ? 1 : 0;
            case AchievementConditionType.GameCleared: return debt != null && debt.IsGameCleared ? 1 : 0;
            case AchievementConditionType.SingleSaleGold: return metrics.LargestSingleSale;
            case AchievementConditionType.ConsecutiveRarityCatches: return metrics.ConsecutiveRareCatches;
            case AchievementConditionType.ConsecutiveSuccessfulCatches: return metrics.ConsecutiveSuccessfulCatches;
            case AchievementConditionType.SalesWithoutAdditionalRodPurchase:
                return metrics.AdditionalRodPurchased ? 0 : metrics.TotalSaleGold;
            default: return 0;
        }
    }

    private int GetTarget(AchievementDefinition definition)
    {
        if (definition.Condition == AchievementConditionType.AllFishDiscovered)
            return Mathf.Max(1, CountValidFish());
        return Mathf.Max(1, definition.Target);
    }

    private void Complete(AchievementDefinition definition, AchievementStateData state)
    {
        state.Completed = true;
        state.Progress = GetTarget(definition);
        if (!state.RewardGranted)
        {
            state.RewardGranted = true;
            if (definition.RewardGold > 0) wallet?.AddMoney(definition.RewardGold);
        }
        notices.Enqueue(new Notice { Title = definition.Title, Reward = definition.RewardGold });
        AchievementCompleted?.Invoke(definition);
    }

    public AchievementSaveData CreateSnapshot()
    {
        var data = new AchievementSaveData
        {
            Metrics = new AchievementMetricsData
            {
                PerfectCatches = metrics.PerfectCatches,
                SuccessfulSaleTransactions = metrics.SuccessfulSaleTransactions,
                TotalSaleGold = metrics.TotalSaleGold,
                SellAllTransactions = metrics.SellAllTransactions,
                FishCaughtAfterFirstRodPurchase = metrics.FishCaughtAfterFirstRodPurchase,
                SuccessfulDebtRepayments = metrics.SuccessfulDebtRepayments,
                LargestSingleSale = metrics.LargestSingleSale,
                ConsecutiveRareCatches = metrics.ConsecutiveRareCatches,
                ConsecutiveSuccessfulCatches = metrics.ConsecutiveSuccessfulCatches,
                AdditionalRodPurchased = metrics.AdditionalRodPurchased
            }
        };
        foreach (AchievementStateData state in states.Values)
            data.States.Add(new AchievementStateData
            {
                Id = state.Id,
                Progress = state.Progress,
                Completed = state.Completed,
                RewardGranted = state.RewardGranted
            });
        return data;
    }

    public void Restore(AchievementSaveData saved)
    {
        states.Clear();
        metrics = saved?.Metrics ?? new AchievementMetricsData();
        if (rodInventory != null && rodInventory.OwnedRodCount > 1) metrics.AdditionalRodPurchased = true;
        EnsureStates();
        if (saved?.States != null)
        {
            foreach (AchievementStateData source in saved.States)
            {
                if (source == null || !states.TryGetValue(source.Id ?? "", out AchievementStateData target)) continue;
                target.Progress = Mathf.Max(0, source.Progress);
                target.Completed = source.Completed;
                target.RewardGranted = source.RewardGranted || source.Completed;
            }
        }
        notices.Clear();
        activeNotice = null;
        noticeTimer = 0f;
        EvaluateAll(false);
        ProgressChanged?.Invoke();
    }

    public void ResetProgress() => Restore(null);

    private int CountDiscoveredFish()
    {
        int count = 0;
        if (fishCatalog == null || fishInventory == null) return count;
        for (int i = 0; i < fishCatalog.Count; i++)
            if (fishCatalog[i] != null && fishInventory.IsDiscovered(fishCatalog[i].Id)) count++;
        return count;
    }

    private int CountValidFish()
    {
        int count = 0;
        if (fishCatalog == null) return count;
        for (int i = 0; i < fishCatalog.Count; i++)
            if (fishCatalog[i] != null && !string.IsNullOrWhiteSpace(fishCatalog[i].Id)) count++;
        return count;
    }

    private bool HasCaughtRarity(FishRarity rarity)
    {
        if (fishCatalog == null || fishInventory == null) return false;
        for (int i = 0; i < fishCatalog.Count; i++)
            if (fishCatalog[i] != null && fishCatalog[i].Rarity == rarity && fishInventory.GetLifetimeCaught(fishCatalog[i].Id) > 0)
                return true;
        return false;
    }

    private void Update()
    {
        if (activeNotice == null && notices.Count > 0)
        {
            activeNotice = notices.Dequeue();
            noticeTimer = Mathf.Max(.5f, config != null ? config.NotificationDuration : 2.8f);
        }
        if (activeNotice == null) return;
        noticeTimer -= Time.unscaledDeltaTime;
        if (noticeTimer <= 0f) activeNotice = null;
    }

    private void OnGUI()
    {
        if (activeNotice == null) return;
        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        FishingUiTheme.Ensure();
        var panel = new Rect((width - 440f) * .5f, 118f, 440f, 92f);
        FishingUiTheme.DrawPanel(panel);
        FishingUiTheme.DrawOutlinedLabel(new Rect(panel.x + 18f, panel.y + 9f, panel.width - 36f, 28f),
            "업적 달성", FishingUiTheme.OverlaySmallCentered, FishingUiTheme.Gold);
        FishingUiTheme.DrawOutlinedLabel(new Rect(panel.x + 18f, panel.y + 38f, panel.width - 36f, 30f),
            activeNotice.Title, FishingUiTheme.OverlayTitle, FishingUiTheme.Cream);
        if (activeNotice.Reward > 0)
            FishingUiTheme.DrawOutlinedLabel(new Rect(panel.x + 18f, panel.y + 68f, panel.width - 36f, 20f),
                $"+{activeNotice.Reward:N0} G", FishingUiTheme.OverlaySmallCentered, FishingUiTheme.Gold);
        GUI.matrix = oldMatrix;
    }

    private void OnDestroy()
    {
        if (fishInventory != null) fishInventory.FishAcquired -= OnFishAcquired;
        if (debt != null)
        {
            debt.DebtRepaid -= OnDebtRepaid;
            debt.DebtFullyRepaid -= OnDebtFullyRepaid;
            debt.GameCleared -= OnGameCleared;
        }
    }

    private static int SafeAdd(int current, int amount) => (int)Math.Min(int.MaxValue, (long)Mathf.Max(0, current) + Mathf.Max(0, amount));
}
