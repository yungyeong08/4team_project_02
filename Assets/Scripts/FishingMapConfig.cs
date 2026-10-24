using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class FishingWaterTileSet
{
    public Texture2D UpLeft;
    public Texture2D UpMiddle;
    public Texture2D UpRight;
    public Texture2D LeftMiddle;
    public Texture2D Middle;
    public Texture2D RightMiddle;
    public Texture2D DownLeft;
    public Texture2D DownMiddle;
    public Texture2D DownRight;

    public bool IsComplete =>
        UpLeft != null && UpMiddle != null && UpRight != null &&
        LeftMiddle != null && Middle != null && RightMiddle != null &&
        DownLeft != null && DownMiddle != null && DownRight != null;

    public Texture2D GetTile(int column, int row, int columns, int rows)
    {
        bool left = column == 0;
        bool right = column == columns - 1;
        bool bottom = row == 0;
        bool top = row == rows - 1;

        if (top) return left ? UpLeft : right ? UpRight : UpMiddle;
        if (bottom) return left ? DownLeft : right ? DownRight : DownMiddle;
        return left ? LeftMiddle : right ? RightMiddle : Middle;
    }
}

/// <summary>Shared map data. Replace this asset later without changing gameplay code.</summary>
[CreateAssetMenu(fileName = "FishingMapConfig", menuName = "Fishing/Fishing Map Config")]
public sealed class FishingMapConfig : ScriptableObject
{
    public Vector2 MapSize = new(24f, 18f);
    public Vector2 PlayerStart = new(-9f, -7f);
    public Vector2 FishingShopPosition = new(8f, -5.2f);
    public Vector2 FishMarketPosition = new(7f, 1.5f);
    public List<FishingSpotDefinition> FishingSpots = new();

    [Header("Replaceable map art")]
    public FishingWaterTileSet WaterTiles = new();
    [Min(1f)] public float WaterTilePixelsPerUnit = 32f;

    [Header("Decoration placement")]
    public List<Vector2> FlowerPositions = new();

    public void ApplyDefaults()
    {
        MapSize = new Vector2(24f, 18f);
        PlayerStart = new Vector2(-9f, -7f);
        FishingShopPosition = new Vector2(8f, -5.2f);
        FishMarketPosition = new Vector2(7f, 1.5f);
        FishingSpots = CreateDefaultSpots();
        FlowerPositions = CreateDefaultFlowerPositions();
    }

    public bool EnsureFirstAreaLayout()
    {
        bool changed = false;
        // The first playable milestone uses a compact starter map. Keep the other
        // spot definitions below as future content data without building them yet.
        if (Vector2.Distance(MapSize, new Vector2(44f, 30f)) < .05f)
        {
            MapSize = new Vector2(24f, 18f);
            PlayerStart = new Vector2(4f, -6f);
            FishingShopPosition = new Vector2(7f, -2.5f);
            FishMarketPosition = new Vector2(3.5f, -2.5f);
            changed = true;
        }

        if (FishingSpots != null && FishingSpots.Count > 0 &&
            FishingSpots[0] != null && FishingSpots[0].Id == "village_pond" &&
            Vector2.Distance(FishingSpots[0].WaterCenter, new Vector2(-9f, -8f)) < .05f)
        {
            FishingSpots[0] = CreateDefaultSpots()[0];
            changed = true;
        }

        // Migrate the old test-map shop positions that overlapped the beginner fishing interaction.
        if (Vector2.Distance(FishingShopPosition, new Vector2(3.2f, -8f)) < .05f)
        {
            FishingShopPosition = new Vector2(3f, -7.5f);
            changed = true;
        }
        if (Vector2.Distance(FishMarketPosition, new Vector2(-3.2f, -8f)) < .05f)
        {
            FishMarketPosition = new Vector2(-1.5f, -5.5f);
            changed = true;
        }
        if (FlowerPositions == null || FlowerPositions.Count == 0)
        {
            FlowerPositions = CreateDefaultFlowerPositions();
            changed = true;
        }
        return changed;
    }

    public static List<Vector2> CreateDefaultFlowerPositions()
    {
        return new List<Vector2>
        {
            new(-8.1f, 3.8f), new(-7.7f, 2.5f), new(-7.8f, 1.2f),
            new(-6.2f, 5.0f), new(-4.8f, 5.2f), new(-3.3f, 5.0f),
            new(5.4f, 3.6f), new(6.8f, 3.8f), new(8.3f, 3.4f),
            new(5.2f, -5.6f), new(6.0f, -6.4f), new(9.7f, -5.8f)
        };
    }

    public static List<FishingSpotDefinition> CreateDefaultSpots()
    {
        return new List<FishingSpotDefinition>
        {
            new(
                "village_pond", "마을 연못 · 초급",
                new Vector2(-3.5f, 1.6f), new Vector2(7f, 5.5f),
                new Vector2(.45f, -.3f), new Vector2(-.4f, -.3f),
                1.65f, .9f,
                new FishRarityWeight(FishRarity.Common, 70f),
                new FishRarityWeight(FishRarity.Uncommon, 25f),
                new FishRarityWeight(FishRarity.Rare, 5f),
                new FishRarityWeight(FishRarity.Epic, 0f),
                new FishRarityWeight(FishRarity.Legendary, 0f)),
            new(
                "forest_lake", "솔바람 호수 · 중급",
                new Vector2(10f, 1f), new Vector2(10f, 7f),
                new Vector2(4.65f, 1f), new Vector2(6.15f, 1f),
                1.7f, 1.08f,
                new FishRarityWeight(FishRarity.Common, 15f),
                new FishRarityWeight(FishRarity.Uncommon, 45f),
                new FishRarityWeight(FishRarity.Rare, 32f),
                new FishRarityWeight(FishRarity.Epic, 8f),
                new FishRarityWeight(FishRarity.Legendary, 0f)),
            new(
                "ancient_lake", "고요한 심연 · 고급",
                new Vector2(-10f, 8.5f), new Vector2(11f, 7f),
                new Vector2(-10f, 4.65f), new Vector2(-10f, 6.15f),
                1.75f, 1.25f,
                new FishRarityWeight(FishRarity.Common, 0f),
                new FishRarityWeight(FishRarity.Uncommon, 12f),
                new FishRarityWeight(FishRarity.Rare, 43f),
                new FishRarityWeight(FishRarity.Epic, 38f),
                new FishRarityWeight(FishRarity.Legendary, 7f))
        };
    }

    public static FishingMapConfig CreateRuntimeDefault()
    {
        FishingMapConfig config = CreateInstance<FishingMapConfig>();
        config.ApplyDefaults();
        return config;
    }
}
