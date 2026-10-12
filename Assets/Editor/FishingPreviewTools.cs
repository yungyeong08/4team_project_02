#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FishingPreviewTools
{
    private const string FishingScenePath = "Assets/Scenes/FishingDemo.unity";
    private const string SmokeTestSessionKey = "Fishing.PlayModeSmokeTest";
    private const string ProgressionValidationSessionKey = "Fishing.ProgressionValidation";

    private const string MapConfigPath = "Assets/Resources/Data/FishingMapConfig.asset";
    private const string BalanceConfigPath = "Assets/Resources/Data/FishingBalanceConfig.asset";

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
        Debug.Log("Fishing map preview rebuilt with three configured fishing spots.");
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
        if (balance.Fish == null || balance.Fish.Count == 0) throw new System.InvalidOperationException("Fish catalog is empty.");
        if (balance.Rods == null || balance.Rods.Count < 2) throw new System.InvalidOperationException("Rod catalog needs a default rod and purchasable upgrades.");

        var fishIds = new System.Collections.Generic.HashSet<string>();
        foreach (FishDefinition fish in balance.Fish)
        {
            if (fish == null || string.IsNullOrWhiteSpace(fish.Id) || !fishIds.Add(fish.Id))
                throw new System.InvalidOperationException("Fish ids must be non-empty and unique.");
            if (fish.SalePrice < 0 || fish.CatchRate <= 0f || fish.MaxSpeed < 0f)
                throw new System.InvalidOperationException($"Invalid balance values for fish {fish.Id}.");
        }

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
            PlayerY = -2f
        };
        FishingGameSaveData restored = JsonUtility.FromJson<FishingGameSaveData>(JsonUtility.ToJson(saveRoundTrip));
        if (restored == null || restored.Money != 777 || restored.FishProgress.Count != 1 || restored.FishProgress[0].LifetimeCaught != 5)
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
        }
        finally
        {
            Object.DestroyImmediate(validationObject);
        }

        Debug.Log("PROGRESSION_VALIDATION_OK: shared fish inventory, codex history, rod ownership/equipment, balance, and save-data JSON verified.");
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
            if (builder.FishingSpots.Count != 3) throw new System.InvalidOperationException("Runtime map does not contain three spots.");
            if (builder.FishingShop == null || builder.FishMarket == null) throw new System.InvalidOperationException("Shop objects were not created.");

            FishingGameController controller = Object.FindFirstObjectByType<FishingGameController>();
            FishingShopSystem shop = Object.FindFirstObjectByType<FishingShopSystem>();
            FishMarketSystem market = Object.FindFirstObjectByType<FishMarketSystem>();
            FishInventory fishInventory = Object.FindFirstObjectByType<FishInventory>();
            FishingRodInventory rodInventory = Object.FindFirstObjectByType<FishingRodInventory>();
            PlayerProgressUI progressUi = Object.FindFirstObjectByType<PlayerProgressUI>();
            GameSaveSystem saveSystem = Object.FindFirstObjectByType<GameSaveSystem>();
            if (controller == null || shop == null || market == null || fishInventory == null || rodInventory == null || progressUi == null || saveSystem == null)
                throw new System.InvalidOperationException("One or more progression systems were not created at runtime.");
            if (!rodInventory.Owns(FishingRodCatalog.DefaultRodId) || string.IsNullOrWhiteSpace(rodInventory.EquippedRodId) ||
                !rodInventory.Owns(rodInventory.EquippedRodId))
                throw new System.InvalidOperationException("The player does not have a valid equipped fishing rod.");

            Vector3 originalPosition = player.transform.position;
            foreach (FishingSpotDefinition spot in builder.FishingSpots)
            {
                player.transform.position = spot.ShorePosition;
                Physics2D.SyncTransforms();
                if (builder.GetNearestFishingSpot(player.transform.position) != spot)
                    throw new System.InvalidOperationException($"Interaction check failed for {spot.DisplayName}.");
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

            Debug.Log("PLAYMODE_SMOKE_OK: runtime map, player, UI/progression components, both shops, equipped rod, and all 3 fishing spot interactions verified.");
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

        if (Vector2.Distance(config.FishingSpots[0].ShorePosition, config.FishingSpots[1].ShorePosition) < 8f ||
            Vector2.Distance(config.FishingSpots[1].ShorePosition, config.FishingSpots[2].ShorePosition) < 8f ||
            Vector2.Distance(config.FishingSpots[0].ShorePosition, config.FishingSpots[2].ShorePosition) < 8f)
            throw new System.InvalidOperationException("Fishing spots are too close together.");

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
        if (scene.path == FishingScenePath && (builder == null || builder.FishingSpots.Count != 3))
            throw new System.InvalidOperationException("Fishing scene preview was not generated correctly.");

        Debug.Log("MAP_VALIDATION_OK: 3 fishing spots, spacing, rarity tables, difficulty progression, selection samples, and scene preview verified.");
    }

    private static FishingMapConfig EnsureMapConfig()
    {
        FishingMapConfig config = AssetDatabase.LoadAssetAtPath<FishingMapConfig>(MapConfigPath);
        if (config != null) return config;

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
            if (config.EnsureValid())
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
        AssetDatabase.CreateAsset(config, BalanceConfigPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Created FishingBalanceConfig with editable fish and rod values.");
        return config;
    }
}
#endif
