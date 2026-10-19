using System.Collections.Generic;
using UnityEngine;

/// <summary>Playable fishing prototype: move, face the water, cast, wait for a bite, then keep the fish inside the reel zone.</summary>
public sealed class FishingGameController : MonoBehaviour
{
    private enum State { Explore, Waiting, Reeling }
    private State state;
    private PixelCharacter character;
    private Camera gameCamera;
    private GameObject bobber;
    private FishingBalanceConfig balanceConfig;
    private List<FishDefinition> fishCatalog;
    private List<FishingRodDefinition> rodCatalog;
    private FishDefinition currentFish;
    private FishingSpotDefinition activeFishingSpot;
    private float waitTimer;
    private float reelPosition = .4f;
    private float reelVelocity;
    private float fishPosition = .55f;
    private float fishVelocity;
    private float directionTimer;
    private float progress;
    private float escape;
    private float resultTimer;
    private string message = "이동: WASD 또는 방향키";
    private FishDefinition resultFish;
    private bool resultSuccess;
    private string resultDetails = "";
    private bool initialized;
    private PixelMapBuilder map;
    private FishingShopSystem shopSystem;
    private FishMarketSystem fishMarketSystem;
    private FishInventory fishInventory;
    private FishingRodInventory rodInventory;
    private PlayerProgressUI progressUI;
    private GameSaveSystem saveSystem;
    private EarlyGameQuestSystem earlyGameProgress;

