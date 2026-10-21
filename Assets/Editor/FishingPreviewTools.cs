#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FishingPreviewTools
{
    private const string FishingScenePath = "Assets/Scenes/FishingDemo.unity";
    private const string SmokeTestSessionKey = "Fishing.PlayModeSmokeTest";
    private const string ProgressionValidationSessionKey = "Fishing.ProgressionValidation.EarlyGameV3";

    private const string MapConfigPath = "Assets/Resources/Data/FishingMapConfig.asset";
    private const string BalanceConfigPath = "Assets/Resources/Data/FishingBalanceConfig.asset";
    private const string QuestConfigPath = "Assets/Resources/Data/EarlyGameQuestConfig.asset";

    [InitializeOnLoadMethod]
    private static void EnsureDataAssetsAfterCompile()
    {
        EditorApplication.delayCall += EnsureDataAssetsWhenReady;
    }

    private static void EnsureDataAssetsWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureDataAssetsWhenReady;
            return;
        }
        EnsureMapConfig();
        EnsureBalanceConfig();
        EnsureQuestConfig();
        if (!SessionState.GetBool(ProgressionValidationSessionKey, false))
        {
            ValidateProgressionSystems();
            SessionState.SetBool(ProgressionValidationSessionKey, true);
        }
    }

    [MenuItem("Tools/Fishing/Rebuild Full Map Preview")]
    public static void RefreshArtPreview()
    {
        EnsureMapConfig();
        EnsureBalanceConfig();
        var scene = EditorSceneManager.OpenScene(FishingScenePath, OpenSceneMode.Single);
        foreach (var builder in Object.FindObjectsByType<PixelMapBuilder>(FindObjectsSortMode.None))
            builder.EnsureDecorations();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Fishing starter map preview rebuilt with the first fishing spot.");
    }

    // Public entry point for command-line validation and repeatable scene generation.
    public static void RebuildMapAndCreateConfig()
    {
        RefreshArtPreview();
        ValidateMapConfiguration();
    }

    [MenuItem("Tools/Fishing/Validate Progression Systems")]
    public static void ValidateProgressionSystems()
    {
        FishingBalanceConfig balance = EnsureBalanceConfig();
        EarlyGameQuestConfig questConfig = EnsureQuestConfig();
        if (balance.Fish == null || balance.Fish.Count == 0) throw new System.InvalidOperationException("Fish catalog is empty.");
        if (balance.Rods == null || balance.Rods.Count < 2) throw new System.InvalidOperationException("Rod catalog needs a default rod and purchasable upgrades.");
        if (questConfig.Quests == null || questConfig.Quests.Count != 5)
            throw new System.InvalidOperationException("The first-area quest line must contain five quests.");
        EarlyQuestDefinition firstCatchQuest = questConfig.Quests.Find(quest => quest.Id == "first_catch");
        EarlyQuestDefinition firstSaleQuest = questConfig.Quests.Find(quest => quest.Id == "first_sale");
        EarlyQuestDefinition rodUpgradeQuest = questConfig.Quests.Find(quest => quest.Id == "rod_upgrade");
        EarlyQuestDefinition useNewRodQuest = questConfig.Quests.Find(quest => quest.Id == "use_new_rod");
        EarlyQuestDefinition beginnerQuest = questConfig.Quests.Find(quest => quest.Id == "beginner_angler");
        if (firstCatchQuest?.NextQuestId != "first_sale" || firstSaleQuest?.NextQuestId != "rod_upgrade" ||
            rodUpgradeQuest?.NextQuestId != "use_new_rod" || useNewRodQuest?.NextQuestId != "beginner_angler" ||
            beginnerQuest == null || !string.IsNullOrEmpty(beginnerQuest.NextQuestId))
            throw new System.InvalidOperationException("The opening quest chain must follow catch -> sale -> rod -> use rod -> free play.");

        var fishIds = new System.Collections.Generic.HashSet<string>();
        var rarities = new System.Collections.Generic.HashSet<FishRarity>();
        foreach (FishDefinition fish in balance.Fish)
        {
            if (fish == null || string.IsNullOrWhiteSpace(fish.Id) || !fishIds.Add(fish.Id))
                throw new System.InvalidOperationException("Fish ids must be non-empty and unique.");
            if (fish.Icon == null)
                throw new System.InvalidOperationException($"Fish art is missing for {fish.Id}.");
            if (fish.SalePrice < 0 || fish.CatchRate <= 0f || fish.MaxSpeed < 0f)
                throw new System.InvalidOperationException($"Invalid balance values for fish {fish.Id}.");
            rarities.Add(fish.Rarity);
        }
        foreach (FishDefinition defaultFish in FishCatalog.CreateDefault())
            if (!fishIds.Contains(defaultFish.Id))
                throw new System.InvalidOperationException($"Default fish is missing: {defaultFish.Id}.");
        foreach (FishRarity rarity in System.Enum.GetValues(typeof(FishRarity)))
            if (!rarities.Contains(rarity))
                throw new System.InvalidOperationException($"Fish catalog has no {rarity} species.");

        var rodIds = new System.Collections.Generic.HashSet<string>();
        FishingRodDefinition previous = null;
        foreach (FishingRodDefinition rod in balance.Rods)
        {
            if (rod == null || string.IsNullOrWhiteSpace(rod.Id) || !rodIds.Add(rod.Id))
                throw new System.InvalidOperationException("Rod ids must be non-empty and unique.");
            if (rod.CaptureZoneMultiplier <= 0f || rod.FishSpeedMultiplier <= 0f || rod.CatchRateMultiplier <= 0f)
                throw new System.InvalidOperationException($"Invalid modifiers for rod {rod.Id}.");
            if (previous != null && (rod.Price < previous.Price || rod.CaptureZoneMultiplier < previous.CaptureZoneMultiplier ||
                rod.FishSpeedMultiplier > previous.FishSpeedMultiplier || rod.CatchRateMultiplier < previous.CatchRateMultiplier))
                throw new System.InvalidOperationException("Rod progression must improve while prices increase.");
            previous = rod;
        }
        if (!rodIds.Contains(FishingRodCatalog.DefaultRodId)) throw new System.InvalidOperationException("Default rod is missing.");

        var saveRoundTrip = new FishingGameSaveData
        {
            Money = 777,
            OwnedRodIds = new System.Collections.Generic.List<string> { FishingRodCatalog.DefaultRodId, "sturdy_rod" },
            EquippedRodId = "sturdy_rod",
            FishProgress = new System.Collections.Generic.List<FishInventory.FishProgress>
            {
                new() { FishId = balance.Fish[0].Id, Count = 2, LifetimeCaught = 5, Discovered = true }
            },
            PlayerX = 3.5f,
            PlayerY = -2f,
            EarlyGameProgress = new EarlyGameProgressSaveData
            {
                TutorialStep = (int)EarlyTutorialStep.ReachFishMarket,
                ActiveQuestId = "first_sale",
                QuestProgress = 0,
                CompletedQuestIds = new System.Collections.Generic.List<string> { "first_catch", "beginner_angler" },
                OpeningSeen = true
            }
        };
        FishingGameSaveData restored = JsonUtility.FromJson<FishingGameSaveData>(JsonUtility.ToJson(saveRoundTrip));
        if (restored == null || restored.Money != 777 || restored.FishProgress.Count != 1 || restored.FishProgress[0].LifetimeCaught != 5 ||
            restored.EarlyGameProgress == null || restored.EarlyGameProgress.ActiveQuestId != "first_sale" ||
            !restored.EarlyGameProgress.OpeningSeen || restored.Version != FishingGameSaveData.CurrentVersion)
            throw new System.InvalidOperationException("Save data JSON round trip failed.");

        var validationObject = new GameObject("Progression Validation (Temporary)");
        try
        {
            FishingRodInventory rods = validationObject.AddComponent<FishingRodInventory>();
            rods.EnsureDefaultRod();
            if (!rods.Owns(FishingRodCatalog.DefaultRodId) || !rods.TryAddAndEquip("sturdy_rod") || rods.TryAddAndEquip("sturdy_rod"))
                throw new System.InvalidOperationException("Rod ownership or duplicate prevention failed.");
            if (!rods.TryEquip(FishingRodCatalog.DefaultRodId))
                throw new System.InvalidOperationException("Equipping an owned rod failed.");

            FishInventory inventory = validationObject.AddComponent<FishInventory>();
            string fishId = balance.Fish[0].Id;
            inventory.Add(fishId, 3);
            if (inventory.GetCount(fishId) != 3 || inventory.GetLifetimeCaught(fishId) != 3 || !inventory.IsDiscovered(fishId))
                throw new System.InvalidOperationException("Fish inventory and codex progress are not sharing data.");
            if (inventory.RemoveAll(fishId) != 3 || inventory.GetCount(fishId) != 0 || inventory.GetLifetimeCaught(fishId) != 3)
                throw new System.InvalidOperationException("Selling must remove owned fish without erasing codex history.");

            PlayerWallet wallet = validationObject.AddComponent<PlayerWallet>();
            wallet.Initialize(0);
            EarlyGameQuestSystem quests = validationObject.AddComponent<EarlyGameQuestSystem>();
            quests.Setup(validationObject.transform, wallet, FishingMapConfig.CreateDefaultSpots()[0], null, null);
            quests.NotifyFishCaught(EarlyGameQuestSystem.FirstLakeId, FishingRodCatalog.DefaultRodId);
            for (int i = 0; i < 3; i++) quests.NotifyFishCaught(EarlyGameQuestSystem.FirstLakeId, FishingRodCatalog.DefaultRodId);
            quests.NotifyFishSold(1);
            quests.NotifyRodPurchased(EarlyGameQuestSystem.UpgradeRodId);
            for (int i = 0; i < 2; i++) quests.NotifyFishCaught(EarlyGameQuestSystem.FirstLakeId, EarlyGameQuestSystem.UpgradeRodId);
            if (!quests.NextRegionReady || quests.ActiveQuest != null || quests.CompletedQuestIds.Count != 5 || wallet.Balance != 950)
                throw new System.InvalidOperationException("First-area quest chain, rewards, or next-region-ready state failed.");
        }
        finally
        {
            Object.DestroyImmediate(validationObject);
        }

        Debug.Log("PROGRESSION_VALIDATION_OK: inventory/codex, rod progression, five-quest early-game chain, rewards, and save-data JSON verified.");
    }

    public static void RunPlayModeSmokeTest()
    {
        EditorSceneManager.OpenScene(FishingScenePath, OpenSceneMode.Single);
        SessionState.SetBool(SmokeTestSessionKey, true);
        SubscribeToSmokeTest();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeSmokeTestAfterReload()
    {
        if (SessionState.GetBool(SmokeTestSessionKey, false)) SubscribeToSmokeTest();
    }

    private static void SubscribeToSmokeTest()
    {
        EditorApplication.update -= UpdatePlayModeSmokeTest;
        EditorApplication.update += UpdatePlayModeSmokeTest;
    }

    private static void UpdatePlayModeSmokeTest()
    {
        if (!EditorApplication.isPlaying || Time.frameCount < 5) return;
        EditorApplication.update -= UpdatePlayModeSmokeTest;

        try
        {
            PixelMapBuilder builder = Object.FindFirstObjectByType<PixelMapBuilder>();
            PixelCharacter player = Object.FindFirstObjectByType<PixelCharacter>();
            if (builder == null || player == null) throw new System.InvalidOperationException("Runtime map or player was not created.");
            if (builder.FishingSpots.Count != 1) throw new System.InvalidOperationException("Runtime starter map must contain only the first fishing spot.");
            if (builder.FishingShop == null || builder.FishMarket == null) throw new System.InvalidOperationException("Shop objects were not created.");

            FishingGameController controller = Object.FindFirstObjectByType<FishingGameController>();
            FishingShopSystem shop = Object.FindFirstObjectByType<FishingShopSystem>();
            FishMarketSystem market = Object.FindFirstObjectByType<FishMarketSystem>();
            FishInventory fishInventory = Object.FindFirstObjectByType<FishInventory>();
            FishingRodInventory rodInventory = Object.FindFirstObjectByType<FishingRodInventory>();
            PlayerProgressUI progressUi = Object.FindFirstObjectByType<PlayerProgressUI>();
            GameSaveSystem saveSystem = Object.FindFirstObjectByType<GameSaveSystem>();
            EarlyGameQuestSystem earlyGameQuests = Object.FindFirstObjectByType<EarlyGameQuestSystem>();
            if (controller == null || shop == null || market == null || fishInventory == null || rodInventory == null ||
                progressUi == null || saveSystem == null || earlyGameQuests == null)
                throw new System.InvalidOperationException("One or more progression systems were not created at runtime.");
            if (!rodInventory.Owns(FishingRodCatalog.DefaultRodId) || string.IsNullOrWhiteSpace(rodInventory.EquippedRodId) ||
                !rodInventory.Owns(rodInventory.EquippedRodId))
                throw new System.InvalidOperationException("The player does not have a valid equipped fishing rod.");

            Vector3 originalPosition = player.transform.position;
            for (int i = 0; i < builder.FishingSpots.Count; i++)
            {
                FishingSpotDefinition spot = builder.FishingSpots[i];
                player.transform.position = spot.ShorePosition;
                Physics2D.SyncTransforms();
                FishingSpotDefinition found = builder.GetNearestFishingSpot(player.transform.position);
                if (found != spot)
                    throw new System.InvalidOperationException($"Early-area access check failed for {spot.DisplayName}.");
            }

            player.transform.position = builder.FishingShop.position;
            shop.SendMessage("Update");
            if (!shop.IsPlayerInRange || !shop.OpenShop()) throw new System.InvalidOperationException("Fishing shop interaction failed.");
            shop.CloseShop();

            player.transform.position = builder.FishMarket.position;
            market.SendMessage("Update");
            if (!market.IsPlayerInRange || !market.OpenShop()) throw new System.InvalidOperationException("Fish market interaction failed.");
            market.CloseShop();
            player.transform.position = originalPosition;

            Debug.Log("PLAYMODE_SMOKE_OK: first-lake-only map, player/UI/progression components, both shops, and equipped rod verified.");
            SessionState.SetBool(SmokeTestSessionKey, false);
            FinishSmokeTest(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(SmokeTestSessionKey, false);
            FinishSmokeTest(1);
        }
    }

    private static void FinishSmokeTest(int exitCode)
    {
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        else EditorApplication.ExitPlaymode();
    }

    [MenuItem("Tools/Fishing/Validate Map Configuration")]
    public static void ValidateMapConfiguration()
    {
        FishingMapConfig config = EnsureMapConfig();
        if (config.FishingSpots.Count != 3)
            throw new System.InvalidOperationException($"Expected 3 fishing spots, found {config.FishingSpots.Count}.");

        if (config.FishingSpots[0].GetWeight(FishRarity.Epic) > 0f || config.FishingSpots[0].GetWeight(FishRarity.Legendary) > 0f)
            throw new System.InvalidOperationException("Beginner pond must not contain Epic or Legendary fish.");
        if (config.FishingSpots[1].GetWeight(FishRarity.Legendary) > 0f)
            throw new System.InvalidOperationException("Intermediate lake must not contain Legendary fish.");
        if (config.FishingSpots[2].GetWeight(FishRarity.Legendary) <= 0f)
            throw new System.InvalidOperationException("Advanced lake needs a Legendary chance.");
        if (!(config.FishingSpots[0].DifficultyMultiplier < config.FishingSpots[1].DifficultyMultiplier &&
              config.FishingSpots[1].DifficultyMultiplier < config.FishingSpots[2].DifficultyMultiplier))
            throw new System.InvalidOperationException("Fishing spot difficulty must increase with progression.");

        var catalog = FishCatalog.CreateDefault();
        for (int spotIndex = 0; spotIndex < config.FishingSpots.Count; spotIndex++)
        {
            FishingSpotDefinition spot = config.FishingSpots[spotIndex];
            for (int sample = 0; sample < 1000; sample++)
            {
                FishDefinition selected = spot.SelectFish(catalog);
                if (selected == null || spot.GetWeight(selected.Rarity) <= 0f)
                    throw new System.InvalidOperationException($"{spot.DisplayName} selected a forbidden fish rarity.");
            }
        }

        Scene scene = SceneManager.GetActiveScene();
        PixelMapBuilder builder = Object.FindFirstObjectByType<PixelMapBuilder>();
        if (scene.path == FishingScenePath)
        {
            if (builder == null) throw new System.InvalidOperationException("Fishing scene preview has no map builder.");
            builder.Build();
            if (builder.FishingSpots.Count != 1)
                throw new System.InvalidOperationException("Fishing scene preview must contain only the first lake.");
        }

        Debug.Log("MAP_VALIDATION_OK: 3 future spot definitions, first-lake-only scene, rarity tables, difficulty progression, and selection samples verified.");
    }

    private static FishingMapConfig EnsureMapConfig()
    {
        FishingMapConfig config = AssetDatabase.LoadAssetAtPath<FishingMapConfig>(MapConfigPath);
        if (config != null)
        {
            if (config.EnsureFirstAreaLayout())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        // Replace an invalid/missing-script config left by an interrupted first import.
        if (AssetDatabase.LoadMainAssetAtPath(MapConfigPath) != null) AssetDatabase.DeleteAsset(MapConfigPath);

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Data"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Data");
        }

        config = ScriptableObject.CreateInstance<FishingMapConfig>();
        config.ApplyDefaults();
        AssetDatabase.CreateAsset(config, MapConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    private static FishingBalanceConfig EnsureBalanceConfig()
    {
        FishingBalanceConfig config = AssetDatabase.LoadAssetAtPath<FishingBalanceConfig>(BalanceConfigPath);
        if (config != null)
        {
            bool changed = config.EnsureValid();
            changed |= ApplyReleasedArt(config);
            if (changed)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        if (AssetDatabase.LoadMainAssetAtPath(BalanceConfigPath) != null) AssetDatabase.DeleteAsset(BalanceConfigPath);
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Data"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Data");
        }

        config = ScriptableObject.CreateInstance<FishingBalanceConfig>();
        config.ApplyDefaults();
        ApplyReleasedArt(config);
        AssetDatabase.CreateAsset(config, BalanceConfigPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created FishingBalanceConfig with editable fish and rod values.");
        return config;
    }

    private static EarlyGameQuestConfig EnsureQuestConfig()
    {
        EarlyGameQuestConfig config = AssetDatabase.LoadAssetAtPath<EarlyGameQuestConfig>(QuestConfigPath);
        if (config != null)
        {
            if (config.EnsureValid())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            return config;
        }

        config = ScriptableObject.CreateInstance<EarlyGameQuestConfig>();
        config.ApplyDefaults();
        AssetDatabase.CreateAsset(config, QuestConfigPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created EarlyGameQuestConfig with the first-lake quest line.");
        return config;
    }

    private static bool ApplyReleasedArt(FishingBalanceConfig config)
    {
        if (config == null) return false;
        bool changed = false;
        changed |= SetFishArt(config, "common_carp", "Assets/Art/Fish/Fish5.png");
        changed |= SetFishArt(config, "atlantic_salmon", "Assets/Art/Fish/Fish4.png");
        changed |= SetFishArt(config, "northern_pike", "Assets/Art/Fish/Fish1.png");
        changed |= SetFishArt(config, "golden_salmon", "Assets/Art/Fish/Fish3.png");
        changed |= SetFishArt(config, "ancient_pike", "Assets/Art/Fish/Fish2.png");
        changed |= SetFishArt(config, "squid", "Assets/Art/Fish/Fish6.png");
        changed |= SetFishArt(config, "pufferfish", "Assets/Art/Fish/Fish7.png");
        changed |= SetFishArt(config, "crucian_carp", "Assets/Art/Fish/Fish8.png");
        changed |= SetFishArt(config, "swordfish", "Assets/Art/Fish/Fish9.png");
        changed |= SetFishArt(config, "bluefin_tuna", "Assets/Art/Fish/Fish10.png");
        changed |= SetRodArt(config, "basic_rod", "Assets/Art/Fishing rod/fishing rod1.png");
        changed |= SetRodArt(config, "sturdy_rod", "Assets/Art/Fishing rod/fishing rod2.png");
        changed |= SetRodArt(config, "river_rod", "Assets/Art/Fishing rod/fishing rod3.png");

        FishDefinition pike = config.Fish?.Find(fish => fish.Id == "northern_pike");
        if (pike != null && pike.DisplayName == "노던 파이크") { pike.DisplayName = "강꼬치고기"; changed = true; }
        FishDefinition catfish = config.Fish?.Find(fish => fish.Id == "golden_salmon");
        if (catfish != null && catfish.DisplayName == "황금 연어")
        {
            catfish.DisplayName = "메기"; catfish.Rarity = FishRarity.Common; catfish.SalePrice = 260;
            catfish.ReelZoneSize = .26f; catfish.MovementAcceleration = .19f; catfish.MaxSpeed = .27f;
            catfish.DirectionChangeInterval = 1.55f; catfish.DirectionImpulse = .10f; catfish.CatchRate = .32f; catfish.EscapeRate = .13f;
            changed = true;
        }
        FishDefinition eel = config.Fish?.Find(fish => fish.Id == "ancient_pike");
        if (eel != null && eel.DisplayName == "고대 파이크")
        {
            eel.DisplayName = "민물장어"; eel.Rarity = FishRarity.Uncommon; eel.SalePrice = 520;
            eel.ReelZoneSize = .20f; eel.MovementAcceleration = .31f; eel.MaxSpeed = .39f;
            eel.DirectionChangeInterval = 1f; eel.DirectionImpulse = .17f; eel.CatchRate = .26f; eel.EscapeRate = .19f;
            changed = true;
        }
        return changed;
    }

    private static bool SetFishArt(FishingBalanceConfig config, string id, string path)
    {
        FishDefinition definition = config.Fish?.Find(fish => fish != null && fish.Id == id);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (definition == null || texture == null || definition.Icon == texture) return false;
        definition.Icon = texture;
        return true;
    }

    private static bool SetRodArt(FishingBalanceConfig config, string id, string path)
    {
        FishingRodDefinition definition = config.Rods?.Find(rod => rod != null && rod.Id == id);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (definition == null || texture == null || definition.Icon == texture) return false;
        definition.Icon = texture;
        return true;
    }
}
#endif
