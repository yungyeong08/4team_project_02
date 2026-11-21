#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FishingPreviewTools
{
    private const string FishingScenePath = "Assets/Scenes/FishingDemo.unity";
    private const string SmokeTestSessionKey = "Fishing.PlayModeSmokeTest";
    private const string SmokeTestSavePathEnvironment = "FISHING_TEST_SAVE_PATH";
    private const string ProgressionValidationSessionKey = "Fishing.ProgressionValidation.ContentExpansionV1";

    private const string MapConfigPath = "Assets/Resources/Data/FishingMapConfig.asset";
    private const string BalanceConfigPath = "Assets/Resources/Data/FishingBalanceConfig.asset";
    private const string QuestConfigPath = "Assets/Resources/Data/EarlyGameQuestConfig.asset";
    private const string AchievementConfigPath = "Assets/Resources/Data/AchievementConfig.asset";
    private const string DebtConfigPath = "Assets/Resources/Data/DebtConfig.asset";

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
        EnsureAchievementConfig();
        EnsureDebtConfig();
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
        AchievementConfig achievementConfig = EnsureAchievementConfig();
        DebtConfig debtConfig = EnsureDebtConfig();
        if (balance.Fish == null || balance.Fish.Count == 0) throw new System.InvalidOperationException("Fish catalog is empty.");
        if (balance.Rods == null || balance.Rods.Count < 2) throw new System.InvalidOperationException("Rod catalog needs a default rod and purchasable upgrades.");
        if (questConfig.Quests == null || questConfig.Quests.Count != 6)
            throw new System.InvalidOperationException("The first-area quest line must contain six quests.");
        if (achievementConfig.Achievements == null || achievementConfig.Achievements.Count != 27)
            throw new System.InvalidOperationException("Achievement config must contain the requested 27 achievements.");
        if (debtConfig.InitialDebt != 30000)
            throw new System.InvalidOperationException("Initial debt must be configured to 30,000G.");
        EarlyQuestDefinition firstCatchQuest = questConfig.Quests.Find(quest => quest.Id == "first_catch");
        EarlyQuestDefinition firstSaleQuest = questConfig.Quests.Find(quest => quest.Id == "first_sale");
        EarlyQuestDefinition rodUpgradeQuest = questConfig.Quests.Find(quest => quest.Id == "rod_upgrade");
        EarlyQuestDefinition useNewRodQuest = questConfig.Quests.Find(quest => quest.Id == "use_new_rod");
        EarlyQuestDefinition beginnerQuest = questConfig.Quests.Find(quest => quest.Id == "beginner_angler");
        EarlyQuestDefinition debtQuest = questConfig.Quests.Find(quest => quest.Id == "repay_debt");
        if (firstCatchQuest?.NextQuestId != "first_sale" || firstSaleQuest?.NextQuestId != "rod_upgrade" ||
            rodUpgradeQuest?.NextQuestId != "use_new_rod" || useNewRodQuest?.NextQuestId != "beginner_angler" ||
            beginnerQuest?.NextQuestId != "repay_debt" || debtQuest?.Goal != EarlyQuestGoal.RepayDebt ||
            debtQuest.TargetAmount != 500 || !string.IsNullOrEmpty(debtQuest.NextQuestId))
            throw new System.InvalidOperationException("The opening quest chain must end with a 500G debt repayment objective.");

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
        FishRarity[] requiredRarities =
        {
            FishRarity.Common, FishRarity.Uncommon, FishRarity.Rare, FishRarity.Epic
        };
        foreach (FishRarity rarity in requiredRarities)
            if (!rarities.Contains(rarity))
                throw new System.InvalidOperationException($"Fish catalog has no {rarity} species.");
        AchievementDefinition legendaryAchievement = achievementConfig.Achievements.Find(
            achievement => achievement != null && achievement.Id == "first_legendary");
        if (!rarities.Contains(FishRarity.Legendary) && legendaryAchievement != null && !legendaryAchievement.Hidden)
            throw new System.InvalidOperationException("The unavailable Legendary-fish achievement must stay hidden in this content phase.");

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
            DebtRemaining = 4200,
            LifetimeDebtRepaid = 800,
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
            !restored.EarlyGameProgress.OpeningSeen || restored.DebtRemaining != 4200 ||
            restored.LifetimeDebtRepaid != 800 || restored.Version != FishingGameSaveData.CurrentVersion)
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
            if (!inventory.Add(fishId, 3) || inventory.Add(fishId))
                throw new System.InvalidOperationException("New fish discovery must be true only for the first acquisition.");
            if (inventory.GetCount(fishId) != 4 || inventory.GetLifetimeCaught(fishId) != 4 || !inventory.IsDiscovered(fishId))
                throw new System.InvalidOperationException("Fish inventory and codex progress are not sharing data.");
            if (inventory.RemoveAll(fishId) != 4 || inventory.GetCount(fishId) != 0 || inventory.GetLifetimeCaught(fishId) != 4)
                throw new System.InvalidOperationException("Selling must remove owned fish without erasing codex history.");

            PlayerWallet wallet = validationObject.AddComponent<PlayerWallet>();
            wallet.Initialize(2000);
            PlayerDebt debt = validationObject.AddComponent<PlayerDebt>();
            debt.InitializeNewGame(5000);
            EarlyGameQuestSystem quests = validationObject.AddComponent<EarlyGameQuestSystem>();
            quests.Setup(validationObject.transform, wallet, FishingMapConfig.CreateDefaultSpots()[0], null, null, debt);
            debt.DebtRepaid += quests.NotifyDebtRepaid;
            quests.NotifyFishCaught(EarlyGameQuestSystem.FirstLakeId, FishingRodCatalog.DefaultRodId);
            quests.NotifyFishSold(1);
            quests.NotifyRodPurchased(EarlyGameQuestSystem.UpgradeRodId);
            for (int i = 0; i < 2; i++) quests.NotifyFishCaught(EarlyGameQuestSystem.FirstLakeId, EarlyGameQuestSystem.UpgradeRodId);
            for (int i = 0; i < 3; i++) quests.NotifyFishCaught(EarlyGameQuestSystem.FirstLakeId, FishingRodCatalog.DefaultRodId);
            if (quests.ActiveQuest?.Goal != EarlyQuestGoal.RepayDebt)
                throw new System.InvalidOperationException("Debt objective did not activate after the fishing quests.");
            if (debt.TryRepay(wallet, 100) != 100 || quests.QuestProgress != 100 ||
                debt.TryRepay(wallet, 200) != 200 || quests.QuestProgress != 300 ||
                debt.TryRepay(wallet, 200) != 200)
                throw new System.InvalidOperationException("Debt repayment event progress did not accumulate 100 + 200 + 200.");
            if (!quests.NextRegionReady || quests.ActiveQuest != null || quests.CompletedQuestIds.Count != 6 ||
                debt.RemainingDebt != 4500 || debt.LifetimeRepaid != 500 || wallet.Balance != 2450)
                throw new System.InvalidOperationException("Debt quest completion, rewards, or debt balance failed.");
        }
        finally
        {
            Object.DestroyImmediate(validationObject);
        }

        ValidateContentExpansion(balance, achievementConfig, debtConfig);

        Debug.Log("PROGRESSION_VALIDATION_OK: fishing, sales, rods, 30,000G debt, 27 achievements, areas, rewards, and save data verified.");
    }

    private static void ValidateContentExpansion(FishingBalanceConfig balance, AchievementConfig achievementConfig, DebtConfig debtConfig)
    {
        var go = new GameObject("Content Expansion Validation (Temporary)");
        try
        {
            PlayerWallet wallet = go.AddComponent<PlayerWallet>();
            wallet.Initialize(40000);
            FishInventory fish = go.AddComponent<FishInventory>();
            FishingRodInventory rods = go.AddComponent<FishingRodInventory>();
            rods.EnsureDefaultRod();
            PlayerDebt debt = go.AddComponent<PlayerDebt>();
            debt.InitializeNewGame(debtConfig.InitialDebt);
            int fullEvents = 0;
            int clearEvents = 0;
            debt.DebtFullyRepaid += () => fullEvents++;
            debt.GameCleared += () => clearEvents++;

            AchievementSystem achievements = go.AddComponent<AchievementSystem>();
            achievements.Setup(achievementConfig, wallet, fish, rods, debt, balance.Fish);
            fish.Add(balance.Fish[0].Id);
            achievements.NotifyFishingResolved(FishingResolution.Success, balance.Fish[0]);
            achievements.NotifySaleCompleted(1, 5000, false);
            if (!rods.TryAddAndEquip("sturdy_rod"))
                throw new System.InvalidOperationException("Additional rod setup failed.");
            achievements.NotifyRodPurchased("sturdy_rod");
            int paid = debt.TryRepay(wallet, 999999);
            if (paid != debtConfig.InitialDebt || debt.RemainingDebt != 0 || debt.LifetimeRepaid != debtConfig.InitialDebt ||
                !debt.IsFullyRepaid || !debt.IsGameCleared || fullEvents != 1 || clearEvents != 1)
                throw new System.InvalidOperationException("Debt transaction or one-time clear events failed.");
            if (!achievements.GetState("first_catch").Completed || !achievements.GetState("first_sale").Completed ||
                !achievements.GetState("first_debt_payment").Completed || !achievements.GetState("debt_free").Completed ||
                !achievements.GetState("new_life").Completed)
                throw new System.InvalidOperationException("Event-driven achievement completion failed.");

            AchievementSaveData achievementRoundTrip = JsonUtility.FromJson<AchievementSaveData>(
                JsonUtility.ToJson(achievements.CreateSnapshot()));
            if (achievementRoundTrip == null || achievementRoundTrip.States.Count != 27 ||
                achievementRoundTrip.Metrics.TotalSaleGold != 5000)
                throw new System.InvalidOperationException("Achievement save round trip failed.");

            var areaDefinitions = new System.Collections.Generic.List<FishingSpotDefinition>
            {
                new() { Id = "first", Enabled = true, InitiallyUnlocked = true },
                new() { Id = "future", Enabled = true, InitiallyUnlocked = false, RequiredQuestId = "future_quest" }
            };
            FishingAreaProgress areas = go.AddComponent<FishingAreaProgress>();
            areas.Setup(areaDefinitions, null);
            if (!areas.IsUnlocked("first") || areas.IsUnlocked("future") || !areas.TryUnlock("future"))
                throw new System.InvalidOperationException("Area lock and unlock state failed.");
            FishingAreaSaveData areaRoundTrip = JsonUtility.FromJson<FishingAreaSaveData>(JsonUtility.ToJson(areas.CreateSnapshot()));
            areas.ResetProgress();
            areas.Restore(areaRoundTrip);
            if (!areas.IsUnlocked("future")) throw new System.InvalidOperationException("Area save restore failed.");

            // A 20-species-era save contains no entries for new fish. Restoring it must keep
            // existing progress while leaving Fish21-Fish30 safely undiscovered.
            fish.Restore(new[]
            {
                new FishInventory.FishProgress
                {
                    FishId = balance.Fish[0].Id, Count = 2, LifetimeCaught = 5, Discovered = true
                }
            });
            if (fish.GetCount(balance.Fish[0].Id) != 2 || fish.GetLifetimeCaught(balance.Fish[0].Id) != 5 ||
                fish.IsDiscovered("fish_21"))
                throw new System.InvalidOperationException("Existing save progress was not preserved while extending the fish catalog.");

            int expectedSale = balance.Fish[0].SalePrice * 2;
            for (int fishIndex = 0; fishIndex < balance.Fish.Count; fishIndex++)
            {
                FishDefinition definition = balance.Fish[fishIndex];
                bool isNewDiscovery = fish.Add(definition.Id);
                if (isNewDiscovery != (fishIndex > 0))
                    throw new System.InvalidOperationException($"NEW discovery state is incorrect for {definition.Id}.");
                expectedSale += definition.SalePrice;
            }
            if (!achievements.GetState("all_fish").Completed || achievements.GetState("first_legendary").Completed)
                throw new System.InvalidOperationException("Collection achievements do not match the 30-fish non-Legendary catalog.");

            FishMarketSystem market = go.AddComponent<FishMarketSystem>();
            market.Setup(null, null, wallet, fish, balance.Fish);
            if (market.TotalSaleValue != expectedSale)
                throw new System.InvalidOperationException("Sell-all preview does not use FishData prices for the full catalog.");
            int moneyBeforeSale = wallet.Balance;
            if (!market.SellFish("fish_21") || fish.GetCount("fish_21") != 0 ||
                wallet.Balance - moneyBeforeSale != 180)
                throw new System.InvalidOperationException("Individual sale failed for Fish21.");
            int remainingExpectedSale = expectedSale - 180;
            if (market.SellAllFish() != remainingExpectedSale || fish.TotalCount != 0)
                throw new System.InvalidOperationException("Sell-all failed for the expanded 30-fish catalog.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [MenuItem("Tools/Fishing/Run PlayMode Smoke Test")]
    public static void RunPlayModeSmokeTest()
    {
        string testSavePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fishing-playmode-smoke-save.json");
        System.Environment.SetEnvironmentVariable(SmokeTestSavePathEnvironment, testSavePath);
        DeleteSmokeTestSaveFiles(testSavePath);
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
            if (builder.FishingSpots.Count != 3)
                throw new System.InvalidOperationException($"Runtime map must contain three fishing spots, found {builder.FishingSpots.Count}.");
            if (builder.FishingShop == null || builder.FishMarket == null) throw new System.InvalidOperationException("Shop objects were not created.");

            FishingGameController controller = Object.FindFirstObjectByType<FishingGameController>();
            FishingShopSystem shop = Object.FindFirstObjectByType<FishingShopSystem>();
            FishMarketSystem market = Object.FindFirstObjectByType<FishMarketSystem>();
            FishInventory fishInventory = Object.FindFirstObjectByType<FishInventory>();
            FishingRodInventory rodInventory = Object.FindFirstObjectByType<FishingRodInventory>();
            PlayerProgressUI progressUi = Object.FindFirstObjectByType<PlayerProgressUI>();
            GameSaveSystem saveSystem = Object.FindFirstObjectByType<GameSaveSystem>();
            EarlyGameQuestSystem earlyGameQuests = Object.FindFirstObjectByType<EarlyGameQuestSystem>();
            PlayerDebt playerDebt = Object.FindFirstObjectByType<PlayerDebt>();
            AchievementSystem achievements = Object.FindFirstObjectByType<AchievementSystem>();
            FishingAreaProgress areaProgress = Object.FindFirstObjectByType<FishingAreaProgress>();
            PlayerWallet wallet = Object.FindFirstObjectByType<PlayerWallet>();
            if (controller == null || shop == null || market == null || fishInventory == null || rodInventory == null ||
                progressUi == null || saveSystem == null || earlyGameQuests == null || playerDebt == null ||
                achievements == null || areaProgress == null || wallet == null)
                throw new System.InvalidOperationException("One or more progression systems were not created at runtime.");
            if (!rodInventory.Owns(FishingRodCatalog.DefaultRodId) || string.IsNullOrWhiteSpace(rodInventory.EquippedRodId) ||
                !rodInventory.Owns(rodInventory.EquippedRodId))
                throw new System.InvalidOperationException("The player does not have a valid equipped fishing rod.");
            if (Application.isBatchMode && !saveSystem.ResetGame())
                throw new System.InvalidOperationException("The isolated smoke-test save could not be reset.");

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

            if (Application.isBatchMode)
            {
                // Run one deterministic controller cycle in the isolated validation project.
                FishingSpotDefinition firstSpot = builder.FishingSpots[0];
                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                System.Reflection.MethodInfo cast = typeof(FishingGameController).GetMethod("Cast", flags);
                System.Reflection.MethodInfo bite = typeof(FishingGameController).GetMethod("Bite", flags);
                System.Reflection.MethodInfo reset = typeof(FishingGameController).GetMethod("ResetFishing", flags);
                System.Reflection.FieldInfo progress = typeof(FishingGameController).GetField("progress", flags);
                if (cast == null || bite == null || reset == null || progress == null)
                    throw new System.InvalidOperationException("Fishing cycle test hooks were not found.");
                int caughtBefore = fishInventory.TotalLifetimeCaught;
                cast.Invoke(controller, new object[] { firstSpot });
                bite.Invoke(controller, null);
                progress.SetValue(controller, 1f);
                reset.Invoke(controller, new object[] { "validation success" });
                if (fishInventory.TotalLifetimeCaught != caughtBefore + 1 || !achievements.GetState("first_catch").Completed)
                    throw new System.InvalidOperationException("Fishing success cycle did not update inventory and achievements once.");

                string caughtFishId = null;
                FishingBalanceConfig runtimeBalance = FishingBalanceConfig.LoadOrCreateRuntimeDefault();
                foreach (FishDefinition fishDefinition in runtimeBalance.Fish)
                    if (fishInventory.GetCount(fishDefinition.Id) > 0) { caughtFishId = fishDefinition.Id; break; }
                if (string.IsNullOrEmpty(caughtFishId) || !market.SellFish(caughtFishId) ||
                    !achievements.GetState("first_sale").Completed)
                    throw new System.InvalidOperationException("Catch-to-sale transaction failed.");

                wallet.AddMoney(10000);
                if (!shop.TryPurchase("sturdy_rod") || !achievements.GetState("buy_rod_1").Completed)
                    throw new System.InvalidOperationException("Rod purchase transaction failed.");
                if (playerDebt.TryRepay(wallet, 100) != 100 || !achievements.GetState("first_debt_payment").Completed)
                    throw new System.InvalidOperationException("Debt repayment transaction failed.");
                if (!saveSystem.Save()) throw new System.InvalidOperationException("Runtime save failed.");
            }

            Debug.Log("PLAYMODE_SMOKE_OK: map, fishing success, inventory, sale, rod purchase, debt repayment, achievements, and save verified.");
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
        string testSavePath = System.Environment.GetEnvironmentVariable(SmokeTestSavePathEnvironment);
        DeleteSmokeTestSaveFiles(testSavePath);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
            return;
        }
        System.Environment.SetEnvironmentVariable(SmokeTestSavePathEnvironment, null);
        EditorApplication.ExitPlaymode();
    }

    private static void DeleteSmokeTestSaveFiles(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string[] paths = { path, path + ".bak", path + ".tmp" };
        foreach (string candidate in paths)
            if (System.IO.File.Exists(candidate)) System.IO.File.Delete(candidate);
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
        if (config.FishingSpots[2].GetWeight(FishRarity.Legendary) > 0f ||
            config.FishingSpots[2].GetWeight(FishRarity.Epic) <= 0f)
            throw new System.InvalidOperationException("Advanced lake must contain Epic fish but no Legendary fish in this content phase.");
        if (!(config.FishingSpots[0].DifficultyMultiplier < config.FishingSpots[1].DifficultyMultiplier &&
              config.FishingSpots[1].DifficultyMultiplier < config.FishingSpots[2].DifficultyMultiplier))
            throw new System.InvalidOperationException("Fishing spot difficulty must increase with progression.");

        var catalog = FishCatalog.CreateDefault();
        if (catalog.Count != 30)
            throw new System.InvalidOperationException($"Expected 30 fish definitions, found {catalog.Count}.");
        var knownIds = new System.Collections.Generic.HashSet<string>();
        foreach (FishDefinition fish in catalog)
            if (fish == null || string.IsNullOrWhiteSpace(fish.Id) || !knownIds.Add(fish.Id))
                throw new System.InvalidOperationException("Fish catalog contains a missing or duplicate ID.");
        foreach (FishDefinition fish in catalog)
            if (fish.Rarity == FishRarity.Legendary)
                throw new System.InvalidOperationException($"{fish.DisplayName} must not use Legendary rarity in this content phase.");

        var expectedPools = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, float>>
        {
            ["village_pond"] = new()
            {
                ["northern_pike"] = 15f, ["common_carp"] = 15f, ["sturgeon"] = 12f,
                ["golden_salmon"] = 12f, ["fish_21"] = 10f, ["ancient_pike"] = 9f,
                ["fish_22"] = 8f, ["atlantic_salmon"] = 7f, ["fish_23"] = 6f, ["fish_27"] = 6f
            },
            ["deep_lake"] = new()
            {
                ["ancient_pike"] = 8f, ["atlantic_salmon"] = 9f, ["squid"] = 9f,
                ["pufferfish"] = 8f, ["crucian_carp"] = 9f, ["snakehead"] = 8f,
                ["jade_carp"] = 7f, ["fish_22"] = 7f, ["fish_23"] = 7f,
                ["fish_24"] = 7f, ["fish_25"] = 6f, ["fish_27"] = 6f,
                ["fish_28"] = 5f, ["golden_crucian"] = 4f
            },
            ["legend_lake"] = new()
            {
                ["swordfish"] = 10f, ["bluefin_tuna"] = 9f, ["lake_catfish"] = 4f,
                ["silk_carp"] = 9f, ["red_trout"] = 8f, ["golden_crucian"] = 8f,
                ["golden_minnow"] = 7f, ["moonlight_carp"] = 8f,
                ["legendary_golden_carp"] = 4f, ["fish_24"] = 6f, ["fish_25"] = 6f,
                ["fish_26"] = 6f, ["fish_28"] = 6f, ["fish_29"] = 3f, ["fish_30"] = 6f
            }
        };

        var fishById = new System.Collections.Generic.Dictionary<string, FishDefinition>();
        foreach (FishDefinition fish in catalog) fishById.Add(fish.Id, fish);
        var pooledIds = new System.Collections.Generic.HashSet<string>();
        float[] expectedValues = { 166.6f, 351.2f, 782.4f };

        for (int spotIndex = 0; spotIndex < config.FishingSpots.Count; spotIndex++)
        {
            FishingSpotDefinition spot = config.FishingSpots[spotIndex];
            if (!spot.Enabled || !spot.InitiallyUnlocked)
                throw new System.InvalidOperationException($"{spot.DisplayName} must be enabled and unlocked for the current test build.");
            if (spot.FishPool == null || spot.FishPool.Count == 0)
                throw new System.InvalidOperationException($"{spot.DisplayName} has no exact fish pool.");
            if (!expectedPools.TryGetValue(spot.Id, out var expectedPool) || spot.FishPool.Count != expectedPool.Count)
                throw new System.InvalidOperationException($"{spot.DisplayName} does not match the expected species count.");
            var poolIds = new System.Collections.Generic.HashSet<string>();
            float totalWeight = 0f;
            foreach (FishPoolEntry entry in spot.FishPool)
            {
                if (entry == null || !knownIds.Contains(entry.FishId) || !poolIds.Add(entry.FishId))
                    throw new System.InvalidOperationException($"{spot.DisplayName} contains a missing, unknown or duplicate fish ID.");
                if (!expectedPool.TryGetValue(entry.FishId, out float expectedWeight) ||
                    Mathf.Abs(expectedWeight - entry.Weight) > .01f)
                    throw new System.InvalidOperationException($"{spot.DisplayName} has an unexpected species or weight: {entry.FishId} = {entry.Weight}.");
                totalWeight += Mathf.Max(0f, entry.Weight);
                pooledIds.Add(entry.FishId);
            }
            if (Mathf.Abs(totalWeight - 100f) > .01f)
                throw new System.InvalidOperationException($"{spot.DisplayName} weights total {totalWeight}, expected 100.");

            float expectedValue = 0f;
            foreach (FishPoolEntry entry in spot.FishPool)
                expectedValue += fishById[entry.FishId].SalePrice * entry.Weight / totalWeight;
            if (Mathf.Abs(expectedValue - expectedValues[spotIndex]) > .1f)
                throw new System.InvalidOperationException($"{spot.DisplayName} expected sale value is {expectedValue:F1}G, expected {expectedValues[spotIndex]:F1}G.");

            var sampleCounts = new System.Collections.Generic.Dictionary<string, int>();
            Random.State previousRandomState = Random.state;
            Random.InitState(20261121 + spotIndex);
            for (int sample = 0; sample < 3000; sample++)
            {
                FishDefinition selected = spot.SelectFish(catalog);
                if (selected == null || spot.GetFishWeight(selected.Id) <= 0f)
                    throw new System.InvalidOperationException($"{spot.DisplayName} selected a fish outside its configured pool.");
                sampleCounts[selected.Id] = sampleCounts.TryGetValue(selected.Id, out int count) ? count + 1 : 1;
            }
            Random.state = previousRandomState;
            if (sampleCounts.Count != spot.FishPool.Count)
                throw new System.InvalidOperationException($"{spot.DisplayName} selection sample did not reach every configured species.");
            Debug.Log($"FISHING_SPOT_SAMPLE: {spot.DisplayName}, 3000 catches, expected value {expectedValue:F1}G, species {sampleCounts.Count}.");
        }

        if (pooledIds.Count != catalog.Count)
            throw new System.InvalidOperationException($"Only {pooledIds.Count} of {catalog.Count} fish appear in a fishing spot.");

        Scene scene = SceneManager.GetActiveScene();
        PixelMapBuilder builder = Object.FindFirstObjectByType<PixelMapBuilder>();
        if (scene.path == FishingScenePath)
        {
            if (builder == null) throw new System.InvalidOperationException("Fishing scene preview has no map builder.");
            builder.Build();
            if (builder.FishingSpots.Count != 3)
                throw new System.InvalidOperationException("Fishing scene preview must contain all three lakes.");
        }

        Debug.Log("MAP_VALIDATION_OK: 30 fish, 3 playable spots, exact 100-weight pools, economy targets, full species coverage, and 3000 selection samples per spot verified.");
    }

    private static FishingMapConfig EnsureMapConfig()
    {
        FishingMapConfig config = AssetDatabase.LoadAssetAtPath<FishingMapConfig>(MapConfigPath);
        if (config != null)
        {
            if (config.EnsureThreeAreaLayout())
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

    private static AchievementConfig EnsureAchievementConfig()
    {
        AchievementConfig config = AssetDatabase.LoadAssetAtPath<AchievementConfig>(AchievementConfigPath);
        if (config != null && config.Achievements != null && config.Achievements.Count == 27)
        {
            AchievementDefinition rare = config.Achievements.Find(item => item.Id == "first_rare");
            AchievementDefinition legendary = config.Achievements.Find(item => item.Id == "first_legendary");
            if (rare != null && legendary != null && rare.Target == 1 && rare.RequiredRarity == FishRarity.Rare &&
                legendary.Target == 1 && legendary.RequiredRarity == FishRarity.Legendary) return config;
            config.ApplyDefaults();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }
        if (AssetDatabase.LoadMainAssetAtPath(AchievementConfigPath) != null) AssetDatabase.DeleteAsset(AchievementConfigPath);
        config = ScriptableObject.CreateInstance<AchievementConfig>();
        config.ApplyDefaults();
        AssetDatabase.CreateAsset(config, AchievementConfigPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    private static DebtConfig EnsureDebtConfig()
    {
        DebtConfig config = AssetDatabase.LoadAssetAtPath<DebtConfig>(DebtConfigPath);
        if (config == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(DebtConfigPath) != null) AssetDatabase.DeleteAsset(DebtConfigPath);
            config = ScriptableObject.CreateInstance<DebtConfig>();
            config.InitialDebt = 30000;
            AssetDatabase.CreateAsset(config, DebtConfigPath);
            AssetDatabase.SaveAssets();
        }
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
        changed |= SetFishArt(config, "lake_catfish", "Assets/Art/Fish/Fish11.png");
        changed |= SetFishArt(config, "silk_carp", "Assets/Art/Fish/Fish12.png");
        changed |= SetFishArt(config, "red_trout", "Assets/Art/Fish/Fish13.png");
        changed |= SetFishArt(config, "golden_crucian", "Assets/Art/Fish/Fish14.png");
        changed |= SetFishArt(config, "snakehead", "Assets/Art/Fish/Fish15.png");
        changed |= SetFishArt(config, "jade_carp", "Assets/Art/Fish/Fish16.png");
        changed |= SetFishArt(config, "golden_minnow", "Assets/Art/Fish/Fish17.png");
        changed |= SetFishArt(config, "sturgeon", "Assets/Art/Fish/Fish18.png");
        changed |= SetFishArt(config, "moonlight_carp", "Assets/Art/Fish/Fish19.png");
        changed |= SetFishArt(config, "legendary_golden_carp", "Assets/Art/Fish/Fish20.png");
        changed |= SetFishArt(config, "fish_21", "Assets/Art/Fish/Fish21.png");
        changed |= SetFishArt(config, "fish_22", "Assets/Art/Fish/Fish22.png");
        changed |= SetFishArt(config, "fish_23", "Assets/Art/Fish/Fish23.png");
        changed |= SetFishArt(config, "fish_24", "Assets/Art/Fish/Fish24.png");
        changed |= SetFishArt(config, "fish_25", "Assets/Art/Fish/Fish25.png");
        changed |= SetFishArt(config, "fish_26", "Assets/Art/Fish/Fish26.png");
        changed |= SetFishArt(config, "fish_27", "Assets/Art/Fish/Fish27.png");
        changed |= SetFishArt(config, "fish_28", "Assets/Art/Fish/Fish28.png");
        changed |= SetFishArt(config, "fish_29", "Assets/Art/Fish/Fish29.png");
        changed |= SetFishArt(config, "fish_30", "Assets/Art/Fish/Fish30.png");
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
