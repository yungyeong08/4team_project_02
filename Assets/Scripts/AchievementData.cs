using System;
using System.Collections.Generic;
using UnityEngine;

public enum AchievementCategory { Fishing, Collection, Economy, Equipment, Debt, Special }

public enum AchievementConditionType
{
    TotalFishCaught,
    PerfectCatches,
    UniqueFishDiscovered,
    FirstRarityCaught,
    TotalSaleGold,
    SuccessfulSales,
    SellAllTransactions,
    UniqueRodsOwned,
    FishCaughtAfterFirstRodPurchase,
    SuccessfulDebtRepayments,
    DebtRepaidRatio,
    DebtFullyRepaid,
    GameCleared,
    SingleSaleGold,
    ConsecutiveRarityCatches,
    ConsecutiveSuccessfulCatches,
    SalesWithoutAdditionalRodPurchase,
    AllFishDiscovered
}

[Serializable]
public sealed class AchievementDefinition
{
    public string Id;
    public AchievementCategory Category;
    public string Title;
    [TextArea] public string Description;
    public AchievementConditionType Condition;
    [Min(1)] public int Target = 1;
    public FishRarity RequiredRarity;
    [Min(0)] public int RewardGold;
    public Texture2D Badge;
    public string SpecialRewardId;
    public bool Hidden;

    public AchievementDefinition(string id, AchievementCategory category, string title, string description,
        AchievementConditionType condition, int target, int rewardGold = 0)
    {
        Id = id;
        Category = category;
        Title = title;
        Description = description;
        Condition = condition;
        Target = Mathf.Max(1, target);
        RewardGold = Mathf.Max(0, rewardGold);
    }
}

public static class AchievementDefaults
{
    public static List<AchievementDefinition> Create() => new()
    {
            A("first_catch", AchievementCategory.Fishing, "첫 출조", "낚시에 처음 성공하세요.", AchievementConditionType.TotalFishCaught, 1, 100),
            A("catch_10", AchievementCategory.Fishing, "낚시 입문자", "물고기를 총 10마리 잡으세요.", AchievementConditionType.TotalFishCaught, 10, 300),
            A("catch_50", AchievementCategory.Fishing, "베테랑 낚시꾼", "물고기를 총 50마리 잡으세요.", AchievementConditionType.TotalFishCaught, 50, 1000),
            A("catch_100", AchievementCategory.Fishing, "백전노장 어부", "물고기를 총 100마리 잡으세요.", AchievementConditionType.TotalFishCaught, 100, 2500),
            A("perfect_10", AchievementCategory.Fishing, "완벽한 손놀림", "PERFECT 포획을 10회 달성하세요.", AchievementConditionType.PerfectCatches, 10, 500),
            A("discover_1", AchievementCategory.Collection, "첫번째 수집품", "물고기 1종을 발견하세요.", AchievementConditionType.UniqueFishDiscovered, 1, 100),
            A("discover_3", AchievementCategory.Collection, "물고기 연구가", "서로 다른 물고기 3종을 발견하세요.", AchievementConditionType.UniqueFishDiscovered, 3, 500),
            A("discover_10", AchievementCategory.Collection, "수집의 달인", "서로 다른 물고기 10종을 발견하세요.", AchievementConditionType.UniqueFishDiscovered, 10, 2000),
            R("first_rare", AchievementCategory.Collection, "희귀한 만남", "희귀 등급 물고기를 처음 잡으세요.", FishRarity.Rare, 1000),
            R("first_legendary", AchievementCategory.Collection, "전설을 찾아서", "전설 등급 물고기를 잡으세요.", FishRarity.Legendary, 3000, true),
            A("first_sale", AchievementCategory.Economy, "첫 수익", "물고기 판매에 처음 성공하세요.", AchievementConditionType.SuccessfulSales, 1, 100),
            A("sales_1000", AchievementCategory.Economy, "장사의 시작", "물고기 판매로 총 1,000 G를 벌으세요.", AchievementConditionType.TotalSaleGold, 1000, 300),
            A("sales_10000", AchievementCategory.Economy, "작은 부자", "물고기 판매로 총 10,000 G를 벌으세요.", AchievementConditionType.TotalSaleGold, 10000, 1000),
            A("sales_50000", AchievementCategory.Economy, "큰손 어부", "물고기 판매로 총 50,000 G를 벌으세요.", AchievementConditionType.TotalSaleGold, 50000, 3000),
            A("sell_all_10", AchievementCategory.Economy, "일괄 판매의 달인", "모두 판매에 10회 성공하세요.", AchievementConditionType.SellAllTransactions, 10, 500),
            A("buy_rod_1", AchievementCategory.Equipment, "새로운 장비", "추가 낚싯대를 처음 구매하세요.", AchievementConditionType.UniqueRodsOwned, 2, 300),
            A("own_rods_3", AchievementCategory.Equipment, "장비 수집가", "낚싯대 3종을 보유하세요.", AchievementConditionType.UniqueRodsOwned, 3, 1500),
            A("rod_investment", AchievementCategory.Equipment, "투자의 결실", "첫 추가 낚싯대 구매 후 물고기 10마리를 잡으세요.", AchievementConditionType.FishCaughtAfterFirstRodPurchase, 10, 1000),
            A("first_debt_payment", AchievementCategory.Debt, "첫번째 상환", "빚을 처음 상환하세요.", AchievementConditionType.SuccessfulDebtRepayments, 1, 500),
            A("half_debt", AchievementCategory.Debt, "빚 줄이기", "초기 빚의 50%를 상환하세요.", AchievementConditionType.DebtRepaidRatio, 50, 1500),
            A("debt_free", AchievementCategory.Debt, "빛나는 자유", "남은 빚을 0 G로 만드세요.", AchievementConditionType.DebtFullyRepaid, 1, 5000),
            A("new_life", AchievementCategory.Debt, "새로운 인생", "빚을 모두 갚고 게임 클리어를 달성하세요.", AchievementConditionType.GameCleared, 1),
            A("single_sale_5000", AchievementCategory.Special, "오늘은 대박!", "한 번의 판매로 5,000 G 이상을 벌으세요.", AchievementConditionType.SingleSaleGold, 5000, 1000),
            A("rare_streak_2", AchievementCategory.Special, "운 좋은 날", "희귀 등급 물고기를 2번 연속으로 잡으세요.", AchievementConditionType.ConsecutiveRarityCatches, 2, 1000),
            A("success_streak_10", AchievementCategory.Special, "낚시왕 · 10연속 포획", "낚시에 10번 연속 성공하세요.", AchievementConditionType.ConsecutiveSuccessfulCatches, 10, 1000),
            A("frugal_angler", AchievementCategory.Special, "절약 정신", "추가 낚싯대를 구매하지 않고 판매로 5,000 G를 벌으세요.", AchievementConditionType.SalesWithoutAdditionalRodPurchase, 5000, 1000),
            A("all_fish", AchievementCategory.Special, "어부의 전설", "현재 물고기 데이터베이스의 모든 종을 발견하세요.", AchievementConditionType.AllFishDiscovered, 1)
    };

    private static AchievementDefinition A(string id, AchievementCategory category, string title, string description,
        AchievementConditionType condition, int target, int reward = 0)
        => new(id, category, title, description, condition, target, reward);

    private static AchievementDefinition R(string id, AchievementCategory category, string title, string description,
        FishRarity rarity, int reward, bool hidden = false)
        => new(id, category, title, description, AchievementConditionType.FirstRarityCaught, 1, reward)
        { RequiredRarity = rarity, Hidden = hidden };

}
