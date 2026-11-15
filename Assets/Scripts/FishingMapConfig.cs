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
    public Vector2 MapSize = new(84f, 38f);
    public Vector2 PlayerStart = new(-33f, -11f);
    public Vector2 FishingShopPosition = new(-12f, -10f);
    public Vector2 FishMarketPosition = new(-12f, -5f);
    public List<FishingSpotDefinition> FishingSpots = new();

    [Header("Replaceable map art")]
    public FishingWaterTileSet WaterTiles = new();
    [Min(1f)] public float WaterTilePixelsPerUnit = 32f;

    [Header("Decoration placement")]
    public List<Vector2> FlowerPositions = new();

    public void ApplyDefaults()
    {
        MapSize = new Vector2(84f, 38f);
        PlayerStart = new Vector2(-33f, -11f);
        FishingShopPosition = new Vector2(-12f, -10f);
        FishMarketPosition = new Vector2(-12f, -5f);
        FishingSpots = CreateDefaultSpots();
        FlowerPositions = CreateDefaultFlowerPositions();
    }

    public bool EnsureThreeAreaLayout()
    {
        bool changed = false;
        bool legacyLayout = MapSize.x < 60f || MapSize.y < 30f || FishingSpots == null ||
            FishingSpots.Count != 3 ||
            FishingSpots.Exists(spot => spot == null || spot.FishPool == null || spot.FishPool.Count == 0);
        if (legacyLayout)
        {
            MapSize = new Vector2(84f, 38f);
            PlayerStart = new Vector2(-33f, -11f);
            FishingShopPosition = new Vector2(-12f, -10f);
            FishMarketPosition = new Vector2(-12f, -5f);
            FishingSpots = CreateDefaultSpots();
            FlowerPositions = CreateDefaultFlowerPositions();
            changed = true;
        }
        if (FlowerPositions == null || FlowerPositions.Count == 0)
        {
            FlowerPositions = CreateDefaultFlowerPositions();
            changed = true;
        }
        return changed;
    }

    // Kept so older editor tooling can migrate safely while packages recompile.
    public bool EnsureFirstAreaLayout() => EnsureThreeAreaLayout();

    public static List<Vector2> CreateDefaultFlowerPositions()
    {
        return new List<Vector2>
        {
            new(-36f, -3f), new(-32f, 2f), new(-27f, 7f), new(-18f, 5f),
            new(-11f, 4f), new(-7f, -1f), new(1f, -5f), new(7f, 9f),
            new(12f, 4f), new(16f, -8f), new(22f, 8f), new(29f, 6f),
            new(35f, -9f), new(38f, 2f), new(33f, 12f), new(-5f, 13f)
        };
    }

    public static List<FishingSpotDefinition> CreateDefaultSpots()
    {
        return new List<FishingSpotDefinition>
        {
            new FishingSpotDefinition(
                "village_pond", "마을 연못",
                new Vector2(-25f, -4f), new Vector2(8f, 6f),
                new Vector2(-20.4f, -4f), new Vector2(-21.7f, -4f),
                1.65f, .9f,
                new FishRarityWeight(FishRarity.Common, 80f),
                new FishRarityWeight(FishRarity.Uncommon, 20f),
                new FishRarityWeight(FishRarity.Rare, 0f),
                new FishRarityWeight(FishRarity.Epic, 0f),
                new FishRarityWeight(FishRarity.Legendary, 0f))
            {
                Enabled = true,
                InitiallyUnlocked = true,
                FishPool = new List<FishPoolEntry>
                {
                    new("northern_pike", 25f), new("common_carp", 22f),
                    new("golden_salmon", 18f), new("sturgeon", 15f),
                    new("ancient_pike", 12f), new("atlantic_salmon", 8f)
                }
            },
            new FishingSpotDefinition(
                "deep_lake", "깊은 호수",
                new Vector2(4f, 4f), new Vector2(10f, 7f),
                new Vector2(-1.6f, 4f), new Vector2(-.2f, 4f),
                1.7f, 1.05f,
                new FishRarityWeight(FishRarity.Common, 8f),
                new FishRarityWeight(FishRarity.Uncommon, 76f),
                new FishRarityWeight(FishRarity.Rare, 16f),
                new FishRarityWeight(FishRarity.Epic, 0f),
                new FishRarityWeight(FishRarity.Legendary, 0f))
            {
                Enabled = true,
                InitiallyUnlocked = true,
                FishPool = new List<FishPoolEntry>
                {
                    new("ancient_pike", 12f), new("atlantic_salmon", 12f),
                    new("squid", 12f), new("pufferfish", 10f),
                    new("crucian_carp", 12f), new("silk_carp", 8f),
                    new("golden_crucian", 8f), new("snakehead", 10f),
                    new("jade_carp", 8f), new("sturgeon", 8f)
                }
            },
            new FishingSpotDefinition(
                "legend_lake", "상급 호수",
                new Vector2(30f, -3f), new Vector2(11f, 8f),
                new Vector2(23.9f, -3f), new Vector2(25.3f, -3f),
                1.75f, 1.18f,
                new FishRarityWeight(FishRarity.Common, 0f),
                new FishRarityWeight(FishRarity.Uncommon, 0f),
                new FishRarityWeight(FishRarity.Rare, 90f),
                new FishRarityWeight(FishRarity.Epic, 10f),
                new FishRarityWeight(FishRarity.Legendary, 0f))
            {
                Enabled = true,
                InitiallyUnlocked = true,
                RequiredQuestId = "",
                FishPool = new List<FishPoolEntry>
                {
                    new("silk_carp", 16f), new("golden_crucian", 14f),
                    new("moonlight_carp", 14f), new("swordfish", 13f),
                    new("bluefin_tuna", 12f), new("red_trout", 11f),
                    new("golden_minnow", 10f), new("lake_catfish", 6f),
                    new("legendary_golden_carp", 4f)
                }
            }
        };
    }

    public static FishingMapConfig CreateRuntimeDefault()
    {
        FishingMapConfig config = CreateInstance<FishingMapConfig>();
        config.ApplyDefaults();
        return config;
    }
}
