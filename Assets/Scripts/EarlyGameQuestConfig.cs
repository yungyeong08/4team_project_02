using System.Collections.Generic;
using UnityEngine;

/// <summary>Inspector-editable first-area quest line. Add later region quest assets without changing the quest runtime.</summary>
[CreateAssetMenu(fileName = "EarlyGameQuestConfig", menuName = "Fishing/Early Game Quest Config")]
public sealed class EarlyGameQuestConfig : ScriptableObject
{
    [Header("HUD Art")]
    public Texture2D QuestIcon;

    [Header("Quest Line")]
    public List<EarlyQuestDefinition> Quests = new();

    public void ApplyDefaults()
    {
        Quests = new List<EarlyQuestDefinition>
        {
            new("first_catch", "첫 낚시", "첫 번째 호수에서 물고기 1마리 잡기",
                EarlyQuestGoal.CatchAtFirstLake, 1, 100, EarlyGameQuestSystem.FirstLakeId, "first_sale"),
            new("beginner_angler", "초보 낚시꾼", "첫 번째 호수에서 물고기 3마리 잡기",
                EarlyQuestGoal.CatchAtFirstLake, 3, 200, EarlyGameQuestSystem.FirstLakeId, "repay_debt"),
            new("first_sale", "첫 판매", "물고기 판매소에서 물고기 판매하기",
                EarlyQuestGoal.SellFish, 1, 150, "", "rod_upgrade"),
            new("rod_upgrade", "장비 업그레이드", "튼튼한 낚싯대 구매하기",
                EarlyQuestGoal.PurchaseRod, 1, 100, EarlyGameQuestSystem.UpgradeRodId, "use_new_rod"),
            new("use_new_rod", "새 낚싯대 사용", "업그레이드 낚싯대로 물고기 2마리 잡기",
                EarlyQuestGoal.CatchWithUpgradedRod, 2, 400, EarlyGameQuestSystem.FirstLakeId, "beginner_angler"),
            new("repay_debt", "빚 상환", "빚을 500G 상환하세요.",
                EarlyQuestGoal.RepayDebt, 500, 0, "", "")
        };
    }

    public bool EnsureValid()
    {
        if (Quests == null || Quests.Count == 0)
        {
            ApplyDefaults();
            return true;
        }

        bool changed = false;
        EarlyQuestDefinition beginner = Quests.Find(quest => quest != null && quest.Id == "beginner_angler");
        EarlyQuestDefinition debt = Quests.Find(quest => quest != null && quest.Id == "repay_debt");
        if (debt == null)
        {
            debt = new EarlyQuestDefinition("repay_debt", "빚 상환", "빚을 500G 상환하세요.",
                EarlyQuestGoal.RepayDebt, 500, 0, "", "");
            Quests.Add(debt);
            changed = true;
        }
        if (beginner != null && beginner.NextQuestId != debt.Id)
        {
            beginner.NextQuestId = debt.Id;
            changed = true;
        }
        return changed;
    }
}
