using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class FishingAreaSaveData
{
    public string CurrentAreaId;
    public List<string> UnlockedAreaIds = new();
}

/// <summary>Owns area unlock state without duplicating map layout or quest progress.</summary>
public sealed class FishingAreaProgress : MonoBehaviour
{
    private readonly List<FishingSpotDefinition> areas = new();
    private readonly HashSet<string> unlockedIds = new();
    private EarlyGameQuestSystem quests;

    public string CurrentAreaId { get; private set; }
    public event Action ProgressChanged;
    public event Action<string> AreaUnlocked;

    public void Setup(IReadOnlyList<FishingSpotDefinition> definitions, EarlyGameQuestSystem questSystem)
    {
        areas.Clear();
        if (definitions != null)
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null && definitions[i].Enabled && !string.IsNullOrWhiteSpace(definitions[i].Id))
                    areas.Add(definitions[i]);
        quests = questSystem;
        if (quests != null) quests.QuestIdCompleted += OnQuestCompleted;
        ResetProgress();
    }

    public bool IsUnlocked(string areaId) => !string.IsNullOrWhiteSpace(areaId) && unlockedIds.Contains(areaId);

    public string GetLockedMessage(FishingSpotDefinition area)
    {
        if (area == null) return "아직 들어갈 수 없는 지역입니다.";
        return string.IsNullOrWhiteSpace(area.RequiredQuestId)
            ? "아직 잠긴 낚시터입니다."
            : "필요한 퀘스트를 먼저 완료하세요.";
    }

    public bool TryUnlock(string areaId)
    {
        FishingSpotDefinition area = areas.Find(candidate => candidate.Id == areaId);
        if (area == null || unlockedIds.Contains(areaId)) return false;
        unlockedIds.Add(areaId);
        AreaUnlocked?.Invoke(areaId);
        ProgressChanged?.Invoke();
        return true;
    }

    public void NotifyAreaEntered(string areaId)
    {
        if (!IsUnlocked(areaId) || CurrentAreaId == areaId) return;
        CurrentAreaId = areaId;
        ProgressChanged?.Invoke();
    }

    private void OnQuestCompleted(string questId)
    {
        for (int i = 0; i < areas.Count; i++)
            if (!string.IsNullOrWhiteSpace(areas[i].RequiredQuestId) && areas[i].RequiredQuestId == questId)
                TryUnlock(areas[i].Id);
    }

    public FishingAreaSaveData CreateSnapshot()
        => new() { CurrentAreaId = CurrentAreaId, UnlockedAreaIds = new List<string>(unlockedIds) };

    public void Restore(FishingAreaSaveData saved)
    {
        unlockedIds.Clear();
        for (int i = 0; i < areas.Count; i++)
            if (areas[i].InitiallyUnlocked) unlockedIds.Add(areas[i].Id);
        if (saved?.UnlockedAreaIds != null)
            foreach (string id in saved.UnlockedAreaIds)
                if (areas.Exists(candidate => candidate.Id == id)) unlockedIds.Add(id);
        CurrentAreaId = IsUnlocked(saved?.CurrentAreaId) ? saved.CurrentAreaId :
            areas.Find(candidate => unlockedIds.Contains(candidate.Id))?.Id ?? "";
        ProgressChanged?.Invoke();
    }

    public void ResetProgress()
    {
        unlockedIds.Clear();
        for (int i = 0; i < areas.Count; i++)
            if (areas[i].InitiallyUnlocked) unlockedIds.Add(areas[i].Id);
        CurrentAreaId = areas.Find(candidate => unlockedIds.Contains(candidate.Id))?.Id ?? "";
        ProgressChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (quests != null) quests.QuestIdCompleted -= OnQuestCompleted;
    }
}