    public bool IsFishingActive => state != State.Explore;
    public string EquippedRodId => rodInventory != null ? rodInventory.EquippedRodId : "";

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized || !Application.isPlaying) return;
        // Clear the temporary Scene-view objects created by the earlier prototype.
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        balanceConfig = FishingBalanceConfig.LoadOrCreateRuntimeDefault();
        fishCatalog = balanceConfig.Fish;
        rodCatalog = balanceConfig.Rods;
        BuildWorld();
        initialized = true;
    }

    private void BuildWorld()
    {
        // Clean up only runtime objects created by this prototype before making a fresh test session.
        foreach (var oldPlayer in Object.FindObjectsByType<PixelCharacter>(FindObjectsSortMode.None)) Destroy(oldPlayer.gameObject);
        foreach (var oldMap in GetComponents<PixelMapBuilder>()) Destroy(oldMap);
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            gameCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        gameCamera.orthographic = true;
        // Show more of the map and keep the 64px character from dominating the screen.
        gameCamera.orthographicSize = 5.2f;
        gameCamera.transform.position = new Vector3(0, 0, -10);
        gameCamera.backgroundColor = new Color(.35f, .60f, .24f);

        map = gameObject.AddComponent<PixelMapBuilder>();
        map.Build();

        var player = new GameObject("Player (Pixel Crawler)");
        player.transform.position = map.PlayerStart;
        character = player.AddComponent<PixelCharacter>();
        character.Setup();
        var wallet = player.AddComponent<PlayerWallet>();
        wallet.Initialize(100);
        rodInventory = player.AddComponent<FishingRodInventory>();
        rodInventory.EnsureDefaultRod();
        fishInventory = player.AddComponent<FishInventory>();

        shopSystem = gameObject.AddComponent<FishingShopSystem>();
        shopSystem.Setup(character, map.FishingShop, wallet, rodInventory, rodCatalog);
        fishMarketSystem = gameObject.AddComponent<FishMarketSystem>();
        fishMarketSystem.Setup(character, map.FishMarket, wallet, fishInventory, fishCatalog);

        earlyGameProgress = gameObject.AddComponent<EarlyGameQuestSystem>();
        earlyGameProgress.Setup(player.transform, wallet, map.GetFishingSpot(EarlyGameQuestSystem.FirstLakeId),
            map.FishMarket, map.FishingShop);
        shopSystem.RodPurchased += earlyGameProgress.NotifyRodPurchased;
        fishMarketSystem.FishSold += (soldCount, _) => earlyGameProgress.NotifyFishSold(soldCount);

        saveSystem = gameObject.AddComponent<GameSaveSystem>();
        saveSystem.Setup(wallet, fishInventory, rodInventory, player.transform, map.PlayerStart, map.MapSize,
            fishCatalog, rodCatalog, earlyGameProgress);

        progressUI = gameObject.AddComponent<PlayerProgressUI>();
        progressUI.Setup(wallet, fishInventory, rodInventory, fishCatalog, rodCatalog, saveSystem, this,
            shopSystem, fishMarketSystem, earlyGameProgress);
        earlyGameProgress.SetUiBlocker(() =>
            (progressUI != null && progressUI.IsBlockingGameplay) ||
            (shopSystem != null && shopSystem.IsOpen) ||
            (fishMarketSystem != null && fishMarketSystem.IsOpen) || IsFishingActive);
        shopSystem.SetInputBlocker(() => progressUI != null && progressUI.IsBlockingGameplay);
        fishMarketSystem.SetInputBlocker(() => progressUI != null && progressUI.IsBlockingGameplay);

        var follow = gameCamera.gameObject.GetComponent<CameraFollow2D>();
        if (follow == null) follow = gameCamera.gameObject.AddComponent<CameraFollow2D>();
        follow.Target = player.transform;
        follow.SetWorldBounds(map.MapSize);
    }

    private static GameObject CreateRect(string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        var go = new GameObject(objectName);
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white); tex.Apply();
        renderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        renderer.color = color; renderer.sortingOrder = sortingOrder;
        go.transform.localScale = size;
        return go;
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
            if (!initialized) return;
        }

        if (resultTimer > 0f)
        {
            resultTimer -= Time.deltaTime;
            if (resultTimer <= 0f)
            {
                resultTimer = 0f;
                resultFish = null;
                resultDetails = "";
            }
        }

        if (state == State.Explore)
        {
            if (progressUI != null && progressUI.IsBlockingGameplay) return;
            if ((shopSystem != null && shopSystem.IsOpen) || (fishMarketSystem != null && fishMarketSystem.IsOpen)) return;
            if (character != null) character.Move();
            if (shopSystem != null && shopSystem.IsPlayerInRange)
            {
                message = "F - 상점 열기";
                return;
            }
            if (fishMarketSystem != null && fishMarketSystem.IsPlayerInRange)
            {
                message = "F - 물고기 판매";
                return;
            }
            FishingSpotDefinition nearbySpot = character != null ? map.GetNearestFishingSpot(character.transform.position) : null;
            FishingSpotDefinition lockedSpot = nearbySpot == null && character != null
                ? map.GetNearestLockedFishingSpot(character.transform.position)
                : null;
            message = nearbySpot != null
                ? $"{nearbySpot.DisplayName}  •  F 키로 낚시하기"
                : lockedSpot != null
                    ? $"{lockedSpot.DisplayName}  •  개발 예정 지역"
                    : "이동: WASD 또는 방향키";
            if (nearbySpot != null && Input.GetKeyDown(KeyCode.F)) Cast(nearbySpot);
        }
        else if (state == State.Waiting)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) ResetFishing("낚시를 취소했습니다.");
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0) Bite();
        }
        else if (state == State.Reeling) Reel();
    }

    private void Cast(FishingSpotDefinition spot)
    {
        activeFishingSpot = spot;
        earlyGameProgress?.NotifyFishingStarted(spot.Id);
        state = State.Waiting; character.SetFishing(true);
        resultFish = null;
        resultDetails = "";
        resultTimer = 0f;
        message = $"{spot.DisplayName}에 낚싯줄을 던졌습니다…  (ESC: 취소)";
        bobber = CreateRect("Bobber", spot.BobberPosition, new Vector2(.22f, .22f), new Color(1f, .28f, .2f), 4);
        waitTimer = Random.Range(2f, 6f);
    }

    private void Bite()
    {
        if (fishCatalog == null || fishCatalog.Count == 0)
        {
            ResetFishing("물고기 정보를 불러오지 못했습니다.");
            return;
        }
        state = State.Reeling;
        currentFish = activeFishingSpot != null ? activeFishingSpot.SelectFish(fishCatalog) : fishCatalog[Random.Range(0, fishCatalog.Count)];
        if (currentFish == null)
        {
            ResetFishing("이 낚시터의 물고기 정보를 불러오지 못했습니다.");
            return;
        }
        fishPosition = Random.Range(.15f, .85f); reelPosition = .4f; reelVelocity = 0; fishVelocity = Random.Range(-.15f, .15f);
        directionTimer = Random.Range(AdjustedDirectionInterval * .65f, AdjustedDirectionInterval * 1.35f);
        progress = escape = 0;
        message = $"입질! {currentFish.DisplayName} · {currentFish.RarityName}  |  마우스 왼쪽 또는 스페이스";
        if (bobber != null) bobber.GetComponent<SpriteRenderer>().color = Color.yellow;
    }

    private void Reel()
    {
        float input = (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space)) ? 1f : -1f;
        float zoneSize = AdjustedReelZoneSize;
        float rodFishSpeedMultiplier = EquippedRod != null ? EquippedRod.FishSpeedMultiplier : 1f;

        // Keep the original weighty control feel; difficulty now comes from reading the fish,
        // not from an overly sensitive player bar.
        reelVelocity += input * 1.65f * Time.deltaTime;
        reelVelocity *= Mathf.Exp(-4f * Time.deltaTime);
        reelVelocity = Mathf.Clamp(reelVelocity, -.55f, .55f);
        reelPosition = Mathf.Clamp(reelPosition + reelVelocity * Time.deltaTime, 0f, 1f - zoneSize);

        const float fishSpeedScale = .72f;
        fishVelocity += Mathf.Sin(Time.time * 3.1f) * currentFish.MovementAcceleration * EffectiveSpotDifficulty * fishSpeedScale * rodFishSpeedMultiplier * Time.deltaTime;
        directionTimer -= Time.deltaTime;
        if (directionTimer <= 0f)
        {
            float impulse = currentFish.DirectionImpulse * EffectiveSpotDifficulty * fishSpeedScale * rodFishSpeedMultiplier;
            fishVelocity += Random.Range(-impulse, impulse);
            directionTimer = Random.Range(AdjustedDirectionInterval * .65f, AdjustedDirectionInterval * 1.35f);
        }
        float maxSpeed = currentFish.MaxSpeed * EffectiveSpotDifficulty * fishSpeedScale * rodFishSpeedMultiplier;
        fishVelocity = Mathf.Clamp(fishVelocity, -maxSpeed, maxSpeed);
        fishPosition += fishVelocity * Time.deltaTime;
        if (fishPosition < .02f || fishPosition > .98f) fishVelocity *= -1f;
        fishPosition = Mathf.Clamp(fishPosition, .02f, .98f);
        bool aligned = fishPosition >= reelPosition && fishPosition <= reelPosition + zoneSize;
        float difficultyRoot = Mathf.Sqrt(EffectiveSpotDifficulty);
        float rodCatchMultiplier = EquippedRod != null ? EquippedRod.CatchRateMultiplier : 1f;
        progress = Mathf.Clamp01(progress + (aligned ? currentFish.CatchRate * rodCatchMultiplier / difficultyRoot : -.08f) * Time.deltaTime);
        escape = Mathf.Clamp01(escape + (aligned ? -.20f : currentFish.EscapeRate * difficultyRoot) * Time.deltaTime);
        if (progress >= 1) ResetFishing("물고기를 잡았습니다! F 키로 다시 낚시할 수 있습니다.");
        else if (escape >= 1) ResetFishing("물고기가 도망갔습니다. F 키로 다시 시도하세요.");
        else if (Input.GetKeyDown(KeyCode.Escape)) ResetFishing("낚시를 취소했습니다.");
    }

    private void ResetFishing(string newMessage)
    {
        bool caughtFish = state == State.Reeling && progress >= 1f;
        bool escapedFish = state == State.Reeling && escape >= 1f;
        resultFish = caughtFish ? currentFish : null;
        resultSuccess = caughtFish;
        resultDetails = caughtFish && currentFish != null
            ? $"{currentFish.DisplayName} · {currentFish.RarityName} · 판매가 {currentFish.SalePrice:N0} G"
            : escapedFish ? "물고기를 놓쳤습니다. 다시 도전해 보세요." : "";
        if (caughtFish && currentFish != null && fishInventory != null)
        {
            string caughtSpotId = activeFishingSpot != null ? activeFishingSpot.Id : "";
            string usedRodId = rodInventory != null ? rodInventory.EquippedRodId : "";
            fishInventory.Add(currentFish.Id);
            earlyGameProgress?.NotifyFishCaught(caughtSpotId, usedRodId);
        }
        resultTimer = caughtFish || escapedFish ? 5.5f : 0f;
        state = State.Explore;
        message = newMessage;
        character?.SetFishing(false);
        reelVelocity = 0f;
        fishVelocity = 0f;
        directionTimer = 0f;
        progress = 0f;
        escape = 0f;
        if (bobber != null) Destroy(bobber);
        bobber = null;
        currentFish = null;
        activeFishingSpot = null;
    }

    private void OnGUI()
    {
        if ((progressUI != null && progressUI.IsBlockingGameplay) ||
            (shopSystem != null && (shopSystem.IsOpen || shopSystem.IsPlayerInRange)) ||
            (fishMarketSystem != null && (fishMarketSystem.IsOpen || fishMarketSystem.IsPlayerInRange))) return;

        float uiScale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
        DrawFishingOverlay(Screen.width / uiScale, Screen.height / uiScale);
        GUI.matrix = previousMatrix;
    }

    private void DrawFishingOverlay(float screenWidth, float screenHeight)
    {
        FishingUiTheme.Ensure();
        float noticeWidth = Mathf.Min(760f, screenWidth - 40f);
        var messagePanel = new Rect((screenWidth - noticeWidth) * .5f, 18f, noticeWidth, 54f);
        FishingUiTheme.DrawNotice(messagePanel, message,
            state == State.Reeling ? FishingUiTheme.Gold : FishingUiTheme.WaterBright);
        if (state != State.Reeling)
        {
            if (resultTimer > 0f) DrawResultWindow(screenWidth, screenHeight);
            return;
        }

        float trackWidth = Mathf.Min(1120f, screenWidth - 100f);
        var track = new Rect((screenWidth - trackWidth) * .5f, screenHeight - 188f, trackWidth, 54f);
        Color trackColor = new(.018f, .018f, .022f, .94f);
        Color borderColor = new(.33f, .34f, .38f, 1f);
        FishingUiTheme.DrawFlatRect(track, trackColor);
        FishingUiTheme.DrawFlatOutline(track, borderColor, 2f);

        GUI.Label(new Rect(track.x - 34f, track.y + 9f, 28f, 34f), "<", FishingUiTheme.Heading);
        GUI.Label(new Rect(track.xMax + 8f, track.y + 9f, 28f, 34f), ">", FishingUiTheme.Heading);

        float zoneSize = currentFish != null ? AdjustedReelZoneSize : .2f;
        float laneX = track.x + 3f;
        float laneWidth = track.width - 6f;
        Rect captureZone = new(laneX + reelPosition * laneWidth, track.y - 4f, zoneSize * laneWidth, track.height + 8f);
        FishingUiTheme.DrawFlatRect(captureZone, new Color(.96f, .96f, .95f, 1f));
        FishingUiTheme.DrawFlatOutline(captureZone, new Color(.72f, .73f, .76f, 1f), 2f);
        FishingUiTheme.ColoredLabel(new Rect(captureZone.xMax - 54f, captureZone.y + 4f, 46f, captureZone.height - 8f),
            "→", FishingUiTheme.Title, new Color(.43f, .44f, .46f, 1f));

        float fishX = Mathf.Lerp(laneX + 8f, laneX + laneWidth - 8f, fishPosition);
        Rect marker = new(fishX - 4f, track.y - 16f, 8f, track.height + 32f);
        FishingUiTheme.DrawFlatRect(marker, new Color(.48f, .50f, .56f, 1f));
        FishingUiTheme.DrawFlatOutline(marker, new Color(.30f, .32f, .38f, 1f), 1f);
        if (currentFish?.Icon != null)
        {
            var iconRect = new Rect(fishX - 22f, track.y - 58f, 44f, 38f);
            GUI.DrawTexture(iconRect, currentFish.Icon, ScaleMode.ScaleToFit, true);
        }
        else FishingUiTheme.DrawFlatRect(new Rect(fishX - 8f, track.y - 40f, 16f, 16f), currentFish.RarityColor);

        float barWidth = Mathf.Min(620f, track.width * .58f);
        Rect progressBar = new((screenWidth - barWidth) * .5f, track.y + 88f, barWidth, 18f);
        FishingUiTheme.DrawFlatRect(progressBar, new Color(.025f, .025f, .03f, .94f));
        FishingUiTheme.DrawFlatOutline(progressBar, borderColor, 2f);
        Rect progressFill = new(progressBar.x + 2f, progressBar.y + 2f,
            (progressBar.width - 4f) * Mathf.Clamp01(progress), progressBar.height - 4f);
        FishingUiTheme.DrawFlatRect(progressFill, new Color(.94f, .94f, .92f, 1f));

        Rect dangerLine = new(progressBar.x + 2f, progressBar.yMax + 5f,
            (progressBar.width - 4f) * Mathf.Clamp01(escape), 4f);
        FishingUiTheme.DrawFlatRect(dangerLine, FishingUiTheme.Danger);
        GUI.Label(new Rect(progressBar.x, progressBar.y - 25f, 180f, 22f),
            $"포획 {Mathf.RoundToInt(progress * 100)}%", FishingUiTheme.Small);
        GUI.Label(new Rect(progressBar.xMax - 180f, progressBar.y - 25f, 180f, 22f),
            $"도주 위험 {Mathf.RoundToInt(escape * 100)}%", FishingUiTheme.SmallCentered);
        GUI.Label(new Rect(progressBar.x, progressBar.yMax + 14f, progressBar.width, 24f),
            "SPACE  누르면 오른쪽 · 놓으면 왼쪽", FishingUiTheme.SmallCentered);
    }

    private void DrawResultWindow(float screenWidth, float screenHeight)
    {
        float width = Mathf.Min(620f, screenWidth - 40f);
        float height = 310f;
        var panel = new Rect((screenWidth - width) * .5f, (screenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);
        Color resultColor = resultSuccess ? FishingUiTheme.AccentBright : FishingUiTheme.Danger;
        FishingUiTheme.DrawTitlePlate(new Rect(panel.x + 70f, panel.y - 16f, panel.width - 140f, 58f),
            resultSuccess ? "낚았다!" : "놓쳤다!", resultColor);
        if (resultSuccess && resultFish?.Icon != null)
        {
            FishingUiTheme.DrawTextureOrSwatch(new Rect(panel.x + 42f, panel.y + 74f, 122f, 122f), resultFish.Icon, resultFish.RarityColor);
            FishingUiTheme.DrawBadge(new Rect(panel.x + 42f, panel.y + 206f, 122f, 30f), resultFish.RarityName, resultFish.RarityColor);
        }
        GUI.Label(new Rect(panel.x + (resultSuccess ? 188f : 42f), panel.y + 72f,
            panel.width - (resultSuccess ? 228f : 84f), 150f), resultDetails, FishingUiTheme.BodyCentered);
        if (FishingUiTheme.StyledButton(new Rect(panel.x + panel.width * .5f - 82f, panel.yMax - 58f, 164f, 42f),
            resultSuccess ? "가방에 넣기" : "다시 도전", primary: true))
        {
            resultTimer = 0f;
            resultFish = null;
            resultDetails = "";
        }
    }

    private float SpotDifficulty => activeFishingSpot != null ? Mathf.Max(.5f, activeFishingSpot.DifficultyMultiplier) : 1f;
    private float EffectiveSpotDifficulty => Mathf.Lerp(1f, SpotDifficulty, .55f);
    private float AdjustedDirectionInterval => currentFish != null ? currentFish.DirectionChangeInterval / EffectiveSpotDifficulty * 1.15f : 1f;
    private FishingRodDefinition EquippedRod => rodInventory?.GetEquipped(rodCatalog);
    private float AdjustedReelZoneSize
    {
        get
        {
            if (currentFish == null) return .24f;
            float rodMultiplier = EquippedRod != null ? EquippedRod.CaptureZoneMultiplier : 1f;
            return Mathf.Clamp(currentFish.ReelZoneSize * 1.25f * rodMultiplier / Mathf.Sqrt(EffectiveSpotDifficulty), .18f, .48f);
        }
    }
}
