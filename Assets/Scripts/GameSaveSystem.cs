using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class FishingGameSaveData
{
    public const int CurrentVersion = 6;
    public int Version = CurrentVersion;
    public int Money = 100;
    public List<FishInventory.FishProgress> FishProgress = new();
    public List<string> OwnedRodIds = new();
    public string EquippedRodId = FishingRodCatalog.DefaultRodId;
    public float PlayerX;
    public float PlayerY;
    public EarlyGameProgressSaveData EarlyGameProgress = new();
}

/// <summary>Persists the existing wallet and inventory components as one versioned JSON save.</summary>
public sealed class GameSaveSystem : MonoBehaviour
{
    private const string SaveFileName = "fishing-save.json";
    private const float AutoSaveDelay = .35f;

    private PlayerWallet wallet;
    private FishInventory fishInventory;
    private FishingRodInventory rodInventory;
    private EarlyGameQuestSystem earlyGameProgress;
    private Transform player;
    private Vector2 defaultPlayerPosition;
    private Vector2 mapSize;
    private HashSet<string> validFishIds = new();
    private HashSet<string> validRodIds = new();
    private bool initialized;
    private bool dirty;
    private float dirtyAt;

    public string LastStatus { get; private set; } = "";
    public event Action NewGameStarted;
    public bool HasSaveData => File.Exists(SavePath) || File.Exists(BackupPath);
    public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    private string BackupPath => SavePath + ".bak";

    public void Setup(
        PlayerWallet playerWallet,
        FishInventory playerFishInventory,
        FishingRodInventory playerRodInventory,
        Transform playerTransform,
        Vector2 newGamePosition,
        Vector2 worldSize,
        IReadOnlyList<FishDefinition> fishCatalog,
        IReadOnlyList<FishingRodDefinition> rodCatalog,
        EarlyGameQuestSystem questSystem)
    {
        wallet = playerWallet;
        fishInventory = playerFishInventory;
        rodInventory = playerRodInventory;
        player = playerTransform;
        earlyGameProgress = questSystem;
        defaultPlayerPosition = newGamePosition;
        mapSize = worldSize;
        validFishIds.Clear();
        validRodIds.Clear();
        if (fishCatalog != null)
            for (int i = 0; i < fishCatalog.Count; i++)
                if (fishCatalog[i] != null && !string.IsNullOrWhiteSpace(fishCatalog[i].Id)) validFishIds.Add(fishCatalog[i].Id);
        if (rodCatalog != null)
            for (int i = 0; i < rodCatalog.Count; i++)
                if (rodCatalog[i] != null && !string.IsNullOrWhiteSpace(rodCatalog[i].Id)) validRodIds.Add(rodCatalog[i].Id);
        validRodIds.Add(FishingRodCatalog.DefaultRodId);

        bool loaded = TryLoad();
        if (!loaded) ApplyNewGameState();

        wallet.BalanceChanged += MarkDirty;
        fishInventory.InventoryChanged += MarkDirty;
        rodInventory.InventoryChanged += MarkDirty;
        if (earlyGameProgress != null) earlyGameProgress.ProgressChanged += MarkDirty;
        initialized = true;
    }

    private void Update()
    {
        if (initialized && dirty && Time.unscaledTime - dirtyAt >= AutoSaveDelay) Save();
    }

    public void MarkDirty()
    {
        dirty = true;
        dirtyAt = Time.unscaledTime;
    }

    private void MarkDirty(int _) => MarkDirty();

