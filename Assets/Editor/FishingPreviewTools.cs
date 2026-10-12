#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FishingPreviewTools
{
    private const string FishingScenePath = "Assets/Scenes/FishingDemo.unity";
    private const string SmokeTestSessionKey = "Fishing.PlayModeSmokeTest";

    private const string MapConfigPath = "Assets/Resources/Data/FishingMapConfig.asset";

    [MenuItem("Tools/Fishing/Rebuild Full Map Preview")]
    public static void RefreshArtPreview()
    {
        EnsureMapConfig();
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

            foreach (FishingSpotDefinition spot in builder.FishingSpots)
            {
                player.transform.position = spot.ShorePosition;
                Physics2D.SyncTransforms();
                if (builder.GetNearestFishingSpot(player.transform.position) != spot)
                    throw new System.InvalidOperationException($"Interaction check failed for {spot.DisplayName}.");
            }

            Debug.Log("PLAYMODE_SMOKE_OK: runtime map, player, shops, and all 3 fishing spot interactions verified.");
            SessionState.SetBool(SmokeTestSessionKey, false);
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(SmokeTestSessionKey, false);
            EditorApplication.Exit(1);
        }
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
}
#endif
