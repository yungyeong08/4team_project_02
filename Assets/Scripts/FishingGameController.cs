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
        wallet.Initialize(1000);
        rodInventory = player.AddComponent<FishingRodInventory>();
        rodInventory.EnsureDefaultRod();
        fishInventory = player.AddComponent<FishInventory>();

        shopSystem = gameObject.AddComponent<FishingShopSystem>();
        shopSystem.Setup(character, map.FishingShop, wallet, rodInventory, rodCatalog);
        fishMarketSystem = gameObject.AddComponent<FishMarketSystem>();
        fishMarketSystem.Setup(character, map.FishMarket, wallet, fishInventory, fishCatalog);

        saveSystem = gameObject.AddComponent<GameSaveSystem>();
        saveSystem.Setup(wallet, fishInventory, rodInventory, player.transform, map.PlayerStart, map.MapSize, fishCatalog, rodCatalog);

        progressUI = gameObject.AddComponent<PlayerProgressUI>();
        progressUI.Setup(wallet, fishInventory, rodInventory, fishCatalog, rodCatalog, saveSystem, this, shopSystem, fishMarketSystem);
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
            message = nearbySpot != null ? $"{nearbySpot.DisplayName}  •  F 키로 낚시하기" : "이동: WASD 또는 방향키";
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
        if (caughtFish && currentFish != null && fishInventory != null) fishInventory.Add(currentFish.Id);
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
        var messagePanel = new Rect(screenWidth / 2f - 390f, 18f, 780f, 54f);
        FishingUiTheme.DrawPanel(messagePanel, true);
        GUI.Label(new Rect(messagePanel.x + 14f, messagePanel.y + 5f, messagePanel.width - 28f, 44f), message, FishingUiTheme.Banner);
        if (state != State.Reeling)
        {
            if (resultTimer > 0f) DrawResultWindow(screenWidth, screenHeight);
            return;
        }

        float trackWidth = Mathf.Min(1100f, screenWidth - 100f);
        var trackPanel = new Rect((screenWidth - trackWidth) * .5f - 16f, screenHeight - 222f, trackWidth + 32f, 176f);
        FishingUiTheme.DrawPanel(trackPanel);
        GUI.Label(new Rect(trackPanel.x + 18f, trackPanel.y + 8f, trackPanel.width - 36f, 28f),
            $"{currentFish.DisplayName}  ·  {currentFish.RarityName}     [스페이스 / 마우스 왼쪽]", FishingUiTheme.BodyCentered);
        var track = new Rect((screenWidth - trackWidth) * .5f, trackPanel.y + 42f, trackWidth, 62f);
        FishingUiTheme.DrawCard(track, true);
        float zoneSize = currentFish != null ? AdjustedReelZoneSize : .2f;
        Rect captureZone = new(track.x + reelPosition * track.width, track.y + 4f, zoneSize * track.width, track.height - 8f);
        GUI.color = new Color(.39f, .90f, .61f, .70f);
        GUI.DrawTexture(captureZone, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (currentFish?.Icon != null)
        {
            float fishX = Mathf.Lerp(track.x + 34f, track.xMax - 34f, fishPosition);
            var iconRect = new Rect(fishX - 34f, track.y - 3f, 68f, 68f);
            FishingUiTheme.DrawTextureOrSwatch(iconRect, currentFish.Icon, currentFish.RarityColor);
        }

        float barWidth = track.width * .46f;
        Rect progressBar = new(track.x, track.y + 78f, barWidth, 22f);
        Rect escapeBar = new(track.xMax - barWidth, track.y + 78f, barWidth, 22f);
        FishingUiTheme.DrawBar(progressBar, progress, FishingUiTheme.AccentBright);
        FishingUiTheme.DrawBar(escapeBar, escape, FishingUiTheme.Danger);
        GUI.Label(new Rect(progressBar.x, progressBar.y + 24f, progressBar.width, 28f), "포획 " + Mathf.RoundToInt(progress * 100) + "%", FishingUiTheme.SmallCentered);
        GUI.Label(new Rect(escapeBar.x, escapeBar.y + 24f, escapeBar.width, 28f), "도주 " + Mathf.RoundToInt(escape * 100) + "%", FishingUiTheme.SmallCentered);
    }

    private void DrawResultWindow(float screenWidth, float screenHeight)
    {
        float width = Mathf.Min(620f, screenWidth - 40f);
        float height = 260f;
        var panel = new Rect((screenWidth - width) * .5f, (screenHeight - height) * .5f, width, height);
        FishingUiTheme.DrawPanel(panel);
        FishingUiTheme.ColoredLabel(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, 42f),
            resultSuccess ? "낚시 성공!" : "낚시 실패", FishingUiTheme.Title,
            resultSuccess ? FishingUiTheme.AccentBright : FishingUiTheme.Danger);
        FishingUiTheme.DrawDivider(new Rect(panel.x + 26f, panel.y + 65f, panel.width - 52f, 2f));
        if (resultSuccess && resultFish?.Icon != null)
            FishingUiTheme.DrawTextureOrSwatch(new Rect(panel.x + 34f, panel.y + 80f, 104f, 104f), resultFish.Icon, resultFish.RarityColor);
        GUI.Label(new Rect(panel.x + (resultSuccess ? 155f : 30f), panel.y + 76f,
            panel.width - (resultSuccess ? 185f : 60f), 104f), resultDetails, FishingUiTheme.BodyCentered);
        if (FishingUiTheme.StyledButton(new Rect(panel.x + panel.width * .5f - 72f, panel.yMax - 58f, 144f, 40f), "확인", primary: true))
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