    public bool Save()
    {
        if (wallet == null || fishInventory == null || rodInventory == null || player == null) return false;
        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var data = new FishingGameSaveData
            {
                Version = FishingGameSaveData.CurrentVersion,
                Money = wallet.Balance,
                FishProgress = fishInventory.CreateSnapshot(),
                OwnedRodIds = rodInventory.CreateSnapshot(),
                EquippedRodId = rodInventory.EquippedRodId,
                PlayerX = player.position.x,
                PlayerY = player.position.y,
                EarlyGameProgress = earlyGameProgress != null ? earlyGameProgress.CreateSnapshot() : new EarlyGameProgressSaveData()
            };

            string tempPath = SavePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Copy(SavePath, BackupPath, true);
            File.Copy(tempPath, SavePath, true);
            File.Delete(tempPath);
            dirty = false;
            LastStatus = "게임을 저장했습니다.";
            return true;
        }
        catch (Exception exception)
        {
            LastStatus = "저장하지 못했습니다: " + exception.Message;
            Debug.LogWarning(LastStatus);
            return false;
        }
    }

    public bool TryLoad()
    {
        if (TryLoadPath(SavePath)) return true;
        if (TryLoadPath(BackupPath))
        {
            LastStatus = "백업 저장 데이터를 불러왔습니다.";
            try { File.Copy(BackupPath, SavePath, true); }
            catch (Exception exception) { Debug.LogWarning("Backup save loaded but primary save could not be repaired: " + exception.Message); }
            return true;
        }
        return false;
    }

    private bool TryLoadPath(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            FishingGameSaveData data = JsonUtility.FromJson<FishingGameSaveData>(File.ReadAllText(path));
            if (data == null) return false;

            if (data.Version > FishingGameSaveData.CurrentVersion)
                Debug.LogWarning($"Save data version {data.Version} is newer than supported version {FishingGameSaveData.CurrentVersion}. Known fields will still be loaded.");

            List<FishInventory.FishProgress> safeFishProgress = SanitizeFishProgress(data.FishProgress);
            List<string> safeRodIds = SanitizeRodIds(data.OwnedRodIds);
            string safeEquippedRod = validRodIds.Contains(data.EquippedRodId) && safeRodIds.Contains(data.EquippedRodId)
                ? data.EquippedRodId
                : FishingRodCatalog.DefaultRodId;

            wallet.Initialize(Mathf.Max(0, data.Money));
            fishInventory.Restore(safeFishProgress);
            rodInventory.Restore(safeRodIds, safeEquippedRod);

            bool requiresStarterPosition = data.Version < FishingGameSaveData.CurrentVersion ||
                data.EarlyGameProgress == null ||
                data.EarlyGameProgress.TutorialStep <= (int)EarlyTutorialStep.ReachFirstLake;
            Vector2 savedPosition = requiresStarterPosition
                ? defaultPlayerPosition
                : new Vector2(data.PlayerX, data.PlayerY);
            if (float.IsNaN(savedPosition.x) || float.IsInfinity(savedPosition.x) ||
                float.IsNaN(savedPosition.y) || float.IsInfinity(savedPosition.y)) savedPosition = defaultPlayerPosition;
            if (mapSize.x > 1f && mapSize.y > 1f)
            {
                float halfWidth = mapSize.x * .5f - .5f;
                float halfHeight = mapSize.y * .5f - .5f;
                savedPosition.x = Mathf.Clamp(savedPosition.x, -halfWidth, halfWidth);
                savedPosition.y = Mathf.Clamp(savedPosition.y, -halfHeight, halfHeight);
            }
            player.position = savedPosition;
            // v6 introduces the prologue and aligns the quest data with the tutorial's
            // first catch -> first sale -> rod purchase order. Existing saves continue
            // without replaying the story and are moved onto the matching objective.
            if (data.Version < 6 && data.EarlyGameProgress != null)
            {
                data.EarlyGameProgress.OpeningSeen = true;
                EarlyTutorialStep savedStep = (EarlyTutorialStep)Mathf.Clamp(data.EarlyGameProgress.TutorialStep,
                    0, (int)EarlyTutorialStep.Complete);
                bool finishedOldQuestLine = data.EarlyGameProgress.NextRegionReady ||
                    (data.EarlyGameProgress.CompletedQuestIds?.Contains("use_new_rod") ?? false);
                if (finishedOldQuestLine)
                {
                    data.EarlyGameProgress.TutorialStep = (int)EarlyTutorialStep.Complete;
                    data.EarlyGameProgress.ActiveQuestId = "";
                    data.EarlyGameProgress.QuestProgress = 0;
                }
                else if (savedStep is EarlyTutorialStep.ReachFishMarket or EarlyTutorialStep.SellFish)
                {
                    data.EarlyGameProgress.ActiveQuestId = "first_sale";
                    data.EarlyGameProgress.QuestProgress = 0;
                }
                else if (savedStep is EarlyTutorialStep.ReachRodShop or EarlyTutorialStep.BuyUpgradeRod)
                {
                    data.EarlyGameProgress.ActiveQuestId = "rod_upgrade";
                    data.EarlyGameProgress.QuestProgress = 0;
                }
            }
            earlyGameProgress?.Restore(data.EarlyGameProgress);
            // Persist the migrated starter position shortly after an older save loads,
            // otherwise the same legacy position would be migrated on every launch.
            dirty = requiresStarterPosition;
            if (dirty) dirtyAt = Time.unscaledTime;
            LastStatus = requiresStarterPosition
                ? "기존 저장 데이터를 첫 호수 시작 위치로 옮겼습니다."
                : "저장 데이터를 불러왔습니다.";
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Save file could not be loaded ({path}): {exception.Message}");
            return false;
        }
    }

    private List<FishInventory.FishProgress> SanitizeFishProgress(List<FishInventory.FishProgress> source)
    {
        var result = new List<FishInventory.FishProgress>();
        if (source == null) return result;
        foreach (FishInventory.FishProgress entry in source)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FishId)) continue;
            if (validFishIds.Count > 0 && !validFishIds.Contains(entry.FishId))
            {
                Debug.LogWarning("Ignoring unknown fish id in save data: " + entry.FishId);
                continue;
            }
            result.Add(entry);
        }
        return result;
    }

    private List<string> SanitizeRodIds(List<string> source)
    {
        var result = new List<string> { FishingRodCatalog.DefaultRodId };
        if (source == null) return result;
        foreach (string rodId in source)
        {
            if (string.IsNullOrWhiteSpace(rodId) || result.Contains(rodId)) continue;
            if (validRodIds.Count > 0 && !validRodIds.Contains(rodId))
            {
                Debug.LogWarning("Ignoring unknown fishing rod id in save data: " + rodId);
                continue;
            }
            result.Add(rodId);
        }
        return result;
    }

    public void ApplyNewGameState()
    {
        wallet.Initialize(100);
        fishInventory.Restore(null);
        rodInventory.Restore(null, FishingRodCatalog.DefaultRodId);
        if (player != null) player.position = defaultPlayerPosition;
        earlyGameProgress?.ResetProgress();
        dirty = false;
        LastStatus = "새 게임으로 시작했습니다.";
        NewGameStarted?.Invoke();
    }

    public bool ResetGame()
    {
        try
        {
            DeleteSaveFileIfPresent(SavePath);
            DeleteSaveFileIfPresent(BackupPath);
            DeleteSaveFileIfPresent(SavePath + ".tmp");
            ApplyNewGameState();
            bool saved = Save();
            if (saved) LastStatus = "게임 진행 상황을 초기화했습니다.";
            return saved;
        }
        catch (Exception exception)
        {
            LastStatus = "게임을 초기화하지 못했습니다: " + exception.Message;
            Debug.LogWarning(LastStatus);
            return false;
        }
    }

    private static void DeleteSaveFileIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && initialized) Save();
    }

    private void OnApplicationQuit()
    {
        if (initialized) Save();
    }

    private void OnDestroy()
    {
        if (wallet != null) wallet.BalanceChanged -= MarkDirty;
        if (fishInventory != null) fishInventory.InventoryChanged -= MarkDirty;
        if (rodInventory != null) rodInventory.InventoryChanged -= MarkDirty;
        if (earlyGameProgress != null) earlyGameProgress.ProgressChanged -= MarkDirty;
    }
}
