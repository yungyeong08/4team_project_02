using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the shared overworld and all enabled fishing areas from map data.</summary>
[ExecuteAlways]
public sealed class PixelMapBuilder : MonoBehaviour
{
    private const string ConfigResourcePath = "Data/FishingMapConfig";

    [SerializeField] private FishingMapConfig mapConfig;
    [SerializeField] private Color groundColor = new(.35f, .60f, .24f);
    [SerializeField] private Color pathColor = new(.68f, .57f, .34f);
    [SerializeField] private Color waterColor = new(.12f, .48f, .78f);
    [SerializeField] private Color deepWaterColor = new(.08f, .31f, .61f);

    private readonly List<FishingSpotDefinition> runtimeSpots = new();
    private FishingAreaProgress areaProgress;

    public Vector2 PlayerStart => Config.PlayerStart;
    public Vector2 MapSize => Config.MapSize;
    public IReadOnlyList<FishingSpotDefinition> FishingSpots => runtimeSpots;
    public Transform FishingShop { get; private set; }
    public Transform FishMarket { get; private set; }

    public void SetAreaProgress(FishingAreaProgress progress) => areaProgress = progress;

    private FishingMapConfig Config
    {
        get
        {
            if (mapConfig == null) mapConfig = Resources.Load<FishingMapConfig>(ConfigResourcePath);
            if (mapConfig == null) mapConfig = FishingMapConfig.CreateRuntimeDefault();
            if (mapConfig.FishingSpots == null || mapConfig.FishingSpots.Count == 0) mapConfig.ApplyDefaults();
            mapConfig.EnsureThreeAreaLayout();
            return mapConfig;
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying && transform.childCount == 0) Build();
        else RefreshSceneReferences();
    }

    public void EnsureDecorations() => Build();

    public void Build()
    {
        ClearGeneratedChildren();
        RefreshFishingSpots();

        Transform groundRoot = CreateRoot("01 Ground and Paths");
        Transform waterRoot = CreateRoot("02 Water Areas");
        Transform decorationRoot = CreateRoot("03 Trees and Grass");
        Transform gameplayRoot = CreateRoot("04 Gameplay Objects");

        CreateGroundAndPaths(groundRoot);
        for (int i = 0; i < runtimeSpots.Count; i++) CreateFishingArea(waterRoot, gameplayRoot, runtimeSpots[i], i);
        CreateDecorations(decorationRoot);
        CreateShops(gameplayRoot);
        CreateMapBounds(gameplayRoot);
    }

    /// <summary>
    /// Reconnects gameplay references to the editable objects already saved in the scene.
    /// This deliberately does not rebuild the hierarchy, so manual Scene-view changes survive Play Mode.
    /// </summary>
    public void RefreshSceneReferences()
    {
        RefreshFishingSpots();
        FishingShop = FindChildByName("Fishing Shop");
        FishMarket = FindChildByName("Fish Market");
    }

    private void RefreshFishingSpots()
    {
        runtimeSpots.Clear();
        for (int i = 0; i < Config.FishingSpots.Count; i++)
            if (Config.FishingSpots[i] != null && Config.FishingSpots[i].Enabled)
                runtimeSpots.Add(Config.FishingSpots[i]);
    }

    private Transform FindChildByName(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i] != transform && descendants[i].name == objectName)
                return descendants[i];
        }
        return null;
    }

    public FishingSpotDefinition GetNearestFishingSpot(Vector2 playerPosition)
    {
        FishingSpotDefinition nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < runtimeSpots.Count; i++)
        {
            FishingSpotDefinition spot = runtimeSpots[i];
            if (areaProgress != null && !areaProgress.IsUnlocked(spot.Id)) continue;
            float distance = Vector2.Distance(playerPosition, spot.ShorePosition);
            if (distance <= spot.InteractionRange && distance < nearestDistance)
            {
                nearest = spot;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    public FishingSpotDefinition GetFishingSpot(string id)
    {
        return runtimeSpots.Find(spot => spot != null && spot.Id == id);
    }

    public FishingSpotDefinition GetNearestRegionSpot(Vector2 playerPosition)
    {
        FishingSpotDefinition nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < runtimeSpots.Count; i++)
        {
            FishingSpotDefinition spot = runtimeSpots[i];
            if (spot == null || (areaProgress != null && !areaProgress.IsUnlocked(spot.Id))) continue;
            float regionRadius = Mathf.Max(7.5f, Mathf.Max(spot.WaterSize.x, spot.WaterSize.y) * .72f);
            float distance = Vector2.Distance(playerPosition, spot.WaterCenter);
            if (distance <= regionRadius && distance < nearestDistance)
            {
                nearest = spot;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    public FishingSpotDefinition GetNearestLockedFishingSpot(Vector2 playerPosition)
    {
        for (int i = 0; i < runtimeSpots.Count; i++)
        {
            FishingSpotDefinition spot = runtimeSpots[i];
            if (spot != null && areaProgress != null && !areaProgress.IsUnlocked(spot.Id) &&
                Vector2.Distance(playerPosition, spot.ShorePosition) <= spot.InteractionRange + .7f)
                return spot;
        }
        return null;
    }

    private Transform CreateRoot(string objectName)
    {
        var root = new GameObject(objectName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void CreateGroundAndPaths(Transform parent)
    {
        CreateRect(parent, "Grass Ground", Vector2.zero, Config.MapSize, groundColor, -10, false);
        CreateRect(parent, "Village Entrance Path", new Vector2(-33f, -8f), new Vector2(2.4f, 8f), pathColor, -8, false);
        CreateRect(parent, "Village Main Lane", new Vector2(-22.5f, -10f), new Vector2(23f, 2.2f), pathColor, -8, false);
        CreateRect(parent, "Beginner Pond Approach", new Vector2(-20.4f, -7f), new Vector2(2.2f, 6f), pathColor, -8, false);
        CreateRect(parent, "Market Square", new Vector2(-12f, -5f), new Vector2(4.2f, 3.2f), new Color(.57f, .49f, .30f), -7, false);
        CreateRect(parent, "Rod Shop Apron", new Vector2(-12f, -10f), new Vector2(4.2f, 2.8f), new Color(.57f, .49f, .30f), -7, false);

        CreatePathBetween(parent, "Pond to Deep Lake Trail", new Vector2(-20.4f, -4f), new Vector2(-1.6f, 4f), -11f);
        CreatePathBetween(parent, "Deep to Legend Lake Trail", new Vector2(-1.6f, 4f), new Vector2(23.9f, -3f), 12f);
    }

    private void CreatePathBetween(Transform parent, string name, Vector2 from, Vector2 to, float bendX)
    {
        const float thickness = 1.8f;
        CreateRect(parent, name + " A", new Vector2((from.x + bendX) * .5f, from.y),
            new Vector2(Mathf.Abs(bendX - from.x) + thickness, thickness), pathColor, -8, false);
        CreateRect(parent, name + " B", new Vector2(bendX, (from.y + to.y) * .5f),
            new Vector2(thickness, Mathf.Abs(to.y - from.y) + thickness), pathColor, -8, false);
        CreateRect(parent, name + " C", new Vector2((bendX + to.x) * .5f, to.y),
            new Vector2(Mathf.Abs(to.x - bendX) + thickness, thickness), pathColor, -8, false);
    }

    private void CreateFishingArea(Transform waterParent, Transform gameplayParent, FishingSpotDefinition spot, int index)
    {
        CreateRect(waterParent, spot.DisplayName + " Shore", spot.WaterCenter,
            spot.WaterSize + new Vector2(.55f, .55f), new Color(.78f, .72f, .45f), -7, false);
        if (!CreateWaterTileGrid(waterParent, spot.DisplayName, spot.WaterCenter, spot.WaterSize, -6))
        {
            Color color = index == 2 ? deepWaterColor : waterColor;
            CreateRect(waterParent, spot.DisplayName, spot.WaterCenter, spot.WaterSize, color, -6, true);
        }
        else
        {
            CreateInvisibleCollider(waterParent, spot.DisplayName + " Water Blocker", spot.WaterCenter, spot.WaterSize);
        }

        Vector2 dockDirection = (spot.BobberPosition - spot.ShorePosition).normalized;
        bool horizontal = Mathf.Abs(dockDirection.x) > Mathf.Abs(dockDirection.y);
        Vector2 dockSize = horizontal ? new Vector2(1.15f, .72f) : new Vector2(.72f, 1.15f);
        CreateRect(gameplayParent, spot.DisplayName + " Dock", Vector2.Lerp(spot.ShorePosition, spot.BobberPosition, .28f),
            dockSize, new Color(.48f, .29f, .15f), 0, false);

        GameObject marker = CreateRect(gameplayParent, spot.DisplayName + " Fishing Marker",
            spot.ShorePosition + new Vector2(0f, .18f), new Vector2(.34f, .34f),
            new Color(.75f, .94f, .40f),
            1, false);
        marker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void CreateDecorations(Transform parent)
    {
        Texture2D treeTexture = Resources.Load<Texture2D>("Sprites/Environment/Tree1");
        Texture2D grassTexture = Resources.Load<Texture2D>("Sprites/Environment/grass1");
        Texture2D flowerTexture = Resources.Load<Texture2D>("Sprites/Environment/flower");
        if (treeTexture != null) treeTexture.filterMode = FilterMode.Point;
        if (grassTexture != null) grassTexture.filterMode = FilterMode.Point;
        if (flowerTexture != null) flowerTexture.filterMode = FilterMode.Point;

        Vector2[] treePositions =
        {
            new(-40f,-15f), new(-40f,-7f), new(-40f,2f), new(-40f,11f),
            new(-32f,16f), new(-22f,16f), new(-12f,16f), new(-2f,16f),
            new(9f,16f), new(20f,16f), new(31f,16f), new(40f,13f),
            new(40f,5f), new(40f,-5f), new(40f,-14f), new(31f,-16f),
            new(20f,-16f), new(9f,-16f), new(-2f,-16f), new(-14f,-16f),
            new(-26f,-16f), new(-36f,-14f), new(-15f,8f), new(14f,8f),
            new(15f,-9f), new(34f,8f)
        };
        if (treeTexture != null)
            foreach (Vector2 position in treePositions) CreateSpriteDecoration(parent, "Tree", treeTexture, position, 32f, 2, true);

        Vector2[] grassPositions =
        {
            new(-35f,7f), new(-30f,10f), new(-18f,10f), new(-8f,9f),
            new(-7f,-9f), new(0f,-10f), new(8f,-8f), new(18f,-12f),
            new(22f,10f), new(29f,11f), new(35f,5f), new(34f,-11f),
            new(-28f,2f), new(-16f,1f), new(11f,1f), new(17f,5f)
        };
        if (grassTexture != null)
            foreach (Vector2 position in grassPositions) CreateSpriteDecoration(parent, "Grass", grassTexture, position, 32f, -1, false);

        if (flowerTexture != null && Config.FlowerPositions != null)
            foreach (Vector2 position in Config.FlowerPositions)
                CreateSpriteDecoration(parent, "Flower Patch", flowerTexture, position, 32f, 0, false);
    }

    private void CreateShops(Transform parent)
    {
        Texture2D shopTexture = Resources.Load<Texture2D>("Sprites/Environment/FishingShop");
        if (shopTexture != null)
        {
            shopTexture.filterMode = FilterMode.Point;
            FishingShop = CreateSpriteDecoration(parent, "Fishing Shop", shopTexture, Config.FishingShopPosition, 32f, 2, true).transform;
        }
        else FishingShop = CreateRect(parent, "Fishing Shop", Config.FishingShopPosition, new Vector2(2f, 1.4f), new Color(.25f, .55f, .86f), 2, true).transform;

        Texture2D marketTexture = Resources.Load<Texture2D>("Sprites/Environment/fishshop");
        if (marketTexture != null)
        {
            marketTexture.filterMode = FilterMode.Point;
            FishMarket = CreateSpriteDecoration(parent, "Fish Market", marketTexture, Config.FishMarketPosition,
                32f, 2, true, new Vector2(1.7f, .5f), new Vector2(0f, .25f)).transform;
        }
        else FishMarket = CreateRect(parent, "Fish Market", Config.FishMarketPosition, new Vector2(2.1f, 1.45f), new Color(.78f, .34f, .18f), 2, true).transform;
    }

    private bool CreateWaterTileGrid(Transform parent, string areaName, Vector2 center, Vector2 size, int sortingOrder)
    {
        FishingWaterTileSet tiles = Config.WaterTiles;
        if (tiles == null || !tiles.IsComplete) return false;

        int columns = Mathf.Max(3, Mathf.RoundToInt(size.x));
        int rows = Mathf.Max(3, Mathf.RoundToInt(size.y));
        float tileWidth = size.x / columns;
        float tileHeight = size.y / rows;
        float pixelsPerUnit = Mathf.Max(1f, Config.WaterTilePixelsPerUnit);
        float left = center.x - size.x * .5f;
        float bottom = center.y - size.y * .5f;
        var spriteCache = new Dictionary<Texture2D, Sprite>();

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Texture2D texture = tiles.GetTile(column, row, columns, rows);
                if (!spriteCache.TryGetValue(texture, out Sprite sprite))
                {
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(.5f, .5f), pixelsPerUnit);
                    sprite.name = texture.name + " Runtime Tile";
                    spriteCache.Add(texture, sprite);
                }

                var tile = new GameObject($"{areaName} Tile {column},{row}");
                tile.transform.SetParent(parent, false);
                tile.transform.position = new Vector3(left + (column + .5f) * tileWidth,
                    bottom + (row + .5f) * tileHeight, 0f);
                float spriteWidth = texture.width / pixelsPerUnit;
                float spriteHeight = texture.height / pixelsPerUnit;
                tile.transform.localScale = new Vector3(tileWidth / spriteWidth, tileHeight / spriteHeight, 1f);
                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
            }
        }
        return true;
    }

    private void CreateMapBounds(Transform parent)
    {
        float halfWidth = Config.MapSize.x * .5f;
        float halfHeight = Config.MapSize.y * .5f;
        CreateInvisibleCollider(parent, "North Boundary", new Vector2(0f, halfHeight + .25f), new Vector2(Config.MapSize.x + 1f, .5f));
        CreateInvisibleCollider(parent, "South Boundary", new Vector2(0f, -halfHeight - .25f), new Vector2(Config.MapSize.x + 1f, .5f));
        CreateInvisibleCollider(parent, "West Boundary", new Vector2(-halfWidth - .25f, 0f), new Vector2(.5f, Config.MapSize.y + 1f));
        CreateInvisibleCollider(parent, "East Boundary", new Vector2(halfWidth + .25f, 0f), new Vector2(.5f, Config.MapSize.y + 1f));
    }

    private static GameObject CreateRect(Transform parent, string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder, bool blocksPlayer)
    {
        var rectangle = new GameObject(objectName);
        rectangle.transform.SetParent(parent, false);
        rectangle.transform.position = position;
        rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = rectangle.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSolidSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (blocksPlayer) rectangle.AddComponent<BoxCollider2D>();
        return rectangle;
    }

    private static GameObject CreateSpriteDecoration(Transform parent, string objectName, Texture2D texture,
        Vector2 position, float pixelsPerUnit, int sortingOrder, bool blocksPlayer,
        Vector2? colliderSize = null, Vector2? colliderOffset = null)
    {
        var decoration = new GameObject(objectName);
        decoration.transform.SetParent(parent, false);
        decoration.transform.position = position;
        var renderer = decoration.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), blocksPlayer ? new Vector2(.5f, 0f) : new Vector2(.5f, .5f), pixelsPerUnit);
        renderer.sortingOrder = sortingOrder;
        if (blocksPlayer)
        {
            var collider = decoration.AddComponent<BoxCollider2D>();
            collider.size = colliderSize ?? new Vector2(.65f, .42f);
            collider.offset = colliderOffset ?? new Vector2(0f, .21f);
        }
        return decoration;
    }

    private static Sprite CreateSolidSprite()
    {
        var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
    }

    private static void CreateInvisibleCollider(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        var boundary = new GameObject(objectName);
        boundary.transform.SetParent(parent, false);
        boundary.transform.position = position;
        boundary.AddComponent<BoxCollider2D>().size = size;
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        FishingShop = null;
        FishMarket = null;
    }
}
